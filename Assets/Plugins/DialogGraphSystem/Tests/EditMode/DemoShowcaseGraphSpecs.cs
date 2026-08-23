using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using DialogSystem.EditorTools.ExportImport;
using DialogSystem.Runtime.Models;

namespace DialogSystem.Tests.EditMode
{
    internal static class DemoShowcaseGraphSpecs
    {
        internal const string ProductPath = "Assets/DialogGraphSystem/Graphs/Demo_ProductTour.asset";
        internal const string ShopPath = "Assets/DialogGraphSystem/Graphs/Demo_ShopGate.asset";
        internal const string ReactorPath = "Assets/DialogGraphSystem/Graphs/Demo_ControlRoomActions.asset";
        internal const string AftermathPath = "Assets/DialogGraphSystem/Graphs/Demo_ReactorAftermath.asset";

        internal const string ProductGraphGuid = "a45cd98a853d4ab0b19aab7545d509e6";
        internal const string ShopGraphGuid = "e94500c7ce5b4251813d0157d807d22e";
        internal const string ReactorGraphGuid = "5f4f3cd6c1a84524927e5274a2cf7d9c";
        internal const string AftermathGraphGuid = "734bf8c42cf14d76a190f175e939e3f9";

        internal static IReadOnlyDictionary<string, DialogGraphExport> CreateAll()
        {
            return new Dictionary<string, DialogGraphExport>(StringComparer.Ordinal)
            {
                [ProductPath] = CreateProductTour(),
                [ShopPath] = CreateShopGate(),
                [ReactorPath] = CreateReactorControlRoom(),
                [AftermathPath] = CreateReactorAftermath()
            };
        }

        private static DialogGraphExport CreateProductTour()
        {
            var start = Id("product.start");
            var end = Id("product.end");
            var welcome = Id("product.welcome");
            var controls = Id("product.controls");
            var focus = Id("product.focus");
            var branchReply = Id("product.branch.reply");
            var branchTv = Id("product.branch.tv");
            var branchWrap = Id("product.branch.wrap");
            var branchOutcome = Id("product.branch.outcome");
            var actionReply = Id("product.action.reply");
            var actionLights = Id("product.action.lights");
            var actionWrap = Id("product.action.wrap");
            var actionOutcome = Id("product.action.outcome");
            var branchChoice = Id("product.choice.branch");
            var actionChoice = Id("product.choice.action");

            var graph = Graph(
                ProductGraphGuid,
                "Product Tour",
                "A friendly guided tour of branching dialogue, variable text, localization, runtime controls, and visible scene actions.",
                "Demos/Product Tour",
                new[] { "demo-entry", "onboarding", "localization", "branching" },
                "Welcome the player and demonstrate the fastest path from authored graph to polished runtime presentation.",
                "Confident and friendly",
                "Keep explanations conversational and concise; every choice must produce a visibly distinct response.",
                start,
                end);

            graph.dialogNodes.AddRange(new[]
            {
                Dialog("Welcome Player", welcome, "Dax", "Welcome, {playerName}. You are already inside a graph: every line, choice, and scene cue was authored visually.", 300, 0),
                Dialog("Runtime Controls", controls, "Maeve", "Try history, autoplay, skip, and runtime settings while we show two genuinely different branches.", 800, 0),
                Dialog("Branching Response", branchReply, "Dax", "Branching keeps player intent explicit. This response exists only on the authoring route you selected.", 1800, -350),
                Dialog("Display Confirmation", branchWrap, "Maeve", "The display changed without custom flow code, and the backlog still records the complete route.", 2800, -350),
                Dialog("Action Response", actionReply, "Maeve", "Action nodes can pause or continue flow while scene objects react immediately.", 1800, 350),
                Dialog("Localized Wrap", actionWrap, "Dax", "Different choice, different response, same reusable runtime. Switch to German and the graph flow stays identical.", 2800, 350)
            });

            graph.choiceNodes.Add(Choice(
                focus,
                "What should we demonstrate first?",
                1300,
                0,
                Option(branchChoice, "Show me meaningful branching", branchReply),
                Option(actionChoice, "Show me a visible scene action", actionReply)));

            graph.actionNodes.AddRange(new[]
            {
                Action(branchTv, "TurnOnTV", "{\"on\":true}", true, 2300, -350),
                Action(actionLights, "FadeLights", "{\"group\":\"rooftop\",\"intensity\":0.45,\"duration\":0.8,\"color\":\"cool\"}", true, 2300, 350)
            });

            graph.outcomeNodes.AddRange(new[]
            {
                Outcome(branchOutcome, "product_tour_branching", "Branching Explored", "The player explored the branching-authoring path.", 3300, -350),
                Outcome(actionOutcome, "product_tour_actions", "Scene Actions Explored", "The player explored the visible-action path.", 3300, 350)
            });

            graph.links.AddRange(new[]
            {
                Link(start, welcome), Link(welcome, controls), Link(controls, focus),
                ChoiceLink(focus, branchChoice, branchReply, 0),
                ChoiceLink(focus, actionChoice, actionReply, 1),
                Link(branchReply, branchTv), ActionLink(branchTv, branchWrap), Link(branchWrap, branchOutcome), Link(branchOutcome, end),
                Link(actionReply, actionLights), ActionLink(actionLights, actionWrap), Link(actionWrap, actionOutcome), Link(actionOutcome, end)
            });

            return graph;
        }

