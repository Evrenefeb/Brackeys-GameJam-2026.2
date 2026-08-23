using UnityEngine;

namespace DialogSystem.Runtime.Utils
{
    /// <summary>
    /// Centralized shared editor paths and runtime resource helpers.
    /// </summary>
    public static class TextResources
    {
        #region ---------------- Editor Menu Paths ----------------

        public const string MENU_ROOT = "Tools/BekaForge";
        public const string MENU_AI_PROVIDERS = MENU_ROOT + "/AI Providers";
        public const string MENU_DIALOGUE = MENU_ROOT + "/Dialogue";
        public const string MENU_DIALOGUE_WELCOME = MENU_ROOT + "/Dialogue Welcome";

        // Legacy aliases kept so older code paths still compile while the visible menu stays flat.
        public const string MENU_DIALOGUE_GRAPH_SYSTEM_ROOT = MENU_DIALOGUE;
        public const string MENU_DIALOGUE_GRAPH_SYSTEM_LAUNCHER = MENU_DIALOGUE;
        public const string MENU_DIALOGUE_GRAPH_SYSTEM_GRAPHS = MENU_DIALOGUE;
        public const string MENU_DIALOGUE_GRAPH_SYSTEM_LOCALIZATION = MENU_DIALOGUE;
        public const string MENU_DIALOGUE_GRAPH_SYSTEM_VALIDATION_PANEL = MENU_DIALOGUE;
        public const string MENU_DIALOGUE_GRAPH_SYSTEM_WELCOME = MENU_DIALOGUE_WELCOME;
        public const string MENU_DIALOGUE_GRAPH_SYSTEM_SETTINGS = MENU_DIALOGUE;

        public const string MENU_AI_PROVIDERS_DISPLAY = "Tools > BekaForge > AI Providers";
        public const string MENU_DIALOGUE_DISPLAY = "Tools > BekaForge > Dialogue";
        public const string MENU_DIALOGUE_WELCOME_DISPLAY = "Tools > BekaForge > Dialogue Welcome";
        public const string MENU_DIALOGUE_GRAPH_SYSTEM_LAUNCHER_DISPLAY = MENU_DIALOGUE_DISPLAY;
        public const string MENU_DIALOGUE_GRAPH_SYSTEM_LOCALIZATION_DISPLAY = MENU_DIALOGUE_DISPLAY;
        public const string MENU_DIALOGUE_GRAPH_SYSTEM_VALIDATION_PANEL_DISPLAY = MENU_DIALOGUE_DISPLAY;

        #endregion

        #region ---------------- Editor USS Asset Paths ----------------

        public const string EDITOR_USS_FOLDER = "Assets/DialogGraphSystem/Scripts/Editor/USS";
        /// <summary>Absolute asset path for the graph editor window stylesheet (use with AssetDatabase.LoadAssetAtPath).</summary>
        public const string STYLE_PATH = EDITOR_USS_FOLDER + "/DialogGraphEditorUSS.uss";
        public const string SETTINGS_STYLE_PATH = EDITOR_USS_FOLDER + "/DialogSettingsStyles.uss";
        public const string LOCALIZATION_MANAGER_STYLE_PATH = EDITOR_USS_FOLDER + "/LocalizationManagerStyles.uss";
        public const string WELCOME_STYLE_PATH = EDITOR_USS_FOLDER + "/DialogWelcomeStyles.uss";
        public const string MAIN_WINDOW_STYLE_PATH = EDITOR_USS_FOLDER + "/DialogSystemMainStyles.uss";
        public const string ICON_STYLE_PATH = EDITOR_USS_FOLDER + "/DialogGraphIcons.uss";
        public const string NODE_BASE_STYLE_PATH = EDITOR_USS_FOLDER + "/NodeBase.uss";
        public const string NODE_TYPES_STYLE_PATH = EDITOR_USS_FOLDER + "/NodeTypes.uss";
        public const string GRAPHS_FOLDER = "Assets/DialogGraphSystem/Graphs";

        #endregion

        #region ---------------- Folder Asset Paths ----------------

        public const string EXPORT_FOLDER = "Assets/DialogGraphSystem/Exports";
        public const string IMPORT_FOLDER = GRAPHS_FOLDER;

        /// <summary>Output folder for localization JSON export files.</summary>
        public const string LOCALIZATION_EXPORT_FOLDER = "Assets/DialogGraphSystem/Localization/Export";

        /// <summary>Asset folder for <c>DialogLocalizationTable</c> ScriptableObject assets.</summary>
        public const string LOCALIZATION_DEFINITIONS_FOLDER = "Assets/DialogGraphSystem/Definitions/Localization";

        /// <summary>
        /// Legacy graph asset location kept for backward-compatible reads.
        /// New graph authoring should prefer <see cref="GRAPHS_FOLDER"/>.
        /// </summary>
        public const string CONVERSATION_FOLDER = "Assets/DialogGraphSystem/Resources/Conversation";

        #endregion

        #region ---------------- Runtime Load Helpers ----------------

        public static Texture2D LoadRuntimeIcon(string resourceKey) =>
            Resources.Load<Texture2D>(resourceKey);

        #endregion

    }
}
