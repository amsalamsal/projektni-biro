using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Elevation;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Terrain;
using SiteDesignPro.Core.Text;
using SiteDesignPro.Core.Units;

namespace SiteDesignPro.Core.Input
{
    /// <summary>
    /// Host-independent steps of the DWG → model workflow, shared by the AutoCAD commands and the tests:
    /// elevation-label targets, terrain sources from mapped layers, and conversions between elevation frames.
    /// All results are in metres in the drawing frame (drawing coordinates × metres per unit).
    /// </summary>
    public static class SiteWorkflow
    {
        /// <summary>Roles whose closed outlines may receive an elevation from a label.</summary>
        public static readonly SourceRole[] TargetRoles = { SourceRole.BuildingFootprint, SourceRole.Roads, SourceRole.ParterreLine, SourceRole.ExistingInfrastructure };

        public static double MetersPerUnit(LengthUnit unit) => UnitConversion.MetersPerUnit(unit);

        /// <summary>Elevation labels of the mapped label layers (metres).</summary>
        public static List<SourceLabel> Labels(IEnumerable<SourceObject> objects, LayerMapping mapping, double k)
        {
            return objects.Where(o => o.Kind == SourceKind.Text && mapping.RolesOf(o.Layer).Contains(SourceRole.ElevationLabels) && o.Vertices.Count > 0)
                .Select(o => new SourceLabel
                {
                    Handle = o.Handle,
                    Text = o.Text,
                    Position = Scale(o.Vertices[0], k),
                    Target = o.Target.HasValue ? Scale(o.Target.Value, k) : (Vec3?)null,
                }).ToList();
        }

        /// <summary>Closed outlines on target roles (metres), each a candidate for a label's elevation.</summary>
        public static List<AssignmentTarget> Targets(IEnumerable<SourceObject> objects, LayerMapping mapping, double k)
        {
            var list = new List<AssignmentTarget>();
            foreach (SourceObject o in objects)
            {
                if (!o.Closed || o.Vertices.Count < 2)
                {
                    continue;
                }

                List<SourceRole> roles = mapping.RolesOf(o.Layer);
                SourceRole? role = TargetRoles.Cast<SourceRole?>().FirstOrDefault(r => roles.Contains(r!.Value));
                if (!role.HasValue)
                {
                    continue;
                }

                var ring = new Ring(o.Vertices.Select(v => Scale(v, k)).ToList(), o.Bulges);
                double area = Math.Abs(GeometryMath.SignedArea(ring));
                list.Add(new AssignmentTarget
                {
                    Handle = o.Handle,
                    Description = SourceRoles.Name(role.Value) + " (" + o.Layer + ", " + o.Handle + ")",
                    Boundary = ring,
                    Closed = true,
                    Area = area,
                });
            }

            return list;
        }

        /// <summary>
        /// Terrain sources from mapped survey points, contours and breaklines (Z as drawn), optionally plus elevation
        /// labels used as spot heights. Labels are converted to the drawing's Z frame through the datum; labels that
        /// cannot be converted (no datum, ambiguous text) are reported, never guessed.
        /// </summary>
        public static (TerrainSources Sources, List<string> Notes) TerrainSourcesFrom(IEnumerable<SourceObject> objects, LayerMapping mapping, ElevationDatum datum,
            double k, bool labelsAsSpotHeights, IReadOnlyList<Vec2>? boundary = null)
        {
            var s = new TerrainSources { Boundary = boundary };
            var notes = new List<string>();
            int zeroPoints = 0;
            foreach (SourceObject o in objects)
            {
                List<SourceRole> roles = mapping.RolesOf(o.Layer);
                if (roles.Contains(SourceRole.TerrainPoints) && (o.Kind == SourceKind.Point || o.Kind == SourceKind.Block) && o.Vertices.Count > 0)
                {
                    Vec3 p = Scale(o.Vertices[0], k);
                    if (Math.Abs(p.Z) < 1e-9)
                    {
                        zeroPoints++;
                        continue; // Z = 0 is not taken as an elevation
                    }

                    s.Points.Add(p);
                }
                else if (roles.Contains(SourceRole.Contours) && o.Vertices.Count >= 2)
                {
                    if (o.Vertices.All(v => Math.Abs(v.Z) < 1e-9))
                    {
                        notes.Add(Bs.T("Izohipsa {0} nema visinu i nije korištena.", o.Handle));
                        continue;
                    }

                    s.Contours.Add(Densify(o, k));
                }
                else if (roles.Contains(SourceRole.Breaklines) && o.Vertices.Count >= 2)
                {
                    s.Breaklines.Add(Densify(o, k));
                }
            }

            if (zeroPoints > 0)
            {
                notes.Add(Bs.T("{0} tačaka terena ima Z = 0 i nisu korištene (visina se ne pretpostavlja).", zeroPoints));
            }

            if (labelsAsSpotHeights)
            {
                int used = 0, skipped = 0;
                foreach (SourceLabel l in Labels(objects, mapping, k))
                {
                    ParsedElevation p = ElevationLabelParser.Parse(l.Text);
                    double? z = DrawingZ(p, datum);
                    if (z.HasValue)
                    {
                        Vec3 at = l.Target ?? l.Position;
                        s.Points.Add(new Vec3(at.X, at.Y, z.Value));
                        used++;
                    }
                    else
                    {
                        skipped++;
                    }
                }

                notes.Add(Bs.T("Oznake kota kao visinske tačke: korišteno {0}, preskočeno {1} (nejasne ili bez nulte kote).", used, skipped));
            }

            return (s, notes);
        }

        /// <summary>Label value in the drawing's Z frame (metres), or null when it cannot be determined.</summary>
        public static double? DrawingZ(ParsedElevation p, ElevationDatum datum)
        {
            try
            {
                if (p.Kind == LabelKind.Absolute && p.Absolute.HasValue)
                {
                    return datum.DrawingZ == DrawingZMode.Absolute ? p.Absolute.Value : datum.ToRelative(p.Absolute.Value);
                }

                if (p.Kind == LabelKind.Relative && p.Relative.HasValue)
                {
                    return datum.DrawingZFromRelative(p.Relative.Value);
                }
            }
            catch (DatumNotDefinedException)
            {
            }

            return null;
        }

        private static IReadOnlyList<Vec3> Densify(SourceObject o, double k)
        {
            List<Vec3> pts = GeometryMath.Tessellate(o.ToRing(), o.Closed, 0.05 / k);
            List<Vec3> scaled = pts.Select(v => Scale(v, k)).ToList();
            if (o.Closed && scaled.Count > 2 && (scaled[0] - scaled[scaled.Count - 1]).Length > 1e-9)
            {
                scaled.Add(scaled[0]);
            }

            return scaled;
        }

        private static Vec3 Scale(Vec3 v, double k) => new Vec3(v.X * k, v.Y * k, v.Z * k);
    }
}
