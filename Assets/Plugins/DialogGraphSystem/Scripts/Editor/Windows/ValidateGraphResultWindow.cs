using System.Linq;
using DialogSystem.EditorTools.AI;
using DialogSystem.EditorTools.Resources;
using DialogSystem.EditorTools.Services.Validation;
using DialogSystem.EditorTools.View.Elements.Nodes;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    /// <summary>
    /// Read-only window that displays the result of a ValidateGraph command.
    ///
    /// Shows:
    ///   - Summary line (error / warning / info counts)
    ///   - Per-issue rows with severity icon, code, and message
    ///   - "Ping" button per issue that selects and frames the offending node
    ///
    /// No graph mutation happens from this window.
    /// </summary>
    public sealed class ValidateGraphResultWindow : EditorWindow
    {
        // ── palette ───────────────────────────────────────────────────────────
        private static readonly Color BgWindow     = new Color(0.133f, 0.141f, 0.157f);
        private static readonly Color BgHeader     = new Color(0.118f, 0.125f, 0.141f);
        private static readonly Color BorderSubtle = new Color(0.212f, 0.224f, 0.255f);
        private static readonly Color TextPrimary  = new Color(0.847f, 0.863f, 0.906f);
        private static readonly Color TextMuted    = new Color(0.478f, 0.510f, 0.573f);
        private static readonly Color AccentBlue   = new Color(0.165f, 0.431f, 0.745f);
        private static readonly Color AccentBlueLt = new Color(0.188f, 0.502f, 0.878f);
        private static readonly Color BtnNeutralBg  = new Color(0.212f, 0.231f, 0.267f);
        private static readonly Color BtnNeutralBdr = new Color(0.278f, 0.302f, 0.353f);
        private static readonly Color ErrBg  = new Color(0.235f, 0.125f, 0.125f);
        private static readonly Color ErrBdr = new Color(0.427f, 0.180f, 0.180f);
        private static readonly Color ErrText = new Color(0.949f, 0.525f, 0.525f);
        private static readonly Color WarnBg  = new Color(0.220f, 0.180f, 0.098f);
        private static readonly Color WarnBdr = new Color(0.400f, 0.314f, 0.118f);
        private static readonly Color WarnText = new Color(0.949f, 0.769f, 0.369f);
        private static readonly Color InfoBg  = new Color(0.118f, 0.180f, 0.235f);
        private static readonly Color InfoBdr = new Color(0.180f, 0.278f, 0.380f);
        private static readonly Color InfoText = new Color(0.502f, 0.710f, 0.918f);
        private static readonly Color OkBg   = new Color(0.125f, 0.212f, 0.141f);
        private static readonly Color OkBdr  = new Color(0.180f, 0.329f, 0.208f);
        private static readonly Color OkText = new Color(0.502f, 0.835f, 0.604f);

        // ── state ─────────────────────────────────────────────────────────────
        private DialogGraphValidationResult _result;
        private DialogGraphEditorWindow _owner;

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Opens the validation result window.</summary>
        public static void Open(
            DialogGraphValidationResult result,
            DialogGraphEditorWindow owner)
        {
            if (result == null)
            {
                Debug.LogWarning("[ValidateGraphResultWindow] Called with null result.");
                return;
            }

            var window = CreateInstance<ValidateGraphResultWindow>();
            window.titleContent = new GUIContent("Graph Validation");
            window._result = result;
            window._owner  = owner;
            window.minSize = new Vector2(560f, 420f);
            window.ShowUtility();
            window.BuildUi();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UI construction
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

            root.Add(BuildHeader());
            root.Add(BuildSummaryBar());

            if (_result.Issues.Count == 0)
            {
                root.Add(BuildCleanState());
            }
            else
            {
                var scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.style.flexGrow    = 1;
                scroll.style.paddingLeft = 10f;
                scroll.style.paddingRight = 10f;
                scroll.style.paddingTop  = 8f;
                scroll.style.paddingBottom = 8f;

                foreach (var issue in _result.Issues)
                    scroll.Add(BuildIssueRow(issue));

                root.Add(scroll);
            }

            root.Add(BuildFooter());
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection    = FlexDirection.Column;
            header.style.backgroundColor  = BgHeader;
            header.style.paddingLeft      = 14f;
            header.style.paddingRight     = 14f;
            header.style.paddingTop       = 12f;
            header.style.paddingBottom    = 10f;
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = BorderSubtle;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems    = Align.Center;
            titleRow.style.marginBottom  = 3f;

            var t = new Label("Graph Validation Results");
            t.style.color                  = TextPrimary;
            t.style.fontSize               = 13f;
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            t.style.flexGrow               = 1;
            titleRow.Add(t);

            // Status badge
            var badgeText  = _result.IsValid ? "VALID" : "INVALID";
            var badgeBg    = _result.IsValid ? OkBg  : ErrBg;
            var badgeBdr   = _result.IsValid ? OkBdr : ErrBdr;
            var badgeColor = _result.IsValid ? OkText : ErrText;

            var badge = new Label(badgeText);
            badge.style.backgroundColor  = badgeBg;
            badge.style.borderTopWidth    = badge.style.borderBottomWidth =
            badge.style.borderLeftWidth   = badge.style.borderRightWidth  = 1f;
            badge.style.borderTopColor    = badge.style.borderBottomColor =
            badge.style.borderLeftColor   = badge.style.borderRightColor  = badgeBdr;
            badge.style.borderTopLeftRadius     = badge.style.borderTopRightRadius    =
            badge.style.borderBottomLeftRadius  = badge.style.borderBottomRightRadius = 4f;
            badge.style.color      = badgeColor;
            badge.style.fontSize   = 9f;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.paddingLeft  = 6f;
            badge.style.paddingRight = 6f;
            badge.style.paddingTop   = 2f;
            badge.style.paddingBottom = 2f;
            titleRow.Add(badge);

            header.Add(titleRow);

            var sub = new Label("Read-only structural check. No graph changes are made.");
            sub.style.color      = TextMuted;
            sub.style.fontSize   = 11f;
            sub.style.whiteSpace = WhiteSpace.Normal;
            header.Add(sub);

            return header;
        }

        private VisualElement BuildSummaryBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection  = FlexDirection.Row;
            bar.style.alignItems     = Align.Center;
            bar.style.paddingLeft    = 14f;
            bar.style.paddingRight   = 14f;
            bar.style.paddingTop     = 7f;
            bar.style.paddingBottom  = 7f;
            bar.style.borderBottomWidth = 1f;
            bar.style.borderBottomColor = BorderSubtle;
            bar.style.backgroundColor   = BgHeader;

            var errors   = _result.ErrorCount;
            var warnings = _result.WarningCount;
            var infos    = _result.Issues.Count(i => i.Severity == DialogGraphValidationSeverity.Info);

            bar.Add(BuildCountChip($"{errors} Error(s)",   ErrBg,  ErrBdr,  ErrText));
            bar.Add(BuildCountChip($"{warnings} Warning(s)", WarnBg, WarnBdr, WarnText));
            bar.Add(BuildCountChip($"{infos} Info",        InfoBg, InfoBdr, InfoText));

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            bar.Add(spacer);

            var total = new Label($"{_result.Issues.Count} total issue(s)");
            total.style.color    = TextMuted;
            total.style.fontSize = 11f;
            bar.Add(total);

            return bar;
        }

        private static VisualElement BuildCountChip(string text, Color bg, Color bdr, Color fg)
        {
            var chip = new Label(text);
            chip.style.backgroundColor  = bg;
            chip.style.borderTopWidth    = chip.style.borderBottomWidth =
            chip.style.borderLeftWidth   = chip.style.borderRightWidth  = 1f;
            chip.style.borderTopColor    = chip.style.borderBottomColor =
            chip.style.borderLeftColor   = chip.style.borderRightColor  = bdr;
            chip.style.borderTopLeftRadius     = chip.style.borderTopRightRadius    =
            chip.style.borderBottomLeftRadius  = chip.style.borderBottomRightRadius = 4f;
            chip.style.color      = fg;
            chip.style.fontSize   = 10f;
            chip.style.paddingLeft  = 6f;
            chip.style.paddingRight = 6f;
            chip.style.paddingTop   = 2f;
            chip.style.paddingBottom = 2f;
            chip.style.marginRight  = 6f;
            return chip;
        }

        private VisualElement BuildIssueRow(DialogGraphValidationIssue issue)
        {
            Color bg, bdr, fg;
            DialogGraphIconId iconId;
            string iconClass;

            switch (issue.Severity)
            {
                case DialogGraphValidationSeverity.Error:
                    bg = ErrBg; bdr = ErrBdr; fg = ErrText; iconId = DialogGraphIconId.StatusError; iconClass = "dgs-icon--error"; break;
                case DialogGraphValidationSeverity.Warning:
                    bg = WarnBg; bdr = WarnBdr; fg = WarnText; iconId = DialogGraphIconId.StatusWarning; iconClass = "dgs-icon--warning"; break;
                default:
                    bg = InfoBg; bdr = InfoBdr; fg = InfoText; iconId = DialogGraphIconId.StatusInfo; iconClass = "dgs-icon--brand"; break;
            }

            var row = new VisualElement();
            row.style.backgroundColor  = bg;
            row.style.borderTopWidth    = row.style.borderBottomWidth =
            row.style.borderRightWidth  = 1f;
            row.style.borderLeftWidth   = 4f;
            row.style.borderTopColor    = row.style.borderBottomColor =
            row.style.borderLeftColor   = row.style.borderRightColor  = bdr;
            row.style.borderTopLeftRadius     = row.style.borderTopRightRadius    =
            row.style.borderBottomLeftRadius  = row.style.borderBottomRightRadius = 4f;
            row.style.paddingLeft   = 10f;
            row.style.paddingRight  = 8f;
            row.style.paddingTop    = 7f;
            row.style.paddingBottom = 7f;
            row.style.marginBottom  = 5f;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems    = Align.Center;

            // Severity icon circle
            var icon = DialogGraphIconManager.CreateImage(iconId, "dgs-icon--sm", iconClass);
            icon.style.minWidth = 16f;
            icon.style.width = 16f;
            row.Add(icon);

            // Code + message
            var textBlock = new VisualElement();
            textBlock.style.flexGrow   = 1;
            textBlock.style.marginLeft = 6f;

            var codeLabel = new Label(issue.Code);
            codeLabel.style.color       = fg;
            codeLabel.style.fontSize    = 9f;
            codeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            codeLabel.style.backgroundColor  = bdr;
            codeLabel.style.borderTopLeftRadius     = codeLabel.style.borderTopRightRadius    =
            codeLabel.style.borderBottomLeftRadius  = codeLabel.style.borderBottomRightRadius = 3f;
            codeLabel.style.paddingLeft   = 5f;
            codeLabel.style.paddingRight  = 5f;
            codeLabel.style.paddingTop    = 1f;
            codeLabel.style.paddingBottom = 1f;
            codeLabel.style.marginBottom  = 3f;
            codeLabel.style.alignSelf     = Align.FlexStart;
            textBlock.Add(codeLabel);

            var msgLabel = new Label(issue.Message);
            msgLabel.style.color      = fg;
            msgLabel.style.fontSize   = 11f;
            msgLabel.style.whiteSpace = WhiteSpace.Normal;
            textBlock.Add(msgLabel);

            row.Add(textBlock);

            // Ping button (only if there is a node GUID)
            if (!string.IsNullOrEmpty(issue.NodeGuid) && _owner != null)
            {
                var pingBtn = new Button(() => PingNode(issue.NodeGuid)) { text = "Ping" };
                pingBtn.style.backgroundColor  = BtnNeutralBg;
                pingBtn.style.borderTopWidth    = pingBtn.style.borderBottomWidth =
                pingBtn.style.borderLeftWidth   = pingBtn.style.borderRightWidth  = 1f;
                pingBtn.style.borderTopColor    = pingBtn.style.borderBottomColor =
                pingBtn.style.borderLeftColor   = pingBtn.style.borderRightColor  = BtnNeutralBdr;
                pingBtn.style.borderTopLeftRadius     = pingBtn.style.borderTopRightRadius    =
                pingBtn.style.borderBottomLeftRadius  = pingBtn.style.borderBottomRightRadius = 4f;
                pingBtn.style.color    = TextPrimary;
                pingBtn.style.fontSize = 10f;
                pingBtn.style.height   = 22f;
                pingBtn.style.paddingLeft  = 8f;
                pingBtn.style.paddingRight = 8f;
                pingBtn.style.flexShrink   = 0f;
                row.Add(pingBtn);
            }

            return row;
        }

        private VisualElement BuildCleanState()
        {
            var container = new VisualElement();
            container.style.flexGrow      = 1;
            container.style.alignItems    = Align.Center;
            container.style.justifyContent = Justify.Center;
            container.style.paddingTop    = 30f;

            var icon = DialogGraphIconManager.CreateImage(DialogGraphIconId.StatusSuccess, "dgs-icon--lg", "dgs-icon--success");
            icon.style.width = 32f;
            icon.style.height = 32f;
            icon.style.minWidth = 32f;
            icon.style.minHeight = 32f;
            icon.style.marginBottom = 12f;
            container.Add(icon);

            var msg = new Label("No issues found. Graph is structurally valid.");
            msg.style.color      = TextMuted;
            msg.style.fontSize   = 13f;
            msg.style.whiteSpace = WhiteSpace.Normal;
            container.Add(msg);

            return container;
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

            var closeBtn = new Button(() => Close()) { text = "Close" };
            closeBtn.style.backgroundColor  = AccentBlue;
            closeBtn.style.borderTopWidth    = closeBtn.style.borderBottomWidth =
            closeBtn.style.borderLeftWidth   = closeBtn.style.borderRightWidth  = 1f;
            closeBtn.style.borderTopColor    = closeBtn.style.borderBottomColor =
            closeBtn.style.borderLeftColor   = closeBtn.style.borderRightColor  = AccentBlueLt;
            closeBtn.style.borderTopLeftRadius     = closeBtn.style.borderTopRightRadius    =
            closeBtn.style.borderBottomLeftRadius  = closeBtn.style.borderBottomRightRadius = 5f;
            closeBtn.style.color    = Color.white;
            closeBtn.style.fontSize = 12f;
            closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            closeBtn.style.height   = 28f;
            closeBtn.style.paddingLeft  = 20f;
            closeBtn.style.paddingRight = 20f;
            footer.Add(closeBtn);

            return footer;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Ping helper
        // ─────────────────────────────────────────────────────────────────────

        private void PingNode(string guid)
        {
            if (_owner == null || string.IsNullOrEmpty(guid)) return;

            if (!_owner.FocusNodeByGuid(guid))
            {
                Debug.LogWarning($"[ValidateGraphResultWindow] Could not focus validation issue node '{guid}'. The graph may no longer be open or the node may have been removed.");
            }
        }

        private static string GetNodeGuid(Node node)
        {
            switch (node)
            {
                case DialogNodeView d: return d.GUID;
                case ChoiceNodeView choice: return choice.GUID;
                case ActionNodeView a: return a.GUID;
                case ConditionNodeView condition: return condition.GUID;
                case VariableMutationNodeView v: return v.GUID;
                case GraphJumpNodeView g: return g.GUID;
                case StartNodeView  s: return s.GUID;
                case EndNodeView    e: return e.GUID;
                default: return string.Empty;
            }
        }
    }
}
