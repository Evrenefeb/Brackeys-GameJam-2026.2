using System.Linq;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Localization
{
    /// <summary>
    /// Editor-only panel that displays source text and its resolved translation
    /// side-by-side for the currently selected node.
    ///
    /// Embed into a graph editor panel by calling <see cref="Build"/> and
    /// adding the returned <see cref="VisualElement"/> to the host container.
    /// Refresh by calling <see cref="Refresh"/> whenever the node selection changes.
    /// </summary>
    public class DialogLocalizationPreviewPanel
    {
        // ── State ─────────────────────────────────────────────────────────────

        private BaseNode                  _currentNode;
        private DialogGraph               _currentGraph;
        private DialogLocalizationTable   _previewTable;
        private DialogLocalizationTable[] _allTables = System.Array.Empty<DialogLocalizationTable>();

        // ── UI ────────────────────────────────────────────────────────────────

        private VisualElement _root;
        private VisualElement _pairsContainer;
        private Label         _noNodeLabel;
        private PopupField<string> _localeDropdown;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the panel UI and returns the root element to embed in a host container.
        /// Call <see cref="Refresh"/> after adding to the host.
        /// </summary>
        public VisualElement Build()
        {
            _root = new VisualElement();
            _root.style.paddingLeft   = 8;
            _root.style.paddingRight  = 8;
            _root.style.paddingTop    = 8;
            _root.style.paddingBottom = 8;

            // Header row: locale dropdown
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems    = Align.Center;
            headerRow.style.marginBottom  = 8;

            var headerLabel = new Label("Localization Preview");
            headerLabel.style.flexGrow    = 1;
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(headerLabel);

            ReloadTableList();
            _localeDropdown = BuildLocaleDropdown();
            headerRow.Add(_localeDropdown);

            _root.Add(headerRow);

            // No-node placeholder
            _noNodeLabel = new Label("Select a Dialog or Choice node to preview its localized text.");
            _noNodeLabel.style.whiteSpace = WhiteSpace.Normal;
            _noNodeLabel.style.color      = new StyleColor(Color.grey);
            _root.Add(_noNodeLabel);

            // Pairs container
            _pairsContainer = new VisualElement();
            _root.Add(_pairsContainer);

            RefreshUI();
            return _root;
        }

        /// <summary>
        /// Refreshes the panel for <paramref name="node"/> inside <paramref name="graph"/>.
        /// Pass <c>null</c> to show the empty state.
        /// </summary>
        public void Refresh(BaseNode node, DialogGraph graph)
        {
            _currentNode  = node;
            _currentGraph = graph;
            ReloadTableList();
            RefreshUI();
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private void ReloadTableList()
        {
            var service = new DialogLocalizationRegistryService();
            _allTables  = service.GetAllTables().ToArray();

            if (_previewTable == null && _allTables.Length > 0)
                _previewTable = _allTables[0];
        }

        private PopupField<string> BuildLocaleDropdown()
        {
            var codes = _allTables.Select(t => t.LocaleCode).ToList();
            if (codes.Count == 0) codes.Add("(no tables)");

            var dropdown = new PopupField<string>("Preview Locale", codes, 0);
            dropdown.RegisterValueChangedCallback(evt =>
            {
                _previewTable = _allTables.FirstOrDefault(
                    t => t.LocaleCode == evt.newValue);
                RefreshUI();
            });
            return dropdown;
        }

        private void RefreshUI()
        {
            if (_pairsContainer == null || _noNodeLabel == null) return;

            _pairsContainer.Clear();

            if (_currentNode == null)
            {
                _noNodeLabel.style.display    = DisplayStyle.Flex;
                _pairsContainer.style.display = DisplayStyle.None;
                return;
            }

            _noNodeLabel.style.display    = DisplayStyle.None;
            _pairsContainer.style.display = DisplayStyle.Flex;

            if (_currentNode is DialogNode dialogNode)
                BuildDialogNodePairs(dialogNode);
            else if (_currentNode is ChoiceNode choiceNode)
                BuildChoiceNodePairs(choiceNode);
        }

        private void BuildDialogNodePairs(DialogNode node)
        {
            // Question text
            AddPair(
                label:         "Dialog Text",
                localeKey:     node.questionTextLocaleKey,
                sourceText:    node.questionText,
                onGenerateKey: () =>
                {
                    var key = DialogLocaleKeyGenerator.GenerateDialogNodeKey(
                        _currentGraph?.graphTitle ?? "graph",
                        node.GetGuid());
                    node.questionTextLocaleKey = key;
                    EditorUtility.SetDirty(node);
                    RefreshUI();
                });

            // Speaker name (only if inline raw string is set)
            if (!string.IsNullOrWhiteSpace(node.speakerName))
            {
                AddPair(
                    label:         "Speaker Name",
                    localeKey:     node.speakerNameLocaleKey,
                    sourceText:    node.speakerName,
                    onGenerateKey: () =>
                    {
                        var key = DialogLocaleKeyGenerator.GenerateSpeakerNameKey(
                            _currentGraph?.graphTitle ?? "graph",
                            node.GetGuid());
                        node.speakerNameLocaleKey = key;
                        EditorUtility.SetDirty(node);
                        RefreshUI();
                    });
            }
        }

        private void BuildChoiceNodePairs(ChoiceNode node)
        {
            // Prompt text
            if (!string.IsNullOrWhiteSpace(node.text))
            {
                AddPair(
                    label:         "Prompt Text",
                    localeKey:     node.textLocaleKey,
                    sourceText:    node.text,
                    onGenerateKey: () =>
                    {
                        node.textLocaleKey = DialogLocaleKeyGenerator.GenerateChoiceNodePromptKey(
                            _currentGraph?.graphTitle ?? "graph",
                            node.GetGuid());
                        EditorUtility.SetDirty(node);
                        RefreshUI();
                    });
            }

            // Individual choices
            if (node.choices == null) return;
            for (var i = 0; i < node.choices.Count; i++)
            {
                var choice = node.choices[i];
                if (choice == null) continue;
                var capturedIndex = i;

                AddPair(
                    label:         $"Choice {i + 1}",
                    localeKey:     choice.answerTextLocaleKey,
                    sourceText:    choice.answerText,
                    onGenerateKey: () =>
                    {
                        node.choices[capturedIndex].answerTextLocaleKey =
                            DialogLocaleKeyGenerator.GenerateChoiceKey(
                                _currentGraph?.graphTitle ?? "graph",
                                node.GetGuid(),
                                choice.choiceId);
                        EditorUtility.SetDirty(node);
                        RefreshUI();
                    });
            }
        }

        private void AddPair(
            string label,
            string localeKey,
            string sourceText,
            System.Action onGenerateKey)
        {
            var card = new VisualElement();
            card.style.borderTopWidth    = 1;
            card.style.borderTopColor    = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            card.style.paddingTop        = 6;
            card.style.paddingBottom     = 6;
            card.style.marginBottom      = 4;

            // Label row
            var labelRow = new VisualElement();
            labelRow.style.flexDirection = FlexDirection.Row;
            labelRow.style.alignItems    = Align.Center;
            labelRow.style.marginBottom  = 4;

            var fieldLabel = new Label(label);
            fieldLabel.style.flexGrow = 1;
            fieldLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            labelRow.Add(fieldLabel);

            var hasKey = !string.IsNullOrWhiteSpace(localeKey);
            if (!hasKey)
            {
                var genBtn = new Button(onGenerateKey) { text = "Generate Key" };
                genBtn.style.fontSize = 10;
                labelRow.Add(genBtn);
            }

            card.Add(labelRow);

            // Key display
            if (hasKey)
            {
                var keyLabel = new Label($"Key: {localeKey}");
                keyLabel.style.fontSize = 10;
                keyLabel.style.color    = new StyleColor(new Color(0.5f, 0.8f, 0.5f));
                keyLabel.style.marginBottom = 4;
                card.Add(keyLabel);
            }
            else
            {
                var missingLabel = new Label("No locale key — text will not be localized.");
                missingLabel.style.fontSize = 10;
                missingLabel.style.color    = new StyleColor(new Color(0.9f, 0.4f, 0.3f));
                missingLabel.style.marginBottom = 4;
                card.Add(missingLabel);
            }

            // Source text
            var sourceContainer = new VisualElement();
            sourceContainer.style.flexDirection = FlexDirection.Row;
            sourceContainer.style.marginBottom  = 4;

            var sourceLabel = new Label("Source:");
            sourceLabel.style.width = 80;
            sourceLabel.style.color = new StyleColor(Color.grey);

            var sourceText2 = new Label(string.IsNullOrWhiteSpace(sourceText) ? "(empty)" : sourceText);
            sourceText2.style.flexGrow  = 1;
            sourceText2.style.whiteSpace = WhiteSpace.Normal;

            sourceContainer.Add(sourceLabel);
            sourceContainer.Add(sourceText2);
            card.Add(sourceContainer);

            // Translation
            if (hasKey)
            {
                var translation = _previewTable?.TryResolve(localeKey);
                var translationContainer = new VisualElement();
                translationContainer.style.flexDirection = FlexDirection.Row;

                var transLabel = new Label("Translation:");
                transLabel.style.width = 80;
                transLabel.style.color = new StyleColor(Color.grey);

                var transText = new Label(
                    string.IsNullOrWhiteSpace(translation) ? "(not translated)" : translation);
                transText.style.flexGrow   = 1;
                transText.style.whiteSpace = WhiteSpace.Normal;
                if (string.IsNullOrWhiteSpace(translation))
                    transText.style.color = new StyleColor(new Color(0.9f, 0.6f, 0.2f));

                translationContainer.Add(transLabel);
                translationContainer.Add(transText);
                card.Add(translationContainer);
            }

            _pairsContainer.Add(card);
        }
    }
}
