using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Nakama.TinyJson;
using UnityEngine;

namespace BiomeRivals.Networking
{
    // JsonUtility materializes null reference fields/array entries as default objects.
    // Restore only explicit wire nulls, never infer hidden values from the viewer.
    internal static class MatchWireJson
    {
        private const int MaximumCharacters = 262144;
        private const int MaximumDepth = 32;

        public static T Deserialize<T>(string json) where T : class
        {
            ValidateEnvelope(json);
            var wire = json.FromJson<object>() as IDictionary<string, object>;
            if (wire == null) throw new FormatException("Match JSON must contain an object envelope.");
            var decoded = JsonUtility.FromJson<T>(json);
            if (decoded == null) throw new FormatException("Match JSON could not be decoded.");
            RestoreNulls(decoded, wire);
            return decoded;
        }

        private static void RestoreNulls(object decoded, object wire)
        {
            if (decoded == null || decoded is string || decoded.GetType().IsValueType) return;
            if (decoded is Array array && wire is IList entries)
            {
                if (array.Length != entries.Count) throw new FormatException("Match JSON array lengths disagree.");
                var elementType = array.GetType().GetElementType();
                for (var index = 0; index < array.Length; index++)
                {
                    if (entries[index] == null && !elementType.IsValueType) array.SetValue(null, index);
                    else RestoreNulls(array.GetValue(index), entries[index]);
                }
                return;
            }
            if (!(wire is IDictionary<string, object> fields)) return;
            foreach (var field in decoded.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!fields.TryGetValue(field.Name, out var wireValue)) continue;
                if (wireValue == null && !field.FieldType.IsValueType) field.SetValue(decoded, null);
                else RestoreNulls(field.GetValue(decoded), wireValue);
            }
        }

        private static void ValidateEnvelope(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumCharacters)
                throw new FormatException("Match JSON is empty or exceeds the message size limit.");
            var brackets = new char[MaximumDepth];
            var depth = 0;
            var inString = false;
            var escaped = false;
            foreach (var character in json)
            {
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (character == '\\') escaped = true;
                    else if (character == '"') inString = false;
                    continue;
                }
                if (character == '"') { inString = true; continue; }
                if (character == '{' || character == '[')
                {
                    if (depth == MaximumDepth) throw new FormatException("Match JSON exceeds the nesting limit.");
                    brackets[depth++] = character;
                }
                else if (character == '}' || character == ']')
                {
                    if (depth == 0 || brackets[--depth] != (character == '}' ? '{' : '['))
                        throw new FormatException("Match JSON contains mismatched brackets.");
                }
            }
            if (inString || depth != 0) throw new FormatException("Match JSON is incomplete.");
        }
    }
}