        private static DialogGraphExport CreateShopGate()
        {
            var start = Id("shop.start");
            var end = Id("shop.end");
            var intro = Id("shop.intro");
            var reply = Id("shop.reply");
            var approach = Id("shop.approach");
            var payChoice = Id("shop.choice.pay");
            var keyChoice = Id("shop.choice.key");
            var trustChoice = Id("shop.choice.trust");
            var canPay = Id("shop.condition.pay");
            var hasKey = Id("shop.condition.key");
            var isTrusted = Id("shop.condition.trust");
            var payGold = Id("shop.mutation.pay");
            var reputation = Id("shop.mutation.reputation");
            var paid = Id("shop.dialog.paid");
            var noFunds = Id("shop.dialog.no_funds");
            var keyAccepted = Id("shop.dialog.key_accepted");
            var noKey = Id("shop.dialog.no_key");
            var trustAccepted = Id("shop.dialog.trust_accepted");
            var trustDenied = Id("shop.dialog.trust_denied");
            var openPaid = Id("shop.action.open_paid");
            var openKey = Id("shop.action.open_key");
            var openTrust = Id("shop.action.open_trust");
            var paidOutcome = Id("shop.outcome.paid");
            var keyOutcome = Id("shop.outcome.key");
            var trustOutcome = Id("shop.outcome.trust");
            var fundsDeniedOutcome = Id("shop.outcome.no_funds");
            var keyDeniedOutcome = Id("shop.outcome.no_key");
            var trustDeniedOutcome = Id("shop.outcome.no_trust");

            var graph = Graph(
                ShopGraphGuid,
                "Shop Gate",
                "A replayable checkpoint negotiation demonstrating variables, conditions, mutations, distinct choices, and successful or denied outcomes.",
                "Demos/Gameplay State",
                new[] { "demo-entry", "variables", "conditions", "outcomes" },
                "Let Veya negotiate passage through Sol's checkpoint using money, a key, or earned trust.",
                "Tense but fair",
                "OpenGate must execute exactly once on every successful route and never on a denied route.",
                start,
                end);

            graph.dialogNodes.AddRange(new[]
            {
                Dialog("Checkpoint Status", intro, "Sol", "Checkpoint rules, Veya. You carry {gold} gold; key status: {hasKey}; trust clearance: {trustLevel}.", 300, 0),
                Dialog("Choose An Approach", reply, "Veya", "Then I will choose the cleanest way through.", 800, 0),
                Dialog("Payment Accepted", paid, "Sol", "Payment accepted. Remaining balance: {gold} gold. The gate is opening.", 2800, -700),
                Dialog("Insufficient Gold", noFunds, "Sol", "Denied. Ten gold is required, and you currently have {gold}.", 2300, -350),
                Dialog("Key Accepted", keyAccepted, "Sol", "The checkpoint key is valid. Passage granted.", 2800, 0),
                Dialog("Key Missing", noKey, "Veya", "No key. I will need another approach.", 2300, 250),
                Dialog("Trust Accepted", trustAccepted, "Sol", "Your clearance is trusted. Passage granted without payment.", 2800, 650),
                Dialog("Exception Denied", trustDenied, "Sol", "Exception denied. Reputation is now {reputation}; return with proof.", 2800, 1000)
            });

            graph.choiceNodes.Add(Choice(
                approach,
                "How should Veya approach the gate?",
                1300,
                0,
                Option(payChoice, "Pay 10 gold ({gold} available)", canPay),
                Option(keyChoice, "Present the checkpoint key", hasKey),
                Option(trustChoice, "Request a trust exception", isTrusted)));

            graph.conditionNodes.AddRange(new[]
            {
                Condition(canPay, "gold", "Integer", "Greater", "9", 1800, -700),
                Condition(hasKey, "hasKey", "Boolean", "IsTrue", string.Empty, 1800, 0),
                Condition(isTrusted, "trustLevel", "Boolean", "IsTrue", string.Empty, 1800, 700)
            });

            graph.variableMutationNodes.AddRange(new[]
            {
                Mutation(payGold, "gold", "Integer", "Subtract", "10", 2300, -700),
                Mutation(reputation, "reputation", "Integer", "Add", "1", 2300, 1000)
            });

            graph.actionNodes.AddRange(new[]
            {
                Action(openPaid, "OpenGate", "{\"doorId\":\"forest_checkpoint\",\"speed\":\"normal\",\"open\":true}", true, 3300, -700),
                Action(openKey, "OpenGate", "{\"doorId\":\"forest_checkpoint\",\"speed\":\"fast\",\"open\":true}", true, 3300, 0),
                Action(openTrust, "OpenGate", "{\"doorId\":\"forest_checkpoint\",\"speed\":\"slow\",\"open\":true}", true, 3300, 650)
            });

            graph.outcomeNodes.AddRange(new[]
            {
                Outcome(paidOutcome, "shop_gate_paid", "Passage Purchased", "Veya paid the checkpoint fee.", 3800, -700),
                Outcome(keyOutcome, "shop_gate_key", "Key Presented", "Veya entered with the checkpoint key.", 3800, 0),
                Outcome(trustOutcome, "shop_gate_trusted", "Trust Exception", "Sol granted passage based on trust.", 3800, 650),
                Outcome(fundsDeniedOutcome, "shop_gate_denied_funds", "Insufficient Gold", "The payment route was denied.", 2800, -350),
                Outcome(keyDeniedOutcome, "shop_gate_denied_key", "Key Missing", "The key route was denied.", 2800, 250),
                Outcome(trustDeniedOutcome, "shop_gate_denied_trust", "Trust Too Low", "The exception route was denied.", 3300, 1000)
            });

            graph.links.AddRange(new[]
            {
                Link(start, intro), Link(intro, reply), Link(reply, approach),
                ChoiceLink(approach, payChoice, canPay, 0), ChoiceLink(approach, keyChoice, hasKey, 1), ChoiceLink(approach, trustChoice, isTrusted, 2),
                ConditionLink(canPay, true, payGold), ConditionLink(canPay, false, noFunds), Link(payGold, paid), Link(paid, openPaid), ActionLink(openPaid, paidOutcome), Link(paidOutcome, end), Link(noFunds, fundsDeniedOutcome), Link(fundsDeniedOutcome, end),
                ConditionLink(hasKey, true, keyAccepted), ConditionLink(hasKey, false, noKey), Link(keyAccepted, openKey), ActionLink(openKey, keyOutcome), Link(keyOutcome, end), Link(noKey, keyDeniedOutcome), Link(keyDeniedOutcome, end),
                ConditionLink(isTrusted, true, trustAccepted), ConditionLink(isTrusted, false, reputation), Link(trustAccepted, openTrust), ActionLink(openTrust, trustOutcome), Link(trustOutcome, end), Link(reputation, trustDenied), Link(trustDenied, trustDeniedOutcome), Link(trustDeniedOutcome, end)
            });

            return graph;
        }

