using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Validation;

namespace SiteDesignPro.Core.Exchange
{
    /// <summary>One element change made in SketchUp (package frame, metres).</summary>
    public sealed class ElementChange
    {
        public string Id { get; set; } = string.Empty;

        public string TypeId { get; set; } = string.Empty;

        public string? Label { get; set; }

        /// <summary>Content hash of the element when it was imported into SketchUp.</summary>
        public string? BaseContentHash { get; set; }

        public ParameterSet Parameters { get; set; } = new ParameterSet();

        public Vec3? Position { get; set; }

        public double? Rotation { get; set; }
    }

    public sealed class ChangeSetDocument
    {
        public string ProjectId { get; set; } = string.Empty;

        public string? SourcePackageId { get; set; }

        public Vec3? Origin { get; set; }

        public List<ElementChange> Changes { get; } = new List<ElementChange>();
    }

    /// <summary>Outcome of applying one change.</summary>
    public enum ChangeOutcome
    {
        Applied,
        Conflict,
        Missing,
        Invalid,
        Unchanged,
    }

    /// <summary>
    /// Reads SketchUp change sets and merges them into drawing elements. The drawing-side adapter supplies
    /// the current elements; conflicts (element changed in AutoCAD since export) are reported, not overwritten,
    /// unless <c>force</c> is set.
    /// </summary>
    public static class ChangeSetReader
    {
        public const string FormatId = "site-design-pro-changes";

        public static ChangeSetDocument Read(string path) => Parse(File.ReadAllText(path));

        public static ChangeSetDocument Parse(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement r = doc.RootElement;
            if (!r.TryGetProperty("format", out JsonElement f) || f.GetString() != FormatId)
            {
                throw new FormatException("Datoteka nije datoteka izmjena programa Vanjsko uređenje PRO.");
            }

            string version = r.GetProperty("schemaVersion").GetString() ?? string.Empty;
            if (!version.StartsWith(PackageFormat.SupportedMajor.ToString(CultureInfo.InvariantCulture) + ".", StringComparison.Ordinal))
            {
                throw new FormatException("Nepodržana verzija datoteke izmjena " + version + ".");
            }

            JsonElement units = r.GetProperty("units");
            if (units.GetProperty("length").GetString() != "m")
            {
                throw new FormatException("Dužine u datoteci izmjena moraju biti u metrima.");
            }

            var result = new ChangeSetDocument
            {
                ProjectId = r.GetProperty("projectId").GetString() ?? string.Empty,
                SourcePackageId = r.TryGetProperty("sourcePackageId", out JsonElement sp) && sp.ValueKind == JsonValueKind.String ? sp.GetString() : null,
            };
            if (r.TryGetProperty("coordinateSystem", out JsonElement cs) && cs.TryGetProperty("origin", out JsonElement o) && o.ValueKind == JsonValueKind.Array)
            {
                result.Origin = new Vec3(o[0].GetDouble(), o[1].GetDouble(), o[2].GetDouble());
            }

            foreach (JsonElement c in r.GetProperty("changes").EnumerateArray())
            {
                var change = new ElementChange
                {
                    Id = c.GetProperty("id").GetString() ?? string.Empty,
                    TypeId = c.GetProperty("type").GetString() ?? string.Empty,
                    Label = c.TryGetProperty("label", out JsonElement l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null,
                    BaseContentHash = c.TryGetProperty("baseContentHash", out JsonElement h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null,
                    Parameters = c.TryGetProperty("parameters", out JsonElement p) ? ParameterSet.FromJson(p) : new ParameterSet(),
                };
                if (c.TryGetProperty("position", out JsonElement pos))
                {
                    var v = new Vec3(pos[0].GetDouble(), pos[1].GetDouble(), pos[2].GetDouble());
                    if (!v.IsFinite)
                    {
                        throw new FormatException("Izmjena za " + change.Id + " ima neispravan položaj.");
                    }

                    change.Position = v;
                }

                if (c.TryGetProperty("rotation", out JsonElement rot))
                {
                    change.Rotation = rot.GetDouble();
                }

                result.Changes.Add(change);
            }

            return result;
        }

        /// <summary>
        /// Applies a change to a drawing-frame element (in place). Positions are converted from the package frame
        /// using the project settings. Returns the outcome and a message.
        /// </summary>
        public static (ChangeOutcome Outcome, string Message) Apply(ElementChange change, SiteElement drawingElement, ProjectSettings settings, ElementCatalog catalog, bool force)
        {
            ElementType? type = catalog.FindType(drawingElement.TypeId);
            if (type == null)
            {
                return (ChangeOutcome.Invalid, "Nepoznat tip elementa " + drawingElement.TypeId + ".");
            }

            if (!force && change.BaseContentHash != null)
            {
                string current = ContentHasher.Hash(PackageWriter.ToPackageFrame(drawingElement, settings));
                if (!string.Equals(current, change.BaseContentHash, StringComparison.Ordinal))
                {
                    return (ChangeOutcome.Conflict, "Element je izmijenjen u AutoCAD-u nakon izvoza; izmjena iz SketchUp-a nije primijenjena.");
                }
            }

            var probe = new SiteElement(drawingElement.Id, drawingElement.TypeId, drawingElement.Geometry) { Parameters = drawingElement.Parameters.Clone() };
            foreach (KeyValuePair<string, ParamValue> kv in change.Parameters)
            {
                if (type.Parameter(kv.Key) == null)
                {
                    return (ChangeOutcome.Invalid, "Nepoznat parametar '" + kv.Key + "'.");
                }

                probe.Parameters[kv.Key] = kv.Value;
            }

            ValidationReport check = new ProjectValidator(catalog).Validate(new[] { PackageWriter.ToPackageFrame(probe, settings) });
            ValidationIssue? paramError = check.Issues.FirstOrDefault(i => i.Severity == Severity.Error && i.Code.StartsWith("SDP-P", StringComparison.Ordinal));
            if (paramError != null)
            {
                return (ChangeOutcome.Invalid, paramError.Message);
            }

            bool changed = false;
            foreach (KeyValuePair<string, ParamValue> kv in change.Parameters)
            {
                if (drawingElement.Parameters[kv.Key] != kv.Value)
                {
                    drawingElement.Parameters[kv.Key] = kv.Value;
                    changed = true;
                }
            }

            if ((change.Position.HasValue || change.Rotation.HasValue) && drawingElement.Geometry is PointGeometry pg)
            {
                Vec3 pos = pg.Position;
                if (change.Position.HasValue)
                {
                    Vec3 o = settings.OriginMeters;
                    double k = settings.Scale.DrawingUnitsPerMeter;
                    Vec3 p = change.Position.Value;
                    pos = new Vec3((p.X + o.X) * k, (p.Y + o.Y) * k, (p.Z + o.Z) * k);
                }

                double rotation = change.Rotation ?? pg.Rotation;
                if ((pos - pg.Position).Length > 1e-9 || Math.Abs(rotation - pg.Rotation) > 1e-12)
                {
                    drawingElement.Geometry = new PointGeometry(pos, rotation);
                    changed = true;
                }
            }
            else if (change.Position.HasValue || change.Rotation.HasValue)
            {
                return (ChangeOutcome.Invalid, "Promjena položaja podržana je samo za tačkaste elemente.");
            }

            if (!changed)
            {
                return (ChangeOutcome.Unchanged, "Već je ažurno.");
            }

            drawingElement.Revision++;
            return (ChangeOutcome.Applied, "Applied.");
        }
    }
}
