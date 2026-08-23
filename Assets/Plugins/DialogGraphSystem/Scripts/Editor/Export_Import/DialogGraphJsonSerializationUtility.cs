using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Variables;
using UnityEngine;

namespace DialogSystem.EditorTools.ExportImport
{
    [Serializable]
    public struct DialogGraphJsonExportOptions
    {
        public bool includeNodePositions;
        public bool includeCategoryMetadata;
        public bool includeGroupLayouts;
        public bool includeJumpReferenceData;

        public static DialogGraphJsonExportOptions Default => new()
        {
            includeNodePositions = true,
            includeCategoryMetadata = true,
            includeGroupLayouts = true,
            includeJumpReferenceData = true
        };
    }

    public sealed class DialogGraphJsonPreviewSummary
    {
        public string graphTitle = string.Empty;
        public int dialogNodeCount;
        public int choiceNodeCount;
        public int actionNodeCount;
        public int conditionNodeCount;
        public int variableMutationNodeCount;
        public int graphJumpNodeCount;
        public int outcomeNodeCount;
        public int linkCount;
        public int groupLayoutCount;
        public int assignedCategoryCount;
        public int jumpReferenceCount;
        public bool hasPrimaryCategory;
        public bool hasImportableContent;
    }

    public static class DialogGraphJsonSerializationUtility
    {
        public static DialogGraphExport BuildExportDto(DialogGraph graph, DialogGraphJsonExportOptions options)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            var export = new DialogGraphExport
            {
                graphGuid = graph.GraphGuid,
                schemaVersion = graph.GraphSchemaVersion,
                graphTitle = graph.graphTitle,
                description = graph.description,
                author = graph.author,
                tags = graph.tags != null ? new List<string>(graph.tags) : new List<string>(),
                primaryCategory = options.includeCategoryMetadata ? graph.primaryCategory : string.Empty,
                categories = options.includeCategoryMetadata && graph.categories != null
                    ? new List<string>(graph.categories)
                    : new List<string>(),
                lastModifiedUtc = graph.lastModifiedUtc,
                editorVersion = graph.editorVersion,
                sceneGoal = graph.sceneGoal,
                tone = graph.tone,
                extraRules = graph.extraRules,
                dialogNodes = new List<DialogExportDialogNode>(),
                choiceNodes = new List<DialogExportChoiceNode>(),
                actionNodes = new List<DialogExportActionNode>(),
                conditionNodes = new List<DialogExportConditionNode>(),
                variableMutationNodes = new List<DialogExportVariableMutationNode>(),
                graphJumpNodes = new List<DialogExportGraphJumpNode>(),
                outcomeNodes = new List<DialogExportOutcomeNode>(),
                links = new List<ExportLink>(),
                groupLayouts = new List<ExportGroupLayoutRecord>()
            };

            if (graph.nodes != null)
            {
                foreach (var node in graph.nodes.Where(node => node != null))
                {
                    export.dialogNodes.Add(new DialogExportDialogNode
                    {
                        title = node.name,
                        guid = node.GetGuid(),
                        speaker = node.speakerName,
                        question = node.questionText,
                        nodePositionX = options.includeNodePositions ? node.GetPosition().x : 0f,
                        nodePositionY = options.includeNodePositions ? node.GetPosition().y : 0f,
                        displayTime = node.displayTime
                    });
                }
            }

            if (graph.choiceNodes != null)
            {
                foreach (var node in graph.choiceNodes.Where(node => node != null))
                {
                    var exportedNode = new DialogExportChoiceNode
                    {
                        guid = node.GetGuid(),
                        text = node.text,
                        nodePositionX = options.includeNodePositions ? node.GetPosition().x : 0f,
                        nodePositionY = options.includeNodePositions ? node.GetPosition().y : 0f,
                        choices = new List<ExportChoice>()
                    };

                    if (node.choices != null)
                    {
                        foreach (var choice in node.choices)
                        {
                            exportedNode.choices.Add(new ExportChoice
                            {
                                choiceId = choice?.choiceId,
                                portKey = choice?.PortKey,
                                answerText = choice?.answerText,
                                nextNodeGUID = choice?.nextNodeGUID
                            });
                        }
                    }

                    export.choiceNodes.Add(exportedNode);
                }
            }

