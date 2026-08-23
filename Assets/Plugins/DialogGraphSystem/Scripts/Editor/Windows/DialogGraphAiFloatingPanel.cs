using System;
using DialogSystem.EditorTools.PublicInformation;
using DialogSystem.EditorTools.Resources;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    /// <summary>
    /// Draggable floating AI Assistant panel over the graph canvas.
    /// The panel docks back near the launcher when collapsed/hidden and
    /// returns to its default open position when expanded.
    /// </summary>
    internal sealed class DialogGraphAiFloatingPanel : VisualElement
    {
        private const string PrefKeyOpen = "DGS_AiFloat_Open";

        private const float ExpandedWidth = 400f;
        private const float CollapsedWidth = 210f;
        private const float ExpandedLeft = 16f;
        private const float ExpandedBottom = 60f;
        private const float CollapsedLeft = 86f;
        private const float CollapsedBottom = 28f;

        private const string AiExtensionIconAssetPath = "Assets/DialogGraphSystem/Resources/Brand/ai-extension-icon-placeholder.png";

        private bool _isDragging;
        private Vector2 _pointerDownWorld;
        private Vector2 _panelStartTopLeft;

        private readonly VisualElement _body;
        private readonly VisualElement _separator;
        private readonly Button _minimizeBtn;
        private bool _bodyVisible = true;

        public bool IsOpen => resolvedStyle.display == DisplayStyle.Flex;

        public DialogGraphAiFloatingPanel(VisualElement aiContent, Action openSettings = null)
        {
            AddToClassList("dlg-ai-float-panel");
            style.position = Position.Absolute;

            var header = new VisualElement();
            header.AddToClassList("dlg-ai-float-header");

            var dragHandle = new Label("|||");
            dragHandle.AddToClassList("dlg-ai-float-drag");
            header.Add(dragHandle);

            var spark = new Label("*");
            spark.AddToClassList("dlg-ai-float-spark");
            header.Add(spark);

            var titleLabel = new Label("AI Assistant");
            titleLabel.AddToClassList("dlg-ai-float-title");
            header.Add(titleLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            if (openSettings != null)
            {
                var settingsBtn = new Button(openSettings) { tooltip = "Open AI Settings" };
                settingsBtn.AddToClassList("dlg-ai-float-ctrl-btn");
                settingsBtn.Add(DialogGraphIconManager.CreateImage(
                    DialogGraphIconId.ToolbarSettings, "dgs-icon--muted"));
                header.Add(settingsBtn);
            }

            _minimizeBtn = new Button(ToggleBody) { tooltip = "Collapse panel" };
            _minimizeBtn.AddToClassList("dlg-ai-float-ctrl-btn");
            header.Add(_minimizeBtn);

            var closeBtn = new Button(Hide)
            {
                text = "x",
                tooltip = "Close AI Assistant"
            };
            closeBtn.AddToClassList("dlg-ai-float-ctrl-btn");
            header.Add(closeBtn);

            Add(header);

            _separator = new VisualElement();
            _separator.AddToClassList("dlg-ai-float-sep");
            Add(_separator);

            _body = new ScrollView(ScrollViewMode.Vertical);
            _body.AddToClassList("dlg-ai-float-body");

            if (aiContent != null)
            {
                _body.Add(aiContent);
            }
            else
            {
                _body.Add(CreateMissingExtensionAd());
            }

            Add(_body);

            header.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);

            ApplyBodyVisibility(false);

            if (EditorPrefs.GetBool(PrefKeyOpen, false))
            {
                Show();
            }
            else
            {
                DockCollapsedPosition();
                style.display = DisplayStyle.None;
            }
        }

        public void Toggle()
        {
            if (resolvedStyle.display == DisplayStyle.Flex)
            {
                Hide();
                return;
            }

            Show();
        }

        public void Show()
        {
            _bodyVisible = true;
            ApplyBodyVisibility(false);
            DockExpandedPosition();
            style.display = DisplayStyle.Flex;
            EditorPrefs.SetBool(PrefKeyOpen, true);
        }

        public void Hide()
        {
            _bodyVisible = false;
            ApplyBodyVisibility(false);
            DockCollapsedPosition();
            style.display = DisplayStyle.None;
            EditorPrefs.SetBool(PrefKeyOpen, false);
        }

        private void ToggleBody()
        {
            _bodyVisible = !_bodyVisible;
            ApplyBodyVisibility(true);

            if (_bodyVisible)
            {
                DockExpandedPosition();
            }
            else
            {
                DockCollapsedPosition();
            }
        }

        private void ApplyBodyVisibility(bool animate)
        {
            _body.style.display = _bodyVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _separator.style.display = _bodyVisible ? DisplayStyle.Flex : DisplayStyle.None;
            style.width = _bodyVisible ? ExpandedWidth : CollapsedWidth;
            _minimizeBtn.text = _bodyVisible ? "-" : "+";
            _minimizeBtn.tooltip = _bodyVisible ? "Collapse panel" : "Expand panel";
        }

        private void OnHeaderPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            _isDragging = true;
            this.CapturePointer(evt.pointerId);
            _pointerDownWorld = evt.position;

            var currentLayout = layout;
            _panelStartTopLeft = new Vector2(currentLayout.x, currentLayout.y);

            style.bottom = StyleKeyword.Auto;
            style.right = StyleKeyword.Auto;
            style.left = currentLayout.x;
            style.top = currentLayout.y;

            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging)
            {
                return;
            }

            var delta = (Vector2)evt.position - _pointerDownWorld;
            var newLeft = _panelStartTopLeft.x + delta.x;
            var newTop = _panelStartTopLeft.y + delta.y;

            if (parent != null)
            {
                var parentWidth = parent.resolvedStyle.width;
                var parentHeight = parent.resolvedStyle.height;
                var panelWidth = resolvedStyle.width;
                newLeft = Mathf.Clamp(newLeft, 0f, Mathf.Max(0f, parentWidth - Mathf.Max(panelWidth, 80f)));
                newTop = Mathf.Clamp(newTop, 0f, Mathf.Max(0f, parentHeight - 40f));
            }

            style.left = newLeft;
            style.top = newTop;

            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;
            this.ReleasePointer(evt.pointerId);
        }

        private void DockExpandedPosition()
        {
            DockTo(ExpandedLeft, ExpandedBottom);
        }

        private void DockCollapsedPosition()
        {
            DockTo(CollapsedLeft, CollapsedBottom);
        }

        private void DockTo(float left, float bottom)
        {
            style.top = StyleKeyword.Auto;
            style.right = StyleKeyword.Auto;
            style.left = left;
            style.bottom = bottom;
        }

        private static VisualElement CreateMissingExtensionAd()
        {
            var ad = new VisualElement();
            ad.AddToClassList("dlg-ai-extension-ad");

            var iconWrap = new VisualElement();
            iconWrap.AddToClassList("dlg-ai-extension-ad-icon");

            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AiExtensionIconAssetPath);
            if (iconTexture != null)
            {
                var icon = new Image
                {
                    image = iconTexture,
                    scaleMode = ScaleMode.ScaleToFit
                };
                icon.AddToClassList("dlg-ai-extension-ad-icon-image");
                iconWrap.Add(icon);
            }
            else
            {
                var placeholder = new Label("AI");
                placeholder.AddToClassList("dlg-ai-extension-ad-icon-placeholder");
                iconWrap.Add(placeholder);
            }

            ad.Add(iconWrap);

            var title = new Label(DialogPublicInformationCatalog.AiComingSoonTitle);
            title.AddToClassList("dlg-ai-extension-ad-title");
            ad.Add(title);

            var copy = new Label(
                "Core 3.0.0 is complete without AI. AI Extension 2.0 remains a separate ready/not-ready checkpoint for DGS 3.0.1.");
            copy.AddToClassList("dlg-ai-extension-ad-copy");
            ad.Add(copy);

            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("dlg-ai-extension-ad-actions");

            var learnMoreButton = new Button(() => DialogPublicInformationCatalog.Open("ai-extension"))
            {
                text = "Learn More",
                tooltip = DialogPublicInformationCatalog.AiLearnMoreUrl
            };
            learnMoreButton.AddToClassList("dlg-ai-extension-ad-primary");
            buttonRow.Add(learnMoreButton);

            var bundledButton = new Button(() => DialogPublicInformationCatalog.OpenBundled("ai-extension"))
            {
                text = "View Bundled Copy"
            };
            bundledButton.AddToClassList("dlg-ai-extension-ad-secondary");
            buttonRow.Add(bundledButton);

            ad.Add(buttonRow);

            return ad;
        }
    }
}
