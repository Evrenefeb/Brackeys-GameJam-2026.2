using DialogSystem.EditorTools.AI;
using DialogSystem.EditorTools.Localization;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Resources;
using DialogSystem.EditorTools.Windows;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Utils;         // TextResources
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using DialogSystem.EditorTools.View;

namespace DialogSystem.EditorTools.View.Elements.Nodes
{
    /// <summary>
    /// GraphView UI for a <see cref="DialogNode"/>.
    /// - 1 input, 1 output.
    /// - Edits title/speaker/portrait/text/audio/displayTime.
    /// - Persists changes to the backing ScriptableObject (via GUID) with Undo support.
    /// </summary>
    public class DialogNodeView : BaseNodeView<DialogNode>
    {
        #region Layout
        private const float NODE_WIDTH = 400f;
        private const float PORT_HOLDER_WIDTH = 28f;
        #endregion

        #region Data
        public string GUID { get; set; }
        public string speakerName;
        public string questionText;
        public string nodeTitle;
        public Sprite portraitSprite;
        public AudioClip dialogueAudio;
        public float displayTimeSeconds;
        public bool waitForAudioFinish;
        #endregion

        #region Graph / UI
        public DialogGraphView graphView;

        private VisualElement _header;
        private Image _avatar;
        private VisualElement _portraitPreview;

        private TextField _titleField;
        private TextField _speakerField;
        private ObjectField _characterField;
        private Button _createCharacterButton;
        private Label _characterStatusLabel;
        private ObjectField _spriteField;
        private TextField _questionField;
        private Button _rewriteButton;
        private FloatField _displayTimeField;
        private ObjectField _audioField;
        private Toggle _waitForAudioField;

        public Port inputPort;
        public Port outputPort;

        #endregion

        #region Asset helpers

        private static string CombineAssetPath(string folder, string fileWithExt)
            => $"{folder.TrimEnd('/')}/{fileWithExt.TrimStart('/')}";

        /// <summary>
        /// Loads the DialogGraph asset using the graphView.GraphId.
        /// </summary>
        private DialogGraph GetAssetSafe()
        {
            if (graphView == null || string.IsNullOrEmpty(graphView.graphId))
                return null;

            return DialogGraphAssetPaths.LoadGraphAsset(graphView.graphId);
        }

        /// <summary>
        /// Finds the ScriptableObject DialogNode by GUID inside the given asset.
        /// </summary>
        private DialogNode FindSoNode(DialogGraph asset)
        {
            if (asset == null || string.IsNullOrEmpty(GUID))
                return null;

            return asset.nodes.FirstOrDefault(n => n != null && n.GetGuid() == GUID);
        }

        /// <summary>
        /// Convenience helper: locate asset + node and apply an action with Undo.
        /// </summary>
        private void WithAssetNode(string undoLabel, Action<DialogGraph, DialogNode> act)
        {
            var asset = GetAssetSafe();
            if (asset == null) return;

            var soNode = FindSoNode(asset);
            if (soNode == null) return;

            Undo.RecordObject(soNode, undoLabel);
            act(asset, soNode);
            EditorUtility.SetDirty(soNode);
            EditorUtility.SetDirty(asset);
        }

        #endregion

        #region API (setters called by other tools)

        public void SetPortraitSprite(Sprite sprite)
        {
            portraitSprite = sprite;

            if (_spriteField != null)
                _spriteField.SetValueWithoutNotify(sprite);

            UpdatePortraitPreview();
            UpdateAvatarVisual();
            RefreshCharacterDefinitionUi();
        }

        public void SetSpeakerName(string name)
        {
            speakerName = name;

            if (_speakerField != null)
                _speakerField.SetValueWithoutNotify(name);

            UpdatePortraitPreview();
            UpdateAvatarVisual();
            RefreshCharacterDefinitionUi();
        }

        public void ApplySidebarCharacterEdits(string name, Sprite sprite, bool applySprite = true)
        {
            speakerName = name ?? string.Empty;
            if (applySprite)
            {
                portraitSprite = sprite;
            }

            _speakerField?.SetValueWithoutNotify(speakerName);
            if (applySprite)
            {
                _spriteField?.SetValueWithoutNotify(portraitSprite);
            }
            UpdatePortraitPreview();
            UpdateAvatarVisual();

            WithAssetNode("Apply Sidebar Character Changes", (_, soNode) =>
            {
                soNode.speakerName = speakerName;
                if (applySprite)
                {
                    soNode.speakerPortrait = portraitSprite;
                }
            });

            RefreshCharacterDefinitionUi();
        }

