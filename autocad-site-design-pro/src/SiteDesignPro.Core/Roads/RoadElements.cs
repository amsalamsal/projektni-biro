using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Roads
{
    /// <summary>
    /// Link between site elements and the road network: which element types are roads of the network, their
    /// cross-section, and the surfaces, markings and arrows the network generates (for schedules and the SketchUp
    /// package). Streets (road.street) carry the full cross-section; the simple roads (access road, driveway,
    /// service route) join the network as a carriageway of their width.
    /// </summary>
    public static class RoadElements
    {
        public const string StreetType = "road.street";
        public const string StreetBuilder = "street";
        public const string CrossingType = "road.pedestrian_crossing";
        public const string MarkingType = "road.marking_line";
        public const string ArrowType = "road.arrow";

        /// <summary>Custom flag on exported roads whose surfaces come from the network (SketchUp builds no strip for them).</summary>
        public const string NetworkFlag = "networkSurface";

        /// <summary>Generated element type of each level of the cross-section.</summary>
        public static IReadOnlyDictionary<RoadLevel, (string Type, string Name)> LevelTypes { get; } = new Dictionary<RoadLevel, (string, string)>
        {
            [RoadLevel.Carriageway] = ("road.carriageway", "Kolovoz"),
            [RoadLevel.Bike] = ("road.street_bike_lane", "Biciklistička staza uz kolovoz"),
            [RoadLevel.Kerb] = ("road.street_kerb", "Ivičnjak 15 cm"),
            [RoadLevel.Sidewalk] = ("road.sidewalk", "Trotoar"),
            [RoadLevel.Edge] = ("road.sidewalk_edge", "Ivičnjak 10 cm uz trotoar"),
        };

        private static readonly HashSet<string> SimpleRoads = new HashSet<string>(StringComparer.Ordinal) { "road.access_road", "road.driveway", "road.service_route" };

        public static bool IsNetworkRoad(ElementType? t) => t != null && (t.Builder.Id == StreetBuilder || SimpleRoads.Contains(t.Id));

        /// <summary>Cross-section of a road element with its axis in metres (geometry units divided by <paramref name="unitsPerMeter"/>).</summary>
        public static RoadSpec? Spec(SiteElement e, ElementType t, double unitsPerMeter = 1.0)
        {
            if (!IsNetworkRoad(t) || e.Geometry is not PathGeometry path || path.Ring.Count < 2)
            {
                return null;
            }

            double N(string key, double fallback)
            {
                double v = e.Parameters.Number(key, double.NaN);
                if (Compat.IsFinite(v))
                {
                    return v;
                }

                double d = t.Parameter(key)?.Default.AsNumber() ?? double.NaN;
                return Compat.IsFinite(d) ? d : fallback;
            }

            string T(string key, string fallback)
            {
                if (e.Parameters.Contains(key) && !e.Parameters[key].IsNull)
                {
                    return e.Parameters[key].AsText();
                }

                ParameterDefinition? def = t.Parameter(key);
                return def != null && !def.Default.IsNull ? def.Default.AsText() : fallback;
            }

            bool B(string key, bool fallback)
            {
                if (e.Parameters.Contains(key) && !e.Parameters[key].IsNull)
                {
                    return e.Parameters[key].AsBool(fallback);
                }

                ParameterDefinition? def = t.Parameter(key);
                return def != null && !def.Default.IsNull ? def.Default.AsBool(fallback) : fallback;
            }

            RoadSpec s;
            if (t.Builder.Id == StreetBuilder)
            {
                s = new RoadSpec
                {
                    OneWay = T("direction", "two_way") == "one_way",
                    LanesLeft = (int)Math.Round(N("lanesLeft", 1)),
                    LanesRight = (int)Math.Round(N("lanesRight", 1)),
                    LaneWidth = N("laneWidth", 3.0),
                    SidewalkLeft = N("sidewalkLeft", 1.6),
                    SidewalkRight = N("sidewalkRight", 1.6),
                    BikeLeft = N("bikeLeft", 0),
                    BikeRight = N("bikeRight", 0),
                    CornerRadius = N("cornerRadius", RoadSpec.DefaultCornerRadius),
                    Arrows = B("arrows", true),
                    ArrowSpacing = N("arrowSpacing", 40),
                };
                s.Normalize();
            }
            else
            {
                // a simple road is a carriageway of its width: two-way with a centre line from 5 m up, else one lane
                double w = Math.Max(1.0, N("width", 5.5));
                bool twoWay = w >= 5.0;
                s = new RoadSpec
                {
                    OneWay = !twoWay,
                    LanesLeft = 1,
                    LanesRight = 1,
                    LaneWidth = twoWay ? w / 2 : w,
                    SidewalkLeft = 0,
                    SidewalkRight = 0,
                    Kerbs = false,
                    Markings = twoWay,
                    Arrows = false,
                    CornerRadius = RoadSpec.DefaultCornerRadius,
                };
            }

            s.Id = e.Id;
            s.Name = !string.IsNullOrWhiteSpace(e.Label) ? e.Label! : e.Name;
            s.Closed = path.Closed;
            s.Axis = GeometryMath.Tessellate(path.Ring, path.Closed, 0.02 * unitsPerMeter).Select(v => new Vec2(v.X / unitsPerMeter, v.Y / unitsPerMeter)).ToList();
            return s;
        }

        /// <summary>Outlines of the pedestrian crossings (metres), where lane lines and arrows stop.</summary>
        public static List<IReadOnlyList<Vec2>> CrossingAreas(IEnumerable<SiteElement> elements, double unitsPerMeter = 1.0)
        {
            var list = new List<IReadOnlyList<Vec2>>();
            foreach (SiteElement e in elements)
            {
                if (e.TypeId == CrossingType && e.Geometry is PolygonGeometry pg)
                {
                    list.Add(GeometryMath.Tessellate(pg.Outer, true, 0.02 * unitsPerMeter).Select(v => new Vec2(v.X / unitsPerMeter, v.Y / unitsPerMeter)).ToList());
                }
            }

            return list;
        }

        /// <summary>The road network of a set of elements, or null when there are no roads.</summary>
        public static RoadNetworkResult? Network(IEnumerable<SiteElement> elements, ElementCatalog catalog, double unitsPerMeter = 1.0)
        {
            List<SiteElement> all = elements as List<SiteElement> ?? elements.ToList();
            var specs = new List<RoadSpec>();
            foreach (SiteElement e in all)
            {
                ElementType? t = catalog.FindType(e.TypeId);
                RoadSpec? s = t != null ? Spec(e, t, unitsPerMeter) : null;
                if (s != null)
                {
                    specs.Add(s);
                }
            }

            return specs.Count == 0 ? null : RoadNetwork.Build(specs, CrossingAreas(all, unitsPerMeter));
        }

        /// <summary>
        /// Elements generated from the roads of a package-frame element list (metres): carriageway, bike lanes, kerbs,
        /// sidewalks and edging as surfaces, lane lines and arrows as markings. Ids are stable for the same roads.
        /// </summary>
        public static List<SiteElement> Derived(IEnumerable<SiteElement> elements, ElementCatalog catalog)
        {
            List<SiteElement> all = elements as List<SiteElement> ?? elements.ToList();
            var list = new List<SiteElement>();
            List<SiteElement> roads = all.Where(e => IsNetworkRoad(catalog.FindType(e.TypeId))).ToList();
            RoadNetworkResult? res = roads.Count == 0 ? null : Network(all, catalog);
            if (res == null)
            {
                return list;
            }

            double z = roads[0].Geometry is PathGeometry first && first.Ring.Count > 0 ? first.Ring.Vertices[0].Z : 0;
            string seed = string.Join("|", roads.Select(r => r.Id).OrderBy(x => x, StringComparer.Ordinal));
            int n = 0;
            void Add(string typeId, string name, ElementGeometry g, params (string Key, double Value)[] values)
            {
                ElementType? t = catalog.FindType(typeId);
                if (t == null)
                {
                    return;
                }

                var e = new SiteElement(Uuid(seed + "|" + typeId + "|" + n++), typeId, g) { Name = name, Parameters = t.CreateDefaultParameters() };
                foreach ((string key, double value) in values)
                {
                    e.Parameters[key] = ParamValue.Of(value);
                }

                list.Add(e);
            }

            foreach (KeyValuePair<RoadLevel, List<RoadRegion>> band in res.Bands)
            {
                (string type, string name) = LevelTypes[band.Key];
                foreach (RoadRegion region in band.Value)
                {
                    Add(type, name, new PolygonGeometry(ToRing(region.Outer, z), region.Holes.Select(h => ToRing(h, z)).ToList()));
                }
            }

            foreach (RoadMarking m in res.Markings)
            {
                Add(MarkingType, m.Centre ? "Razdjelna linija" : "Linija traka", new PathGeometry(new Ring(m.Points.Select(p => p.WithZ(z)).ToList()), false), ("width", 0.12));
            }

            foreach (RoadArrow a in res.Arrows)
            {
                // the arrow element starts at its tail; the network gives the centre of a 5 m arrow
                Vec2 tail = a.Position - (new Vec2(Math.Cos(a.Angle), Math.Sin(a.Angle)) * 2.5);
                Add(ArrowType, "Strelica smjera", new PointGeometry(tail.WithZ(z), a.Angle), ("length", 5.0), ("width", 0.64), ("shaftWidth", 0.18));
            }

            return list;
        }

        /// <summary>The elements together with what their roads generate.</summary>
        public static List<SiteElement> WithDerived(IEnumerable<SiteElement> elements, ElementCatalog catalog)
        {
            List<SiteElement> all = elements.ToList();
            all.AddRange(Derived(all, catalog));
            return all;
        }

        /// <summary>
        /// Stripes of a striped area (pedestrian crossing, hatched area), the same way SketchUp builds them: parallel to
        /// the first edge, from the side of the first vertex across the oriented bounding rectangle.
        /// </summary>
        public static List<Vec2[]> StripeBars(IReadOnlyList<Vec2> outer, double stripe, double gap)
        {
            var bars = new List<Vec2[]>();
            if (outer.Count < 3 || stripe <= 0)
            {
                return bars;
            }

            Vec2 o = outer[0];
            Vec2 d = outer[1] - outer[0];
            Vec2 u = d.Length < 1e-9 ? new Vec2(1, 0) : d.Normalized();
            Vec2 v = u.Perp();
            double minU = outer.Min(p => (p - o).Dot(u)), maxU = outer.Max(p => (p - o).Dot(u));
            double minV = outer.Min(p => (p - o).Dot(v)), maxV = outer.Max(p => (p - o).Dot(v));
            if (Math.Abs(minV) > Math.Abs(maxV))
            {
                v = -v;
                (minV, maxV) = (-maxV, -minV);
            }

            Vec2 origin = o + (u * minU) + (v * minV);
            double lu = maxU - minU, lv = maxV - minV;
            for (double t = 0; t + 1e-6 < lv; t += stripe + Math.Max(gap, 0))
            {
                double w = Math.Min(stripe, lv - t);
                Vec2 a = origin + (v * t);
                bars.Add(new[] { a, a + (u * lu), a + (u * lu) + (v * w), a + (v * w) });
            }

            return bars;
        }

        private static Ring ToRing(RoadLoop loop, double z) => new Ring(loop.Points.Select(p => p.WithZ(z)).ToList(), loop.Bulges.ToList());

        /// <summary>Name-based UUID (SHA-1, version 5 layout) so generated elements keep their ids between exports.</summary>
        internal static string Uuid(string name)
        {
            byte[] h;
            using (SHA1 sha = SHA1.Create())
            {
                h = sha.ComputeHash(Encoding.UTF8.GetBytes("sitedesignpro-road|" + name));
            }

            h[6] = (byte)((h[6] & 0x0F) | 0x50);
            h[8] = (byte)((h[8] & 0x3F) | 0x80);
            var sb = new StringBuilder(36);
            for (int i = 0; i < 16; i++)
            {
                if (i == 4 || i == 6 || i == 8 || i == 10)
                {
                    sb.Append('-');
                }

                sb.Append(h[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }
    }
}