            if (graph.actionNodes != null)
            {
                foreach (var node in graph.actionNodes.Where(node => node != null))
                {
                    export.actionNodes.Add(new DialogExportActionNode
                    {
                        guid = node.GetGuid(),
                        actionId = node.actionId,
                        payloadJson = node.payloadJson,
                        waitForCompletion = node.waitForCompletion,
                        waitSeconds = node.waitSeconds,
                        nodePositionX = options.includeNodePositions ? node.GetPosition().x : 0f,
                        nodePositionY = options.includeNodePositions ? node.GetPosition().y : 0f
                    });
                }
            }

            if (graph.conditionNodes != null)
            {
                foreach (var node in graph.conditionNodes.Where(node => node != null))
                {
                    export.conditionNodes.Add(new DialogExportConditionNode
                    {
                        guid = node.GetGuid(),
                        variableName = node.variableName,
                        valueType = node.valueType.ToString(),
                        conditionOperator = node.conditionOperator.ToString(),
                        comparisonValue = node.comparisonValue,
                        missingVariableResult = node.missingVariableResult,
                        nodePositionX = options.includeNodePositions ? node.GetPosition().x : 0f,
                        nodePositionY = options.includeNodePositions ? node.GetPosition().y : 0f
                    });
                }
            }

            if (graph.variableMutationNodes != null)
            {
                foreach (var node in graph.variableMutationNodes.Where(node => node != null))
                {
                    export.variableMutationNodes.Add(new DialogExportVariableMutationNode
                    {
                        guid = node.GetGuid(),
                        variableName = node.variableName,
                        valueType = node.valueType.ToString(),
                        operation = node.operation.ToString(),
                        value = node.value,
                        nodePositionX = options.includeNodePositions ? node.GetPosition().x : 0f,
                        nodePositionY = options.includeNodePositions ? node.GetPosition().y : 0f
                    });
                }
            }

            if (graph.graphJumpNodes != null)
            {
                foreach (var node in graph.graphJumpNodes.Where(node => node != null))
                {
                    export.graphJumpNodes.Add(new DialogExportGraphJumpNode
                    {
                        guid = node.GetGuid(),
                        nodePositionX = options.includeNodePositions ? node.GetPosition().x : 0f,
                        nodePositionY = options.includeNodePositions ? node.GetPosition().y : 0f,
                        targetGraph = options.includeJumpReferenceData
                            ? ExportGraphReference(node.targetGraph)
                            : null
                    });
                }
            }

            if (graph.outcomeNodes != null)
            {
                foreach (var node in graph.outcomeNodes.Where(node => node != null))
                {
                    export.outcomeNodes.Add(new DialogExportOutcomeNode
                    {
                        guid = node.GetGuid(),
                        outcomeId = node.outcomeId,
                        displayName = node.displayName,
                        description = node.description,
                        nodePositionX = options.includeNodePositions ? node.GetPosition().x : 0f,
                        nodePositionY = options.includeNodePositions ? node.GetPosition().y : 0f
                    });
                }
            }

            if (!string.IsNullOrEmpty(graph.startGuid))
            {
                export.startNode = new ExportStartNode
                {
                    isInitialized = graph.startInitialized,
                    guid = graph.startGuid,
                    nodePositionX = options.includeNodePositions ? graph.startPosition.x : 0f,
                    nodePositionY = options.includeNodePositions ? graph.startPosition.y : 0f
                };
            }

            if (!string.IsNullOrEmpty(graph.endGuid))
            {
                export.endNode = new ExportEndNode
                {
                    isInitialized = graph.endInitialized,
                    guid = graph.endGuid,
                    nodePositionX = options.includeNodePositions ? graph.endPosition.x : 0f,
                    nodePositionY = options.includeNodePositions ? graph.endPosition.y : 0f
                };
            }

            if (graph.links != null)
            {
                export.links = graph.links
                    .Where(link => link != null)
                    .Select(link => new ExportLink
                    {
                        linkGuid = link.LinkGuid,
                        fromGuid = link.fromGuid,
                        toGuid = link.toGuid,
                        fromPortKey = link.fromPortKey,
                        toPortKey = link.toPortKey,
                        fromPortIndex = link.fromPortIndex
                    })
                    .ToList();
            }

