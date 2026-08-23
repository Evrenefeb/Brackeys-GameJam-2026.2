using System;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Definitions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    /// <summary>
    /// Professional creation popup for Character, Action, Environment and Scene Context assets.
    /// Replaces the old "create → ping inspector" flow with an in-editor modal form.
    /// </summary>
    public sealed class DialogCreateAssetWindow : EditorWindow
    {
        // ── Types ────────────────────────────────────────────────────────────
        private enum AssetKind { Character, Action, Environment, SceneContext }

        // ── Colours — match DialogRewritePreviewWindow palette ───────────────
        private static readonly Color BgWindow     = new Color(0.133f, 0.141f, 0.157f);
        private static readonly Color BgHeader     = new Color(0.118f, 0.125f, 0.141f);
        private static readonly Color BgCard       = new Color(0.153f, 0.161f, 0.180f);
        private static readonly Color BgField      = new Color(0.118f, 0.125f, 0.145f);
        private static readonly Color BorderSubtle = new Color(0.212f, 0.224f, 0.255f);
        private static readonly Color BorderAccent = new Color(0.235f, 0.392f, 0.588f);
        private static readonly Color TextPrimary  = new Color(0.847f, 0.863f, 0.906f);
        private static readonly Color TextMuted    = new Color(0.478f, 0.510f, 0.573f);
        private static readonly Color TextDim      = new Color(0.353f, 0.380f, 0.435f);
        private static readonly Color AccentBlue   = new Color(0.165f, 0.431f, 0.745f);
        private static readonly Color AccentBlueLt = new Color(0.188f, 0.502f, 0.878f);
        private static readonly Color BtnNeutralBg = new Color(0.212f, 0.231f, 0.267f);
        private static readonly Color BtnNeutralBdr= new Color(0.278f, 0.302f, 0.353f);
        private static readonly Color SectionTitle = new Color(0.380f, 0.408f, 0.467f);
        private static readonly Color ErrorRed     = new Color(0.878f, 0.318f, 0.318f);
        private static readonly Color TagBg        = new Color(0.200f, 0.216f, 0.255f);

        // ── State ─────────────────────────────────────────────────────────────
        private AssetKind                 _kind;
        private Action<ScriptableObject>  _onCreated;

        // Character form
        private string _charDisplayName = "New Character";
        private string _charId          = "character_id";
        private Sprite _charPortrait;
        private string _charDescription = string.Empty;
        private string _charSpeechStyle = string.Empty;

        // Action form
        private string _actionId          = "action_id";
        private string _actionDisplayName = "New Action";
        private string _actionDescription = string.Empty;
        private string _actionPayload     = "{}";
        private bool   _actionWait        = true;
        private float  _actionDelay       = 0f;

        // Environment form
        private string _envDisplayName = "New Environment";
        private string _envId          = "environment_id";
        private string _envDescription = string.Empty;
        private string _envAtmosphere  = "Neutral";
        private string _envTone        = "Neutral";

        // Scene Context form
        private string              _ctxAssetName   = "New Scene Context";
        private DialogEnvironmentSO _ctxEnvironment;
        private string              _ctxSceneGoal   = string.Empty;
        private string              _ctxTone        = "Neutral";
        private string              _ctxExtraRules  = string.Empty;

        // Validation
        private Label _errorLabel;

        // ── Public factory methods ────────────────────────────────────────────

        public static void OpenForCharacter(
            Action<DialogCharacterSO> onCreated,
            string prefillName    = "New Character")
        {
            var w = CreateInstance<DialogCreateAssetWindow>();
            w.titleContent      = new GUIContent("Create Character");
            w._kind             = AssetKind.Character;
            w._onCreated        = so => onCreated?.Invoke(so as DialogCharacterSO);
            w._charDisplayName  = string.IsNullOrWhiteSpace(prefillName) ? "New Character" : prefillName.Trim();
            w._charId           = DialogGraphDefinitionResolver.CreateSuggestedCharacterId(w._charDisplayName);
            w.minSize           = w.maxSize = new Vector2(420f, 490f);
            w.ShowUtility();
            w.BuildUi();
        }

        public static void OpenForAction(
            Action<DialogActionSO> onCreated,
            string prefillId      = "action_id",
            string prefillPayload = "{}")
        {
            var w = CreateInstance<DialogCreateAssetWindow>();
            w.titleContent      = new GUIContent("Create Action");
            w._kind             = AssetKind.Action;
            w._onCreated        = so => onCreated?.Invoke(so as DialogActionSO);
            w._actionId         = string.IsNullOrWhiteSpace(prefillId) ? "action_id" : prefillId.Trim();
            w._actionDisplayName= w._actionId;
            w._actionPayload    = string.IsNullOrWhiteSpace(prefillPayload) ? "{}" : prefillPayload;
            w.minSize           = w.maxSize = new Vector2(420f, 460f);
            w.ShowUtility();
            w.BuildUi();
        }

        public static void OpenForEnvironment(Action<DialogEnvironmentSO> onCreated)
        {
            var w = CreateInstance<DialogCreateAssetWindow>();
            w.titleContent = new GUIContent("Create Environment");
            w._kind        = AssetKind.Environment;
            w._onCreated   = so => onCreated?.Invoke(so as DialogEnvironmentSO);
            w.minSize      = w.maxSize = new Vector2(420f, 460f);
            w.ShowUtility();
            w.BuildUi();
        }

        public static void OpenForSceneContext(Action<DialogSceneContextSO> onCreated)
        {
            var w = CreateInstance<DialogCreateAssetWindow>();
            w.titleContent = new GUIContent("Create Scene Context");
            w._kind        = AssetKind.SceneContext;
            w._onCreated   = so => onCreated?.Invoke(so as DialogSceneContextSO);
            w.minSize      = w.maxSize = new Vector2(420f, 460f);
            w.ShowUtility();
            w.BuildUi();
        }

        // ── Build UI ─────────────────────────────────────────────────────────

        private void BuildUi()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.backgroundColor = BgWindow;
            root.style.flexDirection   = FlexDirection.Column;
            root.style.flexGrow        = 1;

            root.Add(BuildHeader());

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow      = 1;
            scroll.style.paddingLeft   = 14f;
            scroll.style.paddingRight  = 14f;
            scroll.style.paddingTop    = 12f;
            scroll.style.paddingBottom = 8f;

            switch (_kind)
            {
                case AssetKind.Character:    BuildCharacterForm(scroll); break;
                case AssetKind.Action:       BuildActionForm(scroll);    break;
                case AssetKind.Environment:  BuildEnvironmentForm(scroll);break;
                case AssetKind.SceneContext: BuildContextForm(scroll);   break;
            }

            root.Add(scroll);
            root.Add(BuildFooter());
        }

        // ── Header ────────────────────────────────────────────────────────────

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
            header.style.flexDirection     = FlexDirection.Row;
            header.style.alignItems        = Align.Center;

            var textCol = new VisualElement();
            textCol.style.flexGrow = 1;

            var (title, subtitle, badge) = _kind switch
            {
                AssetKind.Character   => ("Create Character",    "Fill in the fields below and click Create.", "CHARACTER"),
                AssetKind.Action      => ("Create Action",       "Define an action asset for this project.",   "ACTION"),
                AssetKind.Environment => ("Create Environment",  "Define a reusable environment asset.",       "ENVIRONMENT"),
                AssetKind.SceneContext=> ("Create Scene Context","Bundle environment, characters and intent.",  "CONTEXT SO"),
                _                    => ("Create Asset",         string.Empty, string.Empty)
            };

            var row = new VisualElement();
            row.style.flexDirection  = FlexDirection.Row;
            row.style.alignItems     = Align.Center;
            row.style.marginBottom   = 4f;

            var titleLabel = new Label(title);
            titleLabel.style.color                  = TextPrimary;
            titleLabel.style.fontSize               = 14f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginRight            = 8f;
            row.Add(titleLabel);

            var badgeLabel = new Label(badge);
            badgeLabel.style.backgroundColor = TagBg;
            badgeLabel.style.color           = TextMuted;
            badgeLabel.style.fontSize        = 9f;
            badgeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            badgeLabel.style.paddingLeft     = 5f;
            badgeLabel.style.paddingRight    = 5f;
            badgeLabel.style.paddingTop      = 2f;
            badgeLabel.style.paddingBottom   = 2f;
            SetRadius(badgeLabel, 3f);
            row.Add(badgeLabel);

            textCol.Add(row);

            var subLabel = new Label(subtitle);
            subLabel.style.color      = TextMuted;
            subLabel.style.fontSize   = 11f;
            subLabel.style.whiteSpace = WhiteSpace.Normal;
            textCol.Add(subLabel);

            header.Add(textCol);
            return header;
        }

        // ── Forms ─────────────────────────────────────────────────────────────

        private void BuildCharacterForm(VisualElement root)
        {
            root.Add(SectionLabel("Identity"));

            var nameField = MakeTextField("Display Name *", _charDisplayName);
            nameField.RegisterValueChangedCallback(evt =>
            {
                _charDisplayName = evt.newValue ?? string.Empty;
                _charId = DialogGraphDefinitionResolver.CreateSuggestedCharacterId(_charDisplayName);
                // push the auto-ID down without overwriting a manually edited value
                var idField = root.Q<TextField>("field-char-id");
                if (idField != null) idField.SetValueWithoutNotify(_charId);
            });
            root.Add(nameField);

            var idField = MakeTextField("Character ID", _charId);
            idField.name = "field-char-id";
            idField.RegisterValueChangedCallback(evt => _charId = evt.newValue ?? string.Empty);
            root.Add(idField);

            var portraitField = new ObjectField("Portrait (Sprite)")
            {
                objectType     = typeof(Sprite),
                allowSceneObjects = false
            };
            StyleObjectField(portraitField);
            portraitField.RegisterValueChangedCallback(evt => _charPortrait = evt.newValue as Sprite);
            root.Add(portraitField);

            root.Add(SectionLabel("Writing Context"));

            var descField = MakeTextField("Short Description", _charDescription, multiline: true, minHeight: 56f);
            descField.RegisterValueChangedCallback(evt => _charDescription = evt.newValue ?? string.Empty);
            root.Add(descField);

            var styleField = MakeTextField("Speech Style", _charSpeechStyle);
            styleField.RegisterValueChangedCallback(evt => _charSpeechStyle = evt.newValue ?? string.Empty);
            root.Add(styleField);

            root.Add(BuildErrorLabel());
        }

        private void BuildActionForm(VisualElement root)
        {
            root.Add(SectionLabel("Identity"));

            var idField = MakeTextField("Action ID *", _actionId);
            idField.RegisterValueChangedCallback(evt =>
            {
                _actionId = evt.newValue ?? string.Empty;
                var dnField = root.Q<TextField>("field-action-dn");
                if (dnField != null && dnField.value == _actionDisplayName)
                {
                    _actionDisplayName = _actionId;
                    dnField.SetValueWithoutNotify(_actionId);
                }
            });
            root.Add(idField);

            var dnField = MakeTextField("Display Name", _actionDisplayName);
            dnField.name = "field-action-dn";
            dnField.RegisterValueChangedCallback(evt => _actionDisplayName = evt.newValue ?? string.Empty);
            root.Add(dnField);

            var descField = MakeTextField("Description", _actionDescription, multiline: true, minHeight: 48f);
            descField.RegisterValueChangedCallback(evt => _actionDescription = evt.newValue ?? string.Empty);
            root.Add(descField);

            root.Add(SectionLabel("Defaults"));

            var payloadField = MakeTextField("Default Payload JSON", _actionPayload, multiline: true, minHeight: 48f);
            payloadField.RegisterValueChangedCallback(evt => _actionPayload = evt.newValue ?? "{}");
            root.Add(payloadField);

            root.Add(MakeToggleRow("Wait for Completion", _actionWait, v => _actionWait = v));
            root.Add(MakeFloatRow("Default Delay (s)", _actionDelay, v => _actionDelay = v));

            root.Add(BuildErrorLabel());
        }

        private void BuildEnvironmentForm(VisualElement root)
        {
            root.Add(SectionLabel("Identity"));

            var nameField = MakeTextField("Display Name *", _envDisplayName);
            nameField.RegisterValueChangedCallback(evt =>
            {
                _envDisplayName = evt.newValue ?? string.Empty;
                _envId = DialogGraphDefinitionResolver.CreateSuggestedCharacterId(_envDisplayName)
                             .Replace("character_", "env_");
                var idF = root.Q<TextField>("field-env-id");
                if (idF != null) idF.SetValueWithoutNotify(_envId);
            });
            root.Add(nameField);

            var idField = MakeTextField("Environment ID", _envId);
            idField.name = "field-env-id";
            idField.RegisterValueChangedCallback(evt => _envId = evt.newValue ?? string.Empty);
            root.Add(idField);

            root.Add(SectionLabel("Description"));

            var descField = MakeTextField("Description", _envDescription, multiline: true, minHeight: 56f);
            descField.RegisterValueChangedCallback(evt => _envDescription = evt.newValue ?? string.Empty);
            root.Add(descField);

            var atmField = MakeTextField("Atmosphere", _envAtmosphere);
            atmField.RegisterValueChangedCallback(evt => _envAtmosphere = evt.newValue ?? string.Empty);
            root.Add(atmField);

            var toneField = MakeTextField("Default Tone", _envTone);
            toneField.RegisterValueChangedCallback(evt => _envTone = evt.newValue ?? string.Empty);
            root.Add(toneField);

            root.Add(BuildErrorLabel());
        }

        private void BuildContextForm(VisualElement root)
        {
            root.Add(SectionLabel("Asset Identity"));

            var nameField = MakeTextField("Asset Name *", _ctxAssetName);
            nameField.RegisterValueChangedCallback(evt => _ctxAssetName = evt.newValue ?? string.Empty);
            root.Add(nameField);

            root.Add(SectionLabel("References"));

            var envField = new ObjectField("Environment")
            {
                objectType        = typeof(DialogEnvironmentSO),
                allowSceneObjects = false
            };
            StyleObjectField(envField);
            envField.RegisterValueChangedCallback(evt => _ctxEnvironment = evt.newValue as DialogEnvironmentSO);
            root.Add(envField);

            root.Add(SectionLabel("Scene Intent"));

            var goalField = MakeTextField("Scene Goal", _ctxSceneGoal, multiline: true, minHeight: 56f);
            goalField.RegisterValueChangedCallback(evt => _ctxSceneGoal = evt.newValue ?? string.Empty);
            root.Add(goalField);

            var toneField = MakeTextField("Tone", _ctxTone);
            toneField.RegisterValueChangedCallback(evt => _ctxTone = evt.newValue ?? string.Empty);
            root.Add(toneField);

            var rulesField = MakeTextField("Extra Rules", _ctxExtraRules, multiline: true, minHeight: 48f);
            rulesField.RegisterValueChangedCallback(evt => _ctxExtraRules = evt.newValue ?? string.Empty);
            root.Add(rulesField);

            root.Add(BuildErrorLabel());
        }

        // ── Footer ────────────────────────────────────────────────────────────

        private VisualElement BuildFooter()
        {
            var footer = new VisualElement();
            footer.style.flexDirection   = FlexDirection.Row;
            footer.style.justifyContent  = Justify.FlexEnd;
            footer.style.alignItems      = Align.Center;
            footer.style.paddingLeft     = 14f;
            footer.style.paddingRight    = 14f;
            footer.style.paddingTop      = 10f;
            footer.style.paddingBottom   = 10f;
            footer.style.borderTopWidth  = 1f;
            footer.style.borderTopColor  = BorderSubtle;
            footer.style.backgroundColor = BgHeader;

            var cancelBtn = new Button(() => Close()) { text = "Cancel" };
            StyleNeutralButton(cancelBtn);

            var createBtn = new Button(TryCreate) { text = "Create" };
            StylePrimaryButton(createBtn);

            footer.Add(cancelBtn);
            footer.Add(createBtn);
            return footer;
        }

        // ── Create logic ──────────────────────────────────────────────────────

        private void TryCreate()
        {
            switch (_kind)
            {
                case AssetKind.Character:    TryCreateCharacter();    break;
                case AssetKind.Action:       TryCreateAction();       break;
                case AssetKind.Environment:  TryCreateEnvironment();  break;
                case AssetKind.SceneContext: TryCreateSceneContext(); break;
            }
        }

        private void TryCreateCharacter()
        {
            if (string.IsNullOrWhiteSpace(_charDisplayName))
            {
                ShowError("Display Name is required.");
                return;
            }

            var registry = new DialogCharacterRegistryService();
            var asset    = registry.CreateNewAsset(_charDisplayName.Trim());
            if (asset == null) { ShowError("Failed to create asset file."); return; }

            Undo.RecordObject(asset, "Create Character Definition");
            DialogAssetInitializer.InitializeCharacter(asset, _charDisplayName, _charDescription, _charSpeechStyle);
            if (!string.IsNullOrWhiteSpace(_charId))
                asset.CharacterID = _charId.Trim();
            if (_charPortrait != null) asset.Portrait = _charPortrait;

            Finish(asset);
        }

        private void TryCreateAction()
        {
            if (string.IsNullOrWhiteSpace(_actionId))
            {
                ShowError("Action ID is required.");
                return;
            }

            var registry = new DialogActionRegistryService();
            var asset    = registry.CreateNewAsset(_actionId.Trim());
            if (asset == null) { ShowError("Failed to create asset file."); return; }

            Undo.RecordObject(asset, "Create Action Definition");
            DialogAssetInitializer.InitializeAction(asset, _actionId, _actionDescription);
            if (!string.IsNullOrWhiteSpace(_actionId))
                asset.ActionID = _actionId.Trim();
            if (!string.IsNullOrWhiteSpace(_actionDisplayName))
                asset.DisplayName = _actionDisplayName.Trim();
            asset.DefaultPayloadJson = string.IsNullOrWhiteSpace(_actionPayload) ? "{}" : _actionPayload;
            asset.WaitForCompletion = _actionWait;
            asset.DefaultDelay = _actionDelay;

            Finish(asset);
        }

        private void TryCreateEnvironment()
        {
            if (string.IsNullOrWhiteSpace(_envDisplayName))
            {
                ShowError("Display Name is required.");
                return;
            }

            var registry = new DialogEnvironmentRegistryService();
            var asset    = registry.CreateNewAsset(_envDisplayName.Trim());
            if (asset == null) { ShowError("Failed to create asset file."); return; }

            Undo.RecordObject(asset, "Create Environment Definition");
            DialogAssetInitializer.InitializeEnvironment(asset, _envDisplayName, _envDescription, _envAtmosphere, _envTone);
            if (!string.IsNullOrWhiteSpace(_envId))
                asset.EnvironmentID = _envId.Trim();

            Finish(asset);
        }

        private void TryCreateSceneContext()
        {
            if (string.IsNullOrWhiteSpace(_ctxAssetName))
            {
                ShowError("Asset Name is required.");
                return;
            }

            var registry = new DialogSceneContextRegistryService();
            var asset    = registry.CreateNewAsset(_ctxAssetName.Trim());
            if (asset == null) { ShowError("Failed to create asset file."); return; }

            Undo.RecordObject(asset, "Create Scene Context");
            DialogAssetInitializer.InitializeSceneContext(asset, _ctxAssetName, _ctxEnvironment, _ctxSceneGoal, _ctxTone, _ctxExtraRules);

            Finish(asset);
        }

        private void Finish(ScriptableObject asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            DialogGraphDefinitionResolver.PingAndSelect(asset);
            _onCreated?.Invoke(asset);
            Close();
        }

        // ── Validation ────────────────────────────────────────────────────────

        private Label BuildErrorLabel()
        {
            _errorLabel = new Label(string.Empty);
            _errorLabel.style.color      = ErrorRed;
            _errorLabel.style.fontSize   = 11f;
            _errorLabel.style.whiteSpace = WhiteSpace.Normal;
            _errorLabel.style.marginTop  = 6f;
            _errorLabel.style.display    = DisplayStyle.None;
            return _errorLabel;
        }

        private void ShowError(string msg)
        {
            if (_errorLabel == null) return;
            _errorLabel.text = msg;
            _errorLabel.style.display = DisplayStyle.Flex;
        }

        // ── Field helpers ─────────────────────────────────────────────────────

        private TextField MakeTextField(
            string label,
            string initialValue,
            bool   multiline  = false,
            float  minHeight  = 0f)
        {
            var field = new TextField(label)
            {
                value     = initialValue,
                multiline = multiline
            };

            field.style.marginBottom = 8f;

            // Label styling
            var labelEl = field.Q<Label>();
            if (labelEl != null)
            {
                labelEl.style.color    = TextMuted;
                labelEl.style.fontSize = 10f;
                labelEl.style.minWidth = 120f;
                labelEl.style.unityFontStyleAndWeight = FontStyle.Normal;
            }

            // Input area
            var input = field.Q<VisualElement>("unity-text-input");
            if (input != null)
            {
                input.style.backgroundColor = BgField;
                input.style.color           = TextPrimary;
                input.style.fontSize        = 12f;
                input.style.borderTopWidth    = input.style.borderBottomWidth =
                input.style.borderLeftWidth   = input.style.borderRightWidth  = 1f;
                input.style.borderTopColor    = input.style.borderBottomColor =
                input.style.borderLeftColor   = input.style.borderRightColor  = BorderSubtle;
                SetRadius(input, 4f);
                input.style.paddingLeft   = 6f;
                input.style.paddingRight  = 6f;
                input.style.paddingTop    = 4f;
                input.style.paddingBottom = 4f;
                if (minHeight > 0f)
                    input.style.minHeight = minHeight;
            }

            // Re-apply after layout (Unity resets inner input styles)
            field.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var inp = field.Q<VisualElement>("unity-text-input");
                if (inp == null) return;
                inp.style.backgroundColor = BgField;
                inp.style.color           = TextPrimary;
                if (minHeight > 0f) inp.style.minHeight = minHeight;
            });

            return field;
        }

        private static void StyleObjectField(ObjectField field)
        {
            field.style.marginBottom = 8f;

            var labelEl = field.Q<Label>();
            if (labelEl != null)
            {
                labelEl.style.color    = TextMuted;
                labelEl.style.fontSize = 10f;
                labelEl.style.minWidth = 120f;
            }
        }

        private static VisualElement MakeToggleRow(string label, bool initial, Action<bool> onChange)
        {
            var row = new VisualElement();
            row.style.flexDirection  = FlexDirection.Row;
            row.style.alignItems     = Align.Center;
            row.style.marginBottom   = 8f;

            var lbl = new Label(label);
            lbl.style.color    = TextMuted;
            lbl.style.fontSize = 10f;
            lbl.style.minWidth = 120f;
            row.Add(lbl);

            var toggle = new Toggle { value = initial };
            toggle.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(toggle);
            return row;
        }

        private static VisualElement MakeFloatRow(string label, float initial, Action<float> onChange)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems    = Align.Center;
            row.style.marginBottom  = 8f;

            var lbl = new Label(label);
            lbl.style.color    = TextMuted;
            lbl.style.fontSize = 10f;
            lbl.style.minWidth = 120f;
            row.Add(lbl);

            var floatField = new FloatField { value = initial };
            floatField.style.flexGrow = 1;
            floatField.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(floatField);
            return row;
        }

        private static Label SectionLabel(string text)
        {
            var lbl = new Label(text.ToUpperInvariant());
            lbl.style.color                  = SectionTitle;
            lbl.style.fontSize               = 9f;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.marginTop    = 12f;
            lbl.style.marginBottom = 4f;
            lbl.style.letterSpacing = 1.2f;
            return lbl;
        }

        // ── Button style helpers ──────────────────────────────────────────────

        private static void StyleNeutralButton(Button btn)
        {
            btn.style.backgroundColor = BtnNeutralBg;
            SetBorder(btn, 1f, BtnNeutralBdr);
            SetRadius(btn, 5f);
            btn.style.color    = TextPrimary;
            btn.style.fontSize = 12f;
            btn.style.height   = 28f;
            btn.style.paddingLeft  = 16f;
            btn.style.paddingRight = 16f;
            btn.style.marginRight  = 8f;
        }

        private static void StylePrimaryButton(Button btn)
        {
            btn.style.backgroundColor = AccentBlue;
            SetBorder(btn, 1f, AccentBlueLt);
            SetRadius(btn, 5f);
            btn.style.color    = Color.white;
            btn.style.fontSize = 12f;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.height   = 28f;
            btn.style.paddingLeft  = 20f;
            btn.style.paddingRight = 20f;
        }

        private static void SetBorder(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth    = e.style.borderBottomWidth =
            e.style.borderLeftWidth   = e.style.borderRightWidth  = w;
            e.style.borderTopColor    = e.style.borderBottomColor =
            e.style.borderLeftColor   = e.style.borderRightColor  = c;
        }

        private static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius     = e.style.borderTopRightRadius    =
            e.style.borderBottomLeftRadius  = e.style.borderBottomRightRadius = r;
        }
    }
}
