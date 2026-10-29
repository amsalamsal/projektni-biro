using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Validation;

namespace SiteDesignPro.Core.Exchange
{
    public sealed class PackageWriteOptions
    {
        public string GeneratorVersion { get; set; } = "0.1.0";

        /// <summary>Host application description, e.g. "AutoCAD 2025 (R25.0)".</summary>
        public string? Host { get; set; }

        public string? SourceDocument { get; set; }

        public IList<PackageAsset> Assets { get; set; } = new List<PackageAsset>();

        /// <summary>Validation report written to preview/validation-report.json.</summary>
        public ValidationReport? Validation { get; set; }

        /// <summary>Fixed package id (tests); a new UUID when null.</summary>
        public string? PackageId { get; set; }

        /// <summary>Fixed creation time (tests); UtcNow when null.</summary>
        public DateTime? CreatedUtc { get; set; }
    }

    public sealed class PackageWriteResult
    {
        public string Path { get; set; } = string.Empty;

        public int ElementCount { get; set; }

        public List<string> Skipped { get; } = new List<string>();

        public string PackageId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Writes Site Design Pro exchange packages. Input elements are in drawing coordinates (drawing units, WCS);
    /// the writer converts to metres relative to the project origin. Output is a folder or a .sdpkg zip.
    /// </summary>
    public static class PackageWriter
    {
        private static readonly JsonSerializerOptions Indented = new JsonSerializerOptions
        {
            WriteIndented = true,
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>
        /// Converts drawing-frame elements to package frame (metres, relative to origin).
        /// </summary>
        public static SiteElement ToPackageFrame(SiteElement drawingElement, ProjectSettings settings)
        {
            Units.UnitScale scale = settings.Scale;
            Vec3 origin = settings.OriginMeters;
            Vec3 Map(Vec3 v) => new Vec3(scale.ToMeters(v.X) - origin.X, scale.ToMeters(v.Y) - origin.Y, scale.ToMeters(v.Z) - origin.Z);
            return new SiteElement(drawingElement.Id, drawingElement.TypeId, drawingElement.Geometry.Transform(Map))
            {
                Name = drawingElement.Name,
                Label = drawingElement.Label,
                Parameters = drawingElement.Parameters.Clone(),
                Properties = new SortedDictionary<string, string>(drawingElement.Properties, StringComparer.Ordinal),
                Custom = drawingElement.Custom.Clone(),
                Revision = drawingElement.Revision,
                SourceHandle = drawingElement.SourceHandle,
                AssetId = drawingElement.AssetId,
            };
        }

        /// <summary>Builds manifest and geometry documents for elements already in the package frame.</summary>
        public static (JsonObject Manifest, JsonObject Geometry, List<string> Skipped) BuildDocuments(
            ProjectSettings settings, IEnumerable<SiteElement> packageFrameElements, ElementCatalog catalog, PackageWriteOptions options)
        {
            var skipped = new List<string>();
            var elementsJson = new JsonArray();
            var geometryJson = new JsonArray();
            var usedTypes = new SortedDictionary<string, ElementType>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (SiteElement e in packageFrameElements)
            {
                ElementType? type = catalog.FindType(e.TypeId);
                if (type == null)
                {
                    skipped.Add(e.Id + ": unknown element type '" + e.TypeId + "'");
                    continue;
                }

                if (!SiteElement.IsValidId(e.Id) || !seen.Add(e.Id))
                {
                    skipped.Add(e.Id + ": missing, malformed or duplicate element id");
                    continue;
                }

                if (e.Geometry.Kind != type.Geometry)
                {
                    skipped.Add(e.Id + ": geometry kind " + ElementGeometry.KindCode(e.Geometry.Kind) + " does not match builder '" + type.Builder.Id + "'");
                    continue;
                }

                usedTypes[type.Id] = type;
                var el = new JsonObject
                {
                    ["id"] = e.Id,
                    ["type"] = e.TypeId,
                    ["category"] = type.Category,
                    ["subcategory"] = type.Subcategory,
                    ["builder"] = type.Builder.Id,
                    ["name"] = string.IsNullOrEmpty(e.Name) ? type.Name : e.Name,
                };
                if (!string.IsNullOrEmpty(e.Label))
                {
                    el["label"] = e.Label;
                }

                LayerDefinition? layer = catalog.FindLayer(type.LayerKey);
                el["layer"] = layer?.Name ?? type.LayerKey;
                el["modelGroup"] = type.ModelGroup;
                el["revision"] = Math.Max(1, e.Revision);
                el["contentHash"] = ContentHasher.Hash(e);
                el["parameters"] = e.Parameters.ToJson();
                if (e.Properties.Count > 0)
                {
                    el["properties"] = new JsonObject(e.Properties.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, kv.Value)));
                }

                if (e.Custom.Count > 0)
                {
                    el["custom"] = e.Custom.ToJson();
                }

                if (!string.IsNullOrEmpty(e.SourceHandle))
                {
                    el["sourceHandle"] = e.SourceHandle;
                }

                if (!string.IsNullOrEmpty(e.AssetId))
                {
                    el["assetId"] = e.AssetId;
                }

                elementsJson.Add(el);
                geometryJson.Add(GeometryJson.ToJson(e.Id, e.Geometry));
            }

