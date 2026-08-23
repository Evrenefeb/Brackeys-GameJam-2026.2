using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using DialogSystem.EditorTools.Localization;
using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.AI;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Resources;
using DialogSystem.EditorTools.Windows;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Utils;

namespace DialogSystem.EditorTools.View.Elements.Nodes
{
    /// <summary>
    /// GraphView UI for a <see cref="ChoiceNode"/>:
    /// - One input port
    /// - N output ports (one per answer)
    /// - Keeps ScriptableObject asset in sync via GUID and Undo.
    /// </summary>
    public class ChoiceNodeView : BaseNodeView<ChoiceNode>
    {
        #region ---------------- Inspector / Debug ----------------
        [SerializeField] private bool doDebug = false;
        #endregion

        #region ---------------- Layout ----------------
        private const float NODE_WIDTH = 400f;
        #endregion

        #region ---------------- Data / Graph ----------------
        public string GUID { get; set; }
        public DialogGraphView graphView;
        #endregion

        #region ---------------- UI ----------------
        private VisualElement _answerSection;
        private Button _addAnswerBtn;

        public Port inputPort;
        public readonly List<Port> outputPorts = new();   // index == choice index
        public readonly List<string> answers = new();

        private bool _suppressAssetSync = false;
        #endregion

        #region ---------------- Asset helpers ----------------
        private static string CombineAssetPath(string folder, string fileWithExt)
            => $"{folder.TrimEnd('/')}/{fileWithExt.TrimStart('/')}";

        private DialogGraph GetAssetSafe()
        {
            if (graphView == null || string.IsNullOrEmpty(graphView.graphId)) return null;
            return DialogGraphAssetPaths.LoadGraphAsset(graphView.graphId);
        }

        // Resolve the ChoiceNode sub-asset by this view's GUID.
        private ChoiceNode FindSoNode(DialogGraph asset)
        {
            if (asset == null || string.IsNullOrEmpty(GUID)) return null;

            // Prefer the list on the asset if available
            if (asset.choiceNodes != null && asset.choiceNodes.Count > 0)
            {
                var direct = asset.choiceNodes.FirstOrDefault(n => n != null && n.GetGuid() == GUID);
                if (direct != null) return direct;
            }

            // Fallback: scan sub-assets at the same path (defensive)
            var path = AssetDatabase.GetAssetPath(asset);
            var subs = AssetDatabase.LoadAllAssetsAtPath(path);
            return subs.OfType<ChoiceNode>().FirstOrDefault(n => n != null && n.GetGuid() == GUID);
        }

        private void WithAssetNode(string undoLabel, Action<DialogGraph, ChoiceNode> act)
        {
            if (_suppressAssetSync) return;

            var asset = GetAssetSafe();
            if (asset == null) return;

            var soNode = FindSoNode(asset);
            if (soNode == null) return;

            Undo.RecordObject(soNode, undoLabel);
            Undo.RecordObject(asset, undoLabel);
            act(asset, soNode);
            EditorUtility.SetDirty(soNode);
            EditorUtility.SetDirty(asset);
        }
        #endregion

        #region ---------------- Ctor ----------------
        public ChoiceNodeView(string nodeTitle, DialogGraphView graph)
        {
            titleButtonContainer.Clear();
            capabilities &= ~Capabilities.Collapsible;

            graphView = graph;
            GUID = Guid.NewGuid().ToString("N");

            NodeUssLoader.ApplyTo(this);
            AddToClassList("dlg-node");
            AddToClassList("type-choice");

            style.width = NODE_WIDTH;
            title = "Choice Node";

            BuildHeader();
            BuildBody();
            BuildPorts();

            RefreshExpandedState();
            RefreshPorts();

            }
            #endregion

        #region ---------------- Header ----------------
        private void BuildHeader()
        {
            titleContainer?.AddToClassList("node-title-choice");
            var icon = DialogGraphIconManager.CreateImage(DialogGraphIconId.NodeChoice, "dgs-icon--sm", "dgs-icon--choice");
            icon.style.marginRight = 5f;
            icon.style.marginTop = 10f;
            titleContainer?.Insert(0, icon);
        }
        #endregion

        #region ---------------- Body ----------------
        private void BuildBody()
        {
            var space = new VisualElement();
            mainContainer.AddToClassList("node-section");
            mainContainer.Add(space);

            _answerSection = new VisualElement { name = "answers" };

            _addAnswerBtn = new Button(() => AddChoicePort("New Choice", true))
            {
                text = "+ Add Option",
                tooltip = "Add another selectable answer."
            };
            _addAnswerBtn.AddToClassList("add-answer");

            mainContainer.Add(_answerSection);
            mainContainer.Add(_addAnswerBtn);
        }
        #endregion

        #region ---------------- Ports ----------------
        private void BuildPorts()
        {
            inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            inputPort.portName = "In";
            inputContainer.Add(inputPort);
        }

