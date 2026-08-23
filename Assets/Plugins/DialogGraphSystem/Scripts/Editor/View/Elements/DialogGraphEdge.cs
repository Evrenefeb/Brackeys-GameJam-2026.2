using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using DialogSystem.EditorTools.Settings;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DialogSystem.EditorTools.View.Elements
{
    /// <summary>
    /// Custom directed edge for the dialog graph editor.
    /// Keeps serialized reroute points separate from render-only hidden stub points.
    /// </summary>
    public sealed class DialogGraphEdge : Edge
    {
        public enum EdgeFocusState
        {
            Default,
            Highlighted,
            Normal,
            Faded
        }

        private const float RerouteHandleSize = 12f;
        private const float AddButtonSize = 16f;
        private const float PortStubLength = 32f;

        private readonly List<RerouteHandle> _handles = new();
        private readonly AddRerouteButton _addButton;
        private readonly VisualElement _labelContainer;
        private readonly Label _edgeLabel;
        private readonly List<Vector2> _renderRoutePoints = new(8);

        private EdgeFocusState _focusState = EdgeFocusState.Default;
        private bool _isDragging;
        private bool _isHovered;

        public DialogGraphEdge()
        {
            generateVisualContent += OnGenerateVisualContent;

            _addButton = new AddRerouteButton(this);
            Add(_addButton);

            _labelContainer = new VisualElement();
            _labelContainer.AddToClassList("dlg-edge-label-container");
            _labelContainer.pickingMode = PickingMode.Ignore;

            _edgeLabel = new Label();
            _edgeLabel.AddToClassList("dlg-edge-label");
            _labelContainer.Add(_edgeLabel);
            Add(_labelContainer);

            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));

            RegisterCallback<PointerEnterEvent>(_ =>
            {
                _isHovered = true;
                RefreshLabelVisibility();
            });

            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _isHovered = false;
                RefreshLabelVisibility();
            });

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                DialogGraphEditorSettings.OnSettingsChanged -= RefreshLabelVisibility;
                DialogGraphEditorSettings.OnSettingsChanged += RefreshLabelVisibility;
            });

            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                DialogGraphEditorSettings.OnSettingsChanged -= RefreshLabelVisibility;
            });
        }

        public DialogGraph Owner { get; set; }

        public DialogGraphView OwnerGraphView { get; private set; }

        public string linkGuid = string.Empty;
        public string fromGuid = string.Empty;
        public string toGuid = string.Empty;
        public int fromPortIndex;
        public string fromPortKey = string.Empty;
        public string toPortKey = string.Empty;

        public void Initialize(DialogGraph owner, DialogGraphView graphView)
        {
            Owner = owner;
            OwnerGraphView = graphView;
            RefreshVisuals();
            schedule.Execute(_ => RefreshVisuals()).ExecuteLater(0);
        }

        public void SetFocusState(EdgeFocusState state)
        {
            if (_focusState == state)
            {
                return;
            }

            _focusState = state;
            UpdateFocusVisuals();
            RefreshVisuals();
        }

        public override void OnSelected()
        {
            base.OnSelected();
            RefreshLabelVisibility();
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            RefreshLabelVisibility();
        }

        public override bool UpdateEdgeControl()
        {
            var result = base.UpdateEdgeControl();

            if (edgeControl != null)
            {
                edgeControl.style.display = HasSerializedReroutePoints() ? DisplayStyle.None : DisplayStyle.Flex;
            }

            RefreshVisuals();
            return result;
        }

        private void RefreshVisuals()
        {
            MarkDirtyRepaint();
            SyncHandles();
            UpdateAddButton();
            UpdateFocusVisuals();
            UpdateLabelPosition();
            RefreshLabelVisibility();
        }

        public void RefreshGeometry()
        {
            RefreshVisuals();
        }

        private void RefreshLabelVisibility()
        {
            if (_labelContainer == null)
            {
                return;
            }

            var visibilityMode = DialogGraphEditorSettings.EdgeLabelVisibility;
            bool isVisible = visibilityMode switch
            {
                EdgeLabelVisibility.Hidden => false,
                EdgeLabelVisibility.Always => true,
                EdgeLabelVisibility.OnHover => _isHovered,
                EdgeLabelVisibility.OnSelection => selected,
                _ => false
            };

            if (visibilityMode == EdgeLabelVisibility.OnHover && selected)
            {
                isVisible = true;
            }

            _labelContainer.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (isVisible)
            {
                UpdateLabelText();
            }
        }

        private void UpdateLabelText()
        {
            if (_edgeLabel == null || output?.node == null)
            {
                return;
            }

            string text = "Next";
            var sourceNode = output.node;

            if (sourceNode is ChoiceNodeView choiceView)
            {
                int idx = choiceView.outputPorts.IndexOf(output);
                if (idx >= 0 && idx < choiceView.answers.Count)
                {
                    text = choiceView.answers[idx];
                }
            }
            else if (sourceNode is ConditionNodeView condView)
            {
                if (output == condView.trueOutputPort)
                {
                    text = "True";
                }
                else if (output == condView.falseOutputPort)
                {
                    text = "False";
                }
            }
            else if (sourceNode is StartNodeView)
            {
                text = "Start";
            }
            else if (input?.node is EndNodeView)
            {
                text = "End";
            }

            const int maxLen = 24;
            if (!string.IsNullOrEmpty(text) && text.Length > maxLen)
            {
                text = text.Substring(0, maxLen - 3) + "...";
            }

            _edgeLabel.text = text;
        }

        private void UpdateLabelPosition()
        {
            if (_labelContainer == null || _labelContainer.style.display == DisplayStyle.None)
            {
                return;
            }

            Vector2 mid = FindMidpoint(out _);

            float width = _labelContainer.resolvedStyle.width;
            float height = _labelContainer.resolvedStyle.height;

            if (float.IsNaN(width) || width <= 0f)
            {
                width = 60f;
            }

            if (float.IsNaN(height) || height <= 0f)
            {
                height = 18f;
            }

            _labelContainer.style.left = mid.x - (width * 0.5f);
            _labelContainer.style.top = mid.y - height - 10f;
        }

        private void UpdateFocusVisuals()
        {
            float opacity = _focusState == EdgeFocusState.Faded ? 0.7f : 1f;
            style.opacity = opacity;

            if (edgeControl != null)
            {
                const int baseWidth = 2;
                edgeControl.edgeWidth = _focusState == EdgeFocusState.Highlighted
                    ? (int)(baseWidth * 1.5f)
                    : baseWidth;
            }

            _addButton.style.opacity = opacity;
            foreach (var handle in _handles)
            {
                handle.style.opacity = opacity;
            }
        }

        private bool HasSerializedReroutePoints()
        {
            return TryGetLayout(out var layout) &&
                   layout.reroutePoints != null &&
                   layout.reroutePoints.Count > 0;
        }

        private bool TryGetLayout(out EdgeLayoutRecord layout)
        {
            layout = null;
            if (Owner == null || string.IsNullOrEmpty(linkGuid))
            {
                return false;
            }

            layout = Owner.GetEdgeLayout(linkGuid);
            return layout != null;
        }

        private void BuildRenderRoutePoints()
        {
            if (edgeControl == null)
            {
                _renderRoutePoints.Clear();
                return;
            }

            Vector2 sourcePortCenter = edgeControl.from;
            Vector2 targetPortCenter = edgeControl.to;

            Vector2 sourceStubPoint = sourcePortCenter + new Vector2(GetPortStubDirection(output, 1f) * PortStubLength, 0f);
            Vector2 targetStubPoint = targetPortCenter + new Vector2(GetPortStubDirection(input, -1f) * PortStubLength, 0f);
            List<Vector2> serializedReroutePoints = null;

            if (TryGetLayout(out var layout))
            {
                serializedReroutePoints = layout.reroutePoints;
            }

            BuildRenderRoutePoints(
                sourcePortCenter,
                sourceStubPoint,
                serializedReroutePoints,
                targetStubPoint,
                targetPortCenter,
                _renderRoutePoints);
        }

        private static void BuildRenderRoutePoints(
            Vector2 sourcePortCenter,
            Vector2 sourceStubPoint,
            IList<Vector2> serializedUserReroutePoints,
            Vector2 targetStubPoint,
            Vector2 targetPortCenter,
            List<Vector2> renderRoutePoints)
        {
            if (renderRoutePoints == null)
            {
                return;
            }

            renderRoutePoints.Clear();
            renderRoutePoints.Add(sourcePortCenter);

            if (serializedUserReroutePoints == null || serializedUserReroutePoints.Count == 0)
            {
                renderRoutePoints.Add(targetPortCenter);
                return;
            }

            renderRoutePoints.Add(sourceStubPoint);

            for (int i = 0; i < serializedUserReroutePoints.Count; i++)
            {
                renderRoutePoints.Add(serializedUserReroutePoints[i]);
            }

            renderRoutePoints.Add(targetStubPoint);
            renderRoutePoints.Add(targetPortCenter);
        }

        private static float GetPortStubDirection(Port port, float fallbackDirection)
        {
            if (port == null)
            {
                return fallbackDirection;
            }

            return port.direction == Direction.Output ? 1f
                : port.direction == Direction.Input ? -1f
                : fallbackDirection;
        }

        private void UpdateAddButton()
        {
            if (Owner == null || edgeControl == null)
            {
                _addButton.style.display = DisplayStyle.None;
                return;
            }

            _addButton.style.display = DisplayStyle.Flex;
            Vector2 mid = FindMidpoint(out _);
            _addButton.UpdatePosition(mid);
        }

        private Vector2 FindMidpoint(out int segmentIndex)
        {
            segmentIndex = 0;
            BuildRenderRoutePoints();

            if (_renderRoutePoints.Count == 0)
            {
                return Vector2.zero;
            }

            if (_renderRoutePoints.Count == 1)
            {
                return _renderRoutePoints[0];
            }

            float totalLength = 0f;
            for (int i = 0; i < _renderRoutePoints.Count - 1; i++)
            {
                totalLength += Vector2.Distance(_renderRoutePoints[i], _renderRoutePoints[i + 1]);
            }

            if (totalLength <= 0.0001f)
            {
                return _renderRoutePoints[0];
            }

            float targetLen = totalLength * 0.5f;
            float accumulated = 0f;

            for (int i = 0; i < _renderRoutePoints.Count - 1; i++)
            {
                float length = Vector2.Distance(_renderRoutePoints[i], _renderRoutePoints[i + 1]);
                if (length <= 0.0001f)
                {
                    continue;
                }

                if (accumulated + length >= targetLen)
                {
                    float t = (targetLen - accumulated) / length;
                    segmentIndex = i;
                    return Vector2.Lerp(_renderRoutePoints[i], _renderRoutePoints[i + 1], t);
                }

                accumulated += length;
            }

            segmentIndex = Mathf.Max(0, _renderRoutePoints.Count - 2);
            return _renderRoutePoints.Last();
        }

        private void SyncHandles()
        {
            if (!TryGetLayout(out var layout) || layout.reroutePoints == null || layout.reroutePoints.Count == 0)
            {
                ClearHandles();
                return;
            }

            if (_handles.Count != layout.reroutePoints.Count)
            {
                ClearHandles();
                for (int i = 0; i < layout.reroutePoints.Count; i++)
                {
                    var handle = new RerouteHandle(this, i);
                    Add(handle);
                    _handles.Add(handle);
                }
            }

            for (int i = 0; i < _handles.Count; i++)
            {
                var handle = _handles[i];
                var point = layout.reroutePoints[i];
                handle.Index = i;
                handle.SetPosition(new Rect(
                    point.x - (RerouteHandleSize * 0.5f),
                    point.y - (RerouteHandleSize * 0.5f),
                    RerouteHandleSize,
                    RerouteHandleSize));
            }
        }

        private void ClearHandles()
        {
            foreach (var handle in _handles)
            {
                handle.RemoveFromHierarchy();
            }

            _handles.Clear();
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            if (mgc == null || edgeControl == null)
            {
                return;
            }

            if (!HasSerializedReroutePoints())
            {
                return;
            }

            BuildRenderRoutePoints();
            if (_renderRoutePoints.Count < 2)
            {
                return;
            }

            bool isHighlighted = _focusState == EdgeFocusState.Highlighted;
            Color strokeColor = selected || isHighlighted
                ? new Color(0.24f, 0.73f, 1f)
                : edgeControl.inputColor;

            float width = edgeControl.edgeWidth;
            if (isHighlighted)
            {
                width *= 1.5f;
            }

#if UNITY_2022_2_OR_NEWER
            var painter = mgc.painter2D;
            painter.BeginPath();
            painter.MoveTo(_renderRoutePoints[0]);

            for (int i = 1; i < _renderRoutePoints.Count; i++)
            {
                painter.LineTo(_renderRoutePoints[i]);
            }

            painter.strokeColor = strokeColor;
            painter.lineWidth = width;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            painter.Stroke();
#else
            DrawPolylineMesh(mgc, _renderRoutePoints, strokeColor, width);
#endif
        }