            Vec3 origin = settings.OriginMeters;
            var manifest = new JsonObject
            {
                ["format"] = PackageFormat.FormatId,
                ["schemaVersion"] = PackageFormat.SchemaVersion,
                ["packageId"] = options.PackageId ?? Guid.NewGuid().ToString("D"),
                ["createdUtc"] = (options.CreatedUtc ?? DateTime.UtcNow).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["generator"] = Compact(new JsonObject
                {
                    ["application"] = PackageFormat.ApplicationName,
                    ["version"] = options.GeneratorVersion,
                    ["host"] = options.Host,
                    ["sourceDocument"] = options.SourceDocument,
                }),
                ["project"] = Compact(new JsonObject
                {
                    ["id"] = settings.ProjectId,
                    ["name"] = string.IsNullOrWhiteSpace(settings.Name) ? "Untitled site project" : settings.Name,
                    ["number"] = settings.Number,
                    ["client"] = settings.Client,
                    ["location"] = settings.Location,
                    ["description"] = settings.Description,
                    ["author"] = settings.Author,
                    ["revision"] = settings.Revision,
                }),
                ["units"] = new JsonObject
                {
                    ["length"] = "m",
                    ["angle"] = "rad",
                    ["sourceLengthUnit"] = Units.UnitConversion.Code(settings.Unit),
                    ["sourceUnitsToMeters"] = settings.Scale.MetersPerDrawingUnit,
                },
                ["coordinateSystem"] = Compact(new JsonObject
                {
                    ["origin"] = new JsonArray(GeometryJson.Round(origin.X), GeometryJson.Round(origin.Y), GeometryJson.Round(origin.Z)),
                    ["trueNorthDegrees"] = settings.TrueNorthDegrees,
                    ["crs"] = settings.Crs,
                    ["heightDatum"] = settings.HeightDatum,
                }),
                ["catalog"] = new JsonObject
                {
                    ["id"] = catalog.CatalogId,
                    ["version"] = catalog.Version,
                    ["materialsVersion"] = catalog.MaterialsVersion,
                },
                ["typeDefinitions"] = new JsonArray(usedTypes.Values.Select(t => (JsonNode)TypeDefinitionJson(t)).ToArray()),
                ["materials"] = new JsonArray(catalog.Materials.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => (JsonNode)MaterialJson(m)).ToArray()),
                ["elements"] = elementsJson,
            };

            if (options.Assets.Count > 0)
            {
                manifest["assets"] = new JsonArray(options.Assets.Select(a => (JsonNode)Compact(new JsonObject
                {
                    ["id"] = a.Id,
                    ["path"] = a.Path,
                    ["kind"] = a.Kind,
                    ["sha256"] = a.Sha256,
                    ["description"] = a.Description,
                })).ToArray());
            }

