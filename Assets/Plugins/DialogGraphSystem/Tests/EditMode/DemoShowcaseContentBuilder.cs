using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.EditorTools.ExportImport;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Variables;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public static class DemoShowcaseContentBuilder
    {
        private const string DefinitionsRoot = "Assets/DialogGraphSystem/Definitions";
        private const string GraphsRoot = "Assets/DialogGraphSystem/Graphs";

        private static readonly IReadOnlyDictionary<string, string> German =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Welcome, {playerName}. You are already inside a graph: every line, choice, and scene cue was authored visually."] =
                    "Willkommen, {playerName}. Du befindest dich bereits in einem Graphen: Jede Zeile, Auswahl und Szenenaktion wurde visuell erstellt.",
                ["Try history, autoplay, skip, and runtime settings while we show two genuinely different branches."] =
                    "Probiere Verlauf, automatische Wiedergabe, Überspringen und Laufzeiteinstellungen aus, während wir zwei wirklich unterschiedliche Zweige zeigen.",
                ["Branching keeps player intent explicit. This response exists only on the authoring route you selected."] =
                    "Verzweigungen machen die Absicht des Spielers eindeutig. Diese Antwort existiert nur auf dem gewählten Autorenpfad.",
                ["The display changed without custom flow code, and the backlog still records the complete route."] =
                    "Die Anzeige änderte sich ohne eigenen Ablaufcode, und der Verlauf zeichnet weiterhin die vollständige Route auf.",
                ["Action nodes can pause or continue flow while scene objects react immediately."] =
                    "Aktionsknoten können den Ablauf anhalten oder fortsetzen, während Szenenobjekte sofort reagieren.",
                ["Different choice, different response, same reusable runtime. Switch to German and the graph flow stays identical."] =
                    "Andere Auswahl, andere Antwort, dieselbe wiederverwendbare Laufzeit. Beim Wechsel zu Deutsch bleibt der Graphablauf identisch.",
                ["What should we demonstrate first?"] = "Was sollen wir zuerst demonstrieren?",
                ["Show me meaningful branching"] = "Zeig mir aussagekräftige Verzweigungen",
                ["Show me a visible scene action"] = "Zeig mir eine sichtbare Szenenaktion",

                ["Checkpoint rules, Veya. You carry {gold} gold; key status: {hasKey}; trust clearance: {trustLevel}."] =
                    "Die Regeln des Kontrollpunkts, Veya. Du trägst {gold} Gold; Schlüsselstatus: {hasKey}; Vertrauensfreigabe: {trustLevel}.",
                ["Then I will choose the cleanest way through."] = "Dann wähle ich den saubersten Weg hindurch.",
                ["Payment accepted. Remaining balance: {gold} gold. The gate is opening."] =
                    "Zahlung akzeptiert. Verbleibendes Guthaben: {gold} Gold. Das Tor öffnet sich.",
                ["Denied. Ten gold is required, and you currently have {gold}."] =
                    "Abgelehnt. Zehn Gold sind erforderlich, und du hast derzeit {gold}.",
                ["The checkpoint key is valid. Passage granted."] = "Der Kontrollpunktschlüssel ist gültig. Durchgang gewährt.",
                ["No key. I will need another approach."] = "Kein Schlüssel. Ich brauche einen anderen Ansatz.",
                ["Your clearance is trusted. Passage granted without payment."] =
                    "Deine Freigabe ist vertrauenswürdig. Durchgang ohne Zahlung gewährt.",
                ["Exception denied. Reputation is now {reputation}; return with proof."] =
                    "Ausnahme abgelehnt. Der Ruf beträgt jetzt {reputation}; kehre mit einem Nachweis zurück.",
                ["How should Veya approach the gate?"] = "Wie soll Veya sich dem Tor nähern?",
                ["Pay 10 gold ({gold} available)"] = "10 Gold bezahlen ({gold} verfügbar)",
                ["Present the checkpoint key"] = "Den Kontrollpunktschlüssel vorzeigen",
                ["Request a trust exception"] = "Eine Vertrauensausnahme beantragen",

                ["Reactor temperature: {temperature} C. State: {questState}. The wall display is our first feedback channel."] =
                    "Reaktortemperatur: {temperature} C. Status: {questState}. Die Wandanzeige ist unser erster Rückmeldekanal.",
                ["Choose a response. Calm containment preserves control; emergency containment prioritizes speed."] =
                    "Wähle eine Reaktion. Ruhige Eindämmung bewahrt die Kontrolle; Notfalleindämmung priorisiert Geschwindigkeit.",
                ["Lights lowered. Passing control to the containment subgraph."] =
                    "Beleuchtung gedimmt. Die Kontrolle wird an den Eindämmungs-Untergraphen übergeben.",
                ["Alarm and countdown complete. Passing control to emergency containment."] =
                    "Alarm und Countdown abgeschlossen. Die Kontrolle wird an die Notfalleindämmung übergeben.",
                ["Temperature stable at {temperature} C. Containment succeeded."] =
                    "Temperatur stabil bei {temperature} C. Eindämmung erfolgreich.",
                ["Temperature remains {temperature} C. The emergency route bought time, but containment failed."] =
                    "Die Temperatur bleibt bei {temperature} C. Die Notfallroute gewann Zeit, aber die Eindämmung schlug fehl.",
                ["Which containment response should the team run?"] = "Welche Eindämmungsreaktion soll das Team ausführen?",
                ["Run calm containment"] = "Ruhige Eindämmung ausführen",
                ["Run the emergency sequence"] = "Notfallsequenz ausführen",

                ["Calm loop engaged. Coolant flow is stable and the room remains readable."] =
                    "Ruhiger Kreislauf aktiviert. Der Kühlmittelfluss ist stabil und der Raum bleibt übersichtlich.",
                ["Emergency vent selected. Open the containment gate before pressure peaks."] =
                    "Notentlüftung gewählt. Öffne das Eindämmungstor, bevor der Druck seinen Höhepunkt erreicht.",
                ["Calm containment removed twenty-five degrees. Returning to the main graph."] =
                    "Die ruhige Eindämmung senkte die Temperatur um fünfundzwanzig Grad. Rückkehr zum Hauptgraphen.",
                ["Emergency venting removed ten degrees. Returning for the final safety check."] =
                    "Die Notentlüftung senkte die Temperatur um zehn Grad. Rückkehr zur abschließenden Sicherheitsprüfung."
            };

        public static void RebuildFromCommandLine()
        {
            try
            {
                NormalizeAssetPaths();
                NormalizeDefinitions();
                RebuildGraphs();
                AssignGraphDefinitions();
                RebuildLocalization();
                ExportJsonExamples();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[DemoShowcaseContentBuilder] Showcase content rebuilt successfully.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void NormalizeAssetPaths()
        {
            MoveAssetPreservingGuid(GraphsRoot + "/Shop-Gate-Test.asset", DemoShowcaseGraphSpecs.ShopPath);
            MoveAssetPreservingGuid(DefinitionsRoot + "/Variables/Gold.asset", DefinitionsRoot + "/Variables/gold.asset");
            MoveAssetPreservingGuid(DefinitionsRoot + "/Variables/haskey.asset", DefinitionsRoot + "/Variables/hasKey.asset");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void NormalizeDefinitions()
        {
            ConfigureVariable("gold", variable => variable.SetDefaultValue(20));
            ConfigureVariable("hasKey", variable => variable.SetDefaultValue(false));
            ConfigureVariable("playerName", variable => variable.SetDefaultValue("Alex"));
            ConfigureVariable("questState", variable => variable.SetDefaultValue("standby"));
            ConfigureVariable("reputation", variable => variable.SetDefaultValue(1));
            ConfigureVariable("temperature", variable => variable.SetDefaultValue(96f));
            ConfigureVariable("trustLevel", variable => variable.SetDefaultValue(false));

            var veya = Load<DialogCharacterSO>(DefinitionsRoot + "/Characters/Veya.asset");
            veya.SpeechStyle = "Sparse, cool, and slightly poetic. She favors sharp images over long explanations.";
            EditorUtility.SetDirty(veya);

            ConfigureContext(
                "RooftopDebrief", "NeonRooftop",
                new[] { "Dax", "Maeve" }, new[] { "TurnOnTV", "FadeLights" },
                "Welcome a new user and demonstrate branching dialogue with immediate scene feedback.",
                "Confident",
                "Dax keeps the pace practical; Maeve explains the authoring value without turning the scene into documentation.");
            ConfigureContext(
                "CheckpointStandoff", "ForestCheckpoint",
                new[] { "Sol", "Veya" }, new[] { "OpenGate" },
                "Negotiate passage through a guarded checkpoint using money, a key, or trust.",
                "Tense",
                "Sol applies the rules fairly; Veya stays observant and concise. Successful routes open the gate once.");
            ConfigureContext(
                "ReactorShutdown", "ReactorControlRoom",
                new[] { "Dax", "Sol" }, new[] { "TurnOnTV", "PlayAlarm", "StartCountdown", "FadeLights", "OpenGate" },
                "Coordinate a modular reactor containment response before the room locks down.",
                "Critical",
                "Dax owns technical actions; Sol keeps the response understandable. Calm and emergency routes must remain distinct.");
            ConfigureContext(
                "OutpostDeal", "DesertOutpost",
                new[] { "Dax", "Sol", "Veya" }, Array.Empty<string>(),
                "Bargain for scarce supplies while each character wants a different concession.",
                "Wary",
                "Dax may bluff, Sol should de-escalate, and Veya should notice the hidden cost.");
            ConfigureContext(
                "TVBanter", "RainyLivingRoom",
                new[] { "Dax", "Maeve" }, new[] { "TurnOnTV", "FadeLights" },
                "Keep a domestic scene playful while a small problem reveals the relationship dynamic.",
                "Witty",
                "Dax stays restless and practical; Maeve keeps the energy controlled and dry.");

            var catalog = Load<DialogGraphCategoryCatalog>(DefinitionsRoot + "/GraphCategories/DialogGraphCategoryCatalog.asset");
            catalog.categories = new List<DialogGraphCategoryDefinition>
            {
                Category("Demos/Product Tour", new Color(0.22f, 0.64f, 0.96f, 1f)),
                Category("Demos/Gameplay State", new Color(0.30f, 0.78f, 0.48f, 1f)),
                Category("Demos/Action Events", new Color(0.96f, 0.48f, 0.24f, 1f)),
                Category("Demos/Support", new Color(0.55f, 0.58f, 0.68f, 1f))
            };
            EditorUtility.SetDirty(catalog);
        }

        private static void RebuildGraphs()
        {
            foreach (var pair in DemoShowcaseGraphSpecs.CreateAll())
            {
                var json = JsonUtility.ToJson(pair.Value, true);
                DialogGraphImportResult result;
                if (AssetDatabase.LoadAssetAtPath<DialogGraph>(pair.Key) == null)
                {
                    result = DialogGraphImportTransactionService.ImportNew(
                        GraphsRoot,
                        Path.GetFileNameWithoutExtension(pair.Key),
                        json);
                }
                else
                {
                    result = DialogGraphImportTransactionService.ImportOverwrite(pair.Key, json);
                }

                if (!result.Success)
                {
                    throw new InvalidOperationException(
                        $"Could not rebuild {pair.Key} at stage {result.FailureStage}: {result.Message}\n" +
                        string.Join("\n", result.Errors));
                }

                if (!string.IsNullOrWhiteSpace(result.BackupPath))
                {
                    AssetDatabase.DeleteAsset(result.BackupPath);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void AssignGraphDefinitions()
        {
            ConfigureGraph(
                DemoShowcaseGraphSpecs.ProductPath,
                "RooftopDebrief", "NeonRooftop",
                new[] { "Dax", "Maeve" },
                new[] { "TurnOnTV", "FadeLights" },
                new[] { "playerName" });
            ConfigureGraph(
                DemoShowcaseGraphSpecs.ShopPath,
                "CheckpointStandoff", "ForestCheckpoint",
                new[] { "Sol", "Veya" },
                new[] { "OpenGate" },
                new[] { "gold", "hasKey", "trustLevel", "reputation" });
            ConfigureGraph(
                DemoShowcaseGraphSpecs.ReactorPath,
                "ReactorShutdown", "ReactorControlRoom",
                new[] { "Dax", "Sol" },
                new[] { "TurnOnTV", "PlayAlarm", "StartCountdown", "FadeLights", "OpenGate" },
                new[] { "temperature", "questState" });
            ConfigureGraph(
                DemoShowcaseGraphSpecs.AftermathPath,
                "ReactorShutdown", "ReactorControlRoom",
                new[] { "Dax", "Sol" },
                new[] { "OpenGate" },
                new[] { "temperature", "questState" });

            var reactor = Load<DialogGraph>(DemoShowcaseGraphSpecs.ReactorPath);
            var aftermath = Load<DialogGraph>(DemoShowcaseGraphSpecs.AftermathPath);
            var jump = reactor.graphJumpNodes.Single();
            jump.targetGraph.graphAsset = aftermath;
            jump.targetGraph.graphGuid = aftermath.GraphGuid;
            jump.targetGraph.runtimeDialogId = "Demo_ReactorAftermath";
            jump.targetGraph.graphName = aftermath.name;
            jump.targetGraph.assetPath = DemoShowcaseGraphSpecs.AftermathPath;
            jump.targetGraph.entryGuid = aftermath.startGuid;
            EditorUtility.SetDirty(jump);
            EditorUtility.SetDirty(reactor);
        }

        private static void RebuildLocalization()
        {
            var source = Load<DialogLocalizationTable>(DefinitionsRoot + "/Localization/DialogLocalizationTable.asset");
            var german = Load<DialogLocalizationTable>(DefinitionsRoot + "/Localization/DialogLocalizationTable_de_DE.asset");
            source.ConfigureMetadata("en-US", true);
            german.ConfigureMetadata("de-DE", false);
            ClearTable(source);
            ClearTable(german);

            foreach (var graph in DemoShowcaseGraphSpecs.CreateAll().Keys.Select(Load<DialogGraph>))
            {
                var slug = graph.name.ToLowerInvariant();
                foreach (var node in graph.nodes)
                {
                    node.speakerNameLocaleKey = $"graph.{slug}.{node.GetGuid()}.speaker";
                    node.questionTextLocaleKey = $"graph.{slug}.{node.GetGuid()}.text";
                    source.SetEntry(node.speakerNameLocaleKey, node.speakerName);
                    german.SetEntry(node.speakerNameLocaleKey, node.speakerName);
                    source.SetEntry(node.questionTextLocaleKey, node.questionText);
                    german.SetEntry(node.questionTextLocaleKey, Translate(node.questionText));
                    EditorUtility.SetDirty(node);
                }

                foreach (var node in graph.choiceNodes)
                {
                    node.textLocaleKey = $"graph.{slug}.{node.GetGuid()}.prompt";
                    source.SetEntry(node.textLocaleKey, node.text);
                    german.SetEntry(node.textLocaleKey, Translate(node.text));
                    foreach (var choice in node.choices)
                    {
                        choice.answerTextLocaleKey = $"graph.{slug}.{node.GetGuid()}.choice_{choice.choiceId}";
                        source.SetEntry(choice.answerTextLocaleKey, choice.answerText);
                        german.SetEntry(choice.answerTextLocaleKey, Translate(choice.answerText));
                    }

                    EditorUtility.SetDirty(node);
                }

                EditorUtility.SetDirty(graph);
            }

            EditorUtility.SetDirty(source);
            EditorUtility.SetDirty(german);
        }

        private static void ExportJsonExamples()
        {
            var outputPaths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DemoShowcaseGraphSpecs.ProductPath] = "Assets/DialogGraphSystem/Resources/JSON/Demo_ProductTour.json",
                [DemoShowcaseGraphSpecs.ShopPath] = "Assets/DialogGraphSystem/Resources/JSON/Demo_ShopGate.json",
                [DemoShowcaseGraphSpecs.ReactorPath] = "Assets/DialogGraphSystem/Resources/JSON/Demo_ControlRoomActions.json",
                [DemoShowcaseGraphSpecs.AftermathPath] = "Assets/DialogGraphSystem/Resources/JSON/Demo_ReactorAftermath.json"
            };

            foreach (var pair in outputPaths)
            {
                var graph = Load<DialogGraph>(pair.Key);
                var dto = DialogGraphJsonSerializationUtility.BuildExportDto(
                    graph,
                    DialogGraphJsonExportOptions.Default);
                var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), pair.Value);
                File.WriteAllText(absolutePath, JsonUtility.ToJson(dto, true) + Environment.NewLine);
                AssetDatabase.ImportAsset(pair.Value, ImportAssetOptions.ForceSynchronousImport);
            }

            AssetDatabase.DeleteAsset("Assets/DialogGraphSystem/Exports/Shop-Gate-Test.json");
        }

        private static void ConfigureGraph(
            string graphPath,
            string contextName,
            string environmentName,
            IEnumerable<string> characterNames,
            IEnumerable<string> actionNames,
            IEnumerable<string> variableKeys)
        {
            var graph = Load<DialogGraph>(graphPath);
            graph.sceneContext = Load<DialogSceneContextSO>(DefinitionsRoot + "/SceneContexts/" + contextName + ".asset");
            graph.environment = Load<DialogEnvironmentSO>(DefinitionsRoot + "/Environments/" + environmentName + ".asset");
            graph.participatingCharacters = characterNames.Select(name =>
                Load<DialogCharacterSO>(DefinitionsRoot + "/Characters/" + name + ".asset")).ToList();
            graph.availableActions = actionNames.Select(name =>
                Load<DialogActionSO>(DefinitionsRoot + "/Actions/" + name + ".asset")).ToList();
            graph.availableVariables = variableKeys.Select(key =>
                Load<DialogVariableSO>(DefinitionsRoot + "/Variables/" + key + ".asset")).ToList();

            var characters = graph.participatingCharacters.ToDictionary(
                character => character.DisplayName,
                StringComparer.Ordinal);
            foreach (var node in graph.nodes)
            {
                if (characters.TryGetValue(node.speakerName, out var character))
                {
                    node.speakerPortrait = character.Portrait;
                    EditorUtility.SetDirty(node);
                }
            }

            EditorUtility.SetDirty(graph);
        }

        private static void ConfigureContext(
            string contextName,
            string environmentName,
            IEnumerable<string> characterNames,
            IEnumerable<string> actionNames,
            string goal,
            string tone,
            string rules)
        {
            var context = Load<DialogSceneContextSO>(DefinitionsRoot + "/SceneContexts/" + contextName + ".asset");
            context.Environment = Load<DialogEnvironmentSO>(DefinitionsRoot + "/Environments/" + environmentName + ".asset");
            context.ParticipatingCharacters.Clear();
            context.ParticipatingCharacters.AddRange(characterNames.Select(name =>
                Load<DialogCharacterSO>(DefinitionsRoot + "/Characters/" + name + ".asset")));
            context.AvailableActions.Clear();
            context.AvailableActions.AddRange(actionNames.Select(name =>
                Load<DialogActionSO>(DefinitionsRoot + "/Actions/" + name + ".asset")));
            context.SceneGoal = goal;
            context.Tone = tone;
            context.ExtraRules = rules;
            EditorUtility.SetDirty(context);
        }

        private static void ConfigureVariable(string key, Action<DialogVariableSO> configure)
        {
            var variable = Load<DialogVariableSO>(DefinitionsRoot + "/Variables/" + key + ".asset");
            variable.name = key;
            variable.SetKey(key);
            configure(variable);
            EditorUtility.SetDirty(variable);
        }

        private static void MoveAssetPreservingGuid(string sourcePath, string destinationPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
            {
                return;
            }

            var actualDestination = AssetDatabase.LoadMainAssetAtPath(destinationPath);
            if (actualDestination != null &&
                string.Equals(AssetDatabase.GetAssetPath(actualDestination), destinationPath, StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                var directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
                var temporaryPath = directory + "/__dgs_case_normalization__.asset";
                MoveAsset(sourcePath, temporaryPath);
                MoveAsset(temporaryPath, destinationPath);
                return;
            }

            MoveAsset(sourcePath, destinationPath);
        }

        private static void MoveAsset(string sourcePath, string destinationPath)
        {
            var error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException(
                    $"Could not move '{sourcePath}' to '{destinationPath}': {error}");
            }
        }

        private static DialogGraphCategoryDefinition Category(string path, Color color)
        {
            return new DialogGraphCategoryDefinition { path = path, color = color };
        }

        private static void ClearTable(DialogLocalizationTable table)
        {
            foreach (var key in table.AllEntries.Keys.ToList())
            {
                table.RemoveEntry(key);
            }
        }

        private static string Translate(string english)
        {
            if (German.TryGetValue(english, out var translation))
            {
                return translation;
            }

            throw new InvalidOperationException("Missing German demo translation: " + english);
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"Required asset not found at '{path}'.");
            }

            return asset;
        }
    }
}