        public void RefreshCharacterRegistrationUi()
        {
            UpdatePortraitPreview();
            UpdateAvatarVisual();
            RefreshCharacterDefinitionUi();
        }

        public void ApplyCharacterDefinition(DialogCharacterSO character)
        {
            if (character == null)
            {
                return;
            }

            speakerName = !string.IsNullOrWhiteSpace(character.DisplayName)
                ? character.DisplayName.Trim()
                : character.CharacterID?.Trim() ?? string.Empty;

            if (character.Portrait != null || portraitSprite == null)
            {
                portraitSprite = character.Portrait;
            }

            _speakerField?.SetValueWithoutNotify(speakerName);
            _spriteField?.SetValueWithoutNotify(portraitSprite);
            UpdatePortraitPreview();
            UpdateAvatarVisual();

            WithAssetNode("Assign Character Definition", (_, soNode) =>
            {
                soNode.speakerName = speakerName;
                if (character.Portrait != null || soNode.speakerPortrait == null)
                {
                    soNode.speakerPortrait = portraitSprite;
                }
            });

            RefreshCharacterDefinitionUi();
        }

        #endregion

        #region Ctor

        public DialogNodeView(string nodeTitle, DialogGraphView graph)
        {
            graphView = graph;
            this.nodeTitle = nodeTitle;
            title = nodeTitle;
            GUID = Guid.NewGuid().ToString("N");

            titleButtonContainer.Clear();
            capabilities &= ~Capabilities.Collapsible;

            NodeUssLoader.ApplyTo(this);
            AddToClassList("dlg-node");
            AddToClassList("type-dialogue");

            style.width = NODE_WIDTH;

            BuildHeader();
            BuildBody();
            BuildPorts();

            RefreshExpandedState();
            RefreshPorts();
        }

        #endregion

        #region Header

        private void BuildHeader()
        {
            titleContainer?.AddToClassList("node-title-dialogue");
            var icon = DialogGraphIconManager.CreateImage(DialogGraphIconId.NodeDialog, "dgs-icon--sm", "dgs-icon--dialog");
            icon.style.marginRight = 5f;
            icon.style.marginTop = 10f;
            titleContainer?.Insert(0, icon);

            _header = new VisualElement { name = "header" };

            _avatar = new Image { name = "node-avatar" };
            _avatar.AddToClassList("node-avatar");
            _avatar.style.width = 24f;
            _avatar.style.height = 24f;
            _avatar.style.marginRight = 4f;
            _avatar.style.display = DisplayStyle.None;
            _header.Add(_avatar);

            titleContainer.Add(_header);

            UpdateAvatarVisual();
        }

        private void UpdateAvatarVisual()
        {
            if (_avatar == null) return;

            if (portraitSprite != null)
            {
                _avatar.image = portraitSprite.texture;
                _avatar.style.display = DisplayStyle.Flex;
            }
            else
            {
                _avatar.image = null;
                _avatar.style.display = DisplayStyle.None;
            }
        }

        #endregion

        #region Body