            if (options.Validation != null)
            {
                manifest["validation"] = new JsonObject
                {
                    ["errors"] = options.Validation.ErrorCount,
                    ["warnings"] = options.Validation.WarningCount,
                    ["reviews"] = options.Validation.ReviewCount,
                    ["reportPath"] = PackageFormat.ValidationReportFile,
                };
            }

            var geometry = new JsonObject
            {
                ["schemaVersion"] = PackageFormat.SchemaVersion,
                ["lengthUnit"] = "m",
                ["elements"] = geometryJson,
            };
            return (manifest, geometry, skipped);
        }

        /// <summary>
        /// Writes a package. <paramref name="drawingElements"/> are in drawing units/WCS. A path ending in .sdpkg
        /// produces a zip archive; any other path produces a folder. Existing output is replaced atomically.
        /// </summary>
        public static PackageWriteResult Write(string path, ProjectSettings settings, IEnumerable<SiteElement> drawingElements, ElementCatalog catalog, PackageWriteOptions? options = null)
        {
            options ??= new PackageWriteOptions();
            List<SiteElement> local = drawingElements.Select(e => ToPackageFrame(e, settings)).ToList();
            return WritePackageFrame(path, settings, local, catalog, options);
        }

        /// <summary>Writes elements that are already in the package frame (metres relative to origin).</summary>
        public static PackageWriteResult WritePackageFrame(string path, ProjectSettings settings, IReadOnlyList<SiteElement> elements, ElementCatalog catalog, PackageWriteOptions options)
        {
            foreach (PackageAsset a in options.Assets)
            {
                if (a.SourceFile != null)
                {
                    if (!File.Exists(a.SourceFile))
                    {
                        throw new FileNotFoundException("Asset file not found: " + a.SourceFile, a.SourceFile);
                    }

                    a.Sha256 = ContentHasher.Sha256Hex(File.ReadAllBytes(a.SourceFile));
                }

                if (!a.Path.StartsWith(PackageFormat.AssetsFolder + "/", StringComparison.Ordinal) || a.Path.Contains("..", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Asset paths must be inside assets/: " + a.Path);
                }
            }

            (JsonObject manifest, JsonObject geometry, List<string> skipped) = BuildDocuments(settings, elements, catalog, options);
            var files = new List<(string Name, byte[] Data)>
            {
                (PackageFormat.ManifestFile, Encoding.UTF8.GetBytes(manifest.ToJsonString(Indented))),
                (PackageFormat.GeometryFile, Encoding.UTF8.GetBytes(geometry.ToJsonString(Indented))),
            };
            if (options.Validation != null)
            {
                if (options.CreatedUtc.HasValue)
                {
                    options.Validation.CreatedUtc = options.CreatedUtc.Value.ToUniversalTime(); // reproducible packages
                }

                files.Add((PackageFormat.ValidationReportFile, Encoding.UTF8.GetBytes(options.Validation.ToJson().ToJsonString(Indented))));
                files.Add(("preview/validation-report.txt", Encoding.UTF8.GetBytes(options.Validation.ToText())));
            }

            foreach (PackageAsset a in options.Assets.Where(a => a.SourceFile != null))
            {
                files.Add((a.Path, File.ReadAllBytes(a.SourceFile!)));
            }

            string full = System.IO.Path.GetFullPath(path);
            if (full.EndsWith(PackageFormat.PackageExtension, StringComparison.OrdinalIgnoreCase))
            {
                WriteZip(full, files);
            }
            else
            {
                WriteFolder(full, files);
            }

            var result = new PackageWriteResult { Path = full, ElementCount = ((JsonArray)manifest["elements"]!).Count, PackageId = (string)manifest["packageId"]! };
            result.Skipped.AddRange(skipped);
            return result;
        }

        private static void WriteZip(string target, List<(string Name, byte[] Data)> files)
        {
            string dir = System.IO.Path.GetDirectoryName(target) ?? ".";
            Directory.CreateDirectory(dir);
            string temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (FileStream fs = File.Create(temp))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    foreach ((string name, byte[] data) in files)
                    {
                        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                        using Stream s = entry.Open();
                        s.Write(data, 0, data.Length);
                    }
                }

                File.Move(temp, target, true);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
        }

