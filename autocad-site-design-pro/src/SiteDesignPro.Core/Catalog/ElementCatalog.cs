using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Catalog
{
    /// <summary>
    /// The element library: builders, element types, categories, model groups, schedules, layers and materials.
    /// Loaded from the shared catalog (embedded or from a directory) plus optional user catalog files that add or
    /// override element types, layers and materials without code changes.
    /// </summary>
    public sealed class ElementCatalog
    {
        public const string CoreFiles = "builders.json, element-types.json, categories.json, layers.json, materials.json";

        private readonly Dictionary<string, BuilderDefinition> builders = new Dictionary<string, BuilderDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ElementType> types = new Dictionary<string, ElementType>(StringComparer.Ordinal);
        private readonly List<string> typeOrder = new List<string>();
        private readonly Dictionary<string, LayerDefinition> layers = new Dictionary<string, LayerDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, MaterialDefinition> materials = new Dictionary<string, MaterialDefinition>(StringComparer.Ordinal);
        private readonly List<string> loadMessages = new List<string>();

        private ElementCatalog()
        {
        }

        public string CatalogId { get; private set; } = "sdp-core";

        public string Version { get; private set; } = "0";

        public string MaterialsVersion { get; private set; } = "0";

        public IReadOnlyCollection<BuilderDefinition> Builders => builders.Values;

        public IEnumerable<ElementType> Types => typeOrder.Select(id => types[id]);

        public IReadOnlyList<CategoryDefinition> Categories { get; private set; } = Array.Empty<CategoryDefinition>();

        public IReadOnlyList<ModelGroupDefinition> ModelGroups { get; private set; } = Array.Empty<ModelGroupDefinition>();

        public IReadOnlyList<ScheduleDefinition> Schedules { get; private set; } = Array.Empty<ScheduleDefinition>();

        public IReadOnlyList<PropertyDefinition> CommonProperties { get; private set; } = Array.Empty<PropertyDefinition>();

        public IEnumerable<LayerDefinition> Layers => layers.Values;

        public IEnumerable<MaterialDefinition> Materials => materials.Values;

        /// <summary>Informational messages produced while loading user catalogs.</summary>
        public IReadOnlyList<string> LoadMessages => loadMessages;

        public ElementType? FindType(string id) => types.TryGetValue(id, out ElementType? t) ? t : null;

        public ElementType RequireType(string id) => FindType(id) ?? throw new KeyNotFoundException("Unknown element type '" + id + "'.");

        public BuilderDefinition? FindBuilder(string id) => builders.TryGetValue(id, out BuilderDefinition? b) ? b : null;

        public LayerDefinition? FindLayer(string key) => layers.TryGetValue(key, out LayerDefinition? l) ? l : null;

        public MaterialDefinition? FindMaterial(string key) => materials.TryGetValue(key, out MaterialDefinition? m) ? m : null;

        public CategoryDefinition? FindCategory(string id) => Categories.FirstOrDefault(c => c.Id == id);

        public ScheduleDefinition? FindSchedule(string id) => Schedules.FirstOrDefault(s => s.Id == id);

        public ModelGroupDefinition? FindModelGroup(string id) => ModelGroups.FirstOrDefault(g => g.Id == id);

        /// <summary>Loads the catalog embedded in this assembly.</summary>
        public static ElementCatalog LoadEmbedded()
        {
            Assembly asm = typeof(ElementCatalog).Assembly;
            return Load(name =>
            {
                using Stream? s = asm.GetManifestResourceStream("SiteDesignPro.Catalog." + name);
                if (s == null)
                {
                    throw new FileNotFoundException("Embedded catalog resource not found: " + name);
                }

                using var r = new StreamReader(s);
                return r.ReadToEnd();
            });
        }

        /// <summary>Loads the catalog from a directory containing the shared catalog files.</summary>
        public static ElementCatalog LoadFromDirectory(string directory)
        {
            return Load(name => File.ReadAllText(Path.Combine(directory, name)));
        }

        /// <summary>
        /// Loads the catalog from <paramref name="directory"/> when it contains the catalog files, otherwise from the
        /// embedded copy; then applies every *.json user catalog file found in <paramref name="userCatalogDirectory"/>.
        /// </summary>
        public static ElementCatalog LoadWithOverlays(string? directory, string? userCatalogDirectory)
        {
            ElementCatalog cat = directory != null && File.Exists(Path.Combine(directory, "element-types.json"))
                ? LoadFromDirectory(directory)
                : LoadEmbedded();
            if (userCatalogDirectory != null && Directory.Exists(userCatalogDirectory))
            {
                foreach (string file in Directory.GetFiles(userCatalogDirectory, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        cat.ApplyOverlay(File.ReadAllText(file), Path.GetFileName(file));
                    }
                    catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is KeyNotFoundException)
                    {
                        cat.loadMessages.Add("User catalog '" + Path.GetFileName(file) + "' was skipped: " + ex.Message);
                    }
                }
            }

            return cat;
        }

        private static ElementCatalog Load(Func<string, string> read)
        {
            var cat = new ElementCatalog();
            using (JsonDocument doc = JsonDocument.Parse(read("builders.json")))
            {
                foreach (JsonProperty b in doc.RootElement.GetProperty("builders").EnumerateObject())
                {
                    cat.builders[b.Name] = ParseBuilder(b.Name, b.Value);
                }
            }

            using (JsonDocument doc = JsonDocument.Parse(read("categories.json")))
            {
                JsonElement root = doc.RootElement;
                cat.Categories = root.GetProperty("categories").EnumerateArray().Select(c => new CategoryDefinition
                {
                    Id = Str(c, "id"),
                    Name = Str(c, "name"),
                    Panel = Str(c, "panel"),
                    ModelGroup = Str(c, "modelGroup"),
                }).ToList();
                cat.ModelGroups = root.GetProperty("modelGroups").EnumerateArray().Select(g => new ModelGroupDefinition
                {
                    Id = Str(g, "id"),
                    Name = Str(g, "name"),
                    Tag = Str(g, "tag"),
                    Order = g.TryGetProperty("order", out JsonElement o) ? o.GetInt32() : 0,
                }).ToList();
                cat.Schedules = root.GetProperty("schedules").EnumerateArray().Select(s => new ScheduleDefinition
                {
                    Id = Str(s, "id"),
                    Title = Str(s, "title"),
                    Columns = s.GetProperty("columns").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList(),
                    GroupBy = s.GetProperty("groupBy").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList(),
                }).ToList();
                cat.CommonProperties = root.GetProperty("commonProperties").EnumerateArray().Select(p => new PropertyDefinition
                {
                    Key = Str(p, "key"),
                    Label = Str(p, "label"),
                    Group = Str(p, "group", "General"),
                }).ToList();
            }

            using (JsonDocument doc = JsonDocument.Parse(read("layers.json")))
            {
                foreach (JsonElement l in doc.RootElement.GetProperty("layers").EnumerateArray())
                {
                    LayerDefinition def = ParseLayer(l, null);
                    cat.layers[def.Key] = def;
                }
            }

            using (JsonDocument doc = JsonDocument.Parse(read("materials.json")))
            {
                cat.MaterialsVersion = Str(doc.RootElement, "version", "0");
                foreach (JsonElement m in doc.RootElement.GetProperty("materials").EnumerateArray())
                {
                    MaterialDefinition def = ParseMaterial(m);
                    cat.materials[def.Key] = def;
                }
            }

            using (JsonDocument doc = JsonDocument.Parse(read("element-types.json")))
            {
                cat.CatalogId = Str(doc.RootElement, "catalogId", "sdp-core");
                cat.Version = Str(doc.RootElement, "version", "0");
                foreach (JsonElement t in doc.RootElement.GetProperty("types").EnumerateArray())
                {
                    cat.AddType(cat.ParseType(t, "core"));
                }
            }

            return cat;
        }

        /// <summary>
        /// Applies a user catalog overlay: { "types": [...], "layers": [...], "materials": [...] }. Types with an
        /// existing id replace the core definition; layer entries override by key (only the given fields change).
        /// </summary>
        public void ApplyOverlay(string json, string sourceName)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.TryGetProperty("layers", out JsonElement ls))
            {
                foreach (JsonElement l in ls.EnumerateArray())
                {
                    string key = Str(l, "key");
                    LayerDefinition def = ParseLayer(l, FindLayer(key));
                    layers[key] = def;
                }
            }

            if (root.TryGetProperty("materials", out JsonElement ms))
            {
                foreach (JsonElement m in ms.EnumerateArray())
                {
                    MaterialDefinition def = ParseMaterial(m);
                    materials[def.Key] = def;
                }
            }

            if (root.TryGetProperty("types", out JsonElement ts))
            {
                foreach (JsonElement t in ts.EnumerateArray())
                {
                    ElementType type = ParseType(t, sourceName);
                    if (types.ContainsKey(type.Id))
                    {
                        loadMessages.Add("Type '" + type.Id + "' overridden by " + sourceName + ".");
                    }

                    AddType(type);
                }
            }
        }

        private void AddType(ElementType type)
        {
            if (!types.ContainsKey(type.Id))
            {
                typeOrder.Add(type.Id);
            }

            types[type.Id] = type;
        }

        private ElementType ParseType(JsonElement t, string source)
        {
            string id = Str(t, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new FormatException("Element type without id.");
            }

            string builderId = Str(t, "builder");
            BuilderDefinition builder = FindBuilder(builderId)
                ?? throw new FormatException("Element type '" + id + "' references unknown builder '" + builderId + "'.");
            string category = Str(t, "category");
            CategoryDefinition? cat = FindCategory(category);

            var defaults = new Dictionary<string, ParamValue>(StringComparer.Ordinal);
            if (t.TryGetProperty("defaults", out JsonElement d) && d.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty p in d.EnumerateObject())
                {
                    if (!builder.Parameters.Any(x => x.Key == p.Name))
                    {
                        throw new FormatException("Element type '" + id + "' sets unknown parameter '" + p.Name + "'.");
                    }

                    defaults[p.Name] = ParamValue.FromJson(p.Value);
                }
            }

            var parameters = new List<ParameterDefinition>();
            foreach (ParameterDefinition p in builder.Parameters)
            {
                parameters.Add(defaults.TryGetValue(p.Key, out ParamValue v) ? p.WithDefault(v) : p);
            }

            if (t.TryGetProperty("parameters", out JsonElement extra) && extra.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement p in extra.EnumerateArray())
                {
                    ParameterDefinition def = ParseParameter(p);
                    parameters.RemoveAll(x => x.Key == def.Key);
                    parameters.Add(def);
                }
            }

            HatchDefinition? hatch = builder.Hatch;
            if (t.TryGetProperty("hatch", out JsonElement h))
            {
                hatch = ParseHatch(h);
            }

            return new ElementType
            {
                Id = id,
                Name = Str(t, "name", id),
                Category = category,
                Subcategory = Str(t, "subcategory"),
                Builder = builder,
                LayerKey = Str(t, "layer", "construction"),
                ModelGroup = Str(t, "modelGroup", cat?.ModelGroup ?? "annotations"),
                LabelPrefix = Str(t, "labelPrefix", "E"),
                ScheduleId = Str(t, "schedule", "general"),
                Hatch = hatch,
                Parameters = parameters,
                PropertyKeys = StrList(t, "properties"),
                ReviewRequired = StrList(t, "reviewRequired"),
                ExtraPanels = StrList(t, "panels"),
                Source = source,
            };
        }

        private static BuilderDefinition ParseBuilder(string id, JsonElement b)
        {
            var q = new QuantityDefinition();
            if (b.TryGetProperty("quantity", out JsonElement qe))
            {
                q.Measure = Str(qe, "measure", "count");
                q.Unit = Str(qe, "unit", "pcs");
                q.Secondary = OptStr(qe, "secondary");
                q.CountParam = OptStr(qe, "countParam");
                q.CountExpression = OptStr(qe, "countExpression");
            }

            return new BuilderDefinition
            {
                Id = id,
                Geometry = ElementGeometry.ParseKind(Str(b, "geometry")),
                Description = Str(b, "description"),
                Quantity = q,
                Symbol = OptStr(b, "symbol"),
                Hatch = b.TryGetProperty("hatch", out JsonElement h) ? ParseHatch(h) : null,
                PolylineWidthParam = OptStr(b, "polylineWidthParam"),
                Linetype = OptStr(b, "linetype"),
                Parameters = b.GetProperty("parameters").EnumerateArray().Select(ParseParameter).ToList(),
            };
        }

        private static ParameterDefinition ParseParameter(JsonElement p)
        {
            string typeCode = Str(p, "type", "text");
            if (!Enum.TryParse(typeCode, true, out ParameterType type))
            {
                throw new FormatException("Unknown parameter type '" + typeCode + "'.");
            }

            return new ParameterDefinition
            {
                Key = Str(p, "key"),
                Label = Str(p, "label", Str(p, "key")),
                Type = type,
                Unit = OptStr(p, "unit"),
                Default = p.TryGetProperty("default", out JsonElement d) ? ParamValue.FromJson(d) : ParamValue.Null,
                Min = p.TryGetProperty("min", out JsonElement mn) && mn.ValueKind == JsonValueKind.Number ? mn.GetDouble() : (double?)null,
                Max = p.TryGetProperty("max", out JsonElement mx) && mx.ValueKind == JsonValueKind.Number ? mx.GetDouble() : (double?)null,
                Options = StrList(p, "options"),
                Required = p.TryGetProperty("required", out JsonElement r) && r.ValueKind == JsonValueKind.True,
                Group = Str(p, "group", "General"),
                Description = OptStr(p, "description"),
            };
        }

        private static HatchDefinition? ParseHatch(JsonElement h)
        {
            if (h.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new HatchDefinition
            {
                Pattern = Str(h, "pattern", "SOLID"),
                Scale = h.TryGetProperty("scale", out JsonElement s) ? s.GetDouble() : 1.0,
                Angle = h.TryGetProperty("angle", out JsonElement a) ? a.GetDouble() : 0.0,
            };
        }

        private static LayerDefinition ParseLayer(JsonElement l, LayerDefinition? baseDef)
        {
            var def = baseDef == null ? new LayerDefinition() : new LayerDefinition
            {
                Key = baseDef.Key,
                Name = baseDef.Name,
                Color = baseDef.Color,
                Linetype = baseDef.Linetype,
                Lineweight = baseDef.Lineweight,
                Transparency = baseDef.Transparency,
                Plot = baseDef.Plot,
                Description = baseDef.Description,
            };
            def.Key = Str(l, "key", def.Key);
            def.Name = Str(l, "name", def.Name);
            if (l.TryGetProperty("color", out JsonElement c))
            {
                def.Color = c.GetInt16();
            }

            def.Linetype = Str(l, "linetype", def.Linetype);
            if (l.TryGetProperty("lineweight", out JsonElement w))
            {
                def.Lineweight = w.GetInt32();
            }

            if (l.TryGetProperty("transparency", out JsonElement tr))
            {
                def.Transparency = tr.GetInt32();
            }

            if (l.TryGetProperty("plot", out JsonElement pl))
            {
                def.Plot = pl.ValueKind == JsonValueKind.True;
            }

            def.Description = Str(l, "description", def.Description);
            if (string.IsNullOrWhiteSpace(def.Key) || string.IsNullOrWhiteSpace(def.Name))
            {
                throw new FormatException("Layer definitions need 'key' and 'name'.");
            }

            return def;
        }

        private static MaterialDefinition ParseMaterial(JsonElement m)
        {
            return new MaterialDefinition
            {
                Key = Str(m, "key"),
                Name = Str(m, "name"),
                Color = m.GetProperty("color").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
                Alpha = m.TryGetProperty("alpha", out JsonElement a) ? a.GetDouble() : 1.0,
                Texture = OptStr(m, "texture"),
                TextureSize = m.TryGetProperty("textureSize", out JsonElement ts) ? ts.EnumerateArray().Select(x => x.GetDouble()).ToArray() : null,
            };
        }

        internal static string Str(JsonElement e, string name, string fallback = "")
        {
            return e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;
        }

        private static string? OptStr(JsonElement e, string name)
        {
            return e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }

        private static IReadOnlyList<string> StrList(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out JsonElement v) || v.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return v.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList();
        }

        /// <summary>Formats a parameter value for display with its unit.</summary>
        public static string FormatValue(ParameterDefinition def, ParamValue value, int precision = 3)
        {
            if (value.IsNull)
            {
                return "(not set)";
            }

            if (def.IsNumeric && value.Kind == ParamKind.Number)
            {
                string fmt = def.Type == ParameterType.Integer ? "0" : "0." + new string('#', Math.Max(0, precision));
                string unit = def.DisplayUnit;
                return value.AsNumber().ToString(fmt, CultureInfo.InvariantCulture) + (unit.Length > 0 ? " " + unit : string.Empty);
            }

            return value.AsText();
        }
    }
}
