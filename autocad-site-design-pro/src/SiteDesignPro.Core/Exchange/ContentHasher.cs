using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Exchange
{
    /// <summary>
    /// Computes a deterministic SHA-256 hash of an element's exchange content (type, data and geometry).
    /// Object keys are sorted ordinally and numbers are rounded, so the hash only changes when content changes.
    /// The receiving application stores the hash and compares it on re-import to detect changed elements.
    /// </summary>
    public static class ContentHasher
    {
        public static string Hash(SiteElement element)
        {
            var root = new JsonObject
            {
                ["custom"] = element.Custom.ToJson(),
                ["geometry"] = GeometryJson.ToJson(element.Id, element.Geometry),
                ["label"] = element.Label ?? string.Empty,
                ["name"] = element.Name,
                ["parameters"] = element.Parameters.ToJson(),
                ["properties"] = new JsonObject(element.Properties.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, kv.Value))),
                ["asset"] = element.AssetId ?? string.Empty,
                ["type"] = element.TypeId,
            };
            return Sha256Hex(Canonical(root));
        }

        public static string Sha256Hex(string text)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        public static string Sha256Hex(byte[] data)
        {
            byte[] bytes = SHA256.HashData(data);
            return string.Concat(bytes.Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
        }

        /// <summary>Canonical JSON text: sorted keys, no whitespace, rounded invariant numbers.</summary>
        public static string Canonical(JsonNode? node)
        {
            var sb = new StringBuilder();
            Write(node, sb);
            return sb.ToString();
        }

        private static void Write(JsonNode? node, StringBuilder sb)
        {
            switch (node)
            {
                case null:
                    sb.Append("null");
                    break;
                case JsonObject o:
                    sb.Append('{');
                    bool first = true;
                    foreach (KeyValuePair<string, JsonNode?> kv in o.OrderBy(k => k.Key, StringComparer.Ordinal))
                    {
                        if (!first)
                        {
                            sb.Append(',');
                        }

                        first = false;
                        sb.Append(JsonSerializer.Serialize(kv.Key)).Append(':');
                        Write(kv.Value, sb);
                    }

                    sb.Append('}');
                    break;
                case JsonArray a:
                    sb.Append('[');
                    for (int i = 0; i < a.Count; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append(',');
                        }

                        Write(a[i], sb);
                    }

                    sb.Append(']');
                    break;
                case JsonValue v:
                    JsonElement e = JsonSerializer.SerializeToElement(v);
                    switch (e.ValueKind)
                    {
                        case JsonValueKind.Number:
                            sb.Append(GeometryJson.NumberText(e.GetDouble()));
                            break;
                        case JsonValueKind.String:
                            sb.Append(JsonSerializer.Serialize(e.GetString()));
                            break;
                        case JsonValueKind.True:
                            sb.Append("true");
                            break;
                        case JsonValueKind.False:
                            sb.Append("false");
                            break;
                        default:
                            sb.Append("null");
                            break;
                    }

                    break;
            }
        }
    }
}
