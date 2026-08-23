using System.Text;

namespace DialogSystem.Runtime.Variables
{
    /// <summary>
    /// Resolves dialogue text tokens against a <see cref="DialogueVariableStore"/>.
    /// Supported token formats are {variableKey}, {{variableKey}}, and {var:variableKey}.
    /// </summary>
    public static class DialogueVariableTextFormatter
    {
        #region ---------------- Public API ----------------
        /// <summary>
        /// Replaces supported variable tokens in <paramref name="text"/> with runtime values.
        /// Missing variables keep their original token so authoring mistakes remain visible.
        /// </summary>
        public static string Format(string text, DialogueVariableStore store)
        {
            return Format(text, store, null);
        }

        /// <summary>
        /// Replaces supported variable tokens in <paramref name="text"/> with runtime values.
        /// Missing variables use <paramref name="missingVariableFallback"/> when provided,
        /// otherwise their original token remains visible.
        /// </summary>
        public static string Format(string text, DialogueVariableStore store, string missingVariableFallback)
        {
            if (string.IsNullOrEmpty(text) || store == null)
            {
                return text ?? string.Empty;
            }

            var withDoubleBraceTokens = ReplaceTokens(text, "{{", "}}", store);
            var withPrefixedTokens = ReplaceTokens(withDoubleBraceTokens, "{var:", "}", store);
            return ReplaceSimpleBraceTokens(withPrefixedTokens, store, missingVariableFallback);
        }
        #endregion

        #region ---------------- Internals ----------------
        private static string ReplaceTokens(string text, string openToken, string closeToken, DialogueVariableStore store)
        {
            var start = text.IndexOf(openToken, System.StringComparison.Ordinal);
            if (start < 0)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length);
            var cursor = 0;

            while (start >= 0)
            {
                var keyStart = start + openToken.Length;
                var end = text.IndexOf(closeToken, keyStart, System.StringComparison.Ordinal);
                if (end < 0)
                {
                    break;
                }

                builder.Append(text, cursor, start - cursor);

                var key = text.Substring(keyStart, end - keyStart).Trim();
                if (!string.IsNullOrWhiteSpace(key) && TryGetTokenValue(store, key, out var value))
                {
                    builder.Append(value);
                }
                else
                {
                    builder.Append(text, start, end + closeToken.Length - start);
                }

                cursor = end + closeToken.Length;
                start = text.IndexOf(openToken, cursor, System.StringComparison.Ordinal);
            }

            builder.Append(text, cursor, text.Length - cursor);
            return builder.ToString();
        }

        private static string ReplaceSimpleBraceTokens(string text, DialogueVariableStore store, string missingVariableFallback)
        {
            var start = text.IndexOf('{');
            if (start < 0)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length);
            var cursor = 0;

            while (start >= 0)
            {
                if (IsEscapedOpenBrace(text, start))
                {
                    builder.Append(text, cursor, start + 2 - cursor);
                    cursor = start + 2;
                    start = text.IndexOf('{', cursor);
                    continue;
                }

                var end = text.IndexOf('}', start + 1);
                if (end < 0)
                {
                    break;
                }

                builder.Append(text, cursor, start - cursor);

                var rawKey = text.Substring(start + 1, end - start - 1);
                var key = rawKey.Trim();
                if (ShouldSkipSimpleToken(key))
                {
                    builder.Append(text, start, end + 1 - start);
                }
                else if (TryGetTokenValue(store, key, out var value))
                {
                    builder.Append(value);
                }
                else
                {
                    builder.Append(missingVariableFallback ?? text.Substring(start, end + 1 - start));
                }

                cursor = end + 1;
                start = text.IndexOf('{', cursor);
            }

            builder.Append(text, cursor, text.Length - cursor);
            return builder.ToString();
        }

        private static bool IsEscapedOpenBrace(string text, int start)
        {
            return start + 1 < text.Length && text[start + 1] == '{';
        }

        private static bool ShouldSkipSimpleToken(string key)
        {
            return string.IsNullOrWhiteSpace(key) ||
                   key.StartsWith("var:", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetTokenValue(DialogueVariableStore store, string key, out string value)
        {
            if (store.TryGetValueAsStringIgnoreCase(key, out value))
            {
                return true;
            }

            var compactKey = ToCompactCamelKey(key);
            return !string.Equals(compactKey, key, System.StringComparison.Ordinal) &&
                   store.TryGetValueAsStringIgnoreCase(compactKey, out value);
        }

        private static string ToCompactCamelKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return key;
            }

            var builder = new StringBuilder(key.Length);
            var uppercaseNext = false;

            foreach (var c in key.Trim())
            {
                if (char.IsWhiteSpace(c) || c == '-' || c == '_')
                {
                    uppercaseNext = builder.Length > 0;
                    continue;
                }

                if (builder.Length == 0)
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
                else if (uppercaseNext)
                {
                    builder.Append(char.ToUpperInvariant(c));
                    uppercaseNext = false;
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
        #endregion
    }
}
