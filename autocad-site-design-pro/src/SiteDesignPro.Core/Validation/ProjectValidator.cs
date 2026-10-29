using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Validation
{
    /// <summary>Uniform grid for neighbourhood queries on plan bounding boxes (O(n) average for large projects).</summary>
    public sealed class SpatialGrid<T>
    {
        private readonly double cell;
        private readonly Dictionary<(long, long), List<(Box2 Box, T Item)>> cells = new Dictionary<(long, long), List<(Box2, T)>>();

        public SpatialGrid(double cellSize)
        {
            cell = Math.Max(0.5, cellSize);
        }

        public void Add(Box2 box, T item)
        {
            foreach ((long, long) key in Keys(box))
            {
                if (!cells.TryGetValue(key, out List<(Box2, T)>? list))
                {
                    list = new List<(Box2, T)>();
                    cells[key] = list;
                }

                list.Add((box, item));
            }
        }

        public IEnumerable<T> Query(Box2 box)
        {
            var seen = new HashSet<T>();
            foreach ((long, long) key in Keys(box))
            {
                if (!cells.TryGetValue(key, out List<(Box2 Box, T Item)>? list))
                {
                    continue;
                }

                foreach ((Box2 b, T item) in list)
                {
                    if (b.Intersects(box) && seen.Add(item))
                    {
                        yield return item;
                    }
                }
            }
        }

        private IEnumerable<(long, long)> Keys(Box2 b)
        {
            long x0 = (long)Math.Floor(b.MinX / cell), x1 = (long)Math.Floor(b.MaxX / cell);
            long y0 = (long)Math.Floor(b.MinY / cell), y1 = (long)Math.Floor(b.MaxY / cell);
            if ((x1 - x0 + 1) * (y1 - y0 + 1) > 100000)
            {
                throw new InvalidOperationException("Element extent is too large for the spatial index; check units.");
            }

            for (long x = x0; x <= x1; x++)
            {
                for (long y = y0; y <= y1; y++)
                {
                    yield return (x, y);
                }
            }
        }
    }

    /// <summary>
    /// Validates managed elements (package frame: metres). Distinguishes definite geometric/data errors,
    /// potential design conflicts and items that require professional review. No check is a compliance check.
    /// </summary>
    public sealed class ProjectValidator
    {
        private static readonly HashSet<string> SurfaceBuilders = new HashSet<string>(StringComparer.Ordinal)
        {
            "slab", "planting_area", "pool", "basin", "planter", "steps", "ramp", "massing",
        };

        private static readonly HashSet<string> RectangleBuilders = new HashSet<string>(StringComparer.Ordinal)
        {
            "pergola", "steps", "ramp", "stripes",
        };

        /// <summary>Builders whose footprints are fixed obstacles that also conflict with ground markings.</summary>
        private static readonly HashSet<string> ObstacleBuilders = new HashSet<string>(StringComparer.Ordinal)
        {
            "tree", "pole_mounted", "bench", "table", "shelter", "swing", "fountain", "boulder", "sign_post", "underground_container", "bike_stand",
        };

        private readonly ElementCatalog catalog;
        private readonly CheckSettings checks;

        public ProjectValidator(ElementCatalog catalog, CheckSettings? checks = null)
        {
            this.catalog = catalog;
            this.checks = checks ?? new CheckSettings();
        }

        /// <summary>Fills parameters missing from an element with the type defaults (in place).</summary>
        public static void FillDefaults(SiteElement e, ElementType type)
        {
            foreach (ParameterDefinition p in type.Parameters)
            {
                if (!e.Parameters.Contains(p.Key))
                {
                    e.Parameters[p.Key] = p.Default;
                }
            }
        }

        public ValidationReport Validate(IReadOnlyList<SiteElement> elements)
        {
            var report = new ValidationReport { ElementCount = elements.Count };
            var ids = new Dictionary<string, SiteElement>(StringComparer.Ordinal);
            var valid = new List<(SiteElement E, ElementType T)>();

            foreach (SiteElement e in elements)
            {
                int before = report.Issues.Count;
                if (!SiteElement.IsValidId(e.Id))
                {
                    report.Add(Severity.Error, IssueKind.DataError, "SDP-D001", null, "Element of type '" + e.TypeId + "' has a missing or malformed identifier.", "Run SDPAUDIT to assign a new stable identifier.");
                    continue;
                }

                if (ids.ContainsKey(e.Id))
                {
                    report.Add(new ValidationIssue(Severity.Error, IssueKind.DataError, "SDP-D002", e.Id, "Duplicate element identifier (usually an object copied with its data).", "Run SDPAUDIT; copies receive new identifiers and the original keeps its identifier.") { Label = e.Label });
                    continue;
                }

                ids[e.Id] = e;
                ElementType? type = catalog.FindType(e.TypeId);
                if (type == null)
                {
                    report.Add(new ValidationIssue(Severity.Error, IssueKind.DataError, "SDP-D003", e.Id, "Unsupported element type '" + e.TypeId + "'. It cannot be reconstructed in 3D.", "Install the user catalog that defines this type, or change the element type.") { Label = e.Label });
                    continue;
                }

                ValidateGeometry(e, type, report);
                ValidateParameters(e, type, report);
                ValidateLogic(e, type, report);
                foreach (ValidationIssue issue in report.Issues.Skip(before))
                {
                    issue.Label ??= e.Label;
                }

                if (!report.Issues.Skip(before).Any(i => i.Severity == Severity.Error))
                {
                    valid.Add((e, type));
                }
            }

            ValidateLabels(elements, report);
            CheckPointCollisions(valid, report);
            if (checks.CheckSurfaceOverlaps)
            {
                CheckSurfaceOverlaps(valid, report);
            }

            if (checks.CheckCanopyClearance)
            {
                CheckCanopies(valid, report);
            }

            foreach (ValidationIssue issue in report.Issues.Where(i => i.Label == null && i.ElementId != null && ids.ContainsKey(i.ElementId)))
            {
                issue.Label = ids[issue.ElementId!].Label;
            }

            return report;
        }

        private void ValidateGeometry(SiteElement e, ElementType type, ValidationReport report)
        {
            if (e.Geometry.Kind != type.Geometry)
            {
                report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G001", e.Id,
                    "Geometry is a " + ElementGeometry.KindCode(e.Geometry.Kind) + " but type '" + type.Name + "' needs a " + ElementGeometry.KindCode(type.Geometry) + ".",
                    type.Geometry == GeometryKind.Polygon ? "Use a closed polyline boundary." : type.Geometry == GeometryKind.Path ? "Use a polyline alignment." : "Use a point insertion.");
                return;
            }

            switch (e.Geometry)
            {
                case PointGeometry p:
                    if (!p.Position.IsFinite || !Compat.IsFinite(p.Rotation))
                    {
                        report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G002", e.Id, "Insertion point or rotation is not a finite number.", "Move or re-insert the element.");
                    }
                    else
                    {
                        CheckElevation(e, p.Position.Z, report);
                    }

                    break;
                case PathGeometry path:
                    {
                        if (path.Ring.Vertices.Any(v => !v.IsFinite))
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G002", e.Id, "Path contains non-finite coordinates.", "Redraw the alignment.");
                            break;
                        }

                        Ring cleaned = GeometryMath.RemoveDuplicateVertices(path.Ring, path.Closed);
                        if (cleaned.Count < 2 || GeometryMath.Length(cleaned, path.Closed) < 1e-4)
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G003", e.Id, "Path has zero length.", "Redraw the alignment with at least two distinct vertices.");
                            break;
                        }

                        if (cleaned.Count != path.Ring.Count)
                        {
                            report.Add(Severity.Warning, IssueKind.GeometricError, "SDP-G004", e.Id, "Path contains duplicate consecutive vertices.", "Run SDPAUDIT or PEDIT to remove duplicate vertices; they are ignored during export.");
                        }

                        CheckElevation(e, path.Ring.Vertices.Average(v => v.Z), report);
                        break;
                    }

                case PolygonGeometry poly:
                    {
                        if (poly.Outer.Vertices.Any(v => !v.IsFinite))
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G002", e.Id, "Boundary contains non-finite coordinates.", "Redraw the boundary.");
                            break;
                        }

                        Ring outer = GeometryMath.RemoveDuplicateVertices(poly.Outer, true);
                        if (outer.Count < 3 && !(outer.Count == 2 && outer.HasArcs))
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G005", e.Id, "Boundary has fewer than three distinct vertices.", "Redraw the area as a closed polyline with at least three vertices.");
                            break;
                        }

                        if (GeometryMath.IsSelfIntersecting(outer))
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G007", e.Id, "Boundary intersects itself.", "Edit the boundary so its edges do not cross (split figure-of-eight shapes into separate areas).");
                            break;
                        }

                        double area = Math.Abs(GeometryMath.SignedArea(outer));
                        if (area < 1e-6)
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G006", e.Id, "Boundary encloses no area.", "Redraw the boundary; vertices must not be collinear.");
                            break;
                        }

                        List<Vec2> outerPts = GeometryMath.Tessellate(outer, true).Select(v => v.XY).ToList();
                        foreach (Ring hole in poly.Holes)
                        {
                            List<Vec2> holePts = GeometryMath.Tessellate(hole, true).Select(v => v.XY).ToList();
                            Vec2? ip = GeometryMath.InteriorPoint(holePts);
                            if (holePts.Count < 3 || ip == null || !GeometryMath.PointInPolygon(ip.Value, outerPts))
                            {
                                report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G008", e.Id, "An opening (hole) lies outside the boundary or is degenerate.", "Redraw the opening inside the outer boundary.");
                            }
                        }

                        if (RectangleBuilders.Contains(type.Builder.Id) && !GeometryMath.IsRectangle(outer))
                        {
                            report.Add(Severity.Info, IssueKind.GeometricError, "SDP-G009", e.Id,
                                type.Name + " is generated on the oriented bounding rectangle of a non-rectangular boundary.",
                                "Draw a rectangle whose first edge defines the orientation if the exact outline matters.");
                        }

                        if (type.Builder.Id == "sail" && outer.Count != 3 && outer.Count != 4)
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G011", e.Id, "A shade sail needs exactly 3 or 4 corner points (found " + outer.Count + ").", "Redraw the sail outline with 3 or 4 vertices.");
                        }

                        CheckElevation(e, outer.Vertices.Average(v => v.Z), report);
                        break;
                    }
            }
        }

        private void CheckElevation(SiteElement e, double z, ValidationReport report)
        {
            if (Math.Abs(z) > checks.ElevationRange)
            {
                report.Add(Severity.Warning, IssueKind.DataError, "SDP-G012", e.Id,
                    string.Format(CultureInfo.InvariantCulture, "Elevation {0:0.###} m relative to the project origin is outside +/-{1:0} m.", z, checks.ElevationRange),
                    "Check the project origin elevation and the drawing units (a millimetre drawing exported as metres gives 1000x values).");
            }
        }

        private void ValidateParameters(SiteElement e, ElementType type, ValidationReport report)
        {
            foreach (ParameterDefinition def in type.Parameters)
            {
                ParamValue v = e.Parameters.Contains(def.Key) ? e.Parameters[def.Key] : def.Default;
                if (v.IsNull)
                {
                    if (def.Required)
                    {
                        report.Add(Severity.Error, IssueKind.DataError, "SDP-P001", e.Id, "Required parameter '" + def.Label + "' is not set.", "Enter a value with SDPEDIT or the properties palette.");
                    }

                    continue;
                }

                switch (def.Type)
                {
                    case ParameterType.Length:
                    case ParameterType.Number:
                    case ParameterType.Integer:
                    case ParameterType.Angle:
                        {
                            double d = v.AsNumber();
                            if (v.Kind != ParamKind.Number || !Compat.IsFinite(d))
                            {
                                report.Add(Severity.Error, IssueKind.DataError, "SDP-P002", e.Id, "Parameter '" + def.Label + "' must be a number (found '" + v.AsText() + "').", "Enter a numeric value.");
                                continue;
                            }

                            if (def.Type == ParameterType.Integer && Math.Abs(d - Math.Round(d)) > 1e-9)
                            {
                                report.Add(Severity.Error, IssueKind.DataError, "SDP-P003", e.Id, "Parameter '" + def.Label + "' must be a whole number.", "Enter an integer value.");
                            }

                            if ((def.Min.HasValue && d < def.Min.Value - 1e-12) || (def.Max.HasValue && d > def.Max.Value + 1e-12))
                            {
                                report.Add(Severity.Error, IssueKind.DataError, "SDP-P004", e.Id,
                                    string.Format(CultureInfo.InvariantCulture, "Parameter '{0}' = {1} {2} is outside the valid range {3} to {4}.", def.Label, d, def.DisplayUnit, def.Min?.ToString(CultureInfo.InvariantCulture) ?? "-inf", def.Max?.ToString(CultureInfo.InvariantCulture) ?? "+inf"),
                                    "Enter a value inside the valid range. If the value is in millimetres, convert it to metres.");
                            }

                            break;
                        }

                    case ParameterType.Bool:
                        if (v.Kind != ParamKind.Bool)
                        {
                            report.Add(Severity.Error, IssueKind.DataError, "SDP-P005", e.Id, "Parameter '" + def.Label + "' must be true or false.", "Choose Yes or No.");
                        }

                        break;
                    case ParameterType.Enum:
                        if (!def.Options.Contains(v.AsText()))
                        {
                            report.Add(Severity.Error, IssueKind.DataError, "SDP-P006", e.Id, "Parameter '" + def.Label + "' has an invalid option '" + v.AsText() + "'. Valid: " + string.Join(", ", def.Options) + ".", "Choose one of the listed options.");
                        }

                        break;
                    case ParameterType.Material:
                        if (catalog.FindMaterial(v.AsText()) == null)
                        {
                            report.Add(Severity.Warning, IssueKind.DataError, "SDP-P007", e.Id, "Material '" + v.AsText() + "' is not in the material library; the default material is used.", "Choose a library material or add it to a user materials file.");
                        }

                        break;
                }
            }

            foreach (string key in e.Parameters.Keys.Where(k => type.Parameter(k) == null))
            {
                report.Add(Severity.Info, IssueKind.DataError, "SDP-P008", e.Id, "Parameter '" + key + "' is not defined for type '" + type.Name + "' and is ignored.", "Move project-specific values to custom fields.");
            }

            foreach (string req in type.ReviewRequired)
            {
                bool missing;
                string label;
                if (req.StartsWith("prop:", StringComparison.Ordinal))
                {
                    string key = req.Substring(5);
                    missing = string.IsNullOrWhiteSpace(e.Property(key));
                    PropertyDefinition? pd = catalog.CommonProperties.FirstOrDefault(p => p.Key == key);
                    label = pd?.Label ?? key;
                }
                else
                {
                    ParamValue v = e.Parameters.Contains(req) ? e.Parameters[req] : type.Parameter(req)?.Default ?? ParamValue.Null;
                    missing = v.IsNull || (v.Kind == ParamKind.Text && string.IsNullOrWhiteSpace(v.AsText()));
                    label = type.Parameter(req)?.Label ?? req;
                }

                if (missing)
                {
                    report.Add(Severity.Review, IssueKind.ProfessionalReview, "SDP-R001", e.Id,
                        "'" + label + "' has not been defined for " + type.Name + ". Default dimensions and clearances are indicative only.",
                        "Enter the value from the manufacturer, botanical source or applicable standard and record the source.");
                }
            }
        }

        private void ValidateLogic(SiteElement e, ElementType type, ValidationReport report)
        {
            ParameterSet p = e.Parameters;
            double N(string k) => p.Contains(k) ? p.Number(k) : type.Parameter(k)?.Default.AsNumber() ?? double.NaN;
            bool B(string k) => p.Contains(k) ? p.Bool(k) : type.Parameter(k)?.Default.AsBool() ?? false;
            switch (type.Builder.Id)
            {
                case "linear_slab":
                    {
                        double req = N("requiredClearWidth");
                        if (Compat.IsFinite(req) && N("width") + 1e-9 < req)
                        {
                            report.Add(Severity.Warning, IssueKind.DesignConflict, "SDP-C010", e.Id,
                                string.Format(CultureInfo.InvariantCulture, "Width {0:0.###} m is less than the configured required clear width {1:0.###} m.", N("width"), req),
                                "Widen the route or confirm the requirement. This compares project values only and is not a regulatory assessment.");
                        }

                        break;
                    }

                case "ramp":
                    if (e.Geometry is PolygonGeometry rp && Compat.IsFinite(N("maxSlope")))
                    {
                        OrientedRect r = GeometryMath.OrientedBoundingRect(rp.Outer);
                        double slope = r.LengthV > 1e-9 ? N("rise") / r.LengthV * 100 : double.PositiveInfinity;
                        if (slope > N("maxSlope") + 1e-9)
                        {
                            report.Add(Severity.Warning, IssueKind.DesignConflict, "SDP-C011", e.Id,
                                string.Format(CultureInfo.InvariantCulture, "Ramp slope {0:0.0}% exceeds the configured maximum {1:0.0}%.", slope, N("maxSlope")),
                                "Lengthen the ramp or reduce the rise. Verify against the applicable accessibility regulation.");
                        }
                    }

                    break;
                case "pool":
                    if (N("waterLevelOffset") >= N("depth"))
                    {
                        report.Add(Severity.Error, IssueKind.DataError, "SDP-P010", e.Id, "Water level is at or below the pool floor.", "Reduce 'Water level below coping' or increase the depth.");
                    }

                    break;
                case "chamber":
                    if (N("coverSize") > N("innerSize"))
                    {
                        report.Add(Severity.Warning, IssueKind.DataError, "SDP-P011", e.Id, "Cover opening is larger than the chamber's internal size.", "Check the chamber and cover dimensions.");
                    }

                    break;
                case "underground_container":
                    if (p.Text("installation", type.Parameter("installation")?.Default.AsText() ?? "underground") == "underground" && N("columnDiameter") > Math.Min(N("bodyLength"), N("bodyWidth")))
                    {
                        report.Add(Severity.Warning, IssueKind.DataError, "SDP-P012", e.Id, "Insertion column is wider than the container body.", "Check the container dimensions against the manufacturer data.");
                    }

                    break;
                case "tree":
                    if (N("clearTrunkHeight") >= N("height"))
                    {
                        report.Add(Severity.Error, IssueKind.DataError, "SDP-P013", e.Id, "Clear trunk height is not lower than the tree height.", "Reduce the clear trunk height or increase the height.");
                    }

                    if (Compat.IsFinite(N("matureHeight")) && N("matureHeight") < N("height"))
                    {
                        report.Add(Severity.Warning, IssueKind.DataError, "SDP-P014", e.Id, "Mature height is lower than the planting height.", "Check the growth data source.");
                    }

                    break;
                case "bench":
                    if (B("backrest") && N("backHeight") <= N("seatHeight"))
                    {
                        report.Add(Severity.Error, IssueKind.DataError, "SDP-P015", e.Id, "Backrest height is not above the seat height.", "Increase the overall height with backrest.");
                    }

                    break;
                case "fence":
                    if (N("postSpacing") <= N("postSize"))
                    {
                        report.Add(Severity.Error, IssueKind.DataError, "SDP-P016", e.Id, "Post spacing must be larger than the post size.", "Increase the post spacing.");
                    }

                    break;
                case "wall":
                    if (N("footingWidth") > 0 && N("footingDepth") <= 0)
                    {
                        report.Add(Severity.Warning, IssueKind.DataError, "SDP-P017", e.Id, "Footing width is set but footing depth is zero.", "Enter a footing depth or set the footing width to 0.");
                    }

                    break;
                case "basin":
                    if (N("waterDepth") > N("depth"))
                    {
                        report.Add(Severity.Error, IssueKind.DataError, "SDP-P018", e.Id, "Design water depth exceeds the basin depth.", "Reduce the water depth or deepen the basin.");
                    }

                    break;
                case "sail":
                    if (N("highHeight") < N("lowHeight"))
                    {
                        report.Add(Severity.Warning, IssueKind.DataError, "SDP-P019", e.Id, "High corner height is lower than the low corner height.", "Swap the values.");
                    }

                    break;
            }
        }

        private static void ValidateLabels(IReadOnlyList<SiteElement> elements, ValidationReport report)
        {
            foreach (IGrouping<string, SiteElement> g in elements.Where(e => !string.IsNullOrWhiteSpace(e.Label)).GroupBy(e => e.Label!, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                foreach (SiteElement e in g.Skip(1))
                {
                    report.Add(new ValidationIssue(Severity.Warning, IssueKind.DataError, "SDP-D010", e.Id, "Label '" + g.Key + "' is used by " + g.Count() + " elements.", "Run SDPNUMBER to renumber, or edit the label.") { Label = e.Label, RelatedElementId = g.First().Id });
                }
            }
        }

        private void CheckPointCollisions(List<(SiteElement E, ElementType T)> valid, ValidationReport report)
        {
            var items = new List<(SiteElement E, ElementType T, Footprint F)>();
            foreach ((SiteElement e, ElementType t) in valid)
            {
                if (e.Geometry is PointGeometry pg)
                {
                    foreach (Footprint f in Footprints.For(t.Builder.Id, pg, e.Parameters))
                    {
                        items.Add((e, t, f));
                    }
                }
            }

            if (items.Count == 0)
            {
                return;
            }

            var grid = new SpatialGrid<int>(5.0);
            for (int i = 0; i < items.Count; i++)
            {
                grid.Add(items[i].F.Bounds(), i);
            }

            var reported = new HashSet<(string, string)>();
            for (int i = 0; i < items.Count; i++)
            {
                (SiteElement a, ElementType ta, Footprint fa) = items[i];
                foreach (int j in grid.Query(fa.Bounds().Inflate(checks.CollisionTolerance)))
                {
                    if (j <= i)
                    {
                        continue;
                    }

                    (SiteElement b, ElementType tb, Footprint fb) = items[j];
                    if (a.Id == b.Id || !ZonesInteract(fa.Zone, ta, fb.Zone, tb))
                    {
                        continue;
                    }

                    if (fa.Inflate(checks.CollisionTolerance / 2).Intersects(fb.Inflate(checks.CollisionTolerance / 2)))
                    {
                        (string, string) key = string.CompareOrdinal(a.Id, b.Id) < 0 ? (a.Id, b.Id) : (b.Id, a.Id);
                        if (reported.Add(key))
                        {
                            report.Add(new ValidationIssue(Severity.Warning, IssueKind.DesignConflict, "SDP-C001", a.Id,
                                "Potential collision between " + Describe(a, ta) + " and " + Describe(b, tb) + (fa.Zone == FootprintZone.Underground ? " (underground)." : "."),
                                "Move one of the elements or confirm that the overlap is intended.") { RelatedElementId = b.Id });
                        }
                    }
                }
            }

            // Clearance envelopes (service / safety / lifting) entered by the user.
            foreach ((SiteElement e, ElementType t) in valid)
            {
                if (!(e.Geometry is PointGeometry pg))
                {
                    continue;
                }

                foreach (string key in new[] { "serviceClearance", "safetyClearance", "liftingClearance" })
                {
                    double clearance = e.Parameters.Number(key, double.NaN);
                    if (!Compat.IsFinite(clearance) || clearance <= 0)
                    {
                        continue;
                    }

                    foreach (Footprint own in Footprints.For(t.Builder.Id, pg, e.Parameters).Where(f => f.Zone != FootprintZone.Underground))
                    {
                        Footprint zone = own.Inflate(clearance);
                        foreach (int j in grid.Query(zone.Bounds()))
                        {
                            (SiteElement o, ElementType to, Footprint fo) = items[j];
                            if (o.Id == e.Id || fo.Zone == FootprintZone.Underground || fo.Zone == FootprintZone.Marking)
                            {
                                continue;
                            }

                            if (zone.Intersects(fo))
                            {
                                (string, string) pair = (e.Id + "#" + key, o.Id);
                                if (reported.Add(pair))
                                {
                                    report.Add(new ValidationIssue(Severity.Warning, IssueKind.DesignConflict, "SDP-C002", e.Id,
                                        string.Format(CultureInfo.InvariantCulture, "{0} lies within the {1:0.##} m {2} of {3}.", Describe(o, to), clearance, Humanize(key), Describe(e, t)),
                                        "Relocate the element or reduce the clearance only if the source data allows it.") { RelatedElementId = o.Id });
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool ZonesInteract(FootprintZone a, ElementType ta, FootprintZone b, ElementType tb)
        {
            if (a == FootprintZone.Underground || b == FootprintZone.Underground)
            {
                return a == b;
            }

            if (a == FootprintZone.Marking && b == FootprintZone.Marking)
            {
                return true;
            }

            if (a == FootprintZone.Marking)
            {
                return ObstacleBuilders.Contains(tb.Builder.Id);
            }

            if (b == FootprintZone.Marking)
            {
                return ObstacleBuilders.Contains(ta.Builder.Id);
            }

            return true;
        }

        private static string Humanize(string key) => key switch
        {
            "serviceClearance" => "service clearance",
            "safetyClearance" => "safety clearance",
            "liftingClearance" => "lifting clearance",
            _ => key,
        };

        private static string Describe(SiteElement e, ElementType t) => (string.IsNullOrEmpty(e.Label) ? t.Name : e.Label + " (" + t.Name + ")");

        private void CheckSurfaceOverlaps(List<(SiteElement E, ElementType T)> valid, ValidationReport report)
        {
            var polys = new List<(SiteElement E, ElementType T, IReadOnlyList<IReadOnlyList<Vec2>> Rings, Box2 Box)>();
            foreach ((SiteElement e, ElementType t) in valid)
            {
                if (e.Geometry is PolygonGeometry pg && SurfaceBuilders.Contains(t.Builder.Id))
                {
                    var rings = new List<IReadOnlyList<Vec2>> { GeometryMath.PlanOutline(pg, 0.05) };
                    rings.AddRange(pg.Holes.Select(h => (IReadOnlyList<Vec2>)GeometryMath.Tessellate(h, true, 0.05).Select(v => v.XY).ToList()));
                    polys.Add((e, t, rings, GeometryMath.BoundsOf(rings[0])));
                }
            }

            var grid = new SpatialGrid<int>(20.0);
            for (int i = 0; i < polys.Count; i++)
            {
                grid.Add(polys[i].Box, i);
            }

            for (int i = 0; i < polys.Count; i++)
            {
                foreach (int j in grid.Query(polys[i].Box))
                {
                    if (j <= i)
                    {
                        continue;
                    }

                    if (GeometryMath.AreasOverlap(polys[i].Rings, polys[j].Rings))
                    {
                        bool building = polys[i].T.Builder.Id == "massing" || polys[j].T.Builder.Id == "massing";
                        report.Add(new ValidationIssue(Severity.Warning, IssueKind.DesignConflict, building ? "SDP-C004" : "SDP-C003", polys[i].E.Id,
                            (building ? "Area overlaps a building footprint: " : "Overlapping surfaces: ") + Describe(polys[i].E, polys[i].T) + " and " + Describe(polys[j].E, polys[j].T) + ".",
                            "Trim one boundary so surfaces share edges without overlapping, or confirm the layering is intended (e.g. a deck over another surface).") { RelatedElementId = polys[j].E.Id });
                    }
                }
            }
        }

        private void CheckCanopies(List<(SiteElement E, ElementType T)> valid, ValidationReport report)
        {
            var trees = valid.Where(v => v.T.Builder.Id == "tree" && v.E.Geometry is PointGeometry).ToList();
            if (trees.Count == 0)
            {
                return;
            }

            var buildings = valid.Where(v => v.T.Builder.Id == "massing" && v.E.Geometry is PolygonGeometry)
                .Select(v => (v.E, v.T, Pts: GeometryMath.PlanOutline((PolygonGeometry)v.E.Geometry, 0.05)))
                .ToList();
            foreach ((SiteElement tree, ElementType tt) in trees)
            {
                double mature = tree.Parameters.Number("matureCanopyDiameter", double.NaN);
                if (!Compat.IsFinite(mature))
                {
                    continue;
                }

                Vec2 pos = ((PointGeometry)tree.Geometry).Position.XY;
                double r = mature / 2;
                foreach ((SiteElement b, ElementType bt, IReadOnlyList<Vec2> pts) in buildings)
                {
                    bool inside = GeometryMath.PointInPolygon(pos, pts);
                    double d = GeometryMath.DistanceToBoundary(pos, pts);
                    if (inside || d < r)
                    {
                        report.Add(new ValidationIssue(Severity.Warning, IssueKind.DesignConflict, "SDP-C005", tree.Id,
                            string.Format(CultureInfo.InvariantCulture, "Mature canopy (diameter {0:0.#} m) of {1} reaches {2} (distance {3:0.##} m).", mature, Describe(tree, tt), Describe(b, bt), inside ? 0 : d),
                            "Move the tree or select a species with a smaller mature canopy.") { RelatedElementId = b.Id });
                    }
                }
            }
        }
    }
}
