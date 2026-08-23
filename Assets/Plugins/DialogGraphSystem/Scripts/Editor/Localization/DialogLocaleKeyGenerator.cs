using System.Text.RegularExpressions;
using UnityEngine;

namespace DialogSystem.EditorTools.Localization
{
    /// <summary>
    /// Editor-only utility that generates stable, collision-resistant locale keys
    /// from graph and node context. Keys follow the pattern:
    /// <c>{graphSlug}.{nodeToken}.text</c> for dialog nodes and
    /// <c>{graphSlug}.{nodeToken}.choice_{choiceToken}</c> for choice answers.
    /// <para>
    /// Once stamped on a node, a key must never be regenerated automatically.
    /// Only explicit user actions (inspector button or batch tool) should call this.
    /// </para>
    /// </summary>
    public static class DialogLocaleKeyGenerator
    {
        private static readonly Regex SlugPattern = new Regex(
            @"[^a-z0-9]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Generates a stable locale key for a dialog node's <c>questionText</c>.
        /// Example output: <c>the_tavern_001.demo_tour_d1.text</c>
        /// </summary>
        /// <param name="graphTitle">Human-readable title of the owning graph.</param>
        /// <param name="nodeGuid">Full GUID string of the node.</param>
        public static string GenerateDialogNodeKey(string graphTitle, string nodeGuid)
        {
            return $"{Slugify(graphTitle)}.{IdentityToken(nodeGuid)}.text";
        }

        /// <summary>
        /// Generates a stable locale key for a choice node's prompt <c>text</c>.
        /// Example output: <c>the_tavern_001.demo_tour_c1.prompt</c>
        /// </summary>
        public static string GenerateChoiceNodePromptKey(string graphTitle, string nodeGuid)
        {
            return $"{Slugify(graphTitle)}.{IdentityToken(nodeGuid)}.prompt";
        }

        /// <summary>
        /// Generates a stable locale key for a single choice's <c>answerText</c>.
        /// Example output: <c>the_tavern_001.demo_tour_c1.choice_b88acddfc2e341eeb7534be9afd3107c</c>
        /// </summary>
        /// <param name="graphTitle">Human-readable title of the owning graph.</param>
        /// <param name="nodeGuid">Full GUID string of the ChoiceNode.</param>
        /// <param name="choiceId">Stable choice ID from <see cref="Choice.choiceId"/>.</param>
        public static string GenerateChoiceKey(string graphTitle, string nodeGuid, string choiceId)
        {
            return $"{Slugify(graphTitle)}.{IdentityToken(nodeGuid)}.choice_{IdentityToken(choiceId)}";
        }

        /// <summary>
        /// Generates a stable locale key for a dialog node's speaker name.
        /// Only needed when the speaker name is an inline raw string rather
        /// than resolved through a DialogCharacterSO.
        /// </summary>
        public static string GenerateSpeakerNameKey(string graphTitle, string nodeGuid)
        {
            return $"{Slugify(graphTitle)}.{IdentityToken(nodeGuid)}.speaker";
        }

        /// <summary>
        /// Lowercases <paramref name="input"/> and replaces every run of
        /// non-alphanumeric characters with an underscore. Leading/trailing
        /// underscores are trimmed.
        /// </summary>
        public static string Slugify(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "graph";
            var lower = input.Trim().ToLowerInvariant();
            var slug  = SlugPattern.Replace(lower, "_").Trim('_');
            return string.IsNullOrEmpty(slug) ? "graph" : slug;
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private static string IdentityToken(string value)
        {
            return Slugify(value);
        }
    }
}