        public override void RebuildPorts()
        {
            // Only input is fixed; outputs are created per-answer row.
            inputContainer.Clear();
            BuildPorts();
            RefreshExpandedState();
            RefreshPorts();
        }
        #endregion

        #region ---------------- Public API ----------------
        /// <summary>Populate from a list of saved choices (no asset writes).</summary>
        public void LoadNodeData(IList<Choice> choiceList)
        {
            _suppressAssetSync = true;
            LoadAnswers(choiceList);
            _suppressAssetSync = false;
        }

        public void LoadAnswers(IList<Choice> choiceList)
        {
            answers.Clear();
            outputPorts.Clear();
            _answerSection.Clear();

            if (choiceList != null && choiceList.Count > 0)
            {
                foreach (var c in choiceList)
                    AddChoicePort(c?.answerText ?? string.Empty, syncAsset: false, c?.choiceId);
            }
            else
            {
                AddChoicePort("New Choice", syncAsset: false);
            }
        }
        #endregion

        #region ---------------- Answers ----------------
        public void AddChoicePort(string answerText, bool syncAsset, string choiceId = null)
        {
            // Port per answer
            var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            port.AddToClassList("choice-port");
            port.portName = ""; // mapping is by index
            port.userData = choiceId ?? string.Empty;

            // Row: [TextField][Duplicate][Delete][Port]
            var row = new VisualElement { name = "answer-row" };

            var field = new TextField
            {
                value = answerText,
                name = "answer-text",
                isDelayed = false
            };

            field.RegisterValueChangedCallback(e =>
            {
                int index = outputPorts.IndexOf(port);
                if (index < 0) return;

                answers[index] = e.newValue;

                WithAssetNode("Edit Answer Text", (asset, soNode) =>
                {
                    if (soNode.choices == null)
                        soNode.choices = new List<Choice>();

                    while (soNode.choices.Count <= index)
                        soNode.choices.Add(Choice.Create());

                    if (!soNode.choices[index].HasChoiceId)
                        soNode.choices[index].AssignChoiceIdIfMissing(Choice.CreateChoiceId());

                    port.userData = soNode.choices[index].choiceId;
                    soNode.choices[index].answerText = e.newValue;

                    DialogLocalizationAutoSync.OnChoiceAnswerChanged(soNode, asset, soNode.choices[index], e.newValue);
                });
            });

            var dupBtn = TinyIconButton(DialogGraphIconId.ActionDuplicate, "+", "Duplicate option", "btn-dup");
            dupBtn.clicked += () => AddChoicePort(field.value, true);

            var delBtn = TinyIconButton(DialogGraphIconId.ActionDelete, "x", "Delete option", "btn-del");
            delBtn.clicked += () => RemoveChoice(port, row);

            var portHolder = new VisualElement { name = "port-holder" };
            portHolder.Add(port);

            row.Add(field);

            if (DialogGraphAiBridgeLocator.Current?.IsAvailable == true)
            {
                var aiBtn = TinyIconButton(DialogGraphIconId.AiRewrite, "*", "Rewrite this choice with AI", "btn-ai-choice", "dgs-icon--ai");
                aiBtn.clicked += () =>
                {
                    var original = field.value;
                    if (string.IsNullOrWhiteSpace(original)) return;

                    var bridge = DialogGraphAiBridgeLocator.Current;
                    if (bridge == null || !bridge.IsAvailable)
                    {
                        EditorUtility.DisplayDialog("AI Rewrite Failed", "AI rewrite is not available in this version.", "OK");
                        return;
                    }

                    if (!bridge.TryRewriteText(
                        string.Empty,
                        string.Empty,
                        original,
                        null,
                        null,
                        null,
                        null,
                        out var rewritten,
                        out var err))
                    {
                        EditorUtility.DisplayDialog("AI Rewrite Failed", err, "OK");
                        return;
                    }

                    int idx = outputPorts.IndexOf(port);
                    DialogRewritePreviewWindow.Open(
                        originalText: original,
                        rewrittenText: rewritten,
                        onApply: approved =>
                        {
                            field.SetValueWithoutNotify(approved);
                            if (idx >= 0 && idx < answers.Count)
                                answers[idx] = approved;

                            WithAssetNode("AI Rewrite Choice", (_, soNode) =>
                            {
                                if (soNode.choices != null && idx < soNode.choices.Count)
                                    soNode.choices[idx].answerText = approved;
                            });
                        });
                };
                row.Add(aiBtn);
            }

            row.Add(dupBtn);
            row.Add(delBtn);
            row.Add(portHolder);

            _answerSection.Add(row);
            outputPorts.Add(port);
            answers.Add(answerText);

            if (syncAsset)
            {
                WithAssetNode("Add Answer", (asset, soNode) =>
                {
                    if (soNode.choices == null)
                        soNode.choices = new List<Choice>();

                    var choice = Choice.Create(answerText, null);
                    soNode.choices.Add(choice);
                    port.userData = choice.choiceId;
                });
            }

            if (doDebug)
            {
                Debug.Log($"[ChoiceNodeView] Added choice '{answerText}' at index {outputPorts.Count - 1} (GUID={GUID})");
            }
        }

