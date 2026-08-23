using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DialogSystem.EditorTools.Localization;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class LocalizationWorkflowTests
    {
        [Test]
        public void CreateNewTable_PersistsLocaleMetadata()
        {
            var service = new DialogLocalizationRegistryService();
            var localeCode = $"x-test-{Guid.NewGuid():N}".Substring(0, 15);
            var table = service.CreateNewTable(localeCode, isSource: false);

            Assert.That(table, Is.Not.Null);

            var path = AssetDatabase.GetAssetPath(table);
            try
            {
                var reloaded = AssetDatabase.LoadAssetAtPath<DialogLocalizationTable>(path);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.LocaleCode, Is.EqualTo(localeCode));
                Assert.That(reloaded.IsSourceLanguage, Is.False);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.Refresh();
                }
            }
        }

        [Test]
        public void ApplyTableMetadata_EnforcesSingleSourceAcrossProvidedTables()
        {
            var service = new DialogLocalizationRegistryService();
            var first = ScriptableObject.CreateInstance<DialogLocalizationTable>();
            var second = ScriptableObject.CreateInstance<DialogLocalizationTable>();

            try
            {
                first.ConfigureMetadata("en-US", true);
                second.ConfigureMetadata("fr-FR", false);

                service.ApplyTableMetadata(second, "fr-FR", true, new[] { first, second });

                Assert.That(first.IsSourceLanguage, Is.False);
                Assert.That(second.IsSourceLanguage, Is.True);
                Assert.That(second.LocaleCode, Is.EqualTo("fr-FR"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void SetupGraph_AssignsMissingKeysAndSyncsAllTextTypes()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var sourceTable = ScriptableObject.CreateInstance<DialogLocalizationTable>();
            var dialog = ScriptableObject.CreateInstance<DialogNode>();
            var choice = ScriptableObject.CreateInstance<ChoiceNode>();
            var projectSourceTable = new DialogLocalizationRegistryService().GetSourceTable();
            var projectSourcePath = AssetDatabase.GetAssetPath(projectSourceTable);
            var projectSourceSnapshot = File.ReadAllBytes(Path.GetFullPath(projectSourcePath));
            var projectSourceEntryCount = projectSourceTable.Count;

            try
            {
                graph.graphTitle = "Setup Test";
                sourceTable.ConfigureMetadata("en-US", true);

                dialog.SetGuid("dialog-001");
                dialog.speakerName = "Kira";
                dialog.questionText = "Hello there.";
                graph.nodes.Add(dialog);

                choice.SetGuid("choice-001");
                choice.text = "What do you do?";
                choice.choices.Add(new Choice { answerText = "Say hi" });
                graph.choiceNodes.Add(choice);

                var firstPass = DialogLocalizationSetupService.SetupGraph(graph, sourceTable);
                var secondPass = DialogLocalizationSetupService.SetupGraph(graph, sourceTable);

                Assert.That(firstPass.AssignedLocaleKeys, Is.GreaterThan(0));
                Assert.That(dialog.questionTextLocaleKey, Is.Not.Empty);
                Assert.That(dialog.speakerNameLocaleKey, Is.Not.Empty);
                Assert.That(choice.textLocaleKey, Is.Not.Empty);
                Assert.That(choice.choices[0].choiceId, Is.Not.Empty);
                Assert.That(choice.choices[0].answerTextLocaleKey, Is.Not.Empty);

                Assert.That(sourceTable.TryResolve(dialog.questionTextLocaleKey), Is.EqualTo(dialog.questionText));
                Assert.That(sourceTable.TryResolve(dialog.speakerNameLocaleKey), Is.EqualTo(dialog.speakerName));
                Assert.That(sourceTable.TryResolve(choice.textLocaleKey), Is.EqualTo(choice.text));
                Assert.That(sourceTable.TryResolve(choice.choices[0].answerTextLocaleKey), Is.EqualTo(choice.choices[0].answerText));

                Assert.That(secondPass.AssignedLocaleKeys, Is.EqualTo(0));
                Assert.That(
                    projectSourceTable.Count,
                    Is.EqualTo(projectSourceEntryCount),
                    "SetupGraph must sync only the explicitly supplied source table.");
            }
            finally
            {
                File.WriteAllBytes(Path.GetFullPath(projectSourcePath), projectSourceSnapshot);
                AssetDatabase.ImportAsset(projectSourcePath, ImportAssetOptions.ForceUpdate);
                UnityEngine.Object.DestroyImmediate(graph);
                UnityEngine.Object.DestroyImmediate(sourceTable);
                UnityEngine.Object.DestroyImmediate(dialog);
                UnityEngine.Object.DestroyImmediate(choice);
            }
        }

        [Test]
        public void RowBuilder_BuildsRowsFromGraphAndPopulatesContextAndTranslations()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var sourceTable = ScriptableObject.CreateInstance<DialogLocalizationTable>();
            var targetTable = ScriptableObject.CreateInstance<DialogLocalizationTable>();
            var dialog = ScriptableObject.CreateInstance<DialogNode>();
            var choice = ScriptableObject.CreateInstance<ChoiceNode>();

            try
            {
                graph.graphTitle = "Row Builder";
                sourceTable.ConfigureMetadata("en-US", true);
                targetTable.ConfigureMetadata("fr-FR", false);

                dialog.SetGuid("dialog-001");
                dialog.speakerName = "Kira";
                dialog.questionText = "Hello there.";
                graph.nodes.Add(dialog);

                choice.SetGuid("choice-001");
                choice.text = "What do you do?";
                choice.choices.Add(new Choice { answerText = "Say hi" });
                graph.choiceNodes.Add(choice);

                DialogLocalizationSetupService.SetupGraph(graph, sourceTable);
                targetTable.SetEntry(dialog.questionTextLocaleKey, "Bonjour.");

                var rows = DialogLocalizationRowBuilder.BuildRows(
                    graph,
                    new[] { sourceTable, targetTable },
                    new HashSet<string> { "fr-FR" });

                Assert.That(rows.Exists(row => row.EntryType == "dialog_text" && row.Speaker == "Kira"));
                Assert.That(rows.Exists(row => row.EntryType == "speaker_name" && row.DisplayName == "Speaker Name"));
                Assert.That(rows.Exists(row => row.EntryType == "choice_prompt" && row.DisplayName == "Choice Prompt"));
                Assert.That(rows.Exists(row => row.EntryType == "choice_answer" && row.DisplayName == "Choice 1"));

                var dialogRow = rows.Find(row => row.EntryType == "dialog_text");
                Assert.That(dialogRow, Is.Not.Null);
                Assert.That(dialogRow.Translations["fr-FR"], Is.EqualTo("Bonjour."));
                Assert.That(dialogRow.Context, Does.StartWith("dialog:"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graph);
                UnityEngine.Object.DestroyImmediate(sourceTable);
                UnityEngine.Object.DestroyImmediate(targetTable);
                UnityEngine.Object.DestroyImmediate(dialog);
                UnityEngine.Object.DestroyImmediate(choice);
            }
        }

        [Test]
        public void RowBuilder_FindDuplicateKeys_ReturnsRepeatedLocaleKeys()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var first = ScriptableObject.CreateInstance<DialogNode>();
            var second = ScriptableObject.CreateInstance<DialogNode>();

            try
            {
                graph.graphTitle = "Duplicate Keys";

                first.SetGuid("dialog-001");
                first.questionText = "Hello";
                first.questionTextLocaleKey = "duplicate.graph.text";
                graph.nodes.Add(first);

                second.SetGuid("dialog-002");
                second.questionText = "World";
                second.questionTextLocaleKey = "duplicate.graph.text";
                graph.nodes.Add(second);

                var duplicateKeys = DialogLocalizationRowBuilder.FindDuplicateKeys(graph);

                Assert.That(duplicateKeys, Has.Count.EqualTo(1));
                Assert.That(duplicateKeys[0], Is.EqualTo("duplicate.graph.text"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graph);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void HandleLanguageCreated_ActivatesLocaleImmediately()
        {
            var manager = new LocalizationManagerEmbedded();
            var table = ScriptableObject.CreateInstance<DialogLocalizationTable>();

            try
            {
                table.ConfigureMetadata("it-IT", false);

                var method = typeof(LocalizationManagerEmbedded).GetMethod(
                    "HandleLanguageCreated",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(method, Is.Not.Null);
                method.Invoke(manager, new object[] { table });

                var field = typeof(LocalizationManagerEmbedded).GetField(
                    "_activeLocaleCodes",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(field, Is.Not.Null);

                var activeCodes = field.GetValue(manager) as HashSet<string>;
                Assert.That(activeCodes, Is.Not.Null);
                Assert.That(activeCodes.Contains("it-IT"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void OrderedLocaleCodes_FollowAvailableLocaleOrder()
        {
            var manager = new LocalizationManagerEmbedded();

            var availableField = typeof(LocalizationManagerEmbedded).GetField(
                "_availableLocaleCodes",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var activeField = typeof(LocalizationManagerEmbedded).GetField(
                "_activeLocaleCodes",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var method = typeof(LocalizationManagerEmbedded).GetMethod(
                "GetOrderedLocaleCodes",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(availableField, Is.Not.Null);
            Assert.That(activeField, Is.Not.Null);
            Assert.That(method, Is.Not.Null);

            var availableCodes = (List<string>)availableField.GetValue(manager);
            var activeCodes = (HashSet<string>)activeField.GetValue(manager);

            availableCodes.Clear();
            availableCodes.AddRange(new[] { "de-DE", "fr-FR", "it-IT" });

            activeCodes.Clear();
            activeCodes.Add("it-IT");
            activeCodes.Add("de-DE");

            var orderedCodes = method.Invoke(manager, new object[] { false }) as List<string>;

            Assert.That(orderedCodes, Is.EqualTo(new[] { "de-DE", "it-IT" }));
        }

        [Test]
        public void LanguageCatalog_ExposesCommonTargetsAndResolvesChoiceLabels()
        {
            var choices = DialogLocalizationLanguageCatalog.GetChoiceLabels(
                new HashSet<string> { "fr-FR" },
                sourceLocaleCode: "en-US",
                includeCustomOption: true,
                customOptionLabel: "Custom locale code");

            Assert.That(choices.Any(choice => choice.IndexOf("German", StringComparison.OrdinalIgnoreCase) >= 0), Is.True);
            Assert.That(choices.Any(choice => choice.IndexOf("French", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(choices.Any(choice => choice.IndexOf("English", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(choices.Last(), Is.EqualTo("Custom locale code"));

            var germanChoice = choices.First(choice => choice.IndexOf("(de-DE)", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(DialogLocalizationLanguageCatalog.TryGetLocaleCode(germanChoice, out var localeCode), Is.True);
            Assert.That(localeCode, Is.EqualTo("de-DE"));
        }

        [Test]
        public void Runtime_InitializesFromSettingsAndSwitchesLocale()
        {
            var source = ScriptableObject.CreateInstance<DialogLocalizationTable>();
            var target = ScriptableObject.CreateInstance<DialogLocalizationTable>();
            var settings = ScriptableObject.CreateInstance<DialogLocalizationRuntimeSettings>();

            try
            {
                source.ConfigureMetadata("en-US", true);
                target.ConfigureMetadata("fr-FR", false);
                source.SetEntry("line.hello", "Hello");
                target.SetEntry("line.hello", "Bonjour");
                settings.defaultLocaleCode = "en-US";
                settings.rememberPlayerChoice = false;
                settings.localizationTables.Add(source);
                settings.localizationTables.Add(target);

                ResetLocalizationRuntime();
                DialogLocalizationRuntime.Instance.Initialize(settings);

                Assert.That(DialogLocalizationRuntime.Instance.ActiveLocaleCode, Is.EqualTo("en-US"));
                Assert.That(DialogLocalizationRuntime.Instance.Resolve("line.hello", "Fallback"), Is.EqualTo("Hello"));

                Assert.That(DialogLocalizationRuntime.Instance.SetActiveLocale("fr-FR"), Is.True);
                Assert.That(DialogLocalizationRuntime.Instance.ActiveLocaleCode, Is.EqualTo("fr-FR"));
                Assert.That(DialogLocalizationRuntime.Instance.Resolve("line.hello", "Fallback"), Is.EqualTo("Bonjour"));
                Assert.That(DialogLocalizationRuntime.Instance.SetActiveLocale("missing-locale"), Is.False);
            }
            finally
            {
                ResetLocalizationRuntime();
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Runtime_FallsBackWhenSavedLocaleIsInvalid()
        {
            var source = ScriptableObject.CreateInstance<DialogLocalizationTable>();
            var settings = ScriptableObject.CreateInstance<DialogLocalizationRuntimeSettings>();
            var prefsKey = $"DialogSystem.Locale.Test.{Guid.NewGuid():N}";

            try
            {
                source.ConfigureMetadata("en-US", true);
                settings.defaultLocaleCode = "en-US";
                settings.rememberPlayerChoice = true;
                settings.playerPrefsKey = prefsKey;
                settings.localizationTables.Add(source);
                PlayerPrefs.SetString(prefsKey, "zz-ZZ");

                ResetLocalizationRuntime();
                DialogLocalizationRuntime.Instance.Initialize(settings);

                Assert.That(DialogLocalizationRuntime.Instance.ActiveLocaleCode, Is.EqualTo("en-US"));
            }
            finally
            {
                PlayerPrefs.DeleteKey(prefsKey);
                ResetLocalizationRuntime();
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static void ResetLocalizationRuntime()
        {
            typeof(DialogLocalizationRuntime)
                .GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, null);
        }
    }
}
