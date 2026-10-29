using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SiteDesignPro.Core.Model
{
    public enum ParamKind
    {
        Null,
        Number,
        Text,
        Bool,
    }

    /// <summary>A scalar parameter value (number, text, boolean or null).</summary>
    public readonly struct ParamValue : IEquatable<ParamValue>
    {
        private readonly double number;
        private readonly string? text;
        private readonly bool flag;

        private ParamValue(ParamKind kind, double number, string? text, bool flag)
        {
            Kind = kind;
            this.number = number;
            this.text = text;
            this.flag = flag;
        }

        public static ParamValue Null => default;

        public ParamKind Kind { get; }

        public bool IsNull => Kind == ParamKind.Null;

        public static ParamValue Of(double value) => new ParamValue(ParamKind.Number, value, null, false);

        public static ParamValue Of(string? value) => value == null ? Null : new ParamValue(ParamKind.Text, 0, value, false);

        public static ParamValue Of(bool value) => new ParamValue(ParamKind.Bool, 0, null, value);

        public double AsNumber(double fallback = double.NaN)
        {
            switch (Kind)
            {
                case ParamKind.Number: return number;
                case ParamKind.Bool: return flag ? 1 : 0;
                case ParamKind.Text:
                    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : fallback;
                default: return fallback;
            }
        }

        public string AsText()
        {
            switch (Kind)
            {
                case ParamKind.Number: return number.ToString("R", CultureInfo.InvariantCulture);
                case ParamKind.Bool: return flag ? "true" : "false";
                case ParamKind.Text: return text ?? string.Empty;
                default: return string.Empty;
            }
        }

        public bool AsBool(bool fallback = false)
        {
            switch (Kind)
            {
                case ParamKind.Bool: return flag;
                case ParamKind.Number: return number != 0;
                case ParamKind.Text:
                    if (bool.TryParse(text, out bool b))
                    {
                        return b;
                    }

                    return text == "1" || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) || fallback;
                default: return fallback;
            }
        }

        public JsonNode? ToJson() => Kind switch
        {
            ParamKind.Number => JsonValue.Create(number),
            ParamKind.Text => JsonValue.Create(text),
            ParamKind.Bool => JsonValue.Create(flag),
            _ => null,
        };

        public static ParamValue FromJson(JsonNode? node)
        {
            if (node == null)
            {
                return Null;
            }

            return FromJson(JsonSerializer.SerializeToElement(node));
        }

        public static ParamValue FromJson(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Number => Of(e.GetDouble()),
            JsonValueKind.String => Of(e.GetString()),
            JsonValueKind.True => Of(true),
            JsonValueKind.False => Of(false),
            JsonValueKind.Null => Null,
            JsonValueKind.Undefined => Null,
            _ => throw new FormatException("Parameter values must be scalars (number, string, boolean or null)."),
        };

        public bool Equals(ParamValue other)
        {
            if (Kind != other.Kind)
            {
                return false;
            }

            return Kind switch
            {
                ParamKind.Number => number.Equals(other.number),
                ParamKind.Text => string.Equals(text, other.text, StringComparison.Ordinal),
                ParamKind.Bool => flag == other.flag,
                _ => true,
            };
        }

        public override bool Equals(object? obj) => obj is ParamValue p && Equals(p);

        public override int GetHashCode() => Compat.Hash(Kind, number, text, flag);

        public override string ToString() => IsNull ? "<null>" : AsText();

        public static bool operator ==(ParamValue a, ParamValue b) => a.Equals(b);

        public static bool operator !=(ParamValue a, ParamValue b) => !a.Equals(b);
    }

    /// <summary>Ordered (by key, ordinal) set of named scalar values.</summary>
    public sealed class ParameterSet : IEnumerable<KeyValuePair<string, ParamValue>>
    {
        private readonly SortedDictionary<string, ParamValue> values = new SortedDictionary<string, ParamValue>(StringComparer.Ordinal);

        public ParameterSet()
        {
        }

        public ParameterSet(IEnumerable<KeyValuePair<string, ParamValue>> source)
        {
            foreach (KeyValuePair<string, ParamValue> kv in source)
            {
                values[kv.Key] = kv.Value;
            }
        }

        public int Count => values.Count;

        public IEnumerable<string> Keys => values.Keys;

        public ParamValue this[string key]
        {
            get => values.TryGetValue(key, out ParamValue v) ? v : ParamValue.Null;
            set => values[key] = value;
        }

        public bool Contains(string key) => values.ContainsKey(key);

        public bool Remove(string key) => values.Remove(key);

        public double Number(string key, double fallback = double.NaN)
        {
            ParamValue v = this[key];
            return v.IsNull ? fallback : v.AsNumber(fallback);
        }

        public string Text(string key, string fallback = "")
        {
            ParamValue v = this[key];
            return v.IsNull ? fallback : v.AsText();
        }

        public bool Bool(string key, bool fallback = false)
        {
            ParamValue v = this[key];
            return v.IsNull ? fallback : v.AsBool(fallback);
        }

        public ParameterSet Clone() => new ParameterSet(values);

        public JsonObject ToJson()
        {
            var o = new JsonObject();
            foreach (KeyValuePair<string, ParamValue> kv in values)
            {
                o[kv.Key] = kv.Value.ToJson();
            }

            return o;
        }

        public static ParameterSet FromJson(JsonElement obj)
        {
            var set = new ParameterSet();
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return set;
            }

            foreach (JsonProperty p in obj.EnumerateObject())
            {
                set[p.Name] = ParamValue.FromJson(p.Value);
            }

            return set;
        }

        public IEnumerator<KeyValuePair<string, ParamValue>> GetEnumerator() => values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
