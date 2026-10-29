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

        /// <summary>Building zero ±0,00 of the sample lies 0.15 m above the origin elevation (absolute 520.15).</summary>
        public const double BuildingZeroRelativeToOrigin = 0.15;

        /// <summary>
        /// Existing terrain of the sample in the package frame: a deterministic survey grid (10 m) around the parcel and
        /// a breakline along the road edge. Synthetic data for demonstrating the workflow, not a real survey.
        /// </summary>
        public static Terrain.TerrainSources CreateTerrainSources()
        {
            var t = new Terrain.TerrainSources();
            for (int i = -1; i <= 13; i++)
            {
                for (int j = -1; j <= 9; j++)
                {
                    double x = i * 10.0, y = j * 10.0;
                    if (Math.Abs(y - 30) < 1e-9)
                    {
                        continue; // the road edge breakline defines this row
                    }

                    // gentle fall to the south-east plus a shallow swale - deterministic
                    double z = 0.35 - (0.004 * x) + (0.003 * (y - 30)) - (0.12 * Math.Exp(-Math.Pow((x - 95) / 18.0, 2) - Math.Pow((y - 70) / 10.0, 2)));
                    t.Points.Add(new Vec3(x, y, Math.Round(z, 3)));
                }
            }

            var edge = new List<Vec3>();
            for (int i = -1; i <= 13; i++)
            {
                double x = i * 10.0;
                edge.Add(new Vec3(x, 30, Math.Round(0.35 - (0.004 * x), 3)));
            }

            t.Breaklines.Add(edge);
            t.Boundary = new List<Vec2> { new Vec2(-10, -10), new Vec2(130, -10), new Vec2(130, 90), new Vec2(-10, 90) };
            return t;
        }

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
                Name = "Primjer: stambeni kompleks",
                Number = "SDP-SAMPLE-001",
                Client = "Primjer investitora",
                Location = "Primjer lokacije",
                Description = "Referentni projekat za tok rada AutoCAD → SketchUp (dva stambena objekta, parking i uređeno dvorište).",
                Author = "Vanjsko uređenje PRO",
                Revision = "A",
                DrawingUnit = "mm",
                Origin = new[] { OriginMeters.X * 1000, OriginMeters.Y * 1000, OriginMeters.Z * 1000 },
                TrueNorthDegrees = 0,
                Crs = "EPSG:3908",
                Elevations = new Elevation.ElevationDatum
                {
                    // values of this sample only - a new project has no elevation until the user defines it
                    BuildingZeroAbsolute = OriginMeters.Z + BuildingZeroRelativeToOrigin,
                    ParterreEqualsBuildingZero = true,
                    DefaultRoadOffsetBelowParterre = 0.15,
                    VerticalDatum = "Primjer – nije stvarni visinski datum",
                    DrawingZ = Elevation.DrawingZMode.Absolute,
                },
            };
        }

        /// <summary>Elements in package frame (metres relative to the project origin).</summary>
        public static List<SiteElement> CreateLocal(ElementCatalog catalog)
        {
            var s = new SampleResidentialComplex(catalog);
            s.Build();
            var numbering = new LabelNumbering(new LabelSettings(), catalog);
            numbering.Assign(s.elements, true, 3.0);
            AssignElevations(s.elements);
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

        /// <summary>Records which elevation each levelled object was given (object by object, never area-wide).</summary>
        private static void AssignElevations(List<SiteElement> elements)
        {
            Elevation.ElevationDatum datum = CreateSettings().Elevations;
            foreach (SiteElement e in elements)
            {
                Elevation.ElevationAssignment? a = e.TypeId switch
                {
                    "building.footprint" => new Elevation.ElevationAssignment { Mode = Elevation.ElevationMode.Relative, Value = 0, Note = "±0,00" },
                    "surface.concrete_paving" => new Elevation.ElevationAssignment { Mode = Elevation.ElevationMode.ParterreOffset, Value = 0 },
                    "surface.asphalt" => new Elevation.ElevationAssignment { Mode = Elevation.ElevationMode.RoadOffset, Value = 0.15 },
                    "surface.lawn" => new Elevation.ElevationAssignment { Mode = Elevation.ElevationMode.ParterreOffset, Value = 0.15 },
                    _ => null,
                };
                a?.WriteTo(e.Properties, datum.ResolveRelative(a));
            }
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
            Add("building.footprint", Rect(15, 45, 55, 63, 0.15), new { height = 18.0, storeys = 6 }).Name = "Objekat A";
            Add("building.footprint", Rect(70, 45, 105, 63, 0.15), new { height = 15.0, storeys = 5 }).Name = "Objekat B";
            Add("building.entrance", Pt(35, 44.4, 0.15, 90), new { reference = "A" });
            Add("building.entrance", Pt(87.5, 44.4, 0.15, 90), new { reference = "B" });

            // Roads and parking
            Add("road.access_road", Path(0, 0, 30, 120, 30), new { width = 6.0 });
            Add("surface.asphalt", new PolygonGeometry(new Ring(new[]
            {
                new Vec3(10, 4, 0), new Vec3(110, 4, 0), new Vec3(110, 21, 0), new Vec3(65, 21, 0),
                new Vec3(65, 27, 0), new Vec3(55, 27, 0), new Vec3(55, 21, 0), new Vec3(10, 21, 0),
            }))).Name = "Parking prostor";
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
                Add("sustainability.ev_charger_pedestal", Pt(x, 21.5, 0, 0), null, ("notes", "Model punjača potrebno potvrditi"));
            }

            // Pedestrian surfaces
            Add("surface.concrete_paving", Rect(5, 34, 115, 43, 0.15), new { surfaceCode = "P1" }).Name = "Popločanje ispred objekata";
            Add("surface.natural_stone", new PolygonGeometry(new Ring(
                new[] { new Vec3(56, 45, 0.15), new Vec3(69, 45, 0.15), new Vec3(69, 63, 0.15), new Vec3(56, 63, 0.15) },
                new[] { 0.0, 0.0, 0.3, 0.0 })), new { surfaceCode = "P2" }).Name = "Dvorište";
            Add("road.bollard", Pt(57, 44, 0.15));
            Add("road.bollard", Pt(68, 44, 0.15));

            // Soft landscape
            Add("surface.lawn", Rect(5, 65, 115, 78, 0.3, new[] { RectRing(80, 66, 95, 76, 0.3) })).Name = "Sjeverni travnjak";
            Add("surface.rubber_safety", Rect(80, 66, 95, 76, 0.3)).Name = "Podloga igrališta";
            Add("landscape.flower_bed", Rect(20, 43, 50, 45, 0.15));
            Add("landscape.flower_bed", Rect(75, 43, 100, 45, 0.15));
            Add("wall.planter_wall", Path(0.3, 5, 64.5, 115, 64.5), new { height = 0.6 });
            Add("landscape.hedge", Path(0, 1.5, 35, 1.5, 78), new { height = 1.6 });

            var verge = new Ring(new[] { new Vec3(8, 24, 0), new Vec3(50, 24, 0) });
            foreach (Placement p in PathPlacement.Distribute(verge, false, 8, 0, 0, SpacingMode.Fit))
            {
                Add("landscape.tree_deciduous", new PointGeometry(p.Position, 0), new { height = 7.0, canopyDiameter = 5.0, matureCanopyDiameter = 9.0 },
                    ("botanicalName", "Tilia cordata"), ("commonName", "Sitnolisna lipa"), ("plantingSize", "obim debla 18–20 cm"), ("dataSource", "Primjer – zamijeniti podacima rasadnika"));
            }

            var verge2 = new Ring(new[] { new Vec3(70, 24, 0), new Vec3(112, 24, 0) });
            foreach (Placement p in PathPlacement.Distribute(verge2, false, 8, 0, 0, SpacingMode.Fit))
            {
                Add("landscape.tree_deciduous", new PointGeometry(p.Position, 0), new { height = 7.0, canopyDiameter = 5.0, matureCanopyDiameter = 9.0 },
                    ("botanicalName", "Tilia cordata"), ("commonName", "Sitnolisna lipa"), ("plantingSize", "obim debla 18–20 cm"), ("dataSource", "Primjer – zamijeniti podacima rasadnika"));
            }

            foreach ((double x, double y) in new[] { (20.0, 72.0), (35.0, 74.0), (50.0, 70.0) })
            {
                Add("landscape.tree_evergreen", Pt(x, y, 0.3), new { height = 9.0, canopyDiameter = 4.0 },
                    ("botanicalName", "Pinus nigra"), ("commonName", "Crni bor"), ("dataSource", "Primjer – zamijeniti podacima rasadnika"));
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
            Add("water.drainage_channel", channel, new { gradient = 0.5, outletTo = "RO na istočnom kraju" });
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
