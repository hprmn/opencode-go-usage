using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenCodeUsage
{
    internal enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object
    }

    /// <summary>Minimal, dependency-free JSON tree.</summary>
    internal sealed class JsonNode
    {
        public JsonKind Kind;
        public bool BoolValue;
        public double NumberValue;
        public string StringValue;
        public List<JsonNode> Items;
        public List<string> Keys;
        public Dictionary<string, JsonNode> Props;

        public static JsonNode NewNull()
        {
            JsonNode n = new JsonNode();
            n.Kind = JsonKind.Null;
            return n;
        }

        public static JsonNode NewBool(bool v)
        {
            JsonNode n = new JsonNode();
            n.Kind = JsonKind.Bool;
            n.BoolValue = v;
            return n;
        }

        public static JsonNode NewNumber(double v)
        {
            JsonNode n = new JsonNode();
            n.Kind = JsonKind.Number;
            n.NumberValue = v;
            return n;
        }

        public static JsonNode NewString(string v)
        {
            JsonNode n = new JsonNode();
            n.Kind = JsonKind.String;
            n.StringValue = v;
            return n;
        }

        public static JsonNode NewArray()
        {
            JsonNode n = new JsonNode();
            n.Kind = JsonKind.Array;
            n.Items = new List<JsonNode>();
            return n;
        }

        public static JsonNode NewObject()
        {
            JsonNode n = new JsonNode();
            n.Kind = JsonKind.Object;
            n.Keys = new List<string>();
            n.Props = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
            return n;
        }

        public bool IsObject { get { return Kind == JsonKind.Object; } }
        public bool IsArray { get { return Kind == JsonKind.Array; } }
        public bool IsNumber { get { return Kind == JsonKind.Number; } }
        public bool IsString { get { return Kind == JsonKind.String; } }
        public bool IsScalar { get { return Kind == JsonKind.String || Kind == JsonKind.Number || Kind == JsonKind.Bool; } }

        public void Add(string key, JsonNode value)
        {
            if (Props.ContainsKey(key))
            {
                Props[key] = value;
                return;
            }
            Keys.Add(key);
            Props[key] = value;
        }

        public JsonNode Prop(string key)
        {
            if (Kind != JsonKind.Object || key == null) return null;
            JsonNode n;
            if (Props.TryGetValue(key, out n)) return n;
            return null;
        }
    }

    /// <summary>Recursive descent JSON reader. Accepts anything a JSON API throws at us.</summary>
    internal sealed class JsonReader
    {
        private readonly string _s;
        private int _i;

        private JsonReader(string text)
        {
            _s = text ?? "";
            _i = 0;
        }

        public static JsonNode Parse(string text)
        {
            JsonReader r = new JsonReader(text);
            if (!string.IsNullOrEmpty(r._s) && r._s[0] == '\uFEFF') r._i = 1;
            r.SkipWhite();
            JsonNode n = r.ReadValue(0);
            r.SkipWhite();
            return n;
        }

        private void SkipWhite()
        {
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { _i++; continue; }
                break;
            }
        }

        private FormatException Fail(string what)
        {
            return new FormatException("JSON tidak valid pada posisi " + _i + ": " + what);
        }

        private JsonNode ReadValue(int depth)
        {
            if (depth > 64) throw Fail("objek terlalu dalam");
            SkipWhite();
            if (_i >= _s.Length) throw Fail("data terpotong");
            char c = _s[_i];
            if (c == '{') return ReadObject(depth);
            if (c == '[') return ReadArray(depth);
            if (c == '"') return JsonNode.NewString(ReadString());
            if (c == 't')
            {
                Expect("true");
                return JsonNode.NewBool(true);
            }
            if (c == 'f')
            {
                Expect("false");
                return JsonNode.NewBool(false);
            }
            if (c == 'n')
            {
                Expect("null");
                return JsonNode.NewNull();
            }
            if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
            throw Fail("karakter tak terduga '" + c + "'");
        }

        private void Expect(string token)
        {
            if (_i + token.Length > _s.Length) throw Fail("data terpotong");
            if (string.CompareOrdinal(_s, _i, token, 0, token.Length) != 0) throw Fail("diharapkan '" + token + "'");
            _i += token.Length;
        }

        private JsonNode ReadObject(int depth)
        {
            JsonNode obj = JsonNode.NewObject();
            _i++; // {
            SkipWhite();
            if (_i < _s.Length && _s[_i] == '}') { _i++; return obj; }
            while (true)
            {
                SkipWhite();
                if (_i >= _s.Length || _s[_i] != '"') throw Fail("diharapkan nama properti");
                string key = ReadString();
                SkipWhite();
                if (_i >= _s.Length || _s[_i] != ':') throw Fail("diharapkan ':'");
                _i++;
                JsonNode val = ReadValue(depth + 1);
                obj.Add(key, val);
                SkipWhite();
                if (_i >= _s.Length) throw Fail("objek tidak ditutup");
                char c = _s[_i];
                if (c == ',') { _i++; continue; }
                if (c == '}') { _i++; return obj; }
                throw Fail("diharapkan ',' atau '}'");
            }
        }

        private JsonNode ReadArray(int depth)
        {
            JsonNode arr = JsonNode.NewArray();
            _i++; // [
            SkipWhite();
            if (_i < _s.Length && _s[_i] == ']') { _i++; return arr; }
            while (true)
            {
                JsonNode val = ReadValue(depth + 1);
                arr.Items.Add(val);
                SkipWhite();
                if (_i >= _s.Length) throw Fail("array tidak ditutup");
                char c = _s[_i];
                if (c == ',') { _i++; continue; }
                if (c == ']') { _i++; return arr; }
                throw Fail("diharapkan ',' atau ']'");
            }
        }

        private string ReadString()
        {
            _i++; // opening quote
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                if (_i >= _s.Length) throw Fail("string tidak ditutup");
                char c = _s[_i++];
                if (c == '"') return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (_i >= _s.Length) throw Fail("escape terpotong");
                char e = _s[_i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (_i + 4 > _s.Length) throw Fail("escape unicode terpotong");
                        int code;
                        if (!int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            throw Fail("escape unicode tidak valid");
                        sb.Append((char)code);
                        _i += 4;
                        break;
                    default:
                        throw Fail("escape tidak dikenal");
                }
            }
        }

        private JsonNode ReadNumber()
        {
            int start = _i;
            if (_i < _s.Length && (_s[_i] == '-' || _s[_i] == '+')) _i++;
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') { _i++; continue; }
                break;
            }
            string raw = _s.Substring(start, _i - start);
            double d;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw Fail("angka tidak valid '" + raw + "'");
            return JsonNode.NewNumber(d);
        }
    }

    internal static class Json
    {
        public static string Pretty(JsonNode n)
        {
            StringBuilder sb = new StringBuilder();
            Write(sb, n, 0);
            return sb.ToString();
        }

        public static string PrettyOrRaw(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            try
            {
                return Pretty(JsonReader.Parse(raw));
            }
            catch
            {
                return raw;
            }
        }

        private static void Indent(StringBuilder sb, int level)
        {
            sb.Append(new string(' ', level * 2));
        }

        private static void Write(StringBuilder sb, JsonNode n, int level)
        {
            if (n == null) { sb.Append("null"); return; }
            switch (n.Kind)
            {
                case JsonKind.Null:
                    sb.Append("null");
                    break;
                case JsonKind.Bool:
                    sb.Append(n.BoolValue ? "true" : "false");
                    break;
                case JsonKind.Number:
                    sb.Append(NumberText(n.NumberValue));
                    break;
                case JsonKind.String:
                    WriteString(sb, n.StringValue);
                    break;
                case JsonKind.Array:
                    if (n.Items.Count == 0) { sb.Append("[]"); break; }
                    sb.Append("[\n");
                    for (int i = 0; i < n.Items.Count; i++)
                    {
                        Indent(sb, level + 1);
                        Write(sb, n.Items[i], level + 1);
                        if (i < n.Items.Count - 1) sb.Append(',');
                        sb.Append('\n');
                    }
                    Indent(sb, level);
                    sb.Append(']');
                    break;
                case JsonKind.Object:
                    if (n.Keys.Count == 0) { sb.Append("{}"); break; }
                    sb.Append("{\n");
                    for (int i = 0; i < n.Keys.Count; i++)
                    {
                        Indent(sb, level + 1);
                        WriteString(sb, n.Keys[i]);
                        sb.Append(": ");
                        Write(sb, n.Props[n.Keys[i]], level + 1);
                        if (i < n.Keys.Count - 1) sb.Append(',');
                        sb.Append('\n');
                    }
                    Indent(sb, level);
                    sb.Append('}');
                    break;
            }
        }

        private static string NumberText(double d)
        {
            if (d == Math.Floor(d) && Math.Abs(d) < 1e15) return ((long)d).ToString(CultureInfo.InvariantCulture);
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        default:
                            if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else sb.Append(c);
                            break;
                    }
                }
            }
            sb.Append('"');
        }
    }

    internal static class Jx
    {
        public static JsonNode Prop(JsonNode obj, string name)
        {
            if (obj == null || obj.Kind != JsonKind.Object || name == null) return null;
            JsonNode n;
            if (obj.Props.TryGetValue(name, out n)) return n;
            for (int i = 0; i < obj.Keys.Count; i++)
            {
                if (string.Equals(obj.Keys[i], name, StringComparison.OrdinalIgnoreCase))
                    return obj.Props[obj.Keys[i]];
            }
            return null;
        }

        public static JsonNode PropAny(JsonNode obj, params string[] names)
        {
            if (obj == null || names == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                JsonNode n = Prop(obj, names[i]);
                if (n != null) return n;
            }
            return null;
        }

        public static string Scalar(JsonNode n)
        {
            if (n == null) return null;
            switch (n.Kind)
            {
                case JsonKind.String:
                    return n.StringValue;
                case JsonKind.Number:
                    return n.NumberValue == Math.Floor(n.NumberValue)
                        ? ((long)n.NumberValue).ToString(CultureInfo.InvariantCulture)
                        : n.NumberValue.ToString("0.##", CultureInfo.InvariantCulture);
                case JsonKind.Bool:
                    return n.BoolValue ? "true" : "false";
                default:
                    return null;
            }
        }

        public static double? Number(JsonNode n)
        {
            if (n == null) return null;
            if (n.Kind == JsonKind.Number) return n.NumberValue;
            if (n.Kind == JsonKind.String)
            {
                double d;
                if (double.TryParse(n.StringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            }
            return null;
        }

        public static DateTimeOffset? Date(JsonNode n)
        {
            if (n == null) return null;
            if (n.Kind == JsonKind.Number)
            {
                double v = n.NumberValue;
                if (v > 1e12) return DateTimeOffset.FromUnixTimeMilliseconds((long)v).ToLocalTime();
                if (v > 1e9) return DateTimeOffset.FromUnixTimeSeconds((long)v).ToLocalTime();
                return null;
            }
            if (n.Kind != JsonKind.String) return null;
            DateTimeOffset dto;
            string s = n.StringValue;
            if (string.IsNullOrEmpty(s)) return null;
            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dto))
                return dto.ToLocalTime();
            if (DateTimeOffset.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dto))
                return dto.ToLocalTime();
            return null;
        }

        /// <summary>Depth-first walk over every node in the tree.</summary>
        public static IEnumerable<JsonNode> Walk(JsonNode root)
        {
            if (root == null) yield break;
            Stack<JsonNode> stack = new Stack<JsonNode>();
            stack.Push(root);
            int guard = 0;
            while (stack.Count > 0 && guard++ < 20000)
            {
                JsonNode n = stack.Pop();
                yield return n;
                if (n.Kind == JsonKind.Array && n.Items != null)
                {
                    for (int i = n.Items.Count - 1; i >= 0; i--) stack.Push(n.Items[i]);
                }
                else if (n.Kind == JsonKind.Object && n.Keys != null)
                {
                    for (int i = n.Keys.Count - 1; i >= 0; i--) stack.Push(n.Props[n.Keys[i]]);
                }
            }
        }
    }
}
