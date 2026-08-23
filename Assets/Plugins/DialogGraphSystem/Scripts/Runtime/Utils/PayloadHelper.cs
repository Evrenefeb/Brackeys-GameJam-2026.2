using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace DialogSystem.Runtime.Utils
{
    /// <summary>
    /// Simple helpers for reading JSON payloads used by ActionNodes.
    ///
    /// Recommended usage:
    ///   [Serializable] class MyPayload { public int seconds = 5; public string color = "#FFFFFF"; }
    ///   var p = PayloadHelper.Parse(payloadJson, new MyPayload { seconds = 3 }); // JSON overrides defaults
    ///
    /// Extras:
    ///   - Top-level TryGet for quick reads (int/float/bool/string)
    ///   - Color & Vector parsers (from "#RRGGBB"/"r,g,b" or JSON objects with x/y/z)
    ///   - Token interpolation: PayloadHelper.Interpolate("T-{s}", ("s","10"))
    /// </summary>
    public static class PayloadHelper
    {
        // ------------------------------------------------------------
        // Strongly-typed parsing (best UX)
        // ------------------------------------------------------------

        /// <summary>
        /// Deserialize JSON into a new T, pre-populated with 'defaults'.
        /// Only fields present in JSON are overwritten (merge).
        /// </summary>
        public static T Parse<T>(string json, T defaults) where T : class, new()
        {
            var instance = Clone(defaults ?? new T());
            if (!string.IsNullOrEmpty(json))
            {
                try { JsonUtility.FromJsonOverwrite(json, instance); }
                catch (Exception e) { Debug.LogWarning($"[PayloadHelper] Parse<{typeof(T).Name}> failed: {e.Message}"); }
            }
            return instance;
        }

        /// <summary>
        /// Deserialize JSON into a new T (or default(T) if invalid).
        /// </summary>
        public static T ParseOrDefault<T>(string json) where T : class, new()
        {
            if (string.IsNullOrEmpty(json)) return new T();
            try { return JsonUtility.FromJson<T>(json); }
            catch { return new T(); }
        }

        private static T Clone<T>(T src) where T : class, new()
        {
            if (src == null) return new T();
            try { return JsonUtility.FromJson<T>(JsonUtility.ToJson(src)); }
            catch { return new T(); }
        }

        // ------------------------------------------------------------
        // Quick top-level getters (simple payloads)
        // Note: supports only top-level keys (no nested paths/arrays).
        // Works for int/float/bool/string.
        // ------------------------------------------------------------

        public static bool TryGetInt(string json, string key, out int value)
        {
            if (TryGetPrimitive(json, key, out var raw))
            {
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    return true;
                // Handle quoted numbers
                if (int.TryParse(TrimQuotes(raw), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    return true;
            }
            value = default;
            return false;
        }

        public static bool TryGetFloat(string json, string key, out float value)
        {
            if (TryGetPrimitive(json, key, out var raw))
            {
                if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    return true;
                if (float.TryParse(TrimQuotes(raw), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    return true;
            }
            value = default;
            return false;
        }

        public static bool TryGetBool(string json, string key, out bool value)
        {
            if (TryGetPrimitive(json, key, out var raw))
            {
                raw = raw.Trim();
                if (raw.Equals("true", StringComparison.OrdinalIgnoreCase)) { value = true; return true; }
                if (raw.Equals("false", StringComparison.OrdinalIgnoreCase)) { value = false; return true; }
                var tq = TrimQuotes(raw);
                if (tq.Equals("true", StringComparison.OrdinalIgnoreCase)) { value = true; return true; }
                if (tq.Equals("false", StringComparison.OrdinalIgnoreCase)) { value = false; return true; }
            }
            value = default;
            return false;
        }

        public static bool TryGetString(string json, string key, out string value)
        {
            if (TryGetPrimitive(json, key, out var raw))
            {
                value = UnescapeJsonString(TrimQuotes(raw));
                return true;
            }
            value = null;
            return false;
        }

        // Simple top-level key scanner: finds `"key" : <value>` (value can be number, bool, "string")
        private static bool TryGetPrimitive(string json, string key, out string rawValue)
        {
            rawValue = null;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return false;

            // Very small/naive scanner for top-level object only.
            // It is tolerant for spaces and commas. Does not parse nested objects/arrays for values.
            try
            {
                int i = 0;
                SkipWs(json, ref i);
                if (i >= json.Length || json[i] != '{') return false;
                i++; // skip '{'

                while (i < json.Length)
                {
                    SkipWs(json, ref i);
                    if (i < json.Length && json[i] == '}') break;

                    // key
                    if (!ReadQuoted(json, ref i, out var k)) return false;
                    SkipWs(json, ref i);
                    if (i >= json.Length || json[i] != ':') return false;
                    i++; // ':'
                    SkipWs(json, ref i);

                    // value (number/bool/string/…)
                    var start = i;
                    if (i < json.Length && json[i] == '"')
                    {
                        if (!ReadQuotedRaw(json, ref i, out var rawString)) return false;
                        if (k == key) { rawValue = rawString; return true; }
                    }
                    else
                    {
                        // read until ',' or '}' (not robust for nested, but fine for top-level primitives)
                        while (i < json.Length && json[i] != ',' && json[i] != '}') i++;
                        var raw = json.Substring(start, i - start).Trim();
                        if (k == key) { rawValue = raw; return true; }
                    }

                    // next
                    SkipWs(json, ref i);
                    if (i < json.Length && json[i] == ',') { i++; continue; }
                    if (i < json.Length && json[i] == '}') break;
                }
            }
            catch { /* ignore */ }

            return false;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s, i)) i++;
        }

        private static bool ReadQuoted(string s, ref int i, out string content)
        {
            content = null;
            if (i >= s.Length || s[i] != '"') return false;
            i++; // skip first quote
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '\\')
                {
                    if (i >= s.Length) break;
                    char esc = s[i++];
                    sb.Append(esc switch
                    {
                        '"' => '"',
                        '\\' => '\\',
                        '/' => '/',
                        'b' => '\b',
                        'f' => '\f',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        'u' => ReadUnicode(s, ref i),
                        _ => esc
                    });
                }
                else if (c == '"') { content = sb.ToString(); return true; }
                else sb.Append(c);
            }
            return false;
        }

        // Like ReadQuoted, but returns the raw `"..."` span including quotes.
        private static bool ReadQuotedRaw(string s, ref int i, out string rawSpan)
        {
            int start = i;
            string dummy;
            if (!ReadQuoted(s, ref i, out dummy)) { rawSpan = null; return false; }
            rawSpan = s.Substring(start, i - start); // includes quotes
            return true;
        }

        private static char ReadUnicode(string s, ref int i)
        {
            int remain = Math.Min(4, s.Length - i);
            string hex = s.Substring(i, remain);
            i += remain;
            if (ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                return (char)code;
            return '?';
        }

        private static string TrimQuotes(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            raw = raw.Trim();
            if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
                return raw.Substring(1, raw.Length - 2);
            return raw;
        }

        private static string UnescapeJsonString(string s)
        {
            // At this point ReadQuoted has already unescaped if it was read as quoted.
            // If coming from TrimQuotes (manual), we unescape the most common sequences.
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        // ------------------------------------------------------------
        // Color & Vector helpers
        // ------------------------------------------------------------

        /// <summary>Parses HTML color (#RGB, #RRGGBB, #RRGGBBAA). Falls back to 'fallback'.</summary>
        public static Color ParseColor(string html, Color fallback)
        {
            if (!string.IsNullOrEmpty(html) && ColorUtility.TryParseHtmlString(html, out var c)) return c;
            return fallback;
        }

        /// <summary>
        /// Parse "r,g,b(,a)" or JSON object with x/y/z/w fields (via JsonUtility).
        /// Examples: "1,0.5,0", or {"x":1,"y":2}, {"x":1,"y":2,"z":3}
        /// </summary>
        public static Vector2 ParseVector2(string value, Vector2 fallback)
        {
            if (TryParseCsv(value, 2, out var v))
                return new Vector2(v[0], v[1]);
            try { return JsonUtility.FromJson<Vector2>(value); } catch { }
            return fallback;
        }

        public static Vector3 ParseVector3(string value, Vector3 fallback)
        {
            if (TryParseCsv(value, 3, out var v))
                return new Vector3(v[0], v[1], v[2]);
            try { return JsonUtility.FromJson<Vector3>(value); } catch { }
            return fallback;
        }

        public static Vector4 ParseVector4(string value, Vector4 fallback)
        {
            if (TryParseCsv(value, 4, out var v))
                return new Vector4(v[0], v[1], v[2], v[3]);
            try { return JsonUtility.FromJson<Vector4>(value); } catch { }
            return fallback;
        }

        private static bool TryParseCsv(string s, int count, out float[] vals)
        {
            vals = null;
            if (string.IsNullOrEmpty(s)) return false;
            var parts = s.Split(',');
            if (parts.Length != count) return false;
            var arr = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out arr[i]))
                    return false;
            }
            vals = arr;
            return true;
        }

        // ------------------------------------------------------------
        // String interpolation
        // ------------------------------------------------------------

        /// <summary>
        /// Fast token interpolation: replaces {key} with values (case-sensitive).
        /// Example: Interpolate("Wait {s} sec", ("s","3")) -> "Wait 3 sec".
        /// </summary>
        public static string Interpolate(string template, params (string key, string value)[] pairs)
        {
            if (string.IsNullOrEmpty(template) || pairs == null || pairs.Length == 0) return template;
            var sb = new StringBuilder(template);
            for (int i = 0; i < pairs.Length; i++)
            {
                var token = "{" + pairs[i].key + "}";
                sb.Replace(token, pairs[i].value ?? string.Empty);
            }
            return sb.ToString();
        }
    }
}