        private static DialogGraphExport CreateReactorControlRoom()
        {
            var start = Id("reactor.start");
            var end = Id("reactor.end");
            var intro = Id("reactor.intro");
            var tv = Id("reactor.action.tv");
            var prompt = Id("reactor.prompt");
            var route = Id("reactor.route");
            var calmChoice = Id("reactor.choice.calm");
            var emergencyChoice = Id("reactor.choice.emergency");
            var setCalm = Id("reactor.mutation.calm");
            var setEmergency = Id("reactor.mutation.emergency");
            var fade = Id("reactor.action.fade");
            var alarm = Id("reactor.action.alarm");
            var countdown = Id("reactor.action.countdown");
            var calmTransfer = Id("reactor.dialog.calm_transfer");
            var emergencyTransfer = Id("reactor.dialog.emergency_transfer");
            var jump = Id("reactor.jump.aftermath");
            var safe = Id("reactor.condition.safe");
            var success = Id("reactor.dialog.success");
            var failure = Id("reactor.dialog.failure");
            var successOutcome = Id("reactor.outcome.success");
            var failureOutcome = Id("reactor.outcome.failure");

            var graph = Graph(
                ReactorGraphGuid,
                "Reactor Control Room",
                "An action-driven emergency dialogue demonstrating state mutation, environmental feedback, Graph Jump, and distinct final outcomes.",
                "Demos/Action Events",
                new[] { "demo-entry", "actions", "graph-jump", "conditions" },
                "Choose calm or emergency containment, enter the modular aftermath graph, and evaluate the resulting reactor temperature.",
                "Technical urgency",
                "Every action must create immediate scene feedback; the Graph Jump must resolve by direct asset and stable graph identity.",
                start,
                end);

            graph.dialogNodes.AddRange(new[]
            {
                Dialog("Reactor Status", intro, "Dax", "Reactor temperature: {temperature} C. State: {questState}. The wall display is our first feedback channel.", 300, 0),
                Dialog("Choose Response", prompt, "Sol", "Choose a response. Calm containment preserves control; emergency containment prioritizes speed.", 1000, 0),
                Dialog("Calm Transfer", calmTransfer, "Dax", "Lights lowered. Passing control to the containment subgraph.", 2600, -350),
                Dialog("Emergency Transfer", emergencyTransfer, "Sol", "Alarm and countdown complete. Passing control to emergency containment.", 3000, 350),
                Dialog("Containment Success", success, "Dax", "Temperature stable at {temperature} C. Containment succeeded.", 4300, -250),
                Dialog("Containment Failure", failure, "Sol", "Temperature remains {temperature} C. The emergency route bought time, but containment failed.", 4300, 300)
            });

            graph.choiceNodes.Add(Choice(
                route,
                "Which containment response should the team run?",
                1500,
                0,
                Option(calmChoice, "Run calm containment", setCalm),
                Option(emergencyChoice, "Run the emergency sequence", setEmergency)));

            graph.variableMutationNodes.AddRange(new[]
            {
                Mutation(setCalm, "questState", "String", "Set", "calm", 2000, -350),
                Mutation(setEmergency, "questState", "String", "Set", "emergency", 2000, 350)
            });

            graph.actionNodes.AddRange(new[]
            {
                Action(tv, "TurnOnTV", "{\"on\":true}", true, 650, 0),
                Action(fade, "FadeLights", "{\"group\":\"reactor\",\"intensity\":0.55,\"duration\":0.8,\"color\":\"cool\"}", true, 2300, -350),
                Action(alarm, "PlayAlarm", "{\"alarmId\":\"reactor_warning\",\"loop\":false,\"duration\":2.5}", false, 2300, 350),
                Action(countdown, "StartCountdown", "{\"countdownId\":\"containment\",\"seconds\":3,\"showOnHud\":true}", true, 2650, 350)
            });

            graph.graphJumpNodes.Add(new DialogExportGraphJumpNode
            {
                guid = jump,
                nodePositionX = 3400,
                nodePositionY = 0,
                targetGraph = new DialogExportGraphReference
                {
                    graphGuid = AftermathGraphGuid,
                    runtimeDialogId = "Demo_ReactorAftermath",
                    graphName = "Demo_ReactorAftermath",
                    assetPath = AftermathPath,
                    entryGuid = Id("aftermath.start")
                }
            });

            graph.conditionNodes.Add(Condition(safe, "temperature", "Float", "Less", "80", 3900, 0));
            graph.outcomeNodes.AddRange(new[]
            {
                Outcome(successOutcome, "reactor_contained", "Reactor Contained", "The calm route reduced temperature below the safe threshold.", 4800, -250),
                Outcome(failureOutcome, "reactor_unstable", "Reactor Still Unstable", "The emergency route completed but temperature remained above the safe threshold.", 4800, 300)
            });

            graph.links.AddRange(new[]
            {
                Link(start, intro), Link(intro, tv), ActionLink(tv, prompt), Link(prompt, route),
                ChoiceLink(route, calmChoice, setCalm, 0), ChoiceLink(route, emergencyChoice, setEmergency, 1),
                Link(setCalm, fade), ActionLink(fade, calmTransfer), Link(calmTransfer, jump),
                Link(setEmergency, alarm), ActionLink(alarm, countdown), ActionLink(countdown, emergencyTransfer), Link(emergencyTransfer, jump),
                Link(jump, safe), ConditionLink(safe, true, success), ConditionLink(safe, false, failure),
                Link(success, successOutcome), Link(successOutcome, end), Link(failure, failureOutcome), Link(failureOutcome, end)
            });

            return graph;
        }