            if (options.includeGroupLayouts)
            {
                foreach (var groupLayout in graph.EnumerateGroupLayouts())
                {
                    export.groupLayouts.Add(new ExportGroupLayoutRecord
                    {
                        groupId = groupLayout.groupId,
                        title = groupLayout.title,
                        category = groupLayout.category,
                        x = groupLayout.bounds.x,
                        y = groupLayout.bounds.y,
                        width = groupLayout.bounds.width,
                        height = groupLayout.bounds.height,
                        colorR = groupLayout.colorTint.r,
                        colorG = groupLayout.colorTint.g,
                        colorB = groupLayout.colorTint.b,
                        colorA = groupLayout.colorTint.a,
                        nodeGuids = groupLayout.nodeGuids != null
                            ? new List<string>(groupLayout.nodeGuids)
                            : new List<string>()
                    });
                }
            }

            return export;
        }

        public static DialogGraphJsonPreviewSummary BuildPreviewSummary(DialogGraphExport dto)
        {
            dto ??= new DialogGraphExport();
            return new DialogGraphJsonPreviewSummary
            {
                graphTitle = dto.graphTitle ?? string.Empty,
                dialogNodeCount = dto.dialogNodes?.Count ?? 0,
                choiceNodeCount = dto.choiceNodes?.Count ?? 0,
                actionNodeCount = dto.actionNodes?.Count ?? 0,
                conditionNodeCount = dto.conditionNodes?.Count ?? 0,
                variableMutationNodeCount = dto.variableMutationNodes?.Count ?? 0,
                graphJumpNodeCount = dto.graphJumpNodes?.Count ?? 0,
                outcomeNodeCount = dto.outcomeNodes?.Count ?? 0,
                linkCount = dto.links?.Count ?? 0,
                groupLayoutCount = dto.groupLayouts?.Count ?? 0,
                assignedCategoryCount = NormalizeCategoryCount(dto.primaryCategory, dto.categories),
                jumpReferenceCount = CountJumpReferences(dto.graphJumpNodes),
                hasPrimaryCategory = !string.IsNullOrWhiteSpace(dto.primaryCategory),
                hasImportableContent = HasImportableContent(dto)
            };
        }

        public static bool HasImportableContent(DialogGraphExport dto)
        {
            if (dto == null)
            {
                return false;
            }

            return (dto.dialogNodes?.Count ?? 0) > 0 ||
                   (dto.choiceNodes?.Count ?? 0) > 0 ||
                   (dto.actionNodes?.Count ?? 0) > 0 ||
                   (dto.conditionNodes?.Count ?? 0) > 0 ||
                   (dto.variableMutationNodes?.Count ?? 0) > 0 ||
                   (dto.graphJumpNodes?.Count ?? 0) > 0 ||
                   (dto.outcomeNodes?.Count ?? 0) > 0;
        }

        private static int NormalizeCategoryCount(string primaryCategory, IEnumerable<string> categories)
        {
            var values = new List<string>();
            if (!string.IsNullOrWhiteSpace(primaryCategory))
            {
                values.Add(primaryCategory);
            }

            if (categories != null)
            {
                values.AddRange(categories);
            }

            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        private static int CountJumpReferences(IEnumerable<DialogExportGraphJumpNode> graphJumpNodes)
        {
            if (graphJumpNodes == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var jumpNode in graphJumpNodes)
            {
                if (jumpNode?.targetGraph == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(jumpNode.targetGraph.graphGuid) ||
                    !string.IsNullOrWhiteSpace(jumpNode.targetGraph.runtimeDialogId) ||
                    !string.IsNullOrWhiteSpace(jumpNode.targetGraph.graphName) ||
                    !string.IsNullOrWhiteSpace(jumpNode.targetGraph.assetPath) ||
                    !string.IsNullOrWhiteSpace(jumpNode.targetGraph.entryGuid))
                {
                    count++;
                }
            }

            return count;
        }

        private static DialogExportGraphReference ExportGraphReference(GraphReference graphReference)
        {
            if (graphReference == null || !graphReference.HasReference && string.IsNullOrWhiteSpace(graphReference.entryGuid))
            {
                return null;
            }

            return new DialogExportGraphReference
            {
                graphGuid = graphReference.graphGuid ?? string.Empty,
                runtimeDialogId = graphReference.runtimeDialogId ?? string.Empty,
                graphName = graphReference.graphName ?? string.Empty,
                assetPath = graphReference.assetPath ?? string.Empty,
                entryGuid = graphReference.entryGuid ?? string.Empty
            };
        }
    }
}
