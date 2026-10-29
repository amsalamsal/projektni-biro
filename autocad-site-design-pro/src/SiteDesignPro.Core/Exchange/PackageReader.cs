using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Validation;

namespace SiteDesignPro.Core.Exchange
{
    public sealed class PackageReadResult
    {
        public SitePackage? Package { get; set; }

        public ValidationReport Report { get; } = new ValidationReport();

        public bool Success => Package != null && !Report.HasErrors;
    }

    /// <summary>
    /// Reads and structurally validates exchange packages (folder, manifest.json path or .sdpkg zip).
    /// Elements with invalid data are reported and skipped; format errors abort the read.
    /// </summary>
    public static class PackageReader
    {
        /// <summary>Maximum accepted size of a JSON document inside a package (guards against corrupt archives).</summary>
        public const long MaxDocumentBytes = 512L * 1024 * 1024;

        public static PackageReadResult Read(string path)
        {
            var result = new PackageReadResult();
            try
            {
                (string manifestText, string geometryText, Func<string, bool> assetExists) = Load(path);
                Parse(manifestText, geometryText, assetExists, result);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException || ex is JsonException || ex is FormatException)
            {
                result.Package = null;
                result.Report.Add(Severity.Error, IssueKind.Format, "SDP-F001", null, "Paket nije moguće pročitati: " + ex.Message,
                    "Provjerite da je datoteka paket programa Vanjsko uređenje PRO (.sdpkg ili folder s manifest.json i geometry.json) i ponovo ga izvezite.");
            }

            return result;
        }

        public static PackageReadResult ReadFromStrings(string manifestJson, string geometryJson)
        {
            var result = new PackageReadResult();
            try
            {
                Parse(manifestJson, geometryJson, _ => true, result);
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException)
            {
                result.Package = null;
                result.Report.Add(Severity.Error, IssueKind.Format, "SDP-F001", null, "Sadržaj paketa nije ispravan: " + ex.Message, "Ponovo izvezite paket.");
            }

            return result;
        }

        private static (string Manifest, string Geometry, Func<string, bool> AssetExists) Load(string path)
        {
            if (File.Exists(path) && path.EndsWith(PackageFormat.PackageExtension, StringComparison.OrdinalIgnoreCase))
            {
                using ZipArchive zip = ZipFile.OpenRead(path);
                string m = ReadEntry(zip, PackageFormat.ManifestFile);
                string g = ReadEntry(zip, PackageFormat.GeometryFile);
                var names = new HashSet<string>(zip.Entries.Select(e => e.FullName), StringComparer.Ordinal);
                return (m, g, p => names.Contains(p));
            }

            string dir = Directory.Exists(path) ? path : (Path.GetFileName(path) == PackageFormat.ManifestFile ? Path.GetDirectoryName(Path.GetFullPath(path))! : path);
            string manifestPath = Path.Combine(dir, PackageFormat.ManifestFile);
            string geometryPath = Path.Combine(dir, PackageFormat.GeometryFile);
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException("manifest.json nije pronađen u folderu " + dir);
            }

            if (!File.Exists(geometryPath))
            {
                throw new FileNotFoundException("geometry.json nije pronađen u folderu " + dir);
            }

            CheckSize(new FileInfo(manifestPath).Length);
            CheckSize(new FileInfo(geometryPath).Length);
            string root = Path.GetFullPath(dir);
            return (File.ReadAllText(manifestPath), File.ReadAllText(geometryPath), p =>
            {
                string full = Path.GetFullPath(Path.Combine(root, p.Replace('/', Path.DirectorySeparatorChar)));
                return full.StartsWith(root, StringComparison.Ordinal) && File.Exists(full);
            });
        }

        private static string ReadEntry(ZipArchive zip, string name)
        {
            ZipArchiveEntry entry = zip.GetEntry(name) ?? throw new InvalidDataException(name + " nedostaje u arhivi paketa.");
            CheckSize(entry.Length);
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }

        private static void CheckSize(long bytes)
        {
            if (bytes > MaxDocumentBytes)
            {
                throw new InvalidDataException("Dokument paketa prelazi najveću podržanu veličinu.");
            }
        }

        private static void Parse(string manifestText, string geometryText, Func<string, bool> assetExists, PackageReadResult result)
        {
            ValidationReport report = result.Report;
            using JsonDocument manifestDoc = JsonDocument.Parse(manifestText);
            using JsonDocument geometryDoc = JsonDocument.Parse(geometryText);
            JsonElement m = manifestDoc.RootElement;
            JsonElement g = geometryDoc.RootElement;

            if (Str(m, "format") != PackageFormat.FormatId)
            {
                throw new FormatException("manifest.json nije manifest paketa Vanjsko uređenje PRO (polje format).");
            }

            string version = Str(m, "schemaVersion");
            if (!TryMajor(version, out int major) || major != PackageFormat.SupportedMajor)
            {
                throw new FormatException("Nepodržana verzija sheme '" + version + "'. Ova verzija čita shemu " + PackageFormat.SupportedMajor + ".x.");
            }

            if (Version.TryParse(version, out Version? read) && read > Version.Parse(PackageFormat.SchemaVersion))
            {
                report.Add(Severity.Warning, IssueKind.Format, "SDP-F002", null,
                    "Shema paketa " + version + " je novija od ove verzije programa (" + PackageFormat.SchemaVersion + "). Nepoznata polja se zanemaruju.",
                    "Ažurirajte Vanjsko uređenje PRO na najnoviju verziju.");
            }

            if (Str(g, "lengthUnit") != "m" || Str(m.GetProperty("units"), "length") != "m")
            {
                throw new FormatException("Jedinica dužine paketa mora biti metar.");
            }

            JsonElement units = m.GetProperty("units");
            JsonElement cs = m.GetProperty("coordinateSystem");
            JsonElement project = m.GetProperty("project");
            JsonElement generator = m.GetProperty("generator");
            var pkg = new SitePackage
            {
                SchemaVersion = version,
                PackageId = Str(m, "packageId"),
                CreatedUtc = DateTime.Parse(Str(m, "createdUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
                GeneratorApplication = Str(generator, "application"),
                GeneratorVersion = Str(generator, "version"),
                ProjectId = Str(project, "id"),
                ProjectName = Str(project, "name"),
                SourceLengthUnit = Str(units, "sourceLengthUnit"),
                SourceUnitsToMeters = units.GetProperty("sourceUnitsToMeters").GetDouble(),
                Origin = cs.GetProperty("origin").EnumerateArray().Select(x => x.GetDouble()).ToArray(),
                TrueNorthDegrees = cs.GetProperty("trueNorthDegrees").GetDouble(),
                CatalogId = Str(m.GetProperty("catalog"), "id"),
                CatalogVersion = Str(m.GetProperty("catalog"), "version"),
            };
            if (cs.TryGetProperty("crs", out JsonElement crsEl) && crsEl.ValueKind == JsonValueKind.String)
            {
                pkg.Crs = crsEl.GetString() ?? string.Empty;
            }

            if (cs.TryGetProperty("elevation", out JsonElement elev) && elev.ValueKind == JsonValueKind.Object)
            {
                pkg.BuildingZeroAbsolute = NullableNumber(elev, "buildingZeroAbsolute");
                pkg.PackageZeroAbsolute = NullableNumber(elev, "packageZeroAbsolute");
                pkg.VerticalDatum = elev.TryGetProperty("verticalDatum", out JsonElement vd) && vd.ValueKind == JsonValueKind.String ? vd.GetString() ?? string.Empty : string.Empty;
            }

            if (m.TryGetProperty("terrain", out JsonElement terrain) && terrain.ValueKind == JsonValueKind.Object)
            {
                string tPath = Str(terrain, "path");
                if (tPath != PackageFormat.TerrainFile || !assetExists(tPath))
                {
                    result.Report.Add(Severity.Error, IssueKind.Format, "SDP-F030", null,
                        "Paket navodi teren (" + tPath + "), ali datoteka nedostaje.", "Ponovo izvezite paket.");
                }

                foreach (JsonElement sf in terrain.GetProperty("surfaces").EnumerateArray())
                {
                    pkg.TerrainSurfaces.Add((Str(sf, "id"), Str(sf, "name"), sf.GetProperty("triangles").GetInt32()));
                }
            }

            if (!SiteElement.IsValidId(pkg.PackageId))
            {
                throw new FormatException("packageId nije ispravan UUID.");
            }

            var typeGeometry = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonElement t in m.GetProperty("typeDefinitions").EnumerateArray())
            {
                string id = Str(t, "id");
                pkg.TypeDefinitionIds.Add(id);
                typeGeometry[id] = Str(t, "geometry");
            }

            var geometries = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (JsonElement ge in g.GetProperty("elements").EnumerateArray())
            {
                string id = Str(ge, "id");
                if (geometries.ContainsKey(id))
                {
                    report.Add(Severity.Error, IssueKind.Format, "SDP-F010", id, "Dupli zapis geometrije.", "Ponovo izvezite paket; svaki element mora imati tačno jedan zapis geometrije.");
                    continue;
                }

                geometries[id] = ge;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement e in m.GetProperty("elements").EnumerateArray())
            {
                string id = Str(e, "id");
                if (!SiteElement.IsValidId(id))
                {
                    report.Add(Severity.Error, IssueKind.DataError, "SDP-D001", string.IsNullOrEmpty(id) ? null : id, "Element nema ispravan identifikator.", "Pokrenite SDPAUDIT u AutoCAD-u da se dodijele identifikatori, zatim ponovo izvezite.");
                    continue;
                }

                if (!seen.Add(id))
                {
                    report.Add(Severity.Error, IssueKind.DataError, "SDP-D002", id, "Dupli identifikator elementa u paketu.", "Pokrenite SDPAUDIT u AutoCAD-u da se poprave dupli identifikatori (obično nastaju kopiranjem objekata), zatim ponovo izvezite.");
                    continue;
                }

                if (!geometries.TryGetValue(id, out JsonElement geoJson))
                {
                    report.Add(Severity.Error, IssueKind.Format, "SDP-F011", id, "Element nema zapis geometrije.", "Ponovo izvezite paket.");
                    continue;
                }

                string typeId = Str(e, "type");
                ElementGeometry geometry;
                try
                {
                    geometry = GeometryJson.FromJson(geoJson);
                }
                catch (Exception ex) when (ex is FormatException || ex is KeyNotFoundException || ex is InvalidOperationException)
                {
                    report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G010", id, "Neispravna geometrija: " + ex.Message, "Ispravite izvorni objekat u AutoCAD-u i ponovo izvezite.");
                    continue;
                }

                if (typeGeometry.TryGetValue(typeId, out string? expected) && expected != ElementGeometry.KindCode(geometry.Kind))
                {
                    report.Add(Severity.Error, IssueKind.Format, "SDP-F012", id, "Vrsta geometrije '" + ElementGeometry.KindCode(geometry.Kind) + "' ne odgovara definiciji tipa '" + expected + "'.", "Ponovo izvezite paket.");
                    continue;
                }

                if (!typeGeometry.ContainsKey(typeId))
                {
                    report.Add(Severity.Warning, IssueKind.Format, "SDP-F013", id, "Tip '" + typeId + "' nema definiciju u manifestu paketa.", "Ponovo izvezite paket trenutnom verzijom Vanjsko uređenje PRO.");
                }

                var element = new SiteElement(id, typeId, geometry)
                {
                    Name = Str(e, "name"),
                    Label = e.TryGetProperty("label", out JsonElement lab) ? lab.GetString() : null,
                    Revision = e.TryGetProperty("revision", out JsonElement rev) ? rev.GetInt32() : 1,
                    Parameters = ParameterSet.FromJson(e.GetProperty("parameters")),
                    Custom = e.TryGetProperty("custom", out JsonElement cu) ? ParameterSet.FromJson(cu) : new ParameterSet(),
                    SourceHandle = e.TryGetProperty("sourceHandle", out JsonElement sh) ? sh.GetString() : null,
                    AssetId = e.TryGetProperty("assetId", out JsonElement aid) ? aid.GetString() : null,
                };
                if (e.TryGetProperty("properties", out JsonElement props) && props.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty p in props.EnumerateObject())
                    {
                        element.Properties[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? string.Empty : p.Value.ToString();
                    }
                }

                pkg.Elements.Add(new PackageElement(element)
                {
                    Category = Str(e, "category"),
                    Subcategory = Str(e, "subcategory"),
                    Builder = Str(e, "builder"),
                    Layer = Str(e, "layer"),
                    ModelGroup = Str(e, "modelGroup"),
                    ContentHash = Str(e, "contentHash"),
                });
            }

            foreach (string orphan in geometries.Keys.Where(k => !seen.Contains(k)))
            {
                report.Add(Severity.Warning, IssueKind.Format, "SDP-F014", orphan, "Zapis geometrije bez elementa u manifestu se zanemaruje.", "Ponovo izvezite paket.");
            }

            if (m.TryGetProperty("assets", out JsonElement assets))
            {
                foreach (JsonElement a in assets.EnumerateArray())
                {
                    var asset = new PackageAsset { Id = Str(a, "id"), Path = Str(a, "path"), Kind = Str(a, "kind") };
                    if (a.TryGetProperty("sha256", out JsonElement sha))
                    {
                        asset.Sha256 = sha.GetString();
                    }

                    pkg.Assets.Add(asset);
                    if (asset.Path.Contains("..", StringComparison.Ordinal) || !asset.Path.StartsWith(PackageFormat.AssetsFolder + "/", StringComparison.Ordinal))
                    {
                        report.Add(Severity.Error, IssueKind.Format, "SDP-F020", null, "Datoteka modela '" + asset.Id + "' ima nesigurnu putanju '" + asset.Path + "'.", "Putanje datoteka moraju ostati unutar foldera assets/.");
                    }
                    else if (!assetExists(asset.Path))
                    {
                        report.Add(Severity.Warning, IssueKind.DataError, "SDP-F021", null, "Datoteka modela '" + asset.Id + "' nedostaje: " + asset.Path, "Ponovo izvezite paket s modelima ili uklonite referencu. Elementi se prikazuju parametarskom geometrijom.");
                    }
                }
            }

            var assetIds = new HashSet<string>(pkg.Assets.Select(a => a.Id), StringComparer.Ordinal);
            foreach (PackageElement pe in pkg.Elements.Where(x => x.Element.AssetId != null && !assetIds.Contains(x.Element.AssetId!)))
            {
                report.Add(Severity.Warning, IssueKind.DataError, "SDP-F022", pe.Element.Id, "Navedeni model '" + pe.Element.AssetId + "' nije deklarisan u paketu.", "Ponovo izvezite paket s modelom ili uklonite referencu na model u elementu.");
            }

            report.ElementCount = pkg.Elements.Count;
            result.Package = pkg;
        }

        private static double? NullableNumber(JsonElement o, string name) =>
            o.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : (double?)null;

        private static bool TryMajor(string version, out int major)
        {
            major = 0;
            string[] parts = version.Split('.');
            return parts.Length == 3 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out major);
        }

        private static string Str(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out JsonElement v))
            {
                throw new FormatException("Nedostaje obavezno polje '" + name + "'.");
            }

            return v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : v.ToString();
        }
    }
}