#if !UNITY_2022_2_OR_NEWER
        private static void DrawPolylineMesh(MeshGenerationContext mgc, List<Vector2> points, Color color, float width)
        {
            if (mgc == null || points == null || points.Count < 2)
            {
                return;
            }

            float halfWidth = Mathf.Max(1f, width) * 0.5f;
            int segmentCount = 0;

            for (int i = 0; i < points.Count - 1; i++)
            {
                if ((points[i + 1] - points[i]).sqrMagnitude > 0.0001f)
                {
                    segmentCount++;
                }
            }

            if (segmentCount == 0)
            {
                return;
            }

            var vertices = new Vertex[segmentCount * 4];
            var indices = new ushort[segmentCount * 6];
            int vertexOffset = 0;
            int indexOffset = 0;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 start = points[i];
                Vector2 end = points[i + 1];
                Vector2 delta = end - start;

                if (delta.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                Vector2 normal = new Vector2(-delta.y, delta.x).normalized * halfWidth;

                vertices[vertexOffset + 0] = CreateVertex(start - normal, color);
                vertices[vertexOffset + 1] = CreateVertex(start + normal, color);
                vertices[vertexOffset + 2] = CreateVertex(end - normal, color);
                vertices[vertexOffset + 3] = CreateVertex(end + normal, color);

                indices[indexOffset + 0] = (ushort)(vertexOffset + 0);
                indices[indexOffset + 1] = (ushort)(vertexOffset + 1);
                indices[indexOffset + 2] = (ushort)(vertexOffset + 2);
                indices[indexOffset + 3] = (ushort)(vertexOffset + 1);
                indices[indexOffset + 4] = (ushort)(vertexOffset + 3);
                indices[indexOffset + 5] = (ushort)(vertexOffset + 2);

                vertexOffset += 4;
                indexOffset += 6;
            }

            var mesh = mgc.Allocate(vertices.Length, indices.Length, null);
            mesh.SetAllVertices(vertices);
            mesh.SetAllIndices(indices);
        }

        private static Vertex CreateVertex(Vector2 position, Color color)
        {
            return new Vertex
            {
                position = new Vector3(position.x, position.y, Vertex.nearZ),
                tint = color,
                uv = Vector2.zero
            };
        }