        private void BuildBody()
        {
            var dialogSection = new VisualElement();
            var space = new VisualElement();
            space.AddToClassList("node-section");

            var detailsFoldout = new Foldout
            {
                text = "Details",
                value = false
            };
            detailsFoldout.AddToClassList("node-foldout");

            // Node title
            _titleField = new TextField("Node Title")
            {
                value = nodeTitle,
                isDelayed = true        // commit on focus change / Enter
            };
            _titleField.RegisterValueChangedCallback(e =>
            {
                nodeTitle = e.newValue;
                title = e.newValue;

                WithAssetNode("Edit Node Title", (_, soNode) =>
                {
                    var clean = string.IsNullOrWhiteSpace(nodeTitle) ? "Untitled" : nodeTitle.Trim();
                    soNode.name = "Node_" + clean;
                });
            });

            // Speaker
            _speakerField = new TextField("Speaker")
            {
                value = "",
                isDelayed = true
            };
            _speakerField.AddToClassList("node-field");
            _speakerField.RegisterValueChangedCallback(e =>
            {
                speakerName = e.newValue;
                WithAssetNode("Edit Speaker", (asset, soNode) =>
                {
                    soNode.speakerName = speakerName;
                    DialogLocalizationAutoSync.OnSpeakerNameChanged(soNode, asset, speakerName);
                });
                RefreshCharacterDefinitionUi();
            });

            _characterField = new ObjectField("Character")
            {
                objectType = typeof(DialogCharacterSO),
                allowSceneObjects = false
            };
            _characterField.AddToClassList("node-field");
            _characterField.tooltip = "Optional registered character definition for this node.";
            _characterField.RegisterValueChangedCallback(e =>
            {
                if (e.newValue is DialogCharacterSO character)
                {
                    ApplyCharacterDefinition(character);
                }
            });

            _characterStatusLabel = new Label();
            _characterStatusLabel.AddToClassList("node-section-hint");

            _createCharacterButton = new Button(OnClickCreateCharacter)
            {
                text = "Create Character",
                tooltip = "Create a registered character definition from the current speaker."
            };
            _createCharacterButton.AddToClassList("node-mini-button");

            // Portrait Sprite
            _spriteField = new ObjectField("Portrait")
            {
                objectType = typeof(Sprite),
                allowSceneObjects = false
            };
            _spriteField.AddToClassList("node-field");
            _spriteField.RegisterValueChangedCallback(e =>
            {
                portraitSprite = e.newValue as Sprite;
                UpdatePortraitPreview();
                UpdateAvatarVisual();

                WithAssetNode("Change Portrait", (_, soNode) =>
                {
                    soNode.speakerPortrait = portraitSprite;
                });
                RefreshCharacterDefinitionUi();
            });

            // Visual preview next to dialog text
            _portraitPreview = new VisualElement { name = "portrait-preview" };
            _portraitPreview.AddToClassList("portrait-preview");

            // Dialog text
            _questionField = new TextField("Dialog")
            {
                multiline = true,
                isDelayed = true
            };
            _questionField.name = "Dialog";
            _questionField.AddToClassList("dialog-text-field");
            _questionField.RegisterValueChangedCallback(e =>
            {
                questionText = e.newValue;
                UpdateRewriteButtonState();
                WithAssetNode("Edit Dialogue Text", (asset, soNode) =>
                {
                    soNode.questionText = questionText;
                    DialogLocalizationAutoSync.OnDialogTextChanged(soNode, asset, questionText);
                });
            });

            VisualElement dialogActionsRow = null;
            if (DialogGraphAiBridgeLocator.Current?.IsAvailable == true)
            {
                dialogActionsRow = new VisualElement();
                dialogActionsRow.AddToClassList("node-inline-actions");

                _rewriteButton = new Button(OnClickRewrite)
                {
                    text = "AI Rewrite",
                    tooltip = "Rewrite this dialogue line with the local AI settings and review before applying."
                };
                _rewriteButton.AddToClassList("node-mini-button");
                dialogActionsRow.Add(_rewriteButton);
            }

            // Display time
            _displayTimeField = new FloatField("Display Time (sec)")
            {
                value = 0f
            };
            _displayTimeField.AddToClassList("node-field");
            _displayTimeField.RegisterValueChangedCallback(e =>
            {
                displayTimeSeconds = e.newValue;
                WithAssetNode("Edit Display Time", (_, soNode) => soNode.displayTime = displayTimeSeconds);
            });

            // Audio clip
            _audioField = new ObjectField("Audio Clip")
            {
                objectType = typeof(AudioClip),
                allowSceneObjects = false
            };
            _audioField.AddToClassList("node-field");
            _audioField.RegisterValueChangedCallback(e =>
            {
                dialogueAudio = e.newValue as AudioClip;
                WithAssetNode("Change Dialogue Audio", (_, soNode) => soNode.dialogAudio = dialogueAudio);
                UpdateWaitForAudioState();
            });

            // Wait for audio finish toggle
            _waitForAudioField = new Toggle("Wait for Audio")
            {
                tooltip = "Block advancing to the next node until the Audio Clip finishes playing."
            };
            _waitForAudioField.AddToClassList("node-field");
            _waitForAudioField.RegisterValueChangedCallback(e =>
            {
                waitForAudioFinish = e.newValue;
                WithAssetNode("Toggle Wait For Audio", (_, soNode) => soNode.waitForAudioFinish = waitForAudioFinish);
            });

            var dialogRow = new VisualElement { name = "dialogue-row" };

            dialogRow.Add(_portraitPreview);
            dialogRow.Add(_questionField);

            dialogSection.Add(_speakerField);
            dialogSection.Add(_characterField);
            dialogSection.Add(_characterStatusLabel);
            dialogSection.Add(_createCharacterButton);
            dialogSection.Add(dialogRow);
            if (dialogActionsRow != null)
            {
                dialogSection.Add(dialogActionsRow);
            }

            detailsFoldout.Add(_titleField);
            detailsFoldout.Add(_spriteField);
            detailsFoldout.Add(_displayTimeField);
            detailsFoldout.Add(_audioField);
            detailsFoldout.Add(_waitForAudioField);

            mainContainer.Add(space);
            mainContainer.Add(dialogSection);
            mainContainer.Add(detailsFoldout);

            UpdatePortraitPreview();
            UpdateRewriteButtonState();
            RefreshCharacterDefinitionUi();
        }

