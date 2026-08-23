using DialogSystem.EditorTools.Settings;
using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.View
{
    /// <summary>
    /// Owns GraphView canvas adornments such as the grid and minimap.
    /// The minimap is intentionally pinned to the bottom-right corner and is not draggable.
    /// </summary>
    internal sealed class DialogGraphCanvasService
    {
        #region ---------------- Constants ----------------

        private const float MinimapWidth = 200f;
        private const float MinimapHeight = 140f;
        private const float ToggleButtonSize = 30f;
        private const float ToggleGap = 8f;
        private const float MinimapMargin = 35f;

        #endregion

        #region ---------------- Fields ----------------

        private readonly GraphView _graphView;
        private MiniMap _miniMap;
        private Button _miniMapToggleButton;
        private bool _isApplyingMiniMapPosition;

        #endregion

        #region ---------------- Ctor ----------------

        public DialogGraphCanvasService(GraphView graphView)
        {
            _graphView = graphView ?? throw new ArgumentNullException(nameof(graphView));
        }

        #endregion

        #region ---------------- Setup ----------------

        public void Setup()
        {
            SetupGrid();
            SetupMiniMap();
        }

        private void SetupGrid()
        {
            var grid = new GridBackground();
            _graphView.Insert(0, grid);
            grid.StretchToParentSize();
        }

        private void SetupMiniMap()
        {
            _miniMap = new MiniMap { anchored = false };
            _miniMap.style.width = MinimapWidth;
            _miniMap.style.height = MinimapHeight;
            _miniMap.style.minWidth = MinimapWidth;
            _miniMap.style.minHeight = MinimapHeight;
            _miniMap.pickingMode = PickingMode.Ignore;
            _graphView.Add(_miniMap);

            _miniMap.RegisterCallback<GeometryChangedEvent>(OnMiniMapGeometryChanged);
            _graphView.RegisterCallback<GeometryChangedEvent>(_ => RefreshMiniMapOverlayLayout());

            SetMinimapVisible(DialogGraphEditorSettings.MinimapVisible);
            _graphView.schedule.Execute(RefreshMiniMapOverlayLayout).ExecuteLater(0);
        }

        #endregion

        #region ---------------- Updates ----------------

        /// <summary>Shows or hides the minimap overlay.</summary>
        public void SetMinimapVisible(bool visible)
        {
            if (_miniMap == null)
            {
                return;
            }

            _miniMap.style.display = DisplayStyle.Flex;
            _miniMap.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
            _miniMap.style.opacity = visible ? 1f : 0f;
            _miniMap.SetEnabled(visible);

            if (!visible)
            {
                return;
            }

            RefreshMiniMapOverlayLayout();
            _miniMap.BringToFront();
            _miniMapToggleButton?.BringToFront();
            _miniMap.MarkDirtyRepaint();
            _graphView.schedule.Execute(() =>
            {
                RefreshMiniMapOverlayLayout();
                _miniMap?.BringToFront();
                _miniMapToggleButton?.BringToFront();
                _miniMap?.MarkDirtyRepaint();
            }).ExecuteLater(0);
        }

        public void AttachMiniMapToggle(Button button)
        {
            if (button == null)
            {
                return;
            }

            _miniMapToggleButton = button;
            _miniMapToggleButton.style.position = Position.Absolute;
            _miniMapToggleButton.style.width = ToggleButtonSize;
            _miniMapToggleButton.style.height = ToggleButtonSize;
            _miniMapToggleButton.style.minWidth = ToggleButtonSize;
            _miniMapToggleButton.style.minHeight = ToggleButtonSize;

            RefreshMiniMapOverlayLayout();
            _miniMapToggleButton.BringToFront();
        }

        public bool ConsumeToggleClickSuppression()
        {
            return false;
        }

        public void UpdateCanvasHint(IEnumerable<Node> visibleNodes)
        {
            // Canvas empty-state messaging was removed because it lingered after
            // non-creation interactions and obscured the graph workspace.
        }

        private void RefreshMiniMapOverlayLayout()
        {
            if (_graphView.layout.width <= 0f || _graphView.layout.height <= 0f)
            {
                return;
            }

            ApplyMiniMapPosition(new Rect(GetDefaultMiniMapPosition(), new Vector2(MinimapWidth, MinimapHeight)));

            if (_miniMapToggleButton != null)
            {
                SyncToggleButtonToMiniMap();
            }
        }

        private void OnMiniMapGeometryChanged(GeometryChangedEvent evt)
        {
            if (_miniMap == null || _isApplyingMiniMapPosition)
            {
                return;
            }

            RefreshMiniMapOverlayLayout();
        }

        private void ApplyMiniMapPosition(Rect rect)
        {
            if (_miniMap == null)
            {
                return;
            }

            _isApplyingMiniMapPosition = true;
            _miniMap.SetPosition(ClampMiniMapRect(rect));
            _isApplyingMiniMapPosition = false;
        }

        private void SyncToggleButtonToMiniMap()
        {
            if (_miniMapToggleButton == null)
            {
                return;
            }

            var miniMapPosition = GetDefaultMiniMapPosition();
            var target = new Rect(
                miniMapPosition.x + MinimapWidth + ToggleGap,
                miniMapPosition.y + MinimapHeight - ToggleButtonSize,
                ToggleButtonSize,
                ToggleButtonSize);

            var clamped = ClampToggleButtonRect(target);
            _miniMapToggleButton.style.left = clamped.x;
            _miniMapToggleButton.style.top = clamped.y;
            _miniMapToggleButton.style.right = StyleKeyword.Null;
            _miniMapToggleButton.style.bottom = StyleKeyword.Null;
        }

        private Rect ClampMiniMapRect(Rect rect)
        {
            var maxX = Mathf.Max(MinimapMargin, _graphView.layout.width - rect.width - MinimapMargin);
            var maxY = Mathf.Max(MinimapMargin, _graphView.layout.height - rect.height - MinimapMargin);

            rect.x = Mathf.Clamp(rect.x, MinimapMargin, maxX);
            rect.y = Mathf.Clamp(rect.y, MinimapMargin, maxY);
            rect.width = MinimapWidth;
            rect.height = MinimapHeight;
            return rect;
        }

        private Rect ClampToggleButtonRect(Rect rect)
        {
            var maxX = Mathf.Max(MinimapMargin, _graphView.layout.width - rect.width - MinimapMargin);
            var maxY = Mathf.Max(MinimapMargin, _graphView.layout.height - rect.height - MinimapMargin);

            rect.x = Mathf.Clamp(rect.x, MinimapMargin, maxX);
            rect.y = Mathf.Clamp(rect.y, MinimapMargin, maxY);
            rect.width = ToggleButtonSize;
            rect.height = ToggleButtonSize;
            return rect;
        }

        private Vector2 GetDefaultMiniMapPosition()
        {
            return new Vector2(
                Mathf.Max(MinimapMargin, _graphView.layout.width - MinimapWidth - MinimapMargin),
                Mathf.Max(MinimapMargin, _graphView.layout.height - MinimapHeight - MinimapMargin));
        }

        #endregion
    }
}
