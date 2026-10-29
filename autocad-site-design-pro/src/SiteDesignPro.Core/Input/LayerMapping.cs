using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.Core.Input
{
    /// <summary>Standard project roles that source DWG layers can be assigned to.</summary>
    public enum SourceRole
    {
        ParcelBoundary,
        BuildingFootprint,
        BuildingMarker,
        Roads,
        ParterreLine,
        ElevationLabels,
        TerrainPoints,
        Contours,
        Breaklines,
        ExistingInfrastructure,
        ControlPoints,
    }

    public static class SourceRoles
    {
        public static readonly IReadOnlyList<SourceRole> All = (SourceRole[])Enum.GetValues(typeof(SourceRole));

        /// <summary>Bosnian display name of a role.</summary>
        public static string Name(SourceRole r)
        {
            switch (r)
            {
                case SourceRole.ParcelBoundary: return "Parcelna granica";
                case SourceRole.BuildingFootprint: return "Tlocrt objekta";
                case SourceRole.BuildingMarker: return "Oznaka položaja objekta";
                case SourceRole.Roads: return "Saobraćajnice";
                case SourceRole.ParterreLine: return "Linija partera";
                case SourceRole.ElevationLabels: return "Oznake kota";
                case SourceRole.TerrainPoints: return "Tačke terena";
                case SourceRole.Contours: return "Izohipse";
                case SourceRole.Breaklines: return "Prelomne linije";
                case SourceRole.ExistingInfrastructure: return "Postojeća infrastruktura";
                case SourceRole.ControlPoints: return "Kontrolne (geodetske) tačke";
                default: return r.ToString();
            }
        }

        public static string Hint(SourceRole r)
        {
            switch (r)
            {
                case SourceRole.ParcelBoundary: return "Zatvorena polilinija granice parcele (jedna ili više čestica).";
                case SourceRole.BuildingFootprint: return "Zatvorene polilinije stvarnih tlocrta objekata.";
                case SourceRole.BuildingMarker: return "Blokovi ili simboli koji označavaju samo položaj objekta (ne tlocrt).";
                case SourceRole.Roads: return "Ivice i osovine saobraćajnica prema regulacionom planu.";
                case SourceRole.ParterreLine: return "Granica / linija partera.";
                case SourceRole.ElevationLabels: return "Tekstovi s kotama (npr. +0,25, KT 518,81).";
                case SourceRole.TerrainPoints: return "Tačke ili blokovi geodetskog snimka sa stvarnom Z visinom.";
                case SourceRole.Contours: return "Izohipse sa Z visinom (elevation) jednakom koti izohipse.";
                case SourceRole.Breaklines: return "3D linije loma terena (ivice, nožice i krune kosina).";
                case SourceRole.ExistingInfrastructure: return "Postojeći vodovi, šahtovi, stubovi i slično.";
                case SourceRole.ControlPoints: return "Tačke s poznatim koordinatama za georeferenciranje.";
                default: return string.Empty;
            }
        }

        /// <summary>Roles the site workflow needs; a missing one is reported (never invented).</summary>
        public static bool IsRequired(SourceRole r) => r == SourceRole.ParcelBoundary || r == SourceRole.BuildingFootprint || r == SourceRole.ElevationLabels;
    }

    /// <summary>Assignment of source layers (exact names or wildcard patterns with * and ?) to roles.</summary>
    public sealed class LayerMapping
    {
        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        public string Name { get; set; } = "Novo mapiranje";

        public string Description { get; set; } = string.Empty;

        /// <summary>Role → list of layer names or patterns. Layer names are compared case-insensitively.</summary>
        public Dictionary<SourceRole, List<string>> Roles { get; set; } = new Dictionary<SourceRole, List<string>>();

        /// <summary>When true, building markers (blocks) may stand in for footprints with a user-confirmed size.</summary>
        public bool AllowMarkerWorkflow { get; set; }

        public List<string> Patterns(SourceRole role) => Roles.TryGetValue(role, out List<string>? l) ? l : new List<string>();

        public void Set(SourceRole role, IEnumerable<string> layers) => Roles[role] = layers.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        public static bool Matches(string pattern, string layer)
        {
            if (pattern.IndexOfAny(new[] { '*', '?' }) < 0)
            {
                return string.Equals(pattern, layer, StringComparison.OrdinalIgnoreCase);
            }

            string rx = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(layer, rx, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        /// <summary>Roles a layer belongs to (several = ambiguous mapping).</summary>
        public List<SourceRole> RolesOf(string layer) => Roles.Where(kv => kv.Value.Any(p => Matches(p, layer))).Select(kv => kv.Key).ToList();

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        public static LayerMapping FromJson(string json) => JsonSerializer.Deserialize<LayerMapping>(json, JsonOptions) ?? throw new FormatException(Bs.T("Datoteka mapiranja slojeva je prazna."));

        /// <summary>
        /// Suggests roles from common Bosnian/Croatian/Serbian and English layer names. Suggestions are shown to the
        /// user for confirmation; nothing is applied silently.
        /// </summary>
        public static LayerMapping Suggest(IEnumerable<string> layers)
        {
            var rules = new List<(SourceRole Role, string[] Words)>
            {
                (SourceRole.ParcelBoundary, new[] { "parcel", "cestic", "čestic", "granica", "katastar", "kp_", "parcela" }),
                (SourceRole.BuildingFootprint, new[] { "tlocrt", "objekat", "objekti", "zgrad", "building", "footprint", "gabarit" }),
                (SourceRole.Roads, new[] { "saobra", "cest", "ulic", "kolovoz", "road", "regulac", "trotoar" }),
                (SourceRole.ParterreLine, new[] { "parter" }),
                (SourceRole.ElevationLabels, new[] { "kota", "kote", "visin", "elev", "nivelac" }),
                (SourceRole.TerrainPoints, new[] { "snimak", "tacke", "tačke", "survey", "geodet", "points" }),
                (SourceRole.Contours, new[] { "izohips", "slojnic", "contour" }),
                (SourceRole.Breaklines, new[] { "prelom", "breakline", "kosin", "nozica", "nožica", "kruna" }),
                (SourceRole.ExistingInfrastructure, new[] { "vodovod", "kanaliz", "elektro", "infra", "instalac", "sahta", "šaht" }),
            };
            var m = new LayerMapping { Name = Bs.T("Prijedlog prema nazivima slojeva") };
            foreach (string layer in layers)
            {
                string l = layer.ToLowerInvariant();
                foreach ((SourceRole role, string[] words) in rules)
                {
                    if (words.Any(w => l.Contains(w)))
                    {
                        if (!m.Roles.TryGetValue(role, out List<string>? list))
                        {
                            m.Roles[role] = list = new List<string>();
                        }

                        list.Add(layer);
                        break; // first matching role only; the user resolves the rest
                    }
                }
            }

            return m;
        }
    }

    /// <summary>Saved mapping presets (one JSON file per preset) for reuse across projects.</summary>
    public static class LayerMappingPresets
    {
        public static string FileName(string name)
        {
            // characters invalid in Windows file names, independent of the OS the code runs on
            const string invalid = "<>:\"/\\|?*";
            string safe = new string(name.Select(c => c < 32 || invalid.IndexOf(c) >= 0 ? '_' : c).ToArray()).Trim();
            return (safe.Length == 0 ? "mapiranje" : safe) + ".sdpmap.json";
        }

        public static string Save(string folder, LayerMapping mapping)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, FileName(mapping.Name));
            Safety.SafeFile.WriteAllTextAtomic(path, mapping.ToJson());
            return path;
        }

        public static List<(string Path, LayerMapping Mapping)> Load(string folder, List<string>? problems = null)
        {
            var result = new List<(string, LayerMapping)>();
            if (!Directory.Exists(folder))
            {
                return result;
            }

            foreach (string f in Directory.GetFiles(folder, "*.sdpmap.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    result.Add((f, LayerMapping.FromJson(File.ReadAllText(f))));
                }
                catch (Exception ex) when (ex is JsonException || ex is IOException || ex is FormatException)
                {
                    problems?.Add(Bs.T("Mapiranje {0} nije učitano: {1}", Path.GetFileName(f), ex.Message));
                }
            }

            return result;
        }
    }
}