        private static DialogGraphExport CreateReactorAftermath()
        {
            var start = Id("aftermath.start");
            var end = Id("aftermath.end");
            var emergency = Id("aftermath.condition.emergency");
            var calmLine = Id("aftermath.dialog.calm");
            var emergencyLine = Id("aftermath.dialog.emergency");
            var calmCooling = Id("aftermath.mutation.calm_cooling");
            var emergencyGate = Id("aftermath.action.emergency_gate");
            var emergencyCooling = Id("aftermath.mutation.emergency_cooling");
            var calmDone = Id("aftermath.dialog.calm_done");
            var emergencyDone = Id("aftermath.dialog.emergency_done");

            var graph = Graph(
                AftermathGraphGuid,
                "Reactor Containment Procedure",
                "A non-selectable supporting graph that modularizes the calm and emergency containment procedures.",
                "Demos/Support",
                new[] { "demo-support", "graph-jump-target", "modular-flow" },
                "Apply route-specific containment and return control to the Reactor Control Room graph.",
                "Focused procedure",
                "This graph is entered only through Demo_ControlRoomActions and must never jump back recursively.",
                start,
                end);

            graph.conditionNodes.Add(Condition(emergency, "questState", "String", "Equals", "emergency", 500, 0));
            graph.dialogNodes.AddRange(new[]
            {
                Dialog("Calm Cooling Loop", calmLine, "Dax", "Calm loop engaged. Coolant flow is stable and the room remains readable.", 1000, -300),
                Dialog("Emergency Venting", emergencyLine, "Sol", "Emergency vent selected. Open the containment gate before pressure peaks.", 1000, 300),
                Dialog("Calm Procedure Complete", calmDone, "Sol", "Calm containment removed twenty-five degrees. Returning to the main graph.", 2000, -300),
                Dialog("Emergency Procedure Complete", emergencyDone, "Dax", "Emergency venting removed ten degrees. Returning for the final safety check.", 2400, 300)
            });
            graph.variableMutationNodes.AddRange(new[]
            {
                Mutation(calmCooling, "temperature", "Float", "Subtract", "25", 1500, -300),
                Mutation(emergencyCooling, "temperature", "Float", "Subtract", "10", 2000, 300)
            });
            graph.actionNodes.Add(Action(
                emergencyGate,
                "OpenGate",
                "{\"doorId\":\"reactor_vent\",\"speed\":\"fast\",\"open\":true}",
                true,
                1500,
                300));
            graph.links.AddRange(new[]
            {
                Link(start, emergency),
                ConditionLink(emergency, false, calmLine), Link(calmLine, calmCooling), Link(calmCooling, calmDone), Link(calmDone, end),
                ConditionLink(emergency, true, emergencyLine), Link(emergencyLine, emergencyGate), ActionLink(emergencyGate, emergencyCooling), Link(emergencyCooling, emergencyDone), Link(emergencyDone, end)
            });

            return graph;
        }

