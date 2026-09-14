using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BiomeRivals.Demo
{
    /// <summary>
    /// Minimal dependency-free JSON parser used to read Minecraft entity geometry
    /// files. Produces Dictionary&lt;string, object&gt; / List&lt;object&gt; / string /
    /// double / bool / null trees so that dynamic keys such as
    /// "minecraft:geometry" or legacy "geometry.&lt;name&gt;" roots can be read
    /// without a hard schema.
    /// </summary>
    public static class DemoJsonParser
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("JSON document is empty.", nameof(json));
            var index = 0;
            var value = ParseValue(json, ref index);
            SkipWhitespace(json, ref index);
            if (index != json.Length) throw new FormatException($"Trailing characters after JSON value at {index}.");
            return value;
        }

        public static Dictionary<string, object> ParseObject(string json) =>
            Parse(json) as Dictionary<string, object> ?? throw new FormatException("JSON document root is not an object.");

        private static object ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) throw new FormatException("Unexpected end of JSON document.");
            var character = json[index];
            switch (character)
            {
                case '{': return ParseObjectValue(json, ref index);
                case '[': return ParseArrayValue(json, ref index);
                case '"': return ParseString(json, ref index);
                case 't': ExpectLiteral(json, ref index, "true"); return true;
                case 'f': ExpectLiteral(json, ref index, "false"); return false;
                case 'n': ExpectLiteral(json, ref index, "null"); return null;
                default: return ParseNumber(json, ref index);
            }
        }

        private static Dictionary<string, object> ParseObjectValue(string json, ref int index)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            index++; // {
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == '}')
            {
                index++;
                return result;
            }
            while (true)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != '"') throw new FormatException($"Expected object key at {index}.");
                var key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ':') throw new FormatException($"Expected ':' after key at {index}.");
                index++;
                result[key] = ParseValue(json, ref index);
                SkipWhitespace(json, ref index);
                if (index >= json.Length) throw new FormatException("Unterminated JSON object.");
                if (json[index] == ',') { index++; continue; }
                if (json[index] == '}') { index++; return result; }
                throw new FormatException($"Expected ',' or '}}' at {index}.");
            }
        }

        private static List<object> ParseArrayValue(string json, ref int index)
        {
            var result = new List<object>();
            index++; // [
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == ']')
            {
                index++;
                return result;
            }
            while (true)
            {
                result.Add(ParseValue(json, ref index));
                SkipWhitespace(json, ref index);
                if (index >= json.Length) throw new FormatException("Unterminated JSON array.");
                if (json[index] == ',') { index++; continue; }
                if (json[index] == ']') { index++; return result; }
                throw new FormatException($"Expected ',' or ']' at {index}.");
            }
        }

        private static string ParseString(string json, ref int index)
        {
            index++; // opening quote
            var builder = new StringBuilder();
            while (index < json.Length)
            {
                var character = json[index++];
                if (character == '"') return builder.ToString();
                if (character != '\\')
                {
                    builder.Append(character);
                    continue;
                }
                if (index >= json.Length) throw new FormatException("Unterminated escape sequence.");
                var escape = json[index++];
                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > json.Length) throw new FormatException("Incomplete unicode escape.");
                        builder.Append((char)int.Parse(json.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        index += 4;
                        break;
                    default: throw new FormatException($"Unsupported escape sequence '\\{escape}'.");
                }
            }
            throw new FormatException("Unterminated JSON string.");
        }

        private static double ParseNumber(string json, ref int index)
        {
            var start = index;
            while (index < json.Length && "-+.eE0123456789".IndexOf(json[index], StringComparison.Ordinal) >= 0) index++;
            if (start == index) throw new FormatException($"Invalid JSON value at {start}.");
            return double.Parse(json.Substring(start, index - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static void ExpectLiteral(string json, ref int index, string literal)
        {
            if (index + literal.Length > json.Length || string.CompareOrdinal(json, index, literal, 0, literal.Length) != 0)
                throw new FormatException($"Invalid JSON literal at {index}.");
            index += literal.Length;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && (json[index] == ' ' || json[index] == '\t' || json[index] == '\n' || json[index] == '\r')) index++;
        }
    }
}