        private static Button TinyIconButton(DialogGraphIconId iconId, string fallbackText, string tip, string extraClass = null, params string[] iconClasses)
        {
            var b = new Button { tooltip = tip };
            b.AddToClassList("tiny");
            if (!string.IsNullOrEmpty(extraClass)) b.AddToClassList(extraClass);

            if (DialogGraphIconManager.HasIcon(iconId))
            {
                var icon = DialogGraphIconManager.CreateImage(iconId, "dgs-icon--sm");
                icon.style.marginLeft = 2f;
                icon.style.marginRight = 2f;
                icon.style.marginTop = 2f; 

                if (iconClasses != null)
                {
                    foreach (var iconClass in iconClasses)
                    {
                        if (!string.IsNullOrWhiteSpace(iconClass))
                        {
                            icon.AddToClassList(iconClass);
                        }
                    }
                }

                b.Add(icon);
            }
            else
            {
                b.text = fallbackText;
            }

            return b;
        }

        private void RemoveChoice(Port port, VisualElement row)
        {
            int index = outputPorts.IndexOf(port);
            if (index < 0) return;

            if (doDebug)
            {
                Debug.Log($"[ChoiceNodeView] Removing choice at index {index} (GUID={GUID})");
            }

            // Disconnect existing edge (if any)
            var edge = port.connections.FirstOrDefault();
            if (edge != null)
            {
                edge.input?.Disconnect(edge);
                edge.output?.Disconnect(edge);
                edge.RemoveFromHierarchy();
            }

            WithAssetNode("Delete Answer", (asset, soNode) =>
            {
                if (soNode.choices != null && index < soNode.choices.Count)
                {
                    var nodeGuid = soNode.GetGuid();

                    // Clean up graph links for this port
                    if (asset.links != null)
                    {
                        var removedChoice = soNode.choices[index];
                        var removedPortKey = removedChoice?.PortKey;

                        // Remove links from this answer's port
                        DialogGraphLinkMutationService.RemoveLinksFromPort(asset, nodeGuid, index, removedPortKey);

                        // Shift indices for answers after this one
                        for (int i = 0; i < asset.links.Count; i++)
                        {
                            var link = asset.links[i];
                            if (link.fromGuid == nodeGuid && link.fromPortIndex > index)
                            {
                                link.fromPortIndex -= 1;
                                asset.links[i] = link;
                            }
                        }
                    }

                    // Phase 5: Cleanup and shift edge layout records
                    asset.RemoveOrShiftEdgeLayoutsForChoice(nodeGuid, index);

                    // Clear and remove the choice itself
                    soNode.choices[index].nextNodeGUID = null;
                    soNode.choices.RemoveAt(index);
                }
            });

            // Remove UI state
            outputPorts.RemoveAt(index);
            answers.RemoveAt(index);
            row.RemoveFromHierarchy();
        }
        #endregion

        #region ---------------- Edge helpers (for GraphView) ----------------
        public int GetPortIndex(Port p) => outputPorts.IndexOf(p);

        public void SetNextForPort(Port p, string targetGuid)
        {
            int i = GetPortIndex(p);
            if (i < 0) return;

            WithAssetNode("Link Choice", (asset, soNode) =>
            {
                if (soNode.choices == null)
                    soNode.choices = new List<Choice>();

                while (soNode.choices.Count <= i)
                    soNode.choices.Add(Choice.Create());

                if (!soNode.choices[i].HasChoiceId)
                    soNode.choices[i].AssignChoiceIdIfMissing(Choice.CreateChoiceId());

                p.userData = soNode.choices[i].choiceId;
                soNode.choices[i].nextNodeGUID = targetGuid;
            });

            if (doDebug)
            {
                Debug.Log($"[ChoiceNodeView] Set next node for choice {i} to GUID={targetGuid}");
            }
        }

        public void ClearNextForPort(Port p, string targetGuid)
        {
            int i = GetPortIndex(p);
            if (i < 0) return;

            WithAssetNode("Unlink Choice", (asset, soNode) =>
            {
                if (soNode.choices != null && i < soNode.choices.Count &&
                    soNode.choices[i].nextNodeGUID == targetGuid)
                {
                    soNode.choices[i].nextNodeGUID = null;
                }
            });

            if (doDebug)
            {
                Debug.Log($"[ChoiceNodeView] Cleared next node for choice {i} (target was GUID={targetGuid})");
            }
        }
        #endregion
    }
}