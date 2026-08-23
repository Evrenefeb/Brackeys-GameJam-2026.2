using UnityEngine;

namespace DialogSystem.EditorTools.View
{
    /// <summary>
    /// Centralized editor UI constants.
    /// Node colors and per-type accents live in USS (NodeTypes.uss) and are not
    /// duplicated here. Only layout constants that editor C# code needs directly
    /// are kept in this class.
    /// </summary>
    public static class DialogGraphEditorStyles
    {
        // ── Toolbar ───────────────────────────────────────────────
        public const float ToolbarButtonHeight = 26f;
        public const float ToolbarHeight       = 38f;

        // ── Sidebar ───────────────────────────────────────────────
        public const float DefaultSidebarWidth = 340f;
    }
}
