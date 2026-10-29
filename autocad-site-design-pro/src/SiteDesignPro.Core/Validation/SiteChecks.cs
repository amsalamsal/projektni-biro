using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Elevation;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.Core.Validation
{
    /// <summary>
    /// Project-level site checks on package-frame elements: objects outside the parcel, roads above the parterre and
    /// object geometry that no longer matches its assigned elevation. Software warnings, not regulatory approval.
    /// </summary>
    public static class SiteChecks
    {
        public const double Tolerance = 0.01;

        public static ValidationReport Run(IReadOnlyList<SiteElement> elements, ElementCatalog catalog, ProjectSettings settings)
        {
            var r = new ValidationReport();
            OutsideParcel(elements, r);
            Elevations(elements, catalog, settings, r);
            return r;
        }

        private static void OutsideParcel(IReadOnlyList<SiteElement> elements, ValidationReport r)
        {
            List<IReadOnlyList<Vec2>> parcels = elements.Where(e => e.TypeId == "site.boundary" && e.Geometry is PolygonGeometry)
                .Select(e => GeometryMath.PlanOutline((PolygonGeometry)e.Geometry, 0.05)).ToList();
            if (parcels.Count == 0)
            {
                return;
            }

            foreach (SiteElement e in elements.Where(e => e.TypeId != "site.boundary"))
            {
                IEnumerable<Vec2> pts = e.Geometry switch
                {
                    PointGeometry p => new[] { p.Position.XY },
                    PathGeometry pa => GeometryMath.Tessellate(pa.Ring, pa.Closed, 0.05).Select(v => v.XY),
                    PolygonGeometry pg => GeometryMath.PlanOutline(pg, 0.05),
                    _ => Enumerable.Empty<Vec2>(),
                };
                bool inside = pts.All(v => parcels.Any(pl => GeometryMath.PointInPolygon(v, pl) || GeometryMath.DistanceToBoundary(v, pl) <= Tolerance));
                if (!inside)
                {
                    r.Add(new ValidationIssue(Severity.Warning, IssueKind.DesignConflict, "SDP-C020", e.Id, "Element izlazi izvan parcelne granice.",
                        "Provjerite položaj elementa ili granicu parcele.") { Label = e.Label });
                }
            }
        }

        private static void Elevations(IReadOnlyList<SiteElement> elements, ElementCatalog catalog, ProjectSettings s, ValidationReport r)
        {
            ElevationDatum d = s.Elevations;
            double originZ = s.OriginMeters.Z;
            foreach (SiteElement e in elements)
            {
                ElevationAssignment? a = ElevationAssignment.ReadFrom(e.Properties);
                if (a == null || !e.Properties.TryGetValue(ElevationAssignment.PropRelative, out string? relText) ||
                    !double.TryParse(relText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double rel))
                {
                    continue;
                }

                ElementType? t = catalog.FindType(e.TypeId);
                bool road = t?.Category == "roads" || a.Mode == ElevationMode.RoadOffset; // e.g. asphalt surfaces levelled as roads
                if (road && rel > d.ParterreRelativeElevation + Tolerance)
                {
                    r.Add(new ValidationIssue(Severity.Warning, IssueKind.DesignConflict, "SDP-E010", e.Id,
                        Bs.T("Saobraćajnica ({0}) je iznad partera ({1}).", ElevationDatum.FormatRelative(rel), ElevationDatum.FormatRelative(d.ParterreRelativeElevation)),
                        "Provjerite dodijeljenu kotu saobraćajnice i kotu partera (SDPDATUM).") { Label = e.Label });
                }

                double? z = PlanZ(e.Geometry);
                if (!z.HasValue)
                {
                    continue;
                }

                try
                {
                    double current = d.RelativeFromDrawingZ(z.Value + originZ);
                    if (Math.Abs(current - rel) > Tolerance)
                    {
                        r.Add(new ValidationIssue(Severity.Warning, IssueKind.DataError, "SDP-E012", e.Id,
                            Bs.T("Kota objekta ({0}) ne odgovara dodijeljenoj koti ({1}); objekat je vjerovatno pomjeren nakon dodjele.",
                                ElevationDatum.FormatRelative(current), ElevationDatum.FormatRelative(rel)),
                            "Ponovo dodijelite kotu (SDPKOTA) ili vratite objekat na dodijeljenu visinu.") { Label = e.Label });
                    }
                }
                catch (DatumNotDefinedException)
                {
                    r.Add(new ValidationIssue(Severity.Warning, IssueKind.DataError, "SDP-E011", e.Id,
                        "Objekat ima dodijeljenu kotu, a nulta kota objekta više nije definisana.", "Definišite nultu kotu (SDPDATUM).") { Label = e.Label });
                }
            }
        }

        /// <summary>Elevation of a flat object (first vertex / insertion point), package frame.</summary>
        private static double? PlanZ(ElementGeometry g) => g switch
        {
            PointGeometry p => p.Position.Z,
            PathGeometry pa when pa.Ring.Count > 0 => pa.Ring.Vertices[0].Z,
            PolygonGeometry pg when pg.Outer.Count > 0 => pg.Outer.Vertices[0].Z,
            _ => null,
        };
    }
}
