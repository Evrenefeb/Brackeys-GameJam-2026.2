using System.Linq;
using DialogSystem.EditorTools.Services.Validation;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Variables;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogGraphValidatorTests
    {
        [Test]
        public void ValidateReportsConditionBranchAndUnknownVariableIssues()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var condition = ScriptableObject.CreateInstance<ConditionNode>();
            var knownVariable = ScriptableObject.CreateInstance<DialogVariableSO>();

            try
            {
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                condition.SetGuid("condition");
                condition.variableName = "missingFlag";
                condition.valueType = DialogueVariableValueType.Boolean;
                condition.conditionOperator = ConditionOperator.IsTrue;
                graph.conditionNodes.Add(condition);
                graph.links.Add(new GraphLink
                {
                    fromGuid = "Start",
                    toGuid = "condition",
                    fromPortIndex = 0
                });
                graph.links.Add(new GraphLink
                {
                    fromGuid = "condition",
                    toGuid = "End",
                    fromPortIndex = ConditionNode.TruePortIndex
                });

                knownVariable.SetKey("knownFlag");
                knownVariable.SetDefaultValue(true);

                var result = DialogGraphValidator.Validate(
                    graph,
                    knownSpeakerIds: null,
                    knownActionIds: null,
                    knownVariables: new[] { knownVariable });

                Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("CONDITION_MISSING_FALSE_OUTPUT"));
                Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("UNKNOWN_VARIABLE_KEY"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(condition);
                Object.DestroyImmediate(knownVariable);
            }
        }

        [Test]
        public void ValidateReportsV21IdentityIssues()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var choiceNode = ScriptableObject.CreateInstance<ChoiceNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                choiceNode.SetGuid("choice-node");
                choiceNode.choices.Add(new Choice { choiceId = "choice-a", answerText = "A", nextNodeGUID = "End" });
                choiceNode.choices.Add(new Choice { choiceId = "choice-a", answerText = "B", nextNodeGUID = "End" });
                graph.choiceNodes.Add(choiceNode);

                var firstLink = new GraphLink
                {
                    fromGuid = "Start",
                    toGuid = "choice-node",
                    fromPortKey = DialogGraphPortKeys.Default,
                    toPortKey = DialogGraphPortKeys.Default,
                    fromPortIndex = 0
                };
                firstLink.AssignLinkGuidIfMissing("duplicate-link");

                var secondLink = new GraphLink
                {
                    fromGuid = "choice-node",
                    toGuid = "End",
                    fromPortKey = DialogGraphPortKeys.ForChoiceId("choice-a"),
                    toPortKey = DialogGraphPortKeys.Default,
                    fromPortIndex = 0
                };
                secondLink.AssignLinkGuidIfMissing("duplicate-link");

                graph.links.Add(firstLink);
                graph.links.Add(secondLink);
                graph.links.Add(new GraphLink
                {
                    fromGuid = "choice-node",
                    toGuid = "End",
                    fromPortIndex = 5
                });

                var result = DialogGraphValidator.Validate(graph);
                var codes = result.Issues.Select(issue => issue.Code).ToArray();

                Assert.That(codes, Does.Contain("DUPLICATE_CHOICE_ID"));
                Assert.That(codes, Does.Contain("DUPLICATE_LINK_GUID"));
                Assert.That(codes, Does.Contain("MISSING_LINK_GUID"));
                Assert.That(codes, Does.Contain("MISSING_FROM_PORT_IDENTITY"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(choiceNode);
            }
        }

        [Test]
        public void ValidateReportsChoiceNextWithoutMatchingGraphLink()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var choiceNode = ScriptableObject.CreateInstance<ChoiceNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                choiceNode.SetGuid("choice-node");
                choiceNode.choices.Add(new Choice { choiceId = "choice-a", answerText = "A", nextNodeGUID = "End" });
                graph.choiceNodes.Add(choiceNode);
                graph.links.Add(CreateLink("start-link", "Start", "choice-node"));

                var result = DialogGraphValidator.Validate(graph);

                Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("CHOICE_LINK_MISSING"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(choiceNode);
            }
        }

        [Test]
        public void ValidateReportsChoiceGraphLinkWithoutChoiceTarget()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var choiceNode = ScriptableObject.CreateInstance<ChoiceNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                choiceNode.SetGuid("choice-node");
                choiceNode.choices.Add(new Choice { choiceId = "choice-a", answerText = "A", nextNodeGUID = "" });
                graph.choiceNodes.Add(choiceNode);
                graph.links.Add(CreateLink("start-link", "Start", "choice-node"));
                graph.links.Add(CreateLink(
                    "choice-link",
                    "choice-node",
                    "End",
                    DialogGraphPortKeys.ForChoiceId("choice-a")));

                var result = DialogGraphValidator.Validate(graph);

                Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("CHOICE_LINK_ORPHAN"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(choiceNode);
            }
        }

        [Test]
        public void ValidateReportsUnknownVariableTextTokens()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var dialogNode = ScriptableObject.CreateInstance<DialogNode>();
            var knownVariable = ScriptableObject.CreateInstance<DialogVariableSO>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                dialogNode.SetGuid("dialog");
                dialogNode.questionText = "Hello {playerName}, you have {{missingGold}}.";
                graph.nodes.Add(dialogNode);

                var link = new GraphLink
                {
                    fromGuid = "Start",
                    toGuid = "dialog",
                    fromPortKey = DialogGraphPortKeys.Default,
                    toPortKey = DialogGraphPortKeys.Default,
                    fromPortIndex = 0
                };
                link.AssignLinkGuidIfMissing("start-link");
                graph.links.Add(link);

                knownVariable.SetKey("playerName");
                knownVariable.SetDefaultValue("Arjan");

                var result = DialogGraphValidator.Validate(
                    graph,
                    knownSpeakerIds: null,
                    knownActionIds: null,
                    knownVariables: new[] { knownVariable });

                Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("UNKNOWN_VARIABLE_TOKEN"));
                Assert.That(
                    result.Issues.Count(issue => issue.Code == "UNKNOWN_VARIABLE_TOKEN"),
                    Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(dialogNode);
                Object.DestroyImmediate(knownVariable);
            }
        }

        [Test]
        public void ValidateReportsMissingSpeakerAndPortraitGuidance()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var missingSpeakerNode = ScriptableObject.CreateInstance<DialogNode>();
            var missingPortraitNode = ScriptableObject.CreateInstance<DialogNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                missingSpeakerNode.SetGuid("missing-speaker");
                missingSpeakerNode.questionText = "No speaker assigned.";
                graph.nodes.Add(missingSpeakerNode);

                missingPortraitNode.SetGuid("missing-portrait");
                missingPortraitNode.speakerName = "Kira";
                missingPortraitNode.questionText = "No portrait assigned.";
                graph.nodes.Add(missingPortraitNode);

                var startLink = new GraphLink
                {
                    fromGuid = "Start",
                    toGuid = "missing-speaker",
                    fromPortKey = DialogGraphPortKeys.Default,
                    toPortKey = DialogGraphPortKeys.Default,
                    fromPortIndex = 0
                };
                startLink.AssignLinkGuidIfMissing("start-link");
                graph.links.Add(startLink);

                var dialogLink = new GraphLink
                {
                    fromGuid = "missing-speaker",
                    toGuid = "missing-portrait",
                    fromPortKey = DialogGraphPortKeys.Default,
                    toPortKey = DialogGraphPortKeys.Default,
                    fromPortIndex = 0
                };
                dialogLink.AssignLinkGuidIfMissing("dialog-link");
                graph.links.Add(dialogLink);

                var endLink = new GraphLink
                {
                    fromGuid = "missing-portrait",
                    toGuid = "End",
                    fromPortKey = DialogGraphPortKeys.Default,
                    toPortKey = DialogGraphPortKeys.Default,
                    fromPortIndex = 0
                };
                endLink.AssignLinkGuidIfMissing("end-link");
                graph.links.Add(endLink);

                var result = DialogGraphValidator.Validate(graph);
                var codes = result.Issues.Select(issue => issue.Code).ToArray();

                Assert.That(codes, Does.Contain("MISSING_DIALOG_SPEAKER"));
                Assert.That(codes, Does.Contain("MISSING_DIALOG_PORTRAIT"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(missingSpeakerNode);
                Object.DestroyImmediate(missingPortraitNode);
            }
        }

        [Test]
        public void Validate_DoesNotReportMissingPortrait_WhenScopedCharacterProvidesPortrait()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var node = ScriptableObject.CreateInstance<DialogNode>();
            var character = ScriptableObject.CreateInstance<DialogCharacterSO>();
            var texture = new Texture2D(2, 2);
            Sprite portrait = null;

            try
            {
                portrait = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));

                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                character.CharacterID = "dax";
                character.DisplayName = "Dax";
                character.Portrait = portrait;
                graph.participatingCharacters.Add(character);

                node.SetGuid("dialog");
                node.speakerName = "Dax";
                node.questionText = "Portrait comes from the character definition.";
                graph.nodes.Add(node);

                graph.links.Add(CreateLink("start-link", "Start", "dialog"));
                graph.links.Add(CreateLink("end-link", "dialog", "End"));

                var result = DialogGraphValidator.Validate(graph);
                Assert.That(result.Issues.Select(issue => issue.Code), Does.Not.Contain("MISSING_DIALOG_PORTRAIT"));
            }
            finally
            {
                if (portrait != null)
                {
                    Object.DestroyImmediate(portrait);
                }

                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(node);
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void Validate_DoesNotReportUnknownGuid_ForGraphJumpLinks()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var jumpNode = ScriptableObject.CreateInstance<GraphJumpNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                jumpNode.SetGuid("jump");
                jumpNode.targetGraph.graphGuid = "other-graph";
                jumpNode.targetGraph.graphName = "Other Graph";
                graph.graphJumpNodes.Add(jumpNode);

                graph.links.Add(CreateLink("start-link", "Start", "jump"));
                graph.links.Add(CreateLink("end-link", "jump", "End"));

                var result = DialogGraphValidator.Validate(graph);
                var codes = result.Issues.Select(issue => issue.Code).ToArray();

                Assert.That(codes, Does.Not.Contain("UNKNOWN_FROM_GUID"));
                Assert.That(codes, Does.Not.Contain("UNKNOWN_TO_GUID"));
                Assert.That(codes, Does.Not.Contain("LINK_FROM_PORT_MISSING"));
            }
            finally
            {
                Object.DestroyImmediate(jumpNode);
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void Validate_DoesNotReportUnknownGuid_ForOutcomeLinks()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var outcomeNode = ScriptableObject.CreateInstance<OutcomeNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                outcomeNode.SetGuid("outcome");
                outcomeNode.outcomeId = "victory";
                outcomeNode.displayName = "Victory";
                graph.outcomeNodes.Add(outcomeNode);

                graph.links.Add(CreateLink("start-link", "Start", "outcome"));
                graph.links.Add(CreateLink("outcome-link", "outcome", "End"));

                var result = DialogGraphValidator.Validate(graph);
                var codes = result.Issues.Select(issue => issue.Code).ToArray();

                Assert.That(codes, Does.Not.Contain("UNKNOWN_FROM_GUID"));
                Assert.That(codes, Does.Not.Contain("UNKNOWN_TO_GUID"));
                Assert.That(codes, Does.Not.Contain("LINK_FROM_PORT_MISSING"));
                Assert.That(codes, Does.Not.Contain("OUTCOME_NO_OUTPUT"));
                Assert.That(codes, Does.Not.Contain("OUTCOME_NOT_END"));
            }
            finally
            {
                Object.DestroyImmediate(outcomeNode);
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void Validate_MissingStartNode_ReportsError()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            try
            {
                graph.startGuid = "";
                var result = DialogGraphValidator.Validate(graph);
                Assert.That(result.Issues.Select(i => i.Code), Does.Contain("MISSING_START"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Validate_MissingEndPath_ReportsError()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            try
            {
                graph.endGuid = "";
                var result = DialogGraphValidator.Validate(graph);
                Assert.That(result.Issues.Select(i => i.Code), Does.Contain("MISSING_END"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Validate_BrokenLink_ReportsError()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            try
            {
                graph.startGuid = "Start";
                graph.links.Add(CreateLink("broken", "Start", "NonExistent"));
                var result = DialogGraphValidator.Validate(graph);
                Assert.That(result.Issues.Select(i => i.Code), Does.Contain("UNKNOWN_TO_GUID"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Validate_InvalidActionId_ReportsWarning()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var actionNode = ScriptableObject.CreateInstance<ActionNode>();
            try
            {
                actionNode.SetGuid("action");
                actionNode.actionId = "InvalidID";
                graph.actionNodes.Add(actionNode);
                
                var result = DialogGraphValidator.Validate(graph, null, new[] { "ValidID" });
                Assert.That(result.Issues.Select(i => i.Code), Does.Contain("UNKNOWN_ACTION_ID"));
            }
            finally { Object.DestroyImmediate(graph); Object.DestroyImmediate(actionNode); }
        }

        [Test]
        public void Validate_ValidGraph_ProducesNoErrors()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var node = ScriptableObject.CreateInstance<DialogNode>();
            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                node.SetGuid("dialog");
                node.speakerName = "Arjan";
                node.questionText = "Hello";
                graph.nodes.Add(node);

                graph.links.Add(CreateLink("l1", "Start", "dialog"));
                graph.links.Add(CreateLink("l2", "dialog", "End"));

                var result = DialogGraphValidator.Validate(graph);
                Assert.That(result.Issues.Where(i => i.Severity == DialogGraphValidationSeverity.Error), Is.Empty);
            }
            finally { Object.DestroyImmediate(graph); Object.DestroyImmediate(node); }
        }

        [Test]
        public void Validate_ConditionSelfLoopAndSharedTarget_AreWarningsOrInfo()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var condition = ScriptableObject.CreateInstance<ConditionNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                condition.SetGuid("condition");
                condition.variableName = "Gold";
                graph.conditionNodes.Add(condition);

                graph.links.Add(CreateLink("start-link", "Start", "condition"));
                graph.links.Add(CreateConditionLink("true-link", "condition", "condition", DialogGraphPortKeys.True, ConditionNode.TruePortIndex));
                graph.links.Add(CreateConditionLink("false-link", "condition", "condition", DialogGraphPortKeys.False, ConditionNode.FalsePortIndex));

                var result = DialogGraphValidator.Validate(graph);
                var codes = result.Issues.Select(issue => issue.Code).ToArray();

                Assert.That(codes, Does.Contain("CONDITION_TRUE_SELF_LOOP"));
                Assert.That(codes, Does.Contain("CONDITION_FALSE_SELF_LOOP"));
                Assert.That(codes, Does.Contain("CONDITION_BRANCHES_SHARE_TARGET"));
                Assert.That(codes, Does.Contain("HIDDEN_FLOW_CYCLE"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(condition);
            }
        }

        [Test]
        public void Validate_HiddenFlowCycleThroughVariableMutation_ReportsCycle()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var condition = ScriptableObject.CreateInstance<ConditionNode>();
            var mutation = ScriptableObject.CreateInstance<VariableMutationNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                condition.SetGuid("condition");
                condition.variableName = "Gold";
                condition.valueType = DialogueVariableValueType.Integer;
                condition.conditionOperator = ConditionOperator.Greater;
                condition.comparisonValue = "0";
                graph.conditionNodes.Add(condition);

                mutation.SetGuid("mutation");
                mutation.variableName = "Gold";
                mutation.valueType = DialogueVariableValueType.Integer;
                mutation.operation = VariableMutationOperation.Subtract;
                mutation.value = "10";
                graph.variableMutationNodes.Add(mutation);

                graph.links.Add(CreateLink("start-link", "Start", "condition"));
                graph.links.Add(CreateConditionLink("true-link", "condition", "End", DialogGraphPortKeys.True, ConditionNode.TruePortIndex));
                graph.links.Add(CreateConditionLink("false-link", "condition", "mutation", DialogGraphPortKeys.False, ConditionNode.FalsePortIndex));
                graph.links.Add(CreateLink("mutation-link", "mutation", "condition"));

                var result = DialogGraphValidator.Validate(graph);

                Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("HIDDEN_FLOW_CYCLE"));
                Assert.That(result.Issues.First(issue => issue.Code == "HIDDEN_FLOW_CYCLE").Message, Does.Contain("Variable Mutation"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(condition);
                Object.DestroyImmediate(mutation);
            }
        }

        private static GraphLink CreateLink(
string linkGuid,
            string fromGuid,
            string toGuid,
            string fromPortKey = DialogGraphPortKeys.Default)
        {
            var link = new GraphLink
            {
                fromGuid = fromGuid,
                toGuid = toGuid,
                fromPortKey = fromPortKey,
                toPortKey = DialogGraphPortKeys.Default,
                fromPortIndex = 0
            };
            link.AssignLinkGuidIfMissing(linkGuid);
            return link;
        }

        private static GraphLink CreateConditionLink(
            string linkGuid,
            string fromGuid,
            string toGuid,
            string fromPortKey,
            int fromPortIndex)
        {
            var link = CreateLink(linkGuid, fromGuid, toGuid, fromPortKey);
            link.fromPortIndex = fromPortIndex;
            return link;
        }
    }
}