#endif

        private void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (Owner == null)
            {
                return;
            }

            Vector2 graphPos = panel.visualTree.ChangeCoordinatesTo(this, evt.mousePosition);
            evt.menu.AppendAction("Add Reroute Point", _ => AddReroutePoint(graphPos));
        }

        public void AddReroutePoint(Vector2 graphPos)
        {
            if (Owner == null)
            {
                return;
            }

            if (Owner.GraphSchemaVersion < 1)
            {
                EditorUtility.DisplayDialog(
                    "Upgrade Required",
                    "This graph must be upgraded before reroute points can be added.",
                    "OK");
                return;
            }

            if (string.IsNullOrEmpty(linkGuid))
            {
                var link = Owner.links.FirstOrDefault(l =>
                    string.Equals(l.fromGuid, fromGuid, StringComparison.Ordinal) &&
                    string.Equals(l.toGuid, toGuid, StringComparison.Ordinal) &&
                    l.fromPortIndex == fromPortIndex);

                if (link == null)
                {
                    return;
                }

                Undo.RecordObject(Owner, "Add Reroute Point");
                link.AssignLinkGuidIfMissing(Guid.NewGuid().ToString("N"));
                linkGuid = link.LinkGuid;
            }
            else
            {
                Undo.RecordObject(Owner, "Add Reroute Point");
            }

            var layout = Owner.GetOrCreateEdgeLayoutForEditor(linkGuid, fromGuid, toGuid, fromPortIndex);
            FindInsertionPlacement(graphPos, layout.reroutePoints.Count, out int index, out Vector2 insertionPoint);
            layout.reroutePoints.Insert(index, insertionPoint);

            EditorUtility.SetDirty(Owner);
            UpdateEdgeControl();
        }

        private void FindInsertionPlacement(Vector2 hintPos, int serializedReroutePointCount, out int insertionIndex, out Vector2 insertionPoint)
        {
            insertionIndex = serializedReroutePointCount;
            insertionPoint = hintPos;

            BuildRenderRoutePoints();
            if (_renderRoutePoints.Count < 2)
            {
                return;
            }

            float minDistance = float.MaxValue;
            int bestSegmentIndex = 0;

            for (int i = 0; i < _renderRoutePoints.Count - 1; i++)
            {
                float distance = HandleUtility.DistancePointToLineSegment(hintPos, _renderRoutePoints[i], _renderRoutePoints[i + 1]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestSegmentIndex = i;
                }
            }

            insertionIndex = GetSerializedInsertionIndex(bestSegmentIndex, serializedReroutePointCount);
            insertionPoint = Vector2.Lerp(_renderRoutePoints[bestSegmentIndex], _renderRoutePoints[bestSegmentIndex + 1], 0.5f);
        }

        private static int GetSerializedInsertionIndex(int renderSegmentIndex, int serializedReroutePointCount)
        {
            return Mathf.Clamp(renderSegmentIndex - 1, 0, serializedReroutePointCount);
        }

        public void DeleteReroutePoint(int index)
        {
            if (!TryGetLayout(out var layout) || layout.reroutePoints == null || index < 0 || index >= layout.reroutePoints.Count)
            {
                return;
            }

            Undo.RecordObject(Owner, "Delete Reroute Point");
            layout.reroutePoints.RemoveAt(index);
            EditorUtility.SetDirty(Owner);
            UpdateEdgeControl();
        }

        public void OnDragStart()
        {
            if (_isDragging)
            {
                return;
            }

            _isDragging = true;
            Undo.RecordObject(Owner, "Move Reroute Point");
        }

        public void UpdateReroutePoint(int index, Vector2 newPos)
        {
            if (!TryGetLayout(out var layout) || layout.reroutePoints == null || index < 0 || index >= layout.reroutePoints.Count)
            {
                return;
            }

            if (layout.reroutePoints[index] == newPos)
            {
                return;
            }

            layout.reroutePoints[index] = newPos;
            EditorUtility.SetDirty(Owner);
            UpdateEdgeControl();
        }

        public void OnDragEnd()
        {
            _isDragging = false;
        }

        internal void DEBUG_AddTestReroutePoint(Vector2 graphPosition)
        {
            AddReroutePoint(graphPosition);
        }

        public sealed class RerouteHandle : GraphElement
        {
            public int Index { get; set; }
            public DialogGraphEdge ParentEdge { get; }

            public RerouteHandle(DialogGraphEdge edge, int index)
            {
                ParentEdge = edge;
                Index = index;

                style.width = RerouteHandleSize;
                style.height = RerouteHandleSize;
                style.position = Position.Absolute;
                style.backgroundColor = new Color(0.24f, 0.73f, 1f);
                style.borderBottomLeftRadius = 6;
                style.borderBottomRightRadius = 6;
                style.borderTopLeftRadius = 6;
                style.borderTopRightRadius = 6;
                style.borderBottomWidth = 1;
                style.borderLeftWidth = 1;
                style.borderRightWidth = 1;
                style.borderTopWidth = 1;
                style.borderBottomColor = Color.white;
                style.borderLeftColor = Color.white;
                style.borderRightColor = Color.white;
                style.borderTopColor = Color.white;
                style.marginTop = 0;
                style.marginBottom = 0;
                style.marginLeft = 0;
                style.marginRight = 0;
                style.paddingTop = 0;
                style.paddingBottom = 0;
                style.paddingLeft = 0;
                style.paddingRight = 0;
                pickingMode = PickingMode.Position;

                this.AddManipulator(new RerouteHandleManipulator(this));
                this.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Delete Reroute Point", _ => ParentEdge.DeleteReroutePoint(Index));
                    evt.StopPropagation();
                }));
            }
        }

        private sealed class AddRerouteButton : VisualElement
        {
            public DialogGraphEdge ParentEdge { get; }
            public Vector2 TargetPosition { get; private set; }

            public AddRerouteButton(DialogGraphEdge edge)
            {
                ParentEdge = edge;

                style.width = AddButtonSize;
                style.height = AddButtonSize;
                style.position = Position.Absolute;
                style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
                style.borderBottomLeftRadius = 8;
                style.borderBottomRightRadius = 8;
                style.borderTopLeftRadius = 8;
                style.borderTopRightRadius = 8;
                style.borderBottomWidth = 1;
                style.borderLeftWidth = 1;
                style.borderRightWidth = 1;
                style.borderTopWidth = 1;

                var borderColor = new Color(0.61f, 0.73f, 1f, 1f);
                style.borderBottomColor = borderColor;
                style.borderLeftColor = borderColor;
                style.borderRightColor = borderColor;
                style.borderTopColor = borderColor;

                var label = new Label("+");
                label.style.color = Color.white;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.fontSize = 11;
                label.style.marginTop = -1;
                label.style.flexGrow = 1;
                label.pickingMode = PickingMode.Ignore;
                Add(label);

                pickingMode = PickingMode.Position;
                RegisterCallback<PointerDownEvent>(OnPointerDown);
                this.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Add Reroute Point", _ => ParentEdge.AddReroutePoint(TargetPosition));
                    evt.StopPropagation();
                }));
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                if (evt.button == 0)
                {
                    ParentEdge.AddReroutePoint(TargetPosition);
                    evt.StopPropagation();
                }
            }

            public void UpdatePosition(Vector2 position)
            {
                TargetPosition = position;
                style.left = position.x - (AddButtonSize * 0.5f);
                style.top = position.y - (AddButtonSize * 0.5f);
            }
        }

        private sealed class RerouteHandleManipulator : PointerManipulator
        {
            private readonly RerouteHandle _handle;
            private bool _active;
            private Vector2 _startMouseLocal;
            private Vector2 _startPointPos;

            public RerouteHandleManipulator(RerouteHandle handle)
            {
                _handle = handle;
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnPointerDown);
                target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                if (evt.button != 0 || _handle.ParentEdge.Owner == null)
                {
                    return;
                }

                var layout = _handle.ParentEdge.Owner.GetEdgeLayout(_handle.ParentEdge.linkGuid);
                if (layout == null || _handle.Index < 0 || _handle.Index >= layout.reroutePoints.Count)
                {
                    return;
                }

                _active = true;
                _startMouseLocal = _handle.ParentEdge.panel.visualTree.ChangeCoordinatesTo(_handle.ParentEdge, evt.position);
                _startPointPos = layout.reroutePoints[_handle.Index];

                _handle.ParentEdge.OnDragStart();
                target.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }

            private void OnPointerMove(PointerMoveEvent evt)
            {
                if (!_active || !target.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                Vector2 currentMouseLocal = _handle.ParentEdge.panel.visualTree.ChangeCoordinatesTo(_handle.ParentEdge, evt.position);
                Vector2 deltaLocal = currentMouseLocal - _startMouseLocal;
                _handle.ParentEdge.UpdateReroutePoint(_handle.Index, _startPointPos + deltaLocal);
                evt.StopPropagation();
            }

            private void OnPointerUp(PointerUpEvent evt)
            {
                if (!_active || !target.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                _active = false;
                target.ReleasePointer(evt.pointerId);
                _handle.ParentEdge.OnDragEnd();
                evt.StopPropagation();
            }
        }
    }
}
