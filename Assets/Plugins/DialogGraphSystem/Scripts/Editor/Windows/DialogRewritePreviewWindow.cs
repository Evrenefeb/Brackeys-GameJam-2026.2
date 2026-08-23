using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    /// <summary>
    /// Modal preview window for AI dialog rewrites.
    ///
    /// Layout:
    ///   Header  — title + subtitle
    ///   Body    — Original (read-only left) | Rewritten (editable right), side by side
    ///   Footer  — Cancel  /  Apply
    ///
    /// The rewritten text is editable before applying so the user can fine-tune
    /// the AI output. No mutation happens until Apply is pressed.
    /// </summary>
    public sealed class DialogRewritePreviewWindow : EditorWindow
    {
        private Action<string> _onApply;
        private Action         _onCancel;
        private string         _originalText;
        private string         _rewrittenText;

        // ── palette ───────────────────────────────────────────────────────────
        private static readonly Color BgWindow      = new Color(0.133f, 0.141f, 0.157f);
        private static readonly Color BgHeader      = new Color(0.118f, 0.125f, 0.141f);
        private static readonly Color BgCard        = new Color(0.153f, 0.161f, 0.180f);
        private static readonly Color BgCardAlt     = new Color(0.173f, 0.180f, 0.204f);
        private static readonly Color BgEditable    = new Color(0.118f, 0.125f, 0.145f);
        private static readonly Color BorderSubtle  = new Color(0.212f, 0.224f, 0.255f);
        private static readonly Color BorderAccent  = new Color(0.235f, 0.392f, 0.588f);
        private static readonly Color TextPrimary   = new Color(0.847f, 0.863f, 0.906f);
        private static readonly Color TextMuted     = new Color(0.478f, 0.510f, 0.573f);
        private static readonly Color TextDim       = new Color(0.353f, 0.380f, 0.435f);
        private static readonly Color AccentBlue    = new Color(0.165f, 0.431f, 0.745f);
        private static readonly Color AccentBlueLt  = new Color(0.188f, 0.502f, 0.878f);
        private static readonly Color BtnNeutralBg  = new Color(0.212f, 0.231f, 0.267f);
        private static readonly Color BtnNeutralBdr = new Color(0.278f, 0.302f, 0.353f);
        private static readonly Color OriginalBadge = new Color(0.200f, 0.216f, 0.255f);
        private static readonly Color RewriteBadge  = new Color(0.165f, 0.282f, 0.188f);
        private static readonly Color RewriteBdrBdg = new Color(0.220f, 0.392f, 0.259f);
        private static readonly Color RewriteText   = new Color(0.502f, 0.835f, 0.604f);
        private static readonly Color OriginalBdr   = new Color(0.255f, 0.278f, 0.333f);
        private static readonly Color OriginalText  = new Color(0.600f, 0.635f, 0.706f);

        // ─────────────────────────────────────────────────────────────────────

        public static void Open(
            string originalText,
            string rewrittenText,
            Action<string> onApply,
            Action onCancel = null)
        {
            var window = CreateInstance<DialogRewritePreviewWindow>();
            window.titleContent   = new GUIContent("AI Rewrite Preview");
            window._originalText  = originalText  ?? string.Empty;
            window._rewrittenText = rewrittenText ?? string.Empty;
            window._onApply       = onApply;
            window._onCancel      = onCancel;
            window.minSize        = new Vector2(640f, 400f);
            window.ShowUtility();
            window.BuildUi();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Build
        // ─────────────────────────────────────────────────────────────────────

        private void BuildUi()
        {
            var root = rootVisualElement;
            root.Clear();

            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(DialogSystem.Runtime.Utils.TextResources.STYLE_PATH);
            if (ss != null) root.styleSheets.Add(ss);

            root.style.backgroundColor = BgWindow;
            root.style.flexDirection   = FlexDirection.Column;
            root.style.flexGrow        = 1;

            // ── header ────────────────────────────────────────────────────────
            root.Add(BuildHeader());

            // ── body: two columns ─────────────────────────────────────────────
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow      = 1;
            body.style.paddingLeft   = 12f;
            body.style.paddingRight  = 12f;
            body.style.paddingTop    = 12f;
            body.style.paddingBottom = 4f;

            body.Add(BuildOriginalPanel());
            body.Add(BuildDivider());
            body.Add(BuildRewritePanel());

            root.Add(body);

            // ── footer ────────────────────────────────────────────────────────
            root.Add(BuildFooter());
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.style.backgroundColor   = BgHeader;
            header.style.paddingLeft       = 14f;
            header.style.paddingRight      = 14f;
            header.style.paddingTop        = 12f;
            header.style.paddingBottom     = 10f;
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = BorderSubtle;

            var title = new Label("Review Rewrite");
            title.style.color                  = TextPrimary;
            title.style.fontSize               = 13f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom           = 3f;
            header.Add(title);

            var sub = new Label("Compare the original and rewritten line. " +
                                "Edit the rewrite if needed, then Apply or Cancel.");
            sub.style.color      = TextMuted;
            sub.style.fontSize   = 11f;
            sub.style.whiteSpace = WhiteSpace.Normal;
            header.Add(sub);

            return header;
        }

        private VisualElement BuildOriginalPanel()
        {
            var panel = new VisualElement();
            panel.style.flexGrow     = 1;
            panel.style.flexBasis    = 0;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.marginRight  = 6f;

            panel.Add(BuildColumnBadge("Original", OriginalBadge, OriginalBdr, OriginalText));

            var card = new VisualElement();
            card.style.flexGrow         = 1;
            card.style.backgroundColor  = BgCard;
            card.style.borderTopWidth    = card.style.borderBottomWidth =
            card.style.borderLeftWidth   = card.style.borderRightWidth  = 1f;
            card.style.borderTopColor    = card.style.borderBottomColor =
            card.style.borderLeftColor   = card.style.borderRightColor  = BorderSubtle;
            card.style.borderTopLeftRadius     = card.style.borderTopRightRadius    =
            card.style.borderBottomLeftRadius  = card.style.borderBottomRightRadius = 6f;
            card.style.overflow         = Overflow.Hidden;

            var field = new TextField
            {
                value      = _originalText,
                multiline  = true,
                isReadOnly = true
            };
            StyleTextField(field, BgCard, TextDim, readOnly: true);
            card.Add(field);
            panel.Add(card);

            return panel;
        }

        private VisualElement BuildRewritePanel()
        {
            var panel = new VisualElement();
            panel.style.flexGrow      = 1;
            panel.style.flexBasis     = 0;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.marginLeft    = 6f;

            panel.Add(BuildColumnBadge("Rewritten (editable)", RewriteBadge, RewriteBdrBdg, RewriteText));

            var card = new VisualElement();
            card.style.flexGrow         = 1;
            card.style.backgroundColor  = BgEditable;
            card.style.borderTopWidth    = card.style.borderBottomWidth =
            card.style.borderLeftWidth   = card.style.borderRightWidth  = 1f;
            card.style.borderTopColor    = card.style.borderBottomColor =
            card.style.borderLeftColor   = card.style.borderRightColor  = BorderAccent;
            card.style.borderTopLeftRadius     = card.style.borderTopRightRadius    =
            card.style.borderBottomLeftRadius  = card.style.borderBottomRightRadius = 6f;
            card.style.overflow         = Overflow.Hidden;

            var field = new TextField
            {
                value     = _rewrittenText,
                multiline = true
            };
            StyleTextField(field, BgEditable, TextPrimary, readOnly: false);
            field.RegisterValueChangedCallback(evt => _rewrittenText = evt.newValue);
            card.Add(field);
            panel.Add(card);

            return panel;
        }

        private static VisualElement BuildColumnBadge(string text, Color bg, Color border, Color fg)
        {
            var badge = new Label(text);
            badge.style.backgroundColor  = bg;
            badge.style.borderTopWidth    = badge.style.borderBottomWidth =
            badge.style.borderLeftWidth   = badge.style.borderRightWidth  = 1f;
            badge.style.borderTopColor    = badge.style.borderBottomColor =
            badge.style.borderLeftColor   = badge.style.borderRightColor  = border;
            badge.style.borderTopLeftRadius     = badge.style.borderTopRightRadius    = 4f;
            badge.style.borderBottomLeftRadius  = badge.style.borderBottomRightRadius = 0f;
            badge.style.color    = fg;
            badge.style.fontSize = 10f;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.paddingLeft    = 8f;
            badge.style.paddingRight   = 8f;
            badge.style.paddingTop     = 3f;
            badge.style.paddingBottom  = 3f;
            badge.style.alignSelf      = Align.FlexStart;
            badge.style.marginBottom   = -1f;  // overlap card top border for flush look
            return badge;
        }

        private static VisualElement BuildDivider()
        {
            var div = new VisualElement();
            div.style.width           = 1f;
            div.style.backgroundColor = BorderSubtle;
            div.style.marginLeft      = 4f;
            div.style.marginRight     = 4f;
            div.style.marginTop       = 28f; // clear the badge row
            return div;
        }

        private VisualElement BuildFooter()
        {
            var footer = new VisualElement();
            footer.style.flexDirection   = FlexDirection.Row;
            footer.style.justifyContent  = Justify.FlexEnd;
            footer.style.paddingLeft     = 12f;
            footer.style.paddingRight    = 12f;
            footer.style.paddingTop      = 10f;
            footer.style.paddingBottom   = 10f;
            footer.style.borderTopWidth  = 1f;
            footer.style.borderTopColor  = BorderSubtle;
            footer.style.backgroundColor = BgHeader;

            var cancelBtn = new Button(() =>
            {
                _onCancel?.Invoke();
                Close();
            })
            { text = "Cancel" };
            StyleNeutralButton(cancelBtn);

            var applyBtn = new Button(() =>
            {
                _onApply?.Invoke(_rewrittenText);
                Close();
            })
            { text = "Apply" };
            StylePrimaryButton(applyBtn);

            footer.Add(cancelBtn);
            footer.Add(applyBtn);
            return footer;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Style helpers
        // ─────────────────────────────────────────────────────────────────────

        private static void StyleTextField(TextField field, Color bg, Color textColor, bool readOnly)
        {
            field.style.flexGrow        = 1;
            field.style.backgroundColor = bg;
            field.style.color           = textColor;
            field.style.fontSize        = 12f;
            field.style.whiteSpace      = WhiteSpace.Normal;
            field.style.borderTopWidth    = field.style.borderBottomWidth =
            field.style.borderLeftWidth   = field.style.borderRightWidth  = 0f;
            field.style.minHeight       = 120f;
            field.style.paddingLeft     = 10f;
            field.style.paddingRight    = 10f;
            field.style.paddingTop      = 8f;
            field.style.paddingBottom   = 8f;

            // Make the inner text input match
            field.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var input = field.Q<VisualElement>("unity-text-input");
                if (input == null) return;
                input.style.backgroundColor = bg;
                input.style.color           = textColor;
                input.style.borderTopWidth    = input.style.borderBottomWidth =
                input.style.borderLeftWidth   = input.style.borderRightWidth  = 0f;
            });
        }

        private static void StyleNeutralButton(Button btn)
        {
            btn.style.backgroundColor  = BtnNeutralBg;
            btn.style.borderTopWidth    = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth   = btn.style.borderRightWidth  = 1f;
            btn.style.borderTopColor    = btn.style.borderBottomColor =
            btn.style.borderLeftColor   = btn.style.borderRightColor  = BtnNeutralBdr;
            btn.style.borderTopLeftRadius     = btn.style.borderTopRightRadius    =
            btn.style.borderBottomLeftRadius  = btn.style.borderBottomRightRadius = 5f;
            btn.style.color    = TextPrimary;
            btn.style.fontSize = 12f;
            btn.style.height   = 28f;
            btn.style.paddingLeft  = 16f;
            btn.style.paddingRight = 16f;
            btn.style.marginRight  = 8f;
        }

        private static void StylePrimaryButton(Button btn)
        {
            btn.style.backgroundColor  = AccentBlue;
            btn.style.borderTopWidth    = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth   = btn.style.borderRightWidth  = 1f;
            btn.style.borderTopColor    = btn.style.borderBottomColor =
            btn.style.borderLeftColor   = btn.style.borderRightColor  = AccentBlueLt;
            btn.style.borderTopLeftRadius     = btn.style.borderTopRightRadius    =
            btn.style.borderBottomLeftRadius  = btn.style.borderBottomRightRadius = 5f;
            btn.style.color    = Color.white;
            btn.style.fontSize = 12f;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.height   = 28f;
            btn.style.paddingLeft  = 20f;
            btn.style.paddingRight = 20f;
        }
    }
}