        private static DialogGraphExport Graph(
            string graphGuid,
            string title,
            string description,
            string category,
            IEnumerable<string> tags,
            string sceneGoal,
            string tone,
            string rules,
            string startGuid,
            string endGuid)
        {
            return new DialogGraphExport
            {
                graphGuid = graphGuid,
                schemaVersion = DialogGraph.CurrentSchemaVersion,
                graphTitle = title,
                description = description,
                author = "Beka Forge",
                tags = new List<string>(tags),
                primaryCategory = category,
                categories = new List<string> { category },
                lastModifiedUtc = "2026-08-10T00:00:00Z",
                editorVersion = "2022.3.62f3",
                sceneGoal = sceneGoal,
                tone = tone,
                extraRules = rules,
                startNode = new ExportStartNode { guid = startGuid, nodePositionX = -200, nodePositionY = 0, isInitialized = true },
                endNode = new ExportEndNode { guid = endGuid, nodePositionX = 5400, nodePositionY = 0, isInitialized = true }
            };
        }

        private static DialogExportDialogNode Dialog(
            string title,
            string guid,
            string speaker,
            string text,
            float x,
            float y)
        {
            return new DialogExportDialogNode
            {
                title = title,
                guid = guid,
                speaker = speaker,
                question = text,
                nodePositionX = x,
                nodePositionY = y,
                displayTime = 0f
            };
        }

