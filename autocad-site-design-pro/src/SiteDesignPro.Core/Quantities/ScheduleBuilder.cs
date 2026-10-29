using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Quantities
{
    public sealed class ScheduleColumn
    {
        public string Key { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public bool Numeric { get; set; }
    }

    public sealed class ScheduleRow
    {
        public List<string> Cells { get; } = new List<string>();

        /// <summary>Elements aggregated in this row (references back to drawing objects).</summary>
        public List<string> ElementIds { get; } = new List<string>();

        public double Quantity { get; set; }

        public string Unit { get; set; } = string.Empty;
    }

    public sealed class ScheduleTable
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public List<ScheduleColumn> Columns { get; } = new List<ScheduleColumn>();

        public List<ScheduleRow> Rows { get; } = new List<ScheduleRow>();

        /// <summary>Total quantity per unit.</summary>
        public SortedDictionary<string, double> Totals { get; } = new SortedDictionary<string, double>(StringComparer.Ordinal);

        public int ElementCount => Rows.Sum(r => r.ElementIds.Count);

        /// <summary>RFC 4180 CSV. Element ids are appended as the last column so rows remain traceable.</summary>
        public string ToCsv(char separator = ',')
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(separator.ToString(), Columns.Select(c => Escape(c.Title, separator)).Append(Escape("Element IDs", separator))));
            foreach (ScheduleRow r in Rows)
            {
                sb.AppendLine(string.Join(separator.ToString(), r.Cells.Select(c => Escape(c, separator)).Append(Escape(string.Join(" ", r.ElementIds), separator))));
            }

            foreach (KeyValuePair<string, double> t in Totals)
            {
                var cells = Columns.Select(c => c.Key switch
                {
                    "label" => "Total",
                    "quantity" => Format(t.Value, 3),
                    "unit" => t.Key,
                    _ => string.Empty,
                });
                sb.AppendLine(string.Join(separator.ToString(), cells.Select(c => Escape(c, separator)).Append(string.Empty)));
            }

            return sb.ToString();
        }

        public JsonObject ToJson()
        {
            return new JsonObject
            {
                ["id"] = Id,
                ["title"] = Title,
                ["columns"] = new JsonArray(Columns.Select(c => (JsonNode)new JsonObject { ["key"] = c.Key, ["title"] = c.Title }).ToArray()),
                ["rows"] = new JsonArray(Rows.Select(r => (JsonNode)new JsonObject
                {
                    ["cells"] = new JsonArray(r.Cells.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray()),
                    ["quantity"] = r.Quantity,
                    ["unit"] = r.Unit,
                    ["elementIds"] = new JsonArray(r.ElementIds.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray()),
                }).ToArray()),
                ["totals"] = new JsonObject(Totals.Select(t => new KeyValuePair<string, JsonNode?>(t.Key, t.Value))),
            };
        }

        internal static string Format(double v, int decimals) => v.ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);

        private static string Escape(string s, char sep)
        {
            if (s.IndexOfAny(new[] { sep, '"', '\n', '\r' }) >= 0)
            {
                return "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
            }

            return s;
        }
    }

    /// <summary>Builds grouped quantity schedules from elements in the package frame (metres).</summary>
    public sealed class ScheduleBuilder
    {
        private readonly ElementCatalog catalog;

        public ScheduleBuilder(ElementCatalog catalog)
        {
            this.catalog = catalog;
        }

        /// <summary>Schedules that contain at least one element.</summary>
        public IEnumerable<ScheduleDefinition> Applicable(IEnumerable<SiteElement> elements)
        {
            var used = new HashSet<string>(elements.Select(e => catalog.FindType(e.TypeId)?.ScheduleId ?? string.Empty), StringComparer.Ordinal);
            return catalog.Schedules.Where(s => used.Contains(s.Id));
        }

        public ScheduleTable Build(ScheduleDefinition def, IEnumerable<SiteElement> elements, int decimals = 2)
        {
            var table = new ScheduleTable { Id = def.Id, Title = def.Title };
            foreach (string key in def.Columns)
            {
                table.Columns.Add(new ScheduleColumn { Key = key, Title = ColumnTitle(key), Numeric = key == "quantity" || key == "secondary" });
            }

            var members = elements
                .Select(e => (E: e, T: catalog.FindType(e.TypeId)))
                .Where(x => x.T != null && x.T.ScheduleId == def.Id)
                .Select(x => (x.E, T: x.T!))
                .ToList();

            var groups = members
                .GroupBy(x => string.Join("\u001f", def.GroupBy.Select(g => Value(g, x.E, x.T))), StringComparer.Ordinal)
                .OrderBy(g => g.First().T.Name, StringComparer.Ordinal)
                .ThenBy(g => g.Key, StringComparer.Ordinal);

            foreach (IGrouping<string, (SiteElement E, ElementType T)> g in groups)
            {
                var first = g.First();
                var row = new ScheduleRow();
                double quantity = 0, secondary = 0;
                bool hasSecondary = false, approx = false;
                string unit = string.Empty, secondaryUnit = string.Empty;
                foreach ((SiteElement e, ElementType t) in g)
                {
                    ElementQuantity q = QuantityCalculator.For(e, t);
                    quantity += q.Value;
                    unit = q.Unit;
                    if (q.Secondary.HasValue)
                    {
                        hasSecondary = true;
                        secondary += q.Secondary.Value;
                        secondaryUnit = q.SecondaryUnit ?? string.Empty;
                        approx |= q.SecondaryApproximate;
                    }

                    row.ElementIds.Add(e.Id);
                }

                row.Quantity = quantity;
                row.Unit = unit;
                foreach (string key in def.Columns)
                {
                    switch (key)
                    {
                        case "label":
                            row.Cells.Add(CompactLabels(g.Select(x => x.E.Label).Where(l => !string.IsNullOrEmpty(l)).Select(l => l!)));
                            break;
                        case "quantity":
                            row.Cells.Add(ScheduleTable.Format(quantity, unit == "pcs" ? 0 : decimals));
                            break;
                        case "unit":
                            row.Cells.Add(unit);
                            break;
                        case "secondary":
                            row.Cells.Add(hasSecondary ? (approx ? "~" : string.Empty) + ScheduleTable.Format(secondary, decimals) + " " + secondaryUnit : string.Empty);
                            break;
                        case "notes":
                            row.Cells.Add(string.Join("; ", g.Select(x => x.E.Property("notes")).Where(n => n.Length > 0).Distinct(StringComparer.Ordinal)));
                            break;
                        default:
                            row.Cells.Add(Value(key, first.E, first.T));
                            break;
                    }
                }

                table.Rows.Add(row);
                table.Totals[unit] = table.Totals.TryGetValue(unit, out double tot) ? tot + quantity : quantity;
            }

            return table;
        }

        private string Value(string key, SiteElement e, ElementType t)
        {
            if (key == "type")
            {
                return t.Name;
            }

            if (key == "category")
            {
                return catalog.FindCategory(t.Category)?.Name ?? t.Category;
            }

            if (key.StartsWith("prop:", StringComparison.Ordinal))
            {
                return e.Property(key.Substring(5));
            }

            if (key.StartsWith("param:", StringComparison.Ordinal))
            {
                string k = key.Substring(6);
                ParameterDefinition? def = t.Parameter(k);
                if (def == null)
                {
                    return string.Empty;
                }

                ParamValue v = e.Parameters.Contains(k) ? e.Parameters[k] : def.Default;
                if (v.IsNull)
                {
                    return string.Empty;
                }

                if (def.Type == ParameterType.Material)
                {
                    return catalog.FindMaterial(v.AsText())?.Name ?? v.AsText();
                }

                return ElementCatalog.FormatValue(def, v, 3);
            }

            return string.Empty;
        }

        private static string ColumnTitle(string key)
        {
            switch (key)
            {
                case "label": return "No.";
                case "type": return "Type";
                case "category": return "Category";
                case "quantity": return "Quantity";
                case "unit": return "Unit";
                case "secondary": return "Secondary quantity";
                case "notes": return "Notes";
            }

            string k = key.Contains(':', StringComparison.Ordinal) ? key.Substring(key.IndexOf(':', StringComparison.Ordinal) + 1) : key;
            var sb = new StringBuilder();
            foreach (char c in k)
            {
                if (char.IsUpper(c) && sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(sb.Length == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }

        /// <summary>Joins labels, compressing consecutive numbers with a common prefix: T-001..T-004, T-007.</summary>
        public static string CompactLabels(IEnumerable<string> labels)
        {
            var parsed = labels.Distinct(StringComparer.Ordinal).Select(l =>
            {
                int i = l.Length;
                while (i > 0 && char.IsDigit(l[i - 1]))
                {
                    i--;
                }

                bool ok = i < l.Length && int.TryParse(l.Substring(i), NumberStyles.None, CultureInfo.InvariantCulture, out int n);
                return (Label: l, Prefix: l.Substring(0, i), Number: ok ? int.Parse(l.Substring(i), CultureInfo.InvariantCulture) : -1, Width: l.Length - i);
            }).OrderBy(x => x.Prefix, StringComparer.Ordinal).ThenBy(x => x.Number).ToList();

            var parts = new List<string>();
            int idx = 0;
            while (idx < parsed.Count)
            {
                var start = parsed[idx];
                int end = idx;
                while (start.Number >= 0 && end + 1 < parsed.Count && parsed[end + 1].Prefix == start.Prefix && parsed[end + 1].Number == parsed[end].Number + 1)
                {
                    end++;
                }

                parts.Add(end - idx >= 2 ? start.Label + ".." + parsed[end].Label : string.Join(", ", parsed.Skip(idx).Take(end - idx + 1).Select(x => x.Label)));
                idx = end + 1;
            }

            return string.Join(", ", parts);
        }
    }
}