        private static VisualElement CreateSection(string title, string hint)
        {
            var section = new VisualElement();
            section.AddToClassList("node-section");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("node-section-title");
            section.Add(titleLabel);

            if (!string.IsNullOrWhiteSpace(hint))
            {
                var hintLabel = new Label(hint);
                hintLabel.AddToClassList("node-section-hint");
                section.Add(hintLabel);
            }

            return section;
        }

        #endregion

        #region Ports

        private void BuildPorts()
        {
            inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            inputPort.portName = "In";
            inputContainer.Add(inputPort);

            AddOutputPort();
        }

        public void AddOutputPort()
        {
            outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            outputPort.portName = "Out";
            outputContainer.Add(outputPort);
        }

        public override void RebuildPorts()
        {
            inputContainer.Clear();
            outputContainer.Clear();
            BuildPorts();
            RefreshExpandedState();
            RefreshPorts();
        }

        #endregion

        #region Load / Visual

        /// <summary>
        /// Populate the view from existing DialogNode data (LoadGraph path).
        /// Does not record Undo; Undo is handled by changes after this point.
        /// </summary>
        public void LoadNodeData(
            string speaker, string question, string titleText, Sprite sprite,
            AudioClip audioClip, float displayTime, bool waitForAudio = false)
        {
            speakerName = speaker;
            questionText = question;
            nodeTitle = titleText;
            portraitSprite = sprite;
            dialogueAudio = audioClip;
            displayTimeSeconds = displayTime;
            waitForAudioFinish = waitForAudio;

            title = titleText;

            if (_speakerField != null) _speakerField.SetValueWithoutNotify(speaker);
            if (_questionField != null) _questionField.SetValueWithoutNotify(question);
            if (_titleField != null) _titleField.SetValueWithoutNotify(titleText);
            if (_audioField != null) _audioField.SetValueWithoutNotify(audioClip);
            if (_displayTimeField != null) _displayTimeField.SetValueWithoutNotify(displayTime);
            if (_waitForAudioField != null) _waitForAudioField.SetValueWithoutNotify(waitForAudio);
            UpdateWaitForAudioState();

            // If the speaker matches a registered Character SO, always prefer its
            // current portrait so changes made to the SO are visible when the
            // graph is re-opened — even if the node's stored snapshot is stale.
            var matchedChar = DialogGraphDefinitionResolver.FindCharacterBySpeaker(speakerName);
            if (matchedChar != null && matchedChar.Portrait != null)
            {
                // LoadGraph must stay read-only. Show the live character portrait
                // in the editor view without persisting it back to the node asset.
                portraitSprite = matchedChar.Portrait;
            }

            if (_spriteField != null) _spriteField.SetValueWithoutNotify(portraitSprite);

            UpdatePortraitPreview();
            UpdateAvatarVisual();
            UpdateRewriteButtonState();
            RefreshCharacterDefinitionUi();
        }

