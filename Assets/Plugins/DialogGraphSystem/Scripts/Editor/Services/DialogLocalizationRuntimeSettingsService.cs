using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime.Localization;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Services
{
    public static class DialogLocalizationRuntimeSettingsService
    {
        private const string SettingsFolder = "Assets/DialogGraphSystem/Resources/DialogSettingsSO";
        private const string SettingsAssetPath = SettingsFolder + "/DialogLocalizationRuntimeSettings.asset";

        public static DialogLocalizationRuntimeSettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DialogLocalizationRuntimeSettings>(SettingsAssetPath);
            if (settings != null)
            {
                return settings;
            }

            EnsureFolder(SettingsFolder);
            settings = ScriptableObject.CreateInstance<DialogLocalizationRuntimeSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public static DialogLocalizationRuntimeSettings SyncFromProjectTables(string defaultLocaleCode = "en-US")
        {
            var registry = new DialogLocalizationRegistryService();
            return SyncFromTables(registry.GetAllTables(), defaultLocaleCode);
        }

        public static DialogLocalizationRuntimeSettings SyncFromTables(
            IReadOnlyList<DialogLocalizationTable> tables,
            string defaultLocaleCode = "en-US")
        {
            var validTables = (tables ?? Array.Empty<DialogLocalizationTable>())
                .Where(table => table != null &&
                                AssetDatabase.Contains(table) &&
                                !string.IsNullOrWhiteSpace(table.LocaleCode))
                .GroupBy(table => table.LocaleCode, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderByDescending(table => table.IsSourceLanguage)
                .ThenBy(table => table.LocaleCode, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (validTables.Count == 0)
            {
                return AssetDatabase.LoadAssetAtPath<DialogLocalizationRuntimeSettings>(SettingsAssetPath);
            }

            var settings = EnsureSettings();

            var sourceTable = validTables.FirstOrDefault(table => table.IsSourceLanguage);
            settings.defaultLocaleCode = !string.IsNullOrWhiteSpace(sourceTable?.LocaleCode)
                ? sourceTable.LocaleCode
                : (string.IsNullOrWhiteSpace(defaultLocaleCode) ? "en-US" : defaultLocaleCode.Trim());

            settings.localizationTables = validTables;
            EnsureDisplayNameEntries(settings);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void EnsureDisplayNameEntries(DialogLocalizationRuntimeSettings settings)
        {
            settings.localeDisplayNames ??= new List<LocaleDisplayName>();

            foreach (var table in settings.localizationTables ?? new List<DialogLocalizationTable>())
            {
                if (table == null || string.IsNullOrWhiteSpace(table.LocaleCode))
                {
                    continue;
                }

                var exists = settings.localeDisplayNames.Any(entry =>
                    entry != null &&
                    string.Equals(entry.localeCode, table.LocaleCode, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    settings.localeDisplayNames.Add(new LocaleDisplayName
                    {
                        localeCode = table.LocaleCode,
                        displayName = settings.GetDisplayName(table.LocaleCode)
                    });
                }
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