        private static DialogExportChoiceNode Choice(
            string guid,
            string prompt,
            float x,
            float y,
            params ExportChoice[] options)
        {
            return new DialogExportChoiceNode
            {
                guid = guid,
                text = prompt,
                nodePositionX = x,
                nodePositionY = y,
                choices = new List<ExportChoice>(options)
            };
        }

        private static ExportChoice Option(string id, string text, string nextGuid)
        {
            return new ExportChoice
            {
                choiceId = id,
                portKey = DialogGraphPortKeys.ForChoiceId(id),
                answerText = text,
                nextNodeGUID = nextGuid
            };
        }

        private static DialogExportActionNode Action(
            string guid,
            string actionId,
            string payload,
            bool wait,
            float x,
            float y)
        {
            return new DialogExportActionNode
            {
                guid = guid,
                actionId = actionId,
                payloadJson = payload,
                waitForCompletion = wait,
                waitSeconds = 0f,
                nodePositionX = x,
                nodePositionY = y
            };
        }

        private static DialogExportConditionNode Condition(
            string guid,
            string variable,
            string valueType,
            string conditionOperator,
            string comparison,
            float x,
            float y)
        {
            return new DialogExportConditionNode
            {
                guid = guid,
                variableName = variable,
                valueType = valueType,
                conditionOperator = conditionOperator,
                comparisonValue = comparison,
                missingVariableResult = false,
                nodePositionX = x,
                nodePositionY = y
            };
        }

        private static DialogExportVariableMutationNode Mutation(
            string guid,
            string variable,
            string valueType,
            string operation,
            string value,
            float x,
            float y)
        {
            return new DialogExportVariableMutationNode
            {
                guid = guid,
                variableName = variable,
                valueType = valueType,
                operation = operation,
                value = value,
                nodePositionX = x,
                nodePositionY = y
            };
        }

        private static DialogExportOutcomeNode Outcome(
            string guid,
            string outcomeId,
            string displayName,
            string description,
            float x,
            float y)
        {
            return new DialogExportOutcomeNode
            {
                guid = guid,
                outcomeId = outcomeId,
                displayName = displayName,
                description = description,
                nodePositionX = x,
                nodePositionY = y
            };
        }

        private static ExportLink Link(string from, string to)
        {
            return PortLink(from, to, DialogGraphPortKeys.Default, 0);
        }

        private static ExportLink ActionLink(string from, string to)
        {
            return PortLink(from, to, DialogGraphPortKeys.ActionSuccess, 0);
        }

        private static ExportLink ChoiceLink(string from, string choiceId, string to, int index)
        {
            return PortLink(from, to, DialogGraphPortKeys.ForChoiceId(choiceId), index);
        }

        private static ExportLink ConditionLink(string from, bool result, string to)
        {
            return PortLink(
                from,
                to,
                result ? DialogGraphPortKeys.True : DialogGraphPortKeys.False,
                result ? 0 : 1);
        }

        private static ExportLink PortLink(string from, string to, string portKey, int portIndex)
        {
            return new ExportLink
            {
                linkGuid = Id("link." + from + "." + portKey + "." + to),
                fromGuid = from,
                toGuid = to,
                fromPortKey = portKey,
                toPortKey = DialogGraphPortKeys.Default,
                fromPortIndex = portIndex
            };
        }

        internal static string Id(string value)
        {
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