        /// <summary>
        /// Enables the "Wait for Audio" toggle only when an audio clip is assigned.
        /// </summary>
        private void UpdateWaitForAudioState()
        {
            if (_waitForAudioField == null) return;
            _waitForAudioField.SetEnabled(dialogueAudio != null);
        }

        private void OnClickRewrite()
        {
            if (!TryRewriteWithPreview(null, null, null, out var error))
            {
                EditorUtility.DisplayDialog("AI Rewrite Failed", error, "OK");
            }
        }

        private void AppendAiContextAction(
            DropdownMenu menu,
            string menuPath,
            DialogGraphAiQuickAction action)
        {
            menu.AppendAction(
                menuPath,
                _ => RunAiContextAction(action),
                _ => DialogGraphAiBridgeLocator.Current?.IsAvailable == true
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
        }

        private void RunAiContextAction(DialogGraphAiQuickAction action)
        {
            EnsureSelectedForContextAction();

            var bridge = DialogGraphAiBridgeLocator.Current;
            if (bridge == null || !bridge.IsAvailable)
            {
                EditorUtility.DisplayDialog(
                    "AI Tools Unavailable",
                    "Install or enable the AI extension to use node AI context actions.",
                    "OK");
                return;
            }

            var owner = ResolveOwner();
            if (owner == null)
            {
                EditorUtility.DisplayDialog("AI Command Failed", "The graph owner could not be resolved.", "OK");
                return;
            }

            if (!bridge.TryRunQuickAction(owner, action, out var error))
            {
                EditorUtility.DisplayDialog("AI Command Failed", error, "OK");
            }
        }

        private void EnsureSelectedForContextAction()
        {
            if (selected)
            {
                return;
            }

            graphView?.ClearSelection();
            graphView?.AddToSelection(this);
        }

        private IDialogGraphOwner ResolveOwner()
        {
            if (graphView?.GraphOwner != null)
            {
                return graphView.GraphOwner;
            }

            return UnityEngine.Resources.FindObjectsOfTypeAll<DialogGraphEditorWindow>()
                .FirstOrDefault(window => window != null && window.GetGraphView() == graphView);
        }

        private void ApplyRewrittenText(string rewrittenText)
        {
            if (string.IsNullOrWhiteSpace(rewrittenText))
            {
                return;
            }

            questionText = rewrittenText.Trim();
            _questionField?.SetValueWithoutNotify(questionText);

            WithAssetNode("AI Rewrite Dialogue Text", (_, soNode) => soNode.questionText = questionText);
            graphView?.MarkDirtyRepaint();
            UpdateRewriteButtonState();
        }

        /// <summary>
        /// Applies rewritten dialog text from the AI command executor.
        /// Persists to the backing ScriptableObject with Undo support.
        /// </summary>
        public void ApplyRewriteFromExecutor(string rewrittenText)
        {
            if (string.IsNullOrWhiteSpace(rewrittenText)) return;
            ApplyRewrittenText(rewrittenText.Trim());
        }

        internal bool CanRewriteThisNode(out string reason)
        {
            if (string.IsNullOrWhiteSpace(questionText))
            {
                reason = "The selected dialog node has no text to rewrite.";
                return false;
            }

            var bridge = DialogGraphAiBridgeLocator.Current;
            if (bridge == null || !bridge.IsAvailable)
            {
                reason = "Install or enable the AI extension to rewrite dialogue.";
                return false;
            }

            return bridge.CanRewriteText(out reason);
        }

        internal bool TryRewriteWithPreview(
            string customPrompt,
            string desiredTone,
            string instructionPreset,
            out string error)
        {
            error = string.Empty;

            var bridge = DialogGraphAiBridgeLocator.Current;
            if (_rewriteButton == null && (bridge == null || !bridge.IsAvailable))
            {
                error = "AI rewrite is not available in this version.";
                return false;
            }

            if (!CanRewriteThisNode(out error))
            {
                return false;
            }

            _rewriteButton?.SetEnabled(false);
            var originalLabel = _rewriteButton?.text ?? "AI Rewrite";
            if (_rewriteButton != null)
            {
                _rewriteButton.text = "Rewriting...";
            }

            try
            {
                var rewriteContext = BuildRewriteContext(customPrompt, desiredTone, instructionPreset);
                if (bridge == null || !bridge.IsAvailable)
                {
                    error = "AI rewrite is not available in this version.";
                    return false;
                }

                if (!bridge.TryRewriteText(
                    nodeTitle,
                    speakerName,
                    questionText,
                    customPrompt,
                    desiredTone,
                    instructionPreset,
                    rewriteContext,
                    out var rewrittenText,
                    out error))
                {
                    return false;
                }

                DialogRewritePreviewWindow.Open(questionText, rewrittenText, ApplyRewrittenText);
                return true;
            }
            finally
            {
                if (_rewriteButton != null)
                {
                    _rewriteButton.text = originalLabel;
                }

                UpdateRewriteButtonState();
            }
        }

        private DialogGraphAiContext BuildRewriteContext(
            string customPrompt,
            string desiredTone,
            string instructionPreset)
        {
            if (graphView == null)
            {
                return null;
            }

            var context = DialogGraphAiContextBuilder.Build(
                graphView,
                graphView.graphId,
                customPrompt,
                desiredTone,
                instructionPreset,
                graphView.nodes.ToList().OfType<DialogNodeView>().Select(node => node.speakerName),
                graphView.nodes.ToList().OfType<ActionNodeView>().Select(node => node.actionId),
                AiContextDepth.Local,
                GUID);

            DialogGraphAiContextBuilder.ApplyGraphSceneContext(context, GetAssetSafe());
            return AiContextBudgetService.Apply(context);
        }

        private void UpdateRewriteButtonState()
        {
            if (_rewriteButton == null)
            {
                return;
            }

            var hasText = !string.IsNullOrWhiteSpace(questionText);
            var bridge = DialogGraphAiBridgeLocator.Current;
            var reason = "Install or enable the AI extension to rewrite dialogue.";
            var canRewrite = bridge != null && bridge.IsAvailable && bridge.CanRewriteText(out reason);
            _rewriteButton.SetEnabled(hasText && canRewrite);

            if (!hasText)
            {
                _rewriteButton.tooltip = "Enter dialog text before sending a rewrite request.";
            }
            else if (!canRewrite)
            {
                _rewriteButton.tooltip = reason;
            }
            else
            {
                _rewriteButton.tooltip = "Rewrite this dialogue line with the local AI settings and review before applying.";
            }
        }

        private void UpdatePortraitPreview()
        {
            if (_portraitPreview == null) return;

            if (portraitSprite != null)
            {
                _portraitPreview.style.backgroundImage = new StyleBackground(portraitSprite);
                _portraitPreview.style.backgroundColor = Color.clear;
            }
            else
            {
                _portraitPreview.style.backgroundImage = null;
                _portraitPreview.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            }
        }

        private void RefreshCharacterDefinitionUi()
        {
            var matchedCharacter = DialogGraphDefinitionResolver.FindCharacterBySpeaker(speakerName);
            _characterField?.SetValueWithoutNotify(matchedCharacter);

            if (_characterStatusLabel != null)
            {
                _characterStatusLabel.text = matchedCharacter != null
                    ? $"Registered as {matchedCharacter.DisplayName} ({matchedCharacter.CharacterID})"
                    : string.IsNullOrWhiteSpace(speakerName)
                        ? "Manual speaker entry."
                        : "Speaker is not registered. Manual fallback stays active.";
            }

            if (_createCharacterButton != null)
            {
                _createCharacterButton.style.display =
                    matchedCharacter == null && !string.IsNullOrWhiteSpace(speakerName)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }
        }

        private void OnClickCreateCharacter()
        {
            if (string.IsNullOrWhiteSpace(speakerName))
            {
                return;
            }

            var asset = graphView?.GraphOwner?.CreateCharacterAsset(speakerName.Trim(), portraitSprite);
            if (asset != null)
            {
                ApplyCharacterDefinition(asset);
            }
        }

        #endregion

        #region Position persistence

        /// <summary>
        /// We deliberately do NOT write to the ScriptableObject here.
        /// Node movement persistence is handled centrally in DialogGraphView.OnGraphViewChanged
        /// so that all selected nodes move as a single Undo step.
        /// </summary>
        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
        }

        #endregion
    }
}
