using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Labels;
using SiteDesignPro.Core.Layout;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Samples
{
    /// <summary>
    /// Builds the reference sample: a 120 m x 80 m residential complex with two apartment buildings, access road,
    /// parking (standard, accessible and EV bays), paving, a courtyard with an arched edge, lawn with a playground
    /// opening, planting beds, trees, lights, benches, bicycle racks, underground waste containers, drainage,
    /// fences, a gate and a pergola. Element ids are deterministic so repeated generation produces identical data.
    /// Coordinates are produced in a millimetre drawing with a georeferenced project origin.
    /// </summary>
    public sealed class SampleResidentialComplex
    {
        /// <summary>Project origin (metres): easting, northing, elevation.</summary>
        public static readonly Vec3 OriginMeters = new Vec3(6550000.0, 4860000.0, 520.0);

        private readonly ElementCatalog catalog;
        private readonly List<SiteElement> elements = new List<SiteElement>();
        private int counter;

        private SampleResidentialComplex(ElementCatalog catalog)
        {
            this.catalog = catalog;
        }

        public static ProjectSettings CreateSettings()
        {
            return new ProjectSettings
            {
                ProjectId = DeterministicId("project"),
                Name = "Sample Residential Complex",
                Number = "SDP-SAMPLE-001",
                Client = "Sample client",
                Location = "Sample site",
                Description = "Reference project for the AutoCAD to SketchUp workflow (two apartment buildings, parking and landscaped courtyard).",
                Author = "Site Design Pro",
                Revision = "A",
                DrawingUnit = "mm",
                Origin = new[] { OriginMeters.X * 1000, OriginMeters.Y * 1000, OriginMeters.Z * 1000 },
                TrueNorthDegrees = 0,
                Crs = "EPSG:31276",
                HeightDatum = "Sample datum",
            };
        }

        /// <summary>Elements in package frame (metres relative to the project origin).</summary>
        public static List<SiteElement> CreateLocal(ElementCatalog catalog)
        {
            var s = new SampleResidentialComplex(catalog);
            s.Build();
            var numbering = new LabelNumbering(new LabelSettings(), catalog);
            numbering.Assign(s.elements, true, 3.0);
            return s.elements;
        }

        /// <summary>Elements in drawing frame (millimetres, WCS including the project origin).</summary>
        public static (ProjectSettings Settings, List<SiteElement> Elements) CreateDrawing(ElementCatalog catalog)
        {
            ProjectSettings settings = CreateSettings();
            Vec3 o = settings.OriginMeters;
            double k = settings.Scale.DrawingUnitsPerMeter;
            List<SiteElement> local = CreateLocal(catalog);
            foreach (SiteElement e in local)
            {
                e.Geometry = e.Geometry.Transform(v => new Vec3((v.X + o.X) * k, (v.Y + o.Y) * k, (v.Z + o.Z) * k));
            }

            return (settings, local);
        }

        public static string DeterministicId(string seed)
        {
            byte[] h = Compat.Sha256(Encoding.UTF8.GetBytes("site-design-pro-sample:" + seed));
            h[6] = (byte)((h[6] & 0x0F) | 0x40);
            h[8] = (byte)((h[8] & 0x3F) | 0x80);
            string hex = string.Concat(h.Take(16).Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
            return hex.Substring(0, 8) + "-" + hex.Substring(8, 4) + "-" + hex.Substring(12, 4) + "-" + hex.Substring(16, 4) + "-" + hex.Substring(20, 12);
        }

        private SiteElement Add(string typeId, ElementGeometry g, object? parameters = null, params (string Key, string Value)[] props)
        {
            ElementType type = catalog.RequireType(typeId);
            var e = new SiteElement(DeterministicId("element-" + (++counter).ToString(System.Globalization.CultureInfo.InvariantCulture)), typeId, g)
            {
                Name = type.Name,
                Parameters = type.CreateDefaultParameters(),
            };
            if (parameters != null)
            {
                foreach (System.Reflection.PropertyInfo pi in parameters.GetType().GetProperties())
                {
                    object? v = pi.GetValue(parameters);
                    e.Parameters[pi.Name] = v switch
                    {
                        null => ParamValue.Null,
                        bool b => ParamValue.Of(b),
                        string str => ParamValue.Of(str),
                        _ => ParamValue.Of(Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture)),
                    };
                }
            }

            foreach ((string key, string value) in props)
            {
                e.Properties[key] = value;
            }

            elements.Add(e);
            return e;
        }

        private static PointGeometry Pt(double x, double y, double z = 0, double rotDeg = 0) => new PointGeometry(new Vec3(x, y, z), rotDeg * Math.PI / 180);

        private static PolygonGeometry Rect(double x0, double y0, double x1, double y1, double z = 0, Ring[]? holes = null)
            => new PolygonGeometry(new Ring(new[] { new Vec3(x0, y0, z), new Vec3(x1, y0, z), new Vec3(x1, y1, z), new Vec3(x0, y1, z) }), holes);

        private static Ring RectRing(double x0, double y0, double x1, double y1, double z = 0)
            => new Ring(new[] { new Vec3(x0, y0, z), new Vec3(x1, y0, z), new Vec3(x1, y1, z), new Vec3(x0, y1, z) });

        private static PathGeometry Path(double z, params double[] xy)
        {
            var pts = new List<Vec3>();
            for (int i = 0; i + 1 < xy.Length; i += 2)
            {
                pts.Add(new Vec3(xy[i], xy[i + 1], z));
            }

            return new PathGeometry(new Ring(pts), false);
        }

        private void Build()
        {
            // Site and buildings
            Add("site.boundary", Rect(0, 0, 120, 80));
            Add("building.footprint", Rect(15, 45, 55, 63, 0.15), new { height = 18.0, storeys = 6 }).Name = "Building A";
            Add("building.footprint", Rect(70, 45, 105, 63, 0.15), new { height = 15.0, storeys = 5 }).Name = "Building B";
            Add("building.entrance", Pt(35, 44.4, 0.15, 90), new { reference = "A" });
            Add("building.entrance", Pt(87.5, 44.4, 0.15, 90), new { reference = "B" });

            // Roads and parking
            Add("road.access_road", Path(0, 0, 30, 120, 30), new { width = 6.0 });
            Add("surface.asphalt", new PolygonGeometry(new Ring(new[]
            {
                new Vec3(10, 4, 0), new Vec3(110, 4, 0), new Vec3(110, 21, 0), new Vec3(65, 21, 0),
                new Vec3(65, 27, 0), new Vec3(55, 27, 0), new Vec3(55, 21, 0), new Vec3(10, 21, 0),
            }))).Name = "Parking area";
            Add("road.pedestrian_crossing", Rect(58, 27, 62, 33, 0.003));
            Add("road.arrow", Pt(20, 28.5, 0.003, 0));
            Add("road.arrow", Pt(100, 31.5, 0.003, 180));

            void BayRow(Vec3 a, Vec3 b, bool left, string type, int? count, double width)
            {
                foreach (Placement p in ParkingLayout.Row(a, b, width, left, 90, count))
                {
                    Add(type, new PointGeometry(p.Position, p.Rotation));
                }
            }

            BayRow(new Vec3(14, 9, 0), new Vec3(54, 9, 0), false, "road.parking_bay", null, 2.5);
            BayRow(new Vec3(66, 9, 0), new Vec3(106, 9, 0), false, "road.parking_bay", null, 2.5);
            BayRow(new Vec3(14, 16, 0), new Vec3(44, 16, 0), true, "road.parking_bay", null, 2.5);
            BayRow(new Vec3(44, 16, 0), new Vec3(54, 16, 0), true, "road.parking_bay_ev", null, 2.5);
            BayRow(new Vec3(66, 16, 0), new Vec3(73.2, 16, 0), true, "road.parking_bay_accessible", null, 3.6);
            BayRow(new Vec3(73.2, 16, 0), new Vec3(108, 16, 0), true, "road.parking_bay", null, 2.5);
            foreach (double x in new[] { 45.25, 47.75, 50.25, 52.75 })
            {
                Add("sustainability.ev_charger_pedestal", Pt(x, 21.5, 0, 0), null, ("notes", "Charger model to be confirmed"));
            }

            // Pedestrian surfaces
            Add("surface.concrete_paving", Rect(5, 34, 115, 43, 0.15), new { surfaceCode = "P1" }).Name = "Front paving";
            Add("surface.natural_stone", new PolygonGeometry(new Ring(
                new[] { new Vec3(56, 45, 0.15), new Vec3(69, 45, 0.15), new Vec3(69, 63, 0.15), new Vec3(56, 63, 0.15) },
                new[] { 0.0, 0.0, 0.3, 0.0 })), new { surfaceCode = "P2" }).Name = "Courtyard";
            Add("road.bollard", Pt(57, 44, 0.15));
            Add("road.bollard", Pt(68, 44, 0.15));

            // Soft landscape
            Add("surface.lawn", Rect(5, 65, 115, 78, 0.3, new[] { RectRing(80, 66, 95, 76, 0.3) })).Name = "North lawn";
            Add("surface.rubber_safety", Rect(80, 66, 95, 76, 0.3)).Name = "Playground surface";
            Add("landscape.flower_bed", Rect(20, 43, 50, 45, 0.15));
            Add("landscape.flower_bed", Rect(75, 43, 100, 45, 0.15));
            Add("wall.planter_wall", Path(0.3, 5, 64.5, 115, 64.5), new { height = 0.6 });
            Add("landscape.hedge", Path(0, 1.5, 35, 1.5, 78), new { height = 1.6 });

            var verge = new Ring(new[] { new Vec3(8, 24, 0), new Vec3(50, 24, 0) });
            foreach (Placement p in PathPlacement.Distribute(verge, false, 8, 0, 0, SpacingMode.Fit))
            {
                Add("landscape.tree_deciduous", new PointGeometry(p.Position, 0), new { height = 7.0, canopyDiameter = 5.0, matureCanopyDiameter = 9.0 },
                    ("botanicalName", "Tilia cordata"), ("commonName", "Small-leaved lime"), ("plantingSize", "18-20 cm girth"), ("dataSource", "Sample project - replace with nursery data"));
            }

            var verge2 = new Ring(new[] { new Vec3(70, 24, 0), new Vec3(112, 24, 0) });
            foreach (Placement p in PathPlacement.Distribute(verge2, false, 8, 0, 0, SpacingMode.Fit))
            {
                Add("landscape.tree_deciduous", new PointGeometry(p.Position, 0), new { height = 7.0, canopyDiameter = 5.0, matureCanopyDiameter = 9.0 },
                    ("botanicalName", "Tilia cordata"), ("commonName", "Small-leaved lime"), ("plantingSize", "18-20 cm girth"), ("dataSource", "Sample project - replace with nursery data"));
            }

            foreach ((double x, double y) in new[] { (20.0, 72.0), (35.0, 74.0), (50.0, 70.0) })
            {
                Add("landscape.tree_evergreen", Pt(x, y, 0.3), new { height = 9.0, canopyDiameter = 4.0 },
                    ("botanicalName", "Pinus nigra"), ("commonName", "Black pine"), ("dataSource", "Sample project - replace with nursery data"));
            }

            foreach ((double x, double y) in new[] { (8.0, 50.0), (8.0, 58.0), (110.0, 50.0) })
            {
                Add("landscape.shrub", Pt(x, y, 0.15), new { height = 1.5, diameter = 1.8 });
            }

            // Lighting
            var roadAxis = new Ring(new[] { new Vec3(0, 30, 0), new Vec3(120, 30, 0) });
            foreach (Placement p in PathPlacement.Distribute(roadAxis, false, 25, 10, 10, SpacingMode.Fit, 3.5, -Math.PI / 2))
            {
                Add("lighting.street_light", new PointGeometry(p.Position, p.Rotation), new { fixtureCode = "SL-1" });
            }

            var walk = new Ring(new[] { new Vec3(5, 39.5, 0.15), new Vec3(115, 39.5, 0.15) });
            foreach (Placement p in PathPlacement.Distribute(walk, false, 20, 15, 15, SpacingMode.Fixed))
            {
                Add("lighting.pedestrian_light", new PointGeometry(p.Position, 0), new { fixtureCode = "PL-1" });
            }

            Add("lighting.bollard_light", Pt(58, 50, 0.15), new { fixtureCode = "BL-1" });
            Add("lighting.bollard_light", Pt(67, 50, 0.15), new { fixtureCode = "BL-1" });

            // Furniture and cycling
            Add("furniture.bench", Pt(45, 41.5, 0.15, 0));
            Add("furniture.bench", Pt(75, 41.5, 0.15, 0));
            Add("furniture.bench", Pt(60.5, 60, 0.15, 180));
            Add("furniture.bench", Pt(64.5, 60, 0.15, 180));
            Add("furniture.waste_bin", Pt(47, 41.6, 0.15));
            Add("cycling.bike_rack", Pt(30, 38, 0.15, 0), new { count = 5 });
            Add("cycling.bike_rack", Pt(92, 38, 0.15, 0), new { count = 5 });

            // Waste collection (dimensions are indicative; review items are expected)
            Add("utility.waste_container_ug_5m3", Pt(8, 38, 0.15), new { wasteFraction = "mixed" });
            Add("utility.waste_container_ug_5m3", Pt(10.5, 38, 0.15), new { wasteFraction = "paper" });
            Add("utility.waste_container_ug_5m3", Pt(13, 38, 0.15), new { wasteFraction = "plastic" });
            Add("utility.waste_container_ug_3m3", Pt(15.5, 38, 0.15), new { wasteFraction = "glass" });

            // Drainage
            var channel = new PathGeometry(new Ring(new[] { new Vec3(5, 33.2, 0.25), new Vec3(115, 33.2, -0.30) }), false);
            Add("water.drainage_channel", channel, new { gradient = 0.5, outletTo = "IC at east end" });
            Add("water.inspection_chamber", Pt(117, 33.2, 0), new { depth = 1.6 });
            Add("water.drain_pipe", Path(0, 117, 33.2, 117, 12), new { axisOffset = -1.4 });
            Add("water.rainwater_tank", Pt(115, 10, 0, 90));

            // Boundaries
            Add("wall.mesh_fence", Path(0, 0.3, 26, 0.3, 79.7, 119.7, 79.7, 119.7, 26), new { height = 2.0 });
            Add("wall.pedestrian_gate", Pt(60, 79.7, 0, 0));

            // Courtyard pergola and playground
            Add("structure.pergola", Rect(58, 52, 67, 58, 0.15), new { height = 2.7 });
            Add("recreation.swing", Pt(84, 71, 0.3, 0));
            Add("recreation.playground_unit", Pt(90.5, 71, 0.3, 0), new { width = 3.5, depth = 3.0 });
        }
    }
}