        private static void WriteFolder(string target, List<(string Name, byte[] Data)> files)
        {
            string parent = System.IO.Path.GetDirectoryName(target.TrimEnd(System.IO.Path.DirectorySeparatorChar)) ?? ".";
            Directory.CreateDirectory(parent);
            string temp = target.TrimEnd(System.IO.Path.DirectorySeparatorChar) + ".tmp-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(temp);
            try
            {
                foreach ((string name, byte[] data) in files)
                {
                    string file = System.IO.Path.Combine(temp, name.Replace('/', System.IO.Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
                    File.WriteAllBytes(file, data);
                }

                if (Directory.Exists(target))
                {
                    // Only replace folders that are recognisably packages, never arbitrary user folders.
                    if (!File.Exists(System.IO.Path.Combine(target, PackageFormat.ManifestFile)) && Directory.EnumerateFileSystemEntries(target).Any())
                    {
                        throw new IOException("Target folder exists and is not a Site Design Pro package: " + target);
                    }

                    Directory.Delete(target, true);
                }

                Directory.Move(temp, target);
            }
            finally
            {
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, true);
                }
            }
        }

        private static JsonObject TypeDefinitionJson(ElementType t)
        {
            return new JsonObject
            {
                ["id"] = t.Id,
                ["name"] = t.Name,
                ["category"] = t.Category,
                ["subcategory"] = t.Subcategory,
                ["builder"] = t.Builder.Id,
                ["geometry"] = ElementGeometry.KindCode(t.Geometry),
                ["modelGroup"] = t.ModelGroup,
                ["parameters"] = new JsonArray(t.Parameters.Select(p => (JsonNode)Compact(new JsonObject
                {
                    ["key"] = p.Key,
                    ["label"] = p.Label,
                    ["type"] = ParameterDefinition.TypeCode(p.Type),
                    ["unit"] = p.Unit,
                    ["default"] = p.Default.ToJson(),
                    ["min"] = p.Min,
                    ["max"] = p.Max,
                    ["options"] = p.Options.Count > 0 ? new JsonArray(p.Options.Select(o => (JsonNode)JsonValue.Create(o)!).ToArray()) : null,
                    ["required"] = p.Required ? true : null,
                    ["group"] = p.Group,
                }, keepNullKeys: new[] { "default" })).ToArray()),
            };
        }

        private static JsonObject MaterialJson(MaterialDefinition m)
        {
            return Compact(new JsonObject
            {
                ["key"] = m.Key,
                ["name"] = m.Name,
                ["color"] = new JsonArray(m.Color.Select(c => (JsonNode)JsonValue.Create(c)).ToArray()),
                ["alpha"] = m.Alpha < 1 ? m.Alpha : null,
                ["texture"] = m.Texture,
                ["textureSize"] = m.TextureSize != null ? new JsonArray(m.TextureSize.Select(c => (JsonNode)JsonValue.Create(c)).ToArray()) : null,
            });
        }

        /// <summary>Removes null and empty-string properties so optional fields are omitted.</summary>
        private static JsonObject Compact(JsonObject o, string[]? keepNullKeys = null)
        {
            foreach (string key in o.Select(kv => kv.Key).ToList())
            {
                JsonNode? v = o[key];
                bool keep = keepNullKeys != null && keepNullKeys.Contains(key);
                if ((v == null && !keep) || (v is JsonValue jv && jv.TryGetValue(out string? s) && string.IsNullOrEmpty(s)))
                {
                    o.Remove(key);
                }
            }

            return o;
        }
    }
}
