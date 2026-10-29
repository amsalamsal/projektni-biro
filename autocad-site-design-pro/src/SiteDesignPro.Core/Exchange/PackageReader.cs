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
                result.Report.Add(Severity.Error, IssueKind.Format, "SDP-F001", null, "The package could not be read: " + ex.Message,
                    "Check that the file is a Site Design Pro package (.sdpkg or a folder with manifest.json and geometry.json) and re-export it.");
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
                result.Report.Add(Severity.Error, IssueKind.Format, "SDP-F001", null, "The package could not be parsed: " + ex.Message, "Re-export the package.");
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
                throw new FileNotFoundException("manifest.json not found in " + dir);
            }

            if (!File.Exists(geometryPath))
            {
                throw new FileNotFoundException("geometry.json not found in " + dir);
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
            ZipArchiveEntry entry = zip.GetEntry(name) ?? throw new InvalidDataException(name + " is missing from the package archive.");
            CheckSize(entry.Length);
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }

        private static void CheckSize(long bytes)
        {
            if (bytes > MaxDocumentBytes)
            {
                throw new InvalidDataException("Package document exceeds the maximum supported size.");
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
                throw new FormatException("manifest.json is not a Site Design Pro manifest (format field).");
            }

            string version = Str(m, "schemaVersion");
            if (!TryMajor(version, out int major) || major != PackageFormat.SupportedMajor)
            {
                throw new FormatException("Unsupported schema version '" + version + "'. This version reads schema " + PackageFormat.SupportedMajor + ".x.");
            }

            if (Version.TryParse(version, out Version? read) && read > Version.Parse(PackageFormat.SchemaVersion))
            {
                report.Add(Severity.Warning, IssueKind.Format, "SDP-F002", null,
                    "Package schema " + version + " is newer than this reader (" + PackageFormat.SchemaVersion + "). Unknown fields are ignored.",
                    "Update Site Design Pro to the latest version to use all package features.");
            }

            if (Str(g, "lengthUnit") != "m" || Str(m.GetProperty("units"), "length") != "m")
            {
                throw new FormatException("Package length unit must be metres.");
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
            if (!SiteElement.IsValidId(pkg.PackageId))
            {
                throw new FormatException("packageId is not a valid UUID.");
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
                    report.Add(Severity.Error, IssueKind.Format, "SDP-F010", id, "Duplicate geometry record.", "Re-export the package; each element must have exactly one geometry record.");
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
                    report.Add(Severity.Error, IssueKind.DataError, "SDP-D001", string.IsNullOrEmpty(id) ? null : id, "Element has a missing or malformed id.", "Run SDPAUDIT in AutoCAD to assign identifiers, then re-export.");
                    continue;
                }

                if (!seen.Add(id))
                {
                    report.Add(Severity.Error, IssueKind.DataError, "SDP-D002", id, "Duplicate element id in package.", "Run SDPAUDIT in AutoCAD to repair duplicated identifiers (usually caused by copying objects), then re-export.");
                    continue;
                }

                if (!geometries.TryGetValue(id, out JsonElement geoJson))
                {
                    report.Add(Severity.Error, IssueKind.Format, "SDP-F011", id, "Element has no geometry record.", "Re-export the package.");
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
                    report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G010", id, "Invalid geometry: " + ex.Message, "Fix the source object in AutoCAD and re-export.");
                    continue;
                }

                if (typeGeometry.TryGetValue(typeId, out string? expected) && expected != ElementGeometry.KindCode(geometry.Kind))
                {
                    report.Add(Severity.Error, IssueKind.Format, "SDP-F012", id, "Geometry kind '" + ElementGeometry.KindCode(geometry.Kind) + "' does not match type definition '" + expected + "'.", "Re-export the package.");
                    continue;
                }

                if (!typeGeometry.ContainsKey(typeId))
                {
                    report.Add(Severity.Warning, IssueKind.Format, "SDP-F013", id, "Type '" + typeId + "' has no type definition in the manifest.", "Re-export with the current Site Design Pro version.");
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
                report.Add(Severity.Warning, IssueKind.Format, "SDP-F014", orphan, "Geometry record without a manifest element is ignored.", "Re-export the package.");
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
                        report.Add(Severity.Error, IssueKind.Format, "SDP-F020", null, "Asset '" + asset.Id + "' has an unsafe path '" + asset.Path + "'.", "Asset paths must stay inside the assets/ folder.");
                    }
                    else if (!assetExists(asset.Path))
                    {
                        report.Add(Severity.Warning, IssueKind.DataError, "SDP-F021", null, "Asset '" + asset.Id + "' is missing: " + asset.Path, "Re-export the package with its assets, or remove the asset reference. Elements fall back to their parametric geometry.");
                    }
                }
            }

            var assetIds = new HashSet<string>(pkg.Assets.Select(a => a.Id), StringComparer.Ordinal);
            foreach (PackageElement pe in pkg.Elements.Where(x => x.Element.AssetId != null && !assetIds.Contains(x.Element.AssetId!)))
            {
                report.Add(Severity.Warning, IssueKind.DataError, "SDP-F022", pe.Element.Id, "Referenced asset '" + pe.Element.AssetId + "' is not declared.", "Re-export the package with the asset, or clear the element's asset reference.");
            }

            report.ElementCount = pkg.Elements.Count;
            result.Package = pkg;
        }

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
                throw new FormatException("Required field '" + name + "' is missing.");
            }

            return v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : v.ToString();
        }
    }
}
