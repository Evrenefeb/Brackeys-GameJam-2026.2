using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Models;
using System.Collections.Generic;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Variables;

namespace DialogSystem.EditorTools.ExportImport
{
    public static class DialogJsonImportBridge
    {
        public static bool TryImportFromJson(string json, string defaultAssetName)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[DialogJsonImportBridge] Empty JSON.");
                return false;
            }

            DialogGraphExport dto;
            try
            {
                dto = JsonUtility.FromJson<DialogGraphExport>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DialogJsonImportBridge] JSON parse error: {ex.Message}");
                return false;
            }

            if (dto == null ||
                ((dto.dialogNodes == null || dto.dialogNodes.Count == 0) &&
                 (dto.choiceNodes == null || dto.choiceNodes.Count == 0) &&
                 (dto.actionNodes == null || dto.actionNodes.Count == 0) &&
                 (dto.conditionNodes == null || dto.conditionNodes.Count == 0) &&
                 (dto.variableMutationNodes == null || dto.variableMutationNodes.Count == 0) &&
                 (dto.graphJumpNodes == null || dto.graphJumpNodes.Count == 0) &&
                 (dto.outcomeNodes == null || dto.outcomeNodes.Count == 0)))
            {
                Debug.LogError("[DialogJsonImportBridge] Parsed DTO is empty or has no nodes.");
                return false;
            }

            var safeName = string.IsNullOrWhiteSpace(defaultAssetName)
                ? "ImportedConversation"
                : defaultAssetName.Trim();
            safeName = MakeSafeFileName(safeName);

            var targetPath = EditorUtility.SaveFilePanelInProject(
                "Create DialogGraph from JSON",
                safeName,
                "asset",
                "Choose where to save the new DialogGraph asset.");

            if (string.IsNullOrEmpty(targetPath))
            {
                // User cancelled.
                return false;
            }

            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            AssetDatabase.CreateAsset(graph, targetPath);

            BuildFromDto(graph, dto);
            DialogGraphUpgradeService.MigrateToCurrent(graph);

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = graph;
            Debug.Log($"[DialogJsonImportBridge] Created DialogGraph from JSON at: {targetPath}");

            // NEW: ask what to do next
            OfferOpenOptions(graph, targetPath);

            return true;
        }

        // ---------------- NEW: open / show options ----------------

        private static void OfferOpenOptions(DialogGraph graph, string assetPath)
        {
            var choice = EditorUtility.DisplayDialogComplex(
                "Dialogue Graph Created",
                $"A DialogueGraph asset was created at:\n{assetPath}\n\nWhat would you like to do?",
                "Open Graph",     // 0
                "Close",          // 1
                "Show in Project" // 2
            );

            switch (choice)
            {
                case 0: // Open Graph
                    TryOpenGraphEditor(graph);
                    break;

                case 2: // Show in Project
                    EditorGUIUtility.PingObject(graph);
                    Selection.activeObject = graph;
                    break;

                case 1:
                default:
                    // Do nothing
                    break;
            }
        }

        private static void TryOpenGraphEditor(DialogGraph graph)
        {
            if (graph == null)
                return;

            // Try to find the editor window type in the correct namespace.
            // Adjust the assembly name ("DialogSystem.Editor") if your asmdef name is different.
            var editorWindowType = Type.GetType(
                "DialogSystem.EditorTools.Windows.DialogGraphEditorWindow, DialogSystem.Editor");

            if (editorWindowType != null)
            {
                // Prefer an overload that takes the DialogGraph asset directly, if you ever add it.
                var openWithGraphAsset = editorWindowType.GetMethod(
                    "OpenWithGraph",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(DialogGraph) },
                    null);

                if (openWithGraphAsset != null)
                {
                    try
                    {
                        openWithGraphAsset.Invoke(null, new object[] { graph });
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[DialogJsonImportBridge] OpenWithGraph(DialogGraph) failed: {ex}");
                    }
                }

                // Fallback: use the existing string overload (graph name)
                var openWithGraphName = editorWindowType.GetMethod(
                    "OpenWithGraph",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string) },
                    null);

                if (openWithGraphName != null)
                {
                    try
                    {
                        openWithGraphName.Invoke(null, new object[] { graph.name });
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[DialogJsonImportBridge] OpenWithGraph(string) failed: {ex}");
                    }
                }

                Debug.LogWarning("[DialogJsonImportBridge] DialogGraphEditorWindow found, but no suitable OpenWithGraph overload.");
            }
            else
            {
                Debug.LogWarning("[DialogJsonImportBridge] DialogGraphEditorWindow type not found via reflection.");
            }

            // Absolute fallback: open the asset normally (will use default inspector or any OnOpenAsset hook you add).
            AssetDatabase.OpenAsset(graph);
            EditorGUIUtility.PingObject(graph);
        }


        // ---------------- ORIGINAL HELPERS (MUST EXIST) ----------------

        /// <summary>
        /// Builds DialogGraph content from the DTO.
        /// Logic mirrors your DialogJsonIOWindow.BuildFromDto implementation.
        /// </summary>
        internal static void BuildFromDto(DialogGraph graph, DialogGraphExport dto)
        {
            if (graph.nodes == null) graph.nodes = new List<DialogNode>();
            if (graph.choiceNodes == null) graph.choiceNodes = new List<ChoiceNode>();
            if (graph.actionNodes == null) graph.actionNodes = new List<ActionNode>();
            if (graph.conditionNodes == null) graph.conditionNodes = new List<ConditionNode>();
            if (graph.variableMutationNodes == null) graph.variableMutationNodes = new List<VariableMutationNode>();
            if (graph.graphJumpNodes == null) graph.graphJumpNodes = new List<GraphJumpNode>();
            if (graph.outcomeNodes == null) graph.outcomeNodes = new List<OutcomeNode>();
            if (graph.links == null) graph.links = new List<GraphLink>();

            ApplyGraphMetadata(graph, dto);

            var existing = new HashSet<string>();
            var map = new Dictionary<string, string>();

            // Collect existing GUIDs
            existing.UnionWith(graph.nodes.Where(n => n != null).Select(n => n.GetGuid()));
            existing.UnionWith(graph.choiceNodes.Where(n => n != null).Select(n => n.GetGuid()));
            existing.UnionWith(graph.actionNodes.Where(n => n != null).Select(n => n.GetGuid()));
            existing.UnionWith(graph.conditionNodes.Where(n => n != null).Select(n => n.GetGuid()));
            existing.UnionWith(graph.variableMutationNodes.Where(n => n != null).Select(n => n.GetGuid()));
            existing.UnionWith(graph.graphJumpNodes.Where(n => n != null).Select(n => n.GetGuid()));
            existing.UnionWith(graph.outcomeNodes.Where(n => n != null).Select(n => n.GetGuid()));

            // Start node
            if (dto.startNode != null && !string.IsNullOrEmpty(dto.startNode.guid))
            {
                graph.startGuid = dto.startNode.guid;
                graph.startPosition = new Vector2(dto.startNode.nodePositionX, dto.startNode.nodePositionY);

                map[dto.startNode.guid] = graph.startGuid;
                existing.Add(graph.startGuid);
            }
            else
            {
                graph.startGuid = Guid.NewGuid().ToString("N");
                graph.startPosition = new Vector2(-320f, 80f);
            }
            map["Start"] = graph.startGuid;

            // End node
            if (dto.endNode != null && !string.IsNullOrEmpty(dto.endNode.guid))
            {
                graph.endGuid = dto.endNode.guid;
                graph.endPosition = new Vector2(dto.endNode.nodePositionX, dto.endNode.nodePositionY);

                map[dto.endNode.guid] = graph.endGuid;
                existing.Add(graph.endGuid);
            }
            else
            {
                graph.endGuid = Guid.NewGuid().ToString("N");
                graph.endPosition = new Vector2(720f, 80f);
            }
            map["End"] = graph.endGuid;

            graph.startInitialized = dto.startNode != null && dto.startNode.isInitialized;
            graph.endInitialized = dto.endNode != null && dto.endNode.isInitialized;

            // Dialog nodes GUID mapping
            foreach (var d in dto.dialogNodes ?? Enumerable.Empty<DialogExportDialogNode>())
            {
                var src = string.IsNullOrEmpty(d.guid) ? Guid.NewGuid().ToString("N") : d.guid;
                var dst = existing.Contains(src) ? Guid.NewGuid().ToString("N") : src;

                map[src] = dst;
                existing.Add(dst);
            }

            // Choice nodes GUID mapping
            foreach (var c in dto.choiceNodes ?? Enumerable.Empty<DialogExportChoiceNode>())
            {
                var src = string.IsNullOrEmpty(c.guid) ? Guid.NewGuid().ToString("N") : c.guid;
                var dst = existing.Contains(src) ? Guid.NewGuid().ToString("N") : src;

                map[src] = dst;
                existing.Add(dst);
            }

            // Action nodes GUID mapping
            foreach (var a in dto.actionNodes ?? Enumerable.Empty<DialogExportActionNode>())
            {
                var src = string.IsNullOrEmpty(a.guid) ? Guid.NewGuid().ToString("N") : a.guid;
                var dst = existing.Contains(src) ? Guid.NewGuid().ToString("N") : src;

                map[src] = dst;
                existing.Add(dst);
            }

            // Condition nodes GUID mapping
            foreach (var c in dto.conditionNodes ?? Enumerable.Empty<DialogExportConditionNode>())
            {
                var src = string.IsNullOrEmpty(c.guid) ? Guid.NewGuid().ToString("N") : c.guid;
                var dst = existing.Contains(src) ? Guid.NewGuid().ToString("N") : src;

                map[src] = dst;
                existing.Add(dst);
            }

            foreach (var v in dto.variableMutationNodes ?? Enumerable.Empty<DialogExportVariableMutationNode>())
            {
                var src = string.IsNullOrEmpty(v.guid) ? Guid.NewGuid().ToString("N") : v.guid;
                var dst = existing.Contains(src) ? Guid.NewGuid().ToString("N") : src;

                map[src] = dst;
                existing.Add(dst);
            }

            foreach (var j in dto.graphJumpNodes ?? Enumerable.Empty<DialogExportGraphJumpNode>())
            {
                var src = string.IsNullOrEmpty(j.guid) ? Guid.NewGuid().ToString("N") : j.guid;
                var dst = existing.Contains(src) ? Guid.NewGuid().ToString("N") : src;

                map[src] = dst;
                existing.Add(dst);
            }

            foreach (var o in dto.outcomeNodes ?? Enumerable.Empty<DialogExportOutcomeNode>())
            {
                var src = string.IsNullOrEmpty(o.guid) ? Guid.NewGuid().ToString("N") : o.guid;
                var dst = existing.Contains(src) ? Guid.NewGuid().ToString("N") : src;

                map[src] = dst;
                existing.Add(dst);
            }

            // Create dialog node sub-assets
            foreach (var d in dto.dialogNodes ?? Enumerable.Empty<DialogExportDialogNode>())
            {
                var so = ScriptableObject.CreateInstance<DialogNode>();

                if (!map.TryGetValue(d.guid, out var mapped))
                    mapped = Guid.NewGuid().ToString("N");

                so.SetGuid(mapped);
                so.name = "Node_" + (string.IsNullOrEmpty(d.title) ? "Untitled" : MakeSafeFileName(d.title));
                so.speakerName = d.speaker;
                so.questionText = d.question;
                so.displayTime = d.displayTime;
                so.SetPosition(new Vector2(d.nodePositionX, d.nodePositionY));

                graph.nodes.Add(so);
                AssetDatabase.AddObjectToAsset(so, graph);
            }

            // Create choice nodes
            foreach (var c in dto.choiceNodes ?? Enumerable.Empty<DialogExportChoiceNode>())
            {
                var so = ScriptableObject.CreateInstance<ChoiceNode>();

                if (!map.TryGetValue(c.guid, out var mapped))
                    mapped = Guid.NewGuid().ToString("N");

                so.SetGuid(mapped);
                so.name = "ChoiceNode";
                so.text = c.text;
                so.SetPosition(new Vector2(c.nodePositionX, c.nodePositionY));

                so.choices = new List<Choice>();
                foreach (var ch in c.choices ?? Enumerable.Empty<ExportChoice>())
                {
                    map.TryGetValue(ch.nextNodeGUID ?? string.Empty, out var mappedTarget);
                    so.choices.Add(CreateImportedChoice(ch, mappedTarget));
                }

                graph.choiceNodes.Add(so);
                AssetDatabase.AddObjectToAsset(so, graph);
            }

            // Create action nodes
            foreach (var a in dto.actionNodes ?? Enumerable.Empty<DialogExportActionNode>())
            {
                var so = ScriptableObject.CreateInstance<ActionNode>();

                if (!map.TryGetValue(a.guid, out var mapped))
                    mapped = Guid.NewGuid().ToString("N");

                so.SetGuid(mapped);
                so.name = "ActionNode";
                so.actionId = a.actionId;
                so.payloadJson = a.payloadJson;
                so.waitForCompletion = a.waitForCompletion;
                so.waitSeconds = a.waitSeconds;
                so.SetPosition(new Vector2(a.nodePositionX, a.nodePositionY));

                graph.actionNodes.Add(so);
                AssetDatabase.AddObjectToAsset(so, graph);
            }

            // Create condition nodes
            foreach (var c in dto.conditionNodes ?? Enumerable.Empty<DialogExportConditionNode>())
            {
                var so = ScriptableObject.CreateInstance<ConditionNode>();

                if (!map.TryGetValue(c.guid, out var mapped))
                    mapped = Guid.NewGuid().ToString("N");

                so.SetGuid(mapped);
                so.name = "ConditionNode";
                so.variableName = c.variableName;
                so.valueType = TryParseEnum(c.valueType, DialogueVariableValueType.Boolean);
                so.conditionOperator = TryParseEnum(c.conditionOperator, ConditionOperator.IsTrue);
                so.comparisonValue = c.comparisonValue;
                so.missingVariableResult = c.missingVariableResult;
                so.SetPosition(new Vector2(c.nodePositionX, c.nodePositionY));

                graph.conditionNodes.Add(so);
                AssetDatabase.AddObjectToAsset(so, graph);
            }

            foreach (var v in dto.variableMutationNodes ?? Enumerable.Empty<DialogExportVariableMutationNode>())
            {
                var so = ScriptableObject.CreateInstance<VariableMutationNode>();

                if (!map.TryGetValue(v.guid, out var mapped))
                    mapped = Guid.NewGuid().ToString("N");

                so.SetGuid(mapped);
                so.name = "VariableMutationNode";
                so.variableName = v.variableName;
                so.valueType = TryParseEnum(v.valueType, DialogueVariableValueType.Boolean);
                so.operation = TryParseEnum(v.operation, VariableMutationOperation.Set);
                so.value = v.value;
                so.SetPosition(new Vector2(v.nodePositionX, v.nodePositionY));

                graph.variableMutationNodes.Add(so);
                AssetDatabase.AddObjectToAsset(so, graph);
            }

            foreach (var j in dto.graphJumpNodes ?? Enumerable.Empty<DialogExportGraphJumpNode>())
            {
                var so = ScriptableObject.CreateInstance<GraphJumpNode>();

                if (!map.TryGetValue(j.guid, out var mapped))
                    mapped = Guid.NewGuid().ToString("N");

                so.SetGuid(mapped);
                so.name = "GraphJumpNode";
                so.targetGraph = CreateImportedGraphReference(j.targetGraph);
                so.SetPosition(new Vector2(j.nodePositionX, j.nodePositionY));

                graph.graphJumpNodes.Add(so);
                AssetDatabase.AddObjectToAsset(so, graph);
            }

            foreach (var o in dto.outcomeNodes ?? Enumerable.Empty<DialogExportOutcomeNode>())
            {
                if (o == null) continue;
                var so = ScriptableObject.CreateInstance<OutcomeNode>();
                so.name = "OutcomeNode";
                if (!map.TryGetValue(o.guid, out var mapped))
                    mapped = Guid.NewGuid().ToString("N");
                so.SetGuid(mapped);
                so.SetPosition(new Vector2(o.nodePositionX, o.nodePositionY));
                so.outcomeId = o.outcomeId ?? string.Empty;
                so.displayName = o.displayName ?? string.Empty;
                so.description = o.description ?? string.Empty;
                graph.outcomeNodes.Add(so);
                AssetDatabase.AddObjectToAsset(so, graph);
            }

            // Links
            var linkBuffer = new List<ExportLink>();
            if (dto.links != null && dto.links.Count > 0)
            {
                linkBuffer.AddRange(dto.links);
            }
            else
            {
                foreach (var c in dto.choiceNodes ?? Enumerable.Empty<DialogExportChoiceNode>())
                {
                    var idx = 0;
                    foreach (var ch in c.choices ?? Enumerable.Empty<ExportChoice>())
                    {
                        if (!string.IsNullOrEmpty(ch.nextNodeGUID))
                        {
                            linkBuffer.Add(new ExportLink
                            {
                                fromGuid = c.guid,
                                toGuid = ch.nextNodeGUID,
                                fromPortIndex = idx,
                                fromPortKey = ch.portKey
                            });
                        }
                        idx++;
                    }
                }
            }

            foreach (var l in linkBuffer)
            {
                if (string.IsNullOrEmpty(l.fromGuid) || string.IsNullOrEmpty(l.toGuid))
                    continue;

                if (!map.TryGetValue(l.fromGuid, out var fromMapped))
                    continue;
                if (!map.TryGetValue(l.toGuid, out var toMapped))
                    continue;

                var link = new GraphLink
                {
                    fromGuid = fromMapped,
                    toGuid = toMapped,
                    fromPortKey = string.IsNullOrWhiteSpace(l.fromPortKey)
                        ? ResolveImportedFromPortKey(graph, fromMapped, l.fromPortIndex)
                        : l.fromPortKey,
                    toPortKey = string.IsNullOrWhiteSpace(l.toPortKey)
                        ? DialogGraphPortKeys.Default
                        : l.toPortKey,
                    fromPortIndex = l.fromPortIndex
                };
                if (!string.IsNullOrWhiteSpace(l.linkGuid))
                {
                    link.SetLinkGuidForMigration(l.linkGuid);
                }
                else
                {
                    link.AssignLinkGuidIfMissing(Guid.NewGuid().ToString("N"));
                }
                graph.links.Add(link);
            }

            SyncChoiceTargetsFromLinks(graph);
            ApplyImportedGroupLayouts(graph, dto, map);
        }

        /// <summary>
        /// Applies persisted graph-level JSON metadata before migration fills remaining defaults.
        /// </summary>
        private static void ApplyGraphMetadata(DialogGraph graph, DialogGraphExport dto)
        {
            if (graph == null || dto == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(dto.graphGuid))
            {
                graph.SetGraphGuidForMigration(dto.graphGuid);
            }
            else
            {
                graph.AssignGraphGuidIfMissing(Guid.NewGuid().ToString("N"));
            }

            if (dto.schemaVersion > 0)
            {
                graph.SetGraphSchemaVersionForMigration(dto.schemaVersion);
            }

            graph.graphTitle = dto.graphTitle;
            graph.description = dto.description;
            graph.author = dto.author;
            graph.tags = dto.tags != null ? new List<string>(dto.tags) : new List<string>();
            graph.primaryCategory = dto.primaryCategory;
            graph.categories = dto.categories != null ? new List<string>(dto.categories) : new List<string>();
            graph.lastModifiedUtc = dto.lastModifiedUtc;
            graph.editorVersion = dto.editorVersion;
            graph.sceneGoal = dto.sceneGoal;
            graph.tone = dto.tone;
            graph.extraRules = dto.extraRules;
        }

        private static GraphReference CreateImportedGraphReference(DialogExportGraphReference dto)
        {
            return new GraphReference
            {
                graphGuid = dto?.graphGuid ?? string.Empty,
                runtimeDialogId = dto?.runtimeDialogId ?? string.Empty,
                graphName = dto?.graphName ?? string.Empty,
                assetPath = dto?.assetPath ?? string.Empty,
                entryGuid = dto?.entryGuid ?? string.Empty
            };
        }

        private static void ApplyImportedGroupLayouts(
            DialogGraph graph,
            DialogGraphExport dto,
            IReadOnlyDictionary<string, string> guidMap)
        {
            graph.ClearAllGroupLayouts();

            foreach (var groupLayout in dto.groupLayouts ?? Enumerable.Empty<ExportGroupLayoutRecord>())
            {
                if (groupLayout == null || string.IsNullOrWhiteSpace(groupLayout.groupId))
                {
                    continue;
                }

                var record = graph.GetOrCreateGroupLayoutForEditor(groupLayout.groupId, groupLayout.title);
                record.category = groupLayout.category ?? string.Empty;
                record.bounds = new Rect(groupLayout.x, groupLayout.y, groupLayout.width, groupLayout.height);
                record.colorTint = new Color(groupLayout.colorR, groupLayout.colorG, groupLayout.colorB, groupLayout.colorA);
                record.nodeGuids = new List<string>();

                foreach (var nodeGuid in groupLayout.nodeGuids ?? Enumerable.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(nodeGuid))
                    {
                        continue;
                    }

                    record.nodeGuids.Add(
                        guidMap != null && guidMap.TryGetValue(nodeGuid, out var mappedGuid)
                            ? mappedGuid
                            : nodeGuid);
                }
            }
        }

        private static Choice CreateImportedChoice(ExportChoice dto, string mappedTarget)
        {
            return new Choice
            {
                choiceId = ResolveImportedChoiceId(dto),
                answerText = dto?.answerText ?? string.Empty,
                nextNodeGUID = mappedTarget,
                tooltipOrSubLabel = null
            };
        }

        private static string ResolveImportedChoiceId(ExportChoice dto)
        {
            if (!string.IsNullOrWhiteSpace(dto?.choiceId))
            {
                return dto.choiceId;
            }

            if (DialogGraphPortKeys.TryGetChoiceId(dto?.portKey, out var choiceId))
            {
                return choiceId;
            }

            return Choice.CreateChoiceId();
        }

        /// <summary>
        /// Replaces invalid filename characters with underscores.
        /// </summary>
        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Unnamed";

            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (invalid.Contains(chars[i]))
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }

        private static string ResolveImportedFromPortKey(DialogGraph graph, string fromGuid, int fromPortIndex)
        {
            var choiceNode = graph.choiceNodes?.FirstOrDefault(node =>
                node != null &&
                string.Equals(node.GetGuid(), fromGuid, StringComparison.Ordinal));

            if (choiceNode?.choices != null &&
                fromPortIndex >= 0 &&
                fromPortIndex < choiceNode.choices.Count)
            {
                return choiceNode.choices[fromPortIndex]?.PortKey ?? string.Empty;
            }

            var conditionNode = graph.conditionNodes?.FirstOrDefault(node =>
                node != null &&
                string.Equals(node.GetGuid(), fromGuid, StringComparison.Ordinal));

            if (conditionNode != null)
            {
                return fromPortIndex == ConditionNode.FalsePortIndex
                    ? DialogGraphPortKeys.False
                    : DialogGraphPortKeys.True;
            }

            var actionNode = graph.actionNodes?.FirstOrDefault(node =>
                node != null &&
                string.Equals(node.GetGuid(), fromGuid, StringComparison.Ordinal));

            return actionNode != null ? DialogGraphPortKeys.ActionSuccess : DialogGraphPortKeys.Default;
        }

        private static TEnum TryParseEnum<TEnum>(string rawValue, TEnum fallback)
            where TEnum : struct
        {
            return Enum.TryParse(rawValue, true, out TEnum parsed) ? parsed : fallback;
        }

        private static void SyncChoiceTargetsFromLinks(DialogGraph graph)
        {
            if (graph == null || graph.choiceNodes == null || graph.links == null)
                return;

            var linksByChoice = graph.links
                .Where(link => link != null)
                .GroupBy(link => link.fromGuid)
                .ToDictionary(group => group.Key, group => group.OrderBy(link => link.fromPortIndex).ToList());

            foreach (var choiceNode in graph.choiceNodes)
            {
                if (choiceNode == null || choiceNode.choices == null)
                    continue;

                if (!linksByChoice.TryGetValue(choiceNode.GetGuid(), out var outgoingLinks))
                    continue;

                for (int i = 0; i < choiceNode.choices.Count; i++)
                {
                    var link = outgoingLinks.FirstOrDefault(item => item.fromPortIndex == i);
                    if (link != null)
                        choiceNode.choices[i].nextNodeGUID = link.toGuid;
                }
            }
        }
    }
}