using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Clipper2Lib;
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
        public const string HatchedAreaType = "road.hatched_area";
        public const string RampType = "road.curb_ramp";
        public const string LevelMarkType = "road.level_mark";

        /// <summary>A point where the user wants an accessible kerb ramp (on the sidewalk: that side; on the carriageway: both).</summary>
        public const string RampMarkType = "road.curb_ramp_mark";

        /// <summary>
        /// The street as it will look along an axis that is still being drawn (metres): the edges of the carriageway,
        /// kerbs, bike lanes and sidewalks with the bends rounded, and the lane lines. Null when it cannot be built yet.
        /// </summary>
        public static RoadNetworkResult? Preview(IReadOnlyList<Vec2> axis, RoadSpec parameters) => Preview(axis, parameters, null);

        /// <summary>
        /// The same together with the roads already in the drawing that the new axis crosses, touches or comes near
        /// (metres): the junctions that will appear are part of the preview.
        /// </summary>
        public static RoadNetworkResult? Preview(IReadOnlyList<Vec2> axis, RoadSpec parameters, IReadOnlyList<RoadSpec>? existing)
        {
            var pts = new List<Vec2>();
            foreach (Vec2 p in axis)
            {
                if (pts.Count == 0 || pts[pts.Count - 1].DistanceTo(p) > 0.05)
                {
                    pts.Add(p);
                }
            }

            if (pts.Count < 2)
            {
                return null;
            }

            try
            {
                RoadSpec spec = parameters.CloneParameters();
                spec.Id = "preview";
                spec.Axis = pts;
                spec.Closed = false;
                spec.Corners = null;
                var roads = new List<RoadSpec> { spec };
                double own = Math.Max(spec.Extent(RoadLevel.Edge, true), spec.Extent(RoadLevel.Edge, false));
                foreach (RoadSpec other in existing ?? new List<RoadSpec>())
                {
                    double reach = own + Math.Max(other.Extent(RoadLevel.Edge, true), other.Extent(RoadLevel.Edge, false)) + 3;
                    if (Near(pts, other.Axis, other.Closed, reach))
                    {
                        roads.Add(other);
                    }
                }

                return RoadNetwork.Build(roads);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The height of the roads (relative to their base elevation) at a point on or beside a road (metres), from the
        /// elevation marks; null when no road is there or the roads have no marks.
        /// </summary>
        public static double? LevelNear(IEnumerable<SiteElement> elements, ElementCatalog catalog, double unitsPerMeter, Vec2 point)
        {
            List<SiteElement> all = elements as List<SiteElement> ?? elements.ToList();
            List<RoadSpec> specs = Specs(all, catalog, unitsPerMeter);
            bool near = false;
            foreach (RoadSpec r in specs)
            {
                double reach = Math.Max(r.Extent(RoadLevel.Edge, true), r.Extent(RoadLevel.Edge, false)) + 1.5;
                near |= Near(new List<Vec2> { point, point + new Vec2(0.01, 0) }, r.Axis, r.Closed, reach);
            }

            if (!near || LevelMarks(all, unitsPerMeter).Count == 0)
            {
                return null;
            }

            List<RoadProfile> profiles = Profiles(all, catalog, unitsPerMeter);
            return profiles.Count == 0 ? (double?)null : RoadLevels.Height(profiles, point);
        }

        /// <summary>True when two polylines come within a distance of each other (or cross).</summary>
        private static bool Near(List<Vec2> a, List<Vec2> b, bool closedB, double distance)
        {
            static double ToSegment(Vec2 p, Vec2 s0, Vec2 s1)
            {
                Vec2 d = s1 - s0;
                double l2 = d.Dot(d);
                double t = l2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, (p - s0).Dot(d) / l2));
                return (s0 + (d * t)).DistanceTo(p);
            }

            int nb = closedB ? b.Count : b.Count - 1;
            for (int i = 0; i + 1 < a.Count; i++)
            {
                for (int j = 0; j < nb; j++)
                {
                    Vec2 b0 = b[j], b1 = b[(j + 1) % b.Count];
                    if (ToSegment(b0, a[i], a[i + 1]) <= distance || ToSegment(b1, a[i], a[i + 1]) <= distance || ToSegment(a[i], b0, b1) <= distance || ToSegment(a[i + 1], b0, b1) <= distance)
                    {
                        return true;
                    }

                    // crossing segments
                    double d1 = (a[i + 1] - a[i]).Cross(b0 - a[i]), d2 = (a[i + 1] - a[i]).Cross(b1 - a[i]);
                    double d3 = (b1 - b0).Cross(a[i] - b0), d4 = (b1 - b0).Cross(a[i + 1] - b0);
                    if (d1 * d2 < 0 && d3 * d4 < 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The place of the longitudinal section of a garage ramp (its "reference" names the ramp).</summary>
        public const string RampSectionType = "road.ramp_section";

        /// <summary>True when a station of a garage ramp lies at a road (within 1,5 m of its outer edge).</summary>
        public static bool AtRoad(GarageRamp ramp, double station, IEnumerable<RoadSpec> roads)
        {
            Vec2 p = ramp.PointAt(station, 0).Point;
            foreach (RoadSpec r in roads)
            {
                double reach = Math.Max(r.Extent(RoadLevel.Edge, true), r.Extent(RoadLevel.Edge, false)) + 1.5;
                for (int i = 0; i + 1 < r.Axis.Count; i++)
                {
                    Vec2 a = r.Axis[i], b = r.Axis[i + 1], ab = b - a;
                    double l2 = ab.Dot(ab);
                    double t = l2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, (p - a).Dot(ab) / l2));
                    if ((a + (ab * t)).DistanceTo(p) <= reach)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Custom flag on exported roads whose surfaces come from the network (SketchUp builds no strip for them).</summary>
        public const string NetworkFlag = "networkSurface";

        /// <summary>
        /// Custom flag of an element the road network or a garage ramp generated (carriageway, kerb, sidewalk,
        /// markings, …) in a package: a host that draws the network itself (AutoCAD) leaves these out on import.
        /// </summary>
        public const string DerivedFlag = "derivedFromRoads";

        /// <summary>
        /// Types of the elements the generated road elements are made from (drawn by the user): the roads, crossings,
        /// elevation marks, kerb ramp marks, garage ramps, ramp sections, hatched areas and the islands of roundabouts.
        /// </summary>
        public static readonly IReadOnlyCollection<string> SourceTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            StreetType, "road.access_road", "road.driveway", "road.service_route", "road.pedestrian_crossing", LevelMarkType,
            "road.curb_ramp_mark", "road.garage_ramp", RampSectionType, "road.hatched_area", "road.splitter_island", "road.roundabout_apron",
        };

        /// <summary>Generated types (only ever made by the network): left out on import even without the flag (older packages).</summary>
        public static readonly IReadOnlyCollection<string> NetworkOnlyTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "road.carriageway", "road.street_bike_lane", "road.street_kerb", "road.sidewalk", "road.sidewalk_edge",
            "road.garage_ramp_surface", "road.curb_ramp", BikeRampType, "road.bike_crossing",
        };

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
                    Median = N("median", 0),
                    MedianTo = N("medianTo", 0),
                    MedianTaper = N("medianTaper", 0),
                };
                s.Normalize();
            }
            else
            {
                // a simple road is a carriageway of its width: two-way with a centre line from 5 m up, else one lane
                double w = Math.Max(1.0, N("width", 5.5));
                bool twoWay = w >= 5.0 && !path.Closed; // a closed road is a roundabout ring: one-way, no centre line
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
            s.Axis = GeometryMath.Tessellate(path.Ring, path.Closed, 0.002 * unitsPerMeter).Select(v => new Vec2(v.X / unitsPerMeter, v.Y / unitsPerMeter)).ToList();
            var corners = new List<Vec2>();
            int nv = path.Ring.Count;
            for (int i = 0; i < nv; i++)
            {
                bool interior = path.Closed || (i > 0 && i < nv - 1);
                if (interior && path.Ring.BulgeAt((i - 1 + nv) % nv) == 0 && path.Ring.BulgeAt(i) == 0)
                {
                    Vec3 v = path.Ring.Vertices[i];
                    corners.Add(new Vec2(v.X / unitsPerMeter, v.Y / unitsPerMeter));
                }
            }

            s.Corners = corners;
            return s;
        }

        /// <summary>
        /// The axis of a road with its bends between straight segments rounded the way the network rounds them
        /// (points and bulges in drawing units), or null when there is no such bend. Used to make the drawn axis
        /// follow the road, like the road itself.
        /// </summary>
        public static (List<Vec2> Points, List<double> Bulges)? RoundedAxis(IReadOnlyList<Vec2> points, IReadOnlyList<double> bulges, bool closed, RoadSpec spec, double unitsPerMeter = 1.0)
        {
            int n = points.Count;
            if (n < 3)
            {
                return null;
            }

            var m = points.Select(p => new Vec2(p.X / unitsPerMeter, p.Y / unitsPerMeter)).ToList();
            double B(int i) => i >= 0 && i < bulges.Count ? bulges[i] : 0;
            bool IsCorner(int i) => Math.Abs(B(((i - 1) % n + n) % n)) < 1e-9 && Math.Abs(B(i)) < 1e-9;
            (List<int> keep, List<RoadNetwork.BendArc> bends) = RoadNetwork.PlanBends(m, closed, IsCorner, left => RoadNetwork.AxisRadius(spec, left));
            if (bends.Count == 0 && keep.Count == n)
            {
                return null;
            }

            var byFirst = bends.ToDictionary(b => b.First);
            var pts = new List<Vec2>();
            var bs = new List<double>();
            for (int v = 0; v < keep.Count; v++)
            {
                if (!byFirst.TryGetValue(v, out RoadNetwork.BendArc? b))
                {
                    pts.Add(points[keep[v]]);
                    bs.Add(B(keep[v]));
                    continue;
                }

                pts.Add(b.T1 * unitsPerMeter);
                bs.Add(Math.Tan(b.Turn / 4));
                pts.Add(b.T2 * unitsPerMeter);
                bs.Add(B(keep[b.Last]));
                v = b.Last;
            }

            return (pts, bs);
        }

        /// <summary>Outlines of the pedestrian crossings (metres), where lane lines and arrows stop.</summary>
        public static List<IReadOnlyList<Vec2>> CrossingAreas(IEnumerable<SiteElement> elements, double unitsPerMeter = 1.0) => CrossingAreas(elements, unitsPerMeter, true);

        /// <summary>Outlines of the hatched areas (metres), where lane lines stop.</summary>
        public static List<IReadOnlyList<Vec2>> HatchedAreas(IEnumerable<SiteElement> elements, double unitsPerMeter = 1.0) =>
            elements.Where(e => e.TypeId == HatchedAreaType && e.Geometry is PolygonGeometry).Select(e => (IReadOnlyList<Vec2>)Outer((PolygonGeometry)e.Geometry, unitsPerMeter)).ToList();

        private static List<IReadOnlyList<Vec2>> CrossingAreas(IEnumerable<SiteElement> elements, double unitsPerMeter, bool withHatched)
        {
            var list = new List<IReadOnlyList<Vec2>>();
            foreach (SiteElement e in elements)
            {
                if ((e.TypeId == CrossingType || (withHatched && e.TypeId == HatchedAreaType)) && e.Geometry is PolygonGeometry pg)
                {
                    // a crossing with a cycle strip: the strip belongs to it (lane lines stop there too)
                    List<Vec2> outer = Outer(pg, unitsPerMeter);
                    list.Add(HasBikeStrip(e) && outer.Count >= 3 ? CrossingBox(outer, -BikeStripWidth, null) : outer);
                }
            }

            return list;
        }

        /// <summary>The road network of a set of elements, or null when there are no roads.</summary>
        public static RoadNetworkResult? Network(IEnumerable<SiteElement> elements, ElementCatalog catalog, double unitsPerMeter = 1.0)
        {
            List<SiteElement> all = elements as List<SiteElement> ?? elements.ToList();
            List<RoadSpec> specs = Specs(all, catalog, unitsPerMeter);
            // the ramps of a crossing: 1,20 m in its middle; with a cycle strip 2,00 m, starting where the strip ends
            // and running towards the stripes (over the first 2,00 m of the crossing)
            var crossings = new List<IReadOnlyList<Vec2>>();
            var rampWidths = new List<double>();
            foreach (SiteElement e in all.Where(x => x.TypeId == CrossingType && x.Geometry is PolygonGeometry))
            {
                List<Vec2> outer = Outer((PolygonGeometry)e.Geometry, unitsPerMeter);
                bool strip = HasBikeStrip(e) && outer.Count >= 3;
                crossings.Add(strip ? CrossingBox(outer, 0, RoadNetwork.RampWidth + BikeStripWidth) : outer);
                rampWidths.Add(strip ? RoadNetwork.RampWidth + BikeStripWidth : RoadNetwork.RampWidth);
            }

            List<Vec2> rampMarks = all.Where(e => e.TypeId == RampMarkType && e.Geometry is PointGeometry)
                .Select(e => ((PointGeometry)e.Geometry).Position).Select(p => new Vec2(p.X / unitsPerMeter, p.Y / unitsPerMeter)).ToList();
            // garage ramps that start or end at a road open its kerb and sidewalk
            var driveways = new List<RoadDriveway>();
            foreach (GarageRamp ramp in GarageRamp.All(all, unitsPerMeter))
            {
                double total = ramp.Length;
                if (total < 1)
                {
                    continue;
                }

                List<Vec2> outline = ramp.Plan().FirstOrDefault(x => x.Role == "surface") is RampShape surface
                    ? GeometryMath.Tessellate(new Ring(surface.Points.Select(q => q.WithZ(0)).ToList(), surface.Bulges), true, 0.005).Select(v => v.XY).ToList()
                    : new List<Vec2>();
                outline = WithWalls(ramp, outline);
                (Vec2 p0, Vec2 d0) = ramp.PointAt(0, 0);
                (Vec2 p1, Vec2 d1) = ramp.PointAt(total, 0);
                driveways.Add(new RoadDriveway { Point = p0, Direction = d0, Width = ramp.Width, Radius = ramp.JoinRadius, Exclude = outline });
                driveways.Add(new RoadDriveway { Point = p1, Direction = -d1, Width = ramp.Width, Radius = ramp.JoinRadius, Exclude = outline });
            }

            return specs.Count == 0 ? null : RoadNetwork.Build(specs, CrossingAreas(all, unitsPerMeter, false), crossings, rampMarks, driveways, rampWidths, HatchedAreas(all, unitsPerMeter));
        }

        /// <summary>Width of the cycle strip beside a pedestrian crossing.</summary>
        public const double BikeStripWidth = 0.8;

        /// <summary>Generated element type of the cycle strip beside a pedestrian crossing.</summary>
        public const string BikeCrossingType = "road.bike_crossing";

        /// <summary>True for a pedestrian crossing with a cycle strip beside it.</summary>
        public static bool HasBikeStrip(SiteElement e) => e.TypeId == CrossingType && e.Parameters.Bool("bikeLane", false);

        /// <summary>
        /// The oriented rectangle of a crossing outline (metres) between two distances along its first edge, measured
        /// from the start of the crossing (negative: before it, on the side of the cycle strip).
        /// </summary>
        private static List<Vec2> CrossingBox(IReadOnlyList<Vec2> outer, double from, double? to)
        {
            Vec2 o = outer[0];
            Vec2 d = outer[1] - outer[0];
            Vec2 u = d.Length < 1e-9 ? new Vec2(1, 0) : d.Normalized();
            Vec2 v = u.Perp();
            double minU = outer.Min(p => (p - o).Dot(u)), maxU = outer.Max(p => (p - o).Dot(u));
            double minV = outer.Min(p => (p - o).Dot(v)), maxV = outer.Max(p => (p - o).Dot(v));
            double u0 = minU + from, u1 = to.HasValue ? minU + to.Value : maxU;
            return new List<Vec2> { o + (u * u0) + (v * minV), o + (u * u1) + (v * minV), o + (u * u1) + (v * maxV), o + (u * u0) + (v * maxV) };
        }

        private static List<Vec2> Outer(PolygonGeometry pg, double unitsPerMeter) =>
            GeometryMath.Tessellate(pg.Outer, true, 0.02 * unitsPerMeter).Select(v => new Vec2(v.X / unitsPerMeter, v.Y / unitsPerMeter)).ToList();

        /// <summary>
        /// The cycle strips (0,80 m, metres) of the pedestrian crossings that have one: beside the crossing, on the side
        /// its outline starts at (the left side when the road runs to the right), as long as the stripes are wide.
        /// </summary>
        public static List<List<Vec2>> BikeStrips(IEnumerable<SiteElement> elements, double unitsPerMeter = 1.0)
        {
            var list = new List<List<Vec2>>();
            foreach (SiteElement e in elements)
            {
                if (HasBikeStrip(e) && e.Geometry is PolygonGeometry pg && pg.Outer.Count >= 3)
                {
                    list.Add(CrossingBox(Outer(pg, unitsPerMeter), -BikeStripWidth, 0));
                }
            }

            return list;
        }

        /// <summary>Cross-sections of all roads of the network among the elements (metres).</summary>
        public static List<RoadSpec> Specs(IEnumerable<SiteElement> elements, ElementCatalog catalog, double unitsPerMeter = 1.0)
        {
            var specs = new List<RoadSpec>();
            foreach (SiteElement e in elements)
            {
                ElementType? t = catalog.FindType(e.TypeId);
                RoadSpec? s = t != null ? Spec(e, t, unitsPerMeter) : null;
                if (s != null)
                {
                    specs.Add(s);
                }
            }

            return specs;
        }

        /// <summary>Elevation marks on the roads (metres; values relative to the base elevation of the roads).</summary>
        public static List<RoadLevelMark> LevelMarks(IEnumerable<SiteElement> elements, double unitsPerMeter = 1.0)
        {
            var list = new List<RoadLevelMark>();
            foreach (SiteElement e in elements)
            {
                if (e.TypeId == LevelMarkType && e.Geometry is PointGeometry pg)
                {
                    list.Add(new RoadLevelMark(e.Id, new Vec2(pg.Position.X / unitsPerMeter, pg.Position.Y / unitsPerMeter), e.Parameters.Number("level", 0)));
                }
            }

            return list;
        }

        /// <summary>Height profiles of the roads from their elevation marks.</summary>
        public static List<RoadProfile> Profiles(IEnumerable<SiteElement> elements, ElementCatalog catalog, double unitsPerMeter = 1.0)
        {
            List<SiteElement> all = elements as List<SiteElement> ?? elements.ToList();
            return RoadLevels.Profiles(Specs(all, catalog, unitsPerMeter), LevelMarks(all, unitsPerMeter));
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

            // with elevation marks the surfaces follow the road profiles in 3D (triangles on a 0,5 m grid)
            List<RoadProfile>? profiles = LevelMarks(all).Count > 0 ? Profiles(all, catalog) : null;

            // roundabouts: the kerb between the ring and its apron lies at the level of the asphalt (mountable)
            var aprons = new List<(Vec2 Centre, double Radius)>();
            foreach (SiteElement e in all.Where(x => x.TypeId == ApronType && x.Geometry is PolygonGeometry))
            {
                List<Vec2> ring = Outer((PolygonGeometry)e.Geometry, 1.0);
                if (ring.Count >= 3)
                {
                    var centre = new Vec2(ring.Average(q => q.X), ring.Average(q => q.Y));
                    aprons.Add((centre, ring.Max(q => q.DistanceTo(centre))));
                }
            }

            bool AtApron(RoadRegion region) => aprons.Any(a =>
                region.Outer.Points.All(q => q.DistanceTo(a.Centre) <= a.Radius + RoadSpec.KerbWidth + 0.1));

            foreach (KeyValuePair<RoadLevel, List<RoadRegion>> band in res.Bands)
            {
                (string type, string name) = LevelTypes[band.Key];
                foreach (RoadRegion region in band.Value)
                {
                    bool flush = band.Key == RoadLevel.Kerb && aprons.Count > 0 && AtApron(region);
                    Add(type, flush ? "Ivičnjak uz prijelazni prsten (u nivou asfalta)" : name, new PolygonGeometry(ToRing(region.Outer, z), region.Holes.Select(h => ToRing(h, z)).ToList()));
                    if (flush)
                    {
                        list[list.Count - 1].Parameters["topOffset"] = ParamValue.Of(0.0);
                    }

                    if (profiles != null)
                    {
                        list[list.Count - 1].Custom["triangles"] = ParamValue.Of(RoadLevels.Encode(RoadLevels.Drape(region, profiles)));
                    }
                }
            }

            // ramps in 3D: from the carriageway (kerb line) up to the sidewalk, across their whole depth
            double rampTop = catalog.FindType(RampType)?.CreateDefaultParameters().Number("topOffset", 0.02) ?? 0.02;
            double walkTop = catalog.FindType(LevelTypes[RoadLevel.Sidewalk].Type)?.CreateDefaultParameters().Number("topOffset", 0.15) ?? 0.15;
            for (int i = 0; i < res.Ramps.Count && i < res.RampFrames.Count; i++)
            {
                (List<Vec3[]> surface, List<Vec3[]> kerb) = RampTriangles(res.RampFrames[i], walkTop - rampTop, p => z + (profiles != null ? RoadLevels.Height(profiles, p) : 0));
                Add(RampType, "Rampa za invalide", new PolygonGeometry(ToRing(res.Ramps[i], z)));
                list[list.Count - 1].Custom["triangles"] = ParamValue.Of(RoadLevels.Encode(surface));

                // the lowered kerb and its flares, painted yellow
                Add(RampType, "Spušteni ivičnjak rampe", new PolygonGeometry(ToRing(res.Ramps[i], z)));
                list[list.Count - 1].Custom["triangles"] = ParamValue.Of(RoadLevels.Encode(kerb));
                list[list.Count - 1].Custom["openSurface"] = ParamValue.Of(true);
                list[list.Count - 1].Parameters["material"] = ParamValue.Of("line_paint_yellow");
            }

            // the bike lane where a driveway crosses it: at the carriageway across the driveway, sloping up to the
            // bike lane over 2 m on both sides; the kerb beside the slopes is lowered with it
            double bikeRise = catalog.FindType(LevelTypes[RoadLevel.Bike].Type)?.CreateDefaultParameters().Number("topOffset", 0.15) ?? 0.15;
            double kerbRise = catalog.FindType(LevelTypes[RoadLevel.Kerb].Type)?.CreateDefaultParameters().Number("topOffset", 0.15) ?? 0.15;
            List<List<Vec2>> rampBase = res.BikeRampBase;
            List<Vec2> rampDirs = res.BikeRampDirections;
            double Lowered(Vec2 p, double rise)
            {
                double d = RoadNetwork.BikeRampLength;
                for (int k = 0; k < rampBase.Count; k++)
                {
                    d = Math.Min(d, k < rampDirs.Count ? DistanceAlong(rampBase[k], rampDirs[k], p) : DistanceTo(rampBase[k], p));
                }

                return z + (profiles != null ? RoadLevels.Height(profiles, p) : 0) + (rise * Math.Min(1, d / RoadNetwork.BikeRampLength));
            }

            foreach (RoadRegion region in res.BikeRamps)
            {
                Add(BikeRampType, "Spuštena biciklistička staza preko kolnog prilaza", new PolygonGeometry(ToRing(region.Outer, z), region.Holes.Select(h => ToRing(h, z)).ToList()));
                list[list.Count - 1].Custom["triangles"] = ParamValue.Of(RoadLevels.Encode(RoadLevels.Drape(region, p => Lowered(p, bikeRise))));
            }

            foreach (RoadRegion region in res.KerbRamps)
            {
                Add(LevelTypes[RoadLevel.Kerb].Type, "Spušteni ivičnjak uz biciklističku stazu", new PolygonGeometry(ToRing(region.Outer, z), region.Holes.Select(h => ToRing(h, z)).ToList()));
                list[list.Count - 1].Parameters["topOffset"] = ParamValue.Of(0.0);
                list[list.Count - 1].Custom["triangles"] = ParamValue.Of(RoadLevels.Encode(RoadLevels.Drape(region, p => Lowered(p, kerbRise))));
            }

            // cycle strips beside the pedestrian crossings (blue, a little above the carriageway)
            foreach (List<Vec2> strip in BikeStrips(all))
            {
                Add(BikeCrossingType, "Biciklistička staza uz pješački prijelaz", new PolygonGeometry(new Ring(strip.Select(p => p.WithZ(z)).ToList())));
                if (profiles != null)
                {
                    var tris = new List<Vec3[]>();
                    int parts = Math.Max(1, (int)Math.Ceiling(strip[1].DistanceTo(strip[2]) / 0.5));
                    Vec3 H(Vec2 p) => p.WithZ(z + RoadLevels.Height(profiles, p));
                    for (int i = 0; i < parts; i++)
                    {
                        Vec2 a0 = strip[0] + ((strip[3] - strip[0]) * ((double)i / parts)), a1 = strip[0] + ((strip[3] - strip[0]) * ((double)(i + 1) / parts));
                        Vec2 b0 = strip[1] + ((strip[2] - strip[1]) * ((double)i / parts)), b1 = strip[1] + ((strip[2] - strip[1]) * ((double)(i + 1) / parts));
                        tris.Add(new[] { H(a0), H(b0), H(b1) });
                        tris.Add(new[] { H(a0), H(b1), H(a1) });
                    }

                    list[list.Count - 1].Custom["triangles"] = ParamValue.Of(RoadLevels.Encode(tris));
                }
            }

            foreach (RoadMarking m in res.Markings)
            {
                Add(MarkingType, m.Centre ? "Razdjelna linija" : "Linija traka", new PathGeometry(new Ring(m.Points.Select(p => p.WithZ(z)).ToList()), false), ("width", 0.12));
                if (profiles != null)
                {
                    PathsD band = Clipper.InflatePaths(new PathsD { new PathD(m.Points.Select(p => new PointD(p.X, p.Y))) }, 0.06, JoinType.Miter, EndType.Butt, 4, 4);
                    list[list.Count - 1].Custom["triangles"] = ParamValue.Of(RoadLevels.Encode(DrapeOn(band, profiles, MarkingLift)));
                }
            }

            foreach (RoadArrow a in res.Arrows)
            {
                // the arrow element starts at its tail; the network gives the centre of a 5 m arrow
                Vec2 tail = a.Position - (new Vec2(Math.Cos(a.Angle), Math.Sin(a.Angle)) * 2.5);
                Add(ArrowType, "Strelica smjera", new PointGeometry(tail.WithZ(z), a.Angle), ("length", 5.0), ("width", 0.64), ("shaftWidth", 0.18));
                if (profiles != null)
                {
                    var outline = new PathsD { new PathD(a.Outline().Select(p => new PointD(p.X, p.Y))) };
                    list[list.Count - 1].Custom["triangles"] = ParamValue.Of(RoadLevels.Encode(DrapeOn(outline, profiles, MarkingLift)));
                }
            }

            // on roads with heights the stripes of the crossings and hatched areas lie on the sloped surface too
            if (profiles != null)
            {
                foreach (SiteElement e in all.Where(x => (x.TypeId == CrossingType || x.TypeId == HatchedAreaType) && x.Geometry is PolygonGeometry))
                {
                    List<Vec2> outer = Outer((PolygonGeometry)e.Geometry, 1.0);
                    double stripe = e.Parameters.Number("stripeWidth", e.TypeId == CrossingType ? 0.5 : 0.3);
                    double gap = e.Parameters.Number("gapWidth", e.TypeId == CrossingType ? 0.5 : 1.0);
                    var bars = new PathsD(StripeBars(outer, stripe, gap).Select(b => new PathD(b.Select(p => new PointD(p.X, p.Y)))));
                    List<Vec3[]> tris = DrapeOn(bars, profiles, MarkingLift);
                    if (tris.Count > 0)
                    {
                        e.Custom["triangles"] = ParamValue.Of(RoadLevels.Encode(tris));
                    }
                }
            }

            return list;
        }

        /// <summary>Generated element type of the bike lane lowered across a driveway.</summary>
        public const string BikeRampType = "road.bike_ramp";

        /// <summary>Distance from a point to a polygon (0 inside).</summary>
        /// <summary>The ramp's outline together with its retaining walls (nothing of the road lies on either).</summary>
        private static List<Vec2> WithWalls(GarageRamp ramp, List<Vec2> outline)
        {
            (double From, double To)? span = ramp.WallSpan();
            if (outline.Count < 3 || !span.HasValue || span.Value.To - span.Value.From < 0.01)
            {
                return outline;
            }

            List<Vec2> Line((List<Vec2> Points, List<double> Bulges) sub) =>
                GeometryMath.Tessellate(new Ring(sub.Points.Select(q => q.WithZ(0)).ToList(), sub.Bulges), false, 0.005).Select(v => v.XY).ToList();

            double half = ramp.Width / 2;
            var all = new PathsD { new PathD(outline.Select(v => new PointD(v.X, v.Y))) };
            foreach (double side in new[] { 1.0, -1.0 })
            {
                List<Vec2> inner = Line(ramp.Sub(side * half, span.Value.From, span.Value.To));
                List<Vec2> outer = Line(ramp.Sub(side * (half + GarageRamp.WallThickness), span.Value.From, span.Value.To));
                outer.Reverse();
                all.Add(new PathD(inner.Concat(outer).Select(v => new PointD(v.X, v.Y))));
            }

            PathsD union = Clipper.Union(all, FillRule.NonZero);
            PathD? best = union.OrderByDescending(x => Math.Abs(Clipper.Area(x))).FirstOrDefault();
            return best == null || best.Count < 3 ? outline : best.Select(v => new Vec2(v.x, v.y)).ToList();
        }

        /// <summary>
        /// Distance from <paramref name="p"/> to a polygon measured along <paramref name="dir"/> (forwards or back): 0
        /// inside it or beside it. To a straight side
        /// it changes evenly, so a slope measured with it is one plane.
        /// </summary>
        private static double DistanceAlong(IReadOnlyList<Vec2> poly, Vec2 dir, Vec2 p)
        {
            // a point beside the polygon (the kerb next to the bike lane) is measured as if it were at its side
            var n = new Vec2(-dir.Y, dir.X);
            double wMin = poly.Min(v => v.Dot(n)) + 1e-6, wMax = poly.Max(v => v.Dot(n)) - 1e-6, w = p.Dot(n);
            if (wMin <= wMax)
            {
                p = p + (n * (Math.Max(wMin, Math.Min(wMax, w)) - w));
            }

            if (DistanceTo(poly, p) < 1e-9)
            {
                return 0;
            }

            double best = double.MaxValue;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vec2 a = poly[j], e = poly[i] - a;
                double den = (dir.X * e.Y) - (dir.Y * e.X);
                if (Math.Abs(den) < 1e-12)
                {
                    continue;
                }

                Vec2 ap = a - p;
                double s = ((ap.X * e.Y) - (ap.Y * e.X)) / den; // along dir
                double u = ((ap.X * dir.Y) - (ap.Y * dir.X)) / den; // along the side
                if (u >= -1e-9 && u <= 1 + 1e-9)
                {
                    best = Math.Min(best, Math.Abs(s));
                }
            }

            return best;
        }

        private static double DistanceTo(IReadOnlyList<Vec2> poly, Vec2 p)
        {
            bool inside = false;
            double best = double.MaxValue;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vec2 a = poly[j], b = poly[i];
                if ((b.Y > p.Y) != (a.Y > p.Y) && p.X < ((a.X - b.X) * (p.Y - b.Y) / (a.Y - b.Y)) + b.X)
                {
                    inside = !inside;
                }

                Vec2 ab = b - a;
                double l2 = ab.Dot(ab);
                double t = l2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, (p - a).Dot(ab) / l2));
                best = Math.Min(best, p.DistanceTo(a + (ab * t)));
            }

            return inside ? 0 : best;
        }

        /// <summary>Element type of the mountable apron around the island of a roundabout.</summary>
        public const string ApronType = "road.roundabout_apron";

        /// <summary>Height of painted markings above the road surface in 3D.</summary>
        public const double MarkingLift = 0.005;

        /// <summary>Triangles of flat shapes (metres) laid on the road surfaces with heights, lifted a little above them.</summary>
        private static List<Vec3[]> DrapeOn(PathsD shapes, IReadOnlyList<RoadProfile> profiles, double lift)
        {
            var tris = new List<Vec3[]>();
            PathsD union = Clipper.Union(shapes, new PathsD(), FillRule.NonZero, 4);
            foreach (PathD path in union.Where(q => q.Count >= 3 && Clipper.Area(q) > 1e-6))
            {
                var outer = new RoadLoop(path.Select(q => new Vec2(q.x, q.y)).ToList(), path.Select(_ => 0.0).ToList());
                var holes = union.Where(q => q.Count >= 3 && Clipper.Area(q) < -1e-6 && Clipper.PointInPolygon(q[0], path) != PointInPolygonResult.IsOutside)
                    .Select(q => new RoadLoop(q.Select(v => new Vec2(v.x, v.y)).ToList(), q.Select(_ => 0.0).ToList())).ToList();
                foreach (Vec3[] t in RoadLevels.Drape(new RoadRegion(outer, holes, Clipper.Area(path)), profiles))
                {
                    tris.Add(t.Select(v => new Vec3(v.X, v.Y, v.Z + lift)).ToArray());
                }
            }

            return tris;
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

        /// <summary>
        /// A kerb ramp in 3D, like a dropped kerb with flares: the kerb is lowered to the carriageway over 1,20 m and rises
        /// back to its height over 0,80 m on both sides; the surface behind slopes from the lowered kerb up by
        /// <paramref name="rise"/> to the sidewalk at the back and at the sides. Returns the surface and the kerb.
        /// </summary>
        public static (List<Vec3[]> Surface, List<Vec3[]> Kerb) RampTriangles(RoadRampFrame f, double rise, Func<Vec2, double> baseHeight)
        {
            double a = f.Width / 2, k = Math.Min(RoadSpec.KerbWidth, f.Depth / 2), d = f.Depth;
            double a1 = a + 0.045, f0 = a + RoadNetwork.RampFlare, f1 = f0 - 0.045, b = a + RoadNetwork.RampBackFlare;
            double zk = Math.Min(0.02, rise);
            bool flip = f.Along.Cross(f.Inward) < 0;
            Vec3 P(double u, double v, double z)
            {
                Vec2 p = f.Foot + (f.Along * u) + (f.Inward * v);
                return p.WithZ(baseHeight(p) + z);
            }

            Vec3[] T(Vec3 p, Vec3 q, Vec3 r) => flip ? new[] { p, r, q } : new[] { p, q, r };
            var surface = new List<Vec3[]>
            {
                T(P(-a1, k, zk), P(a1, k, zk), P(b, d, rise)),
                T(P(-a1, k, zk), P(b, d, rise), P(-b, d, rise)),
                T(P(a1, k, zk), P(f1, k, rise), P(b, d, rise)),
                T(P(-f1, k, rise), P(-a1, k, zk), P(-b, d, rise)),
            };
            var kerb = new List<Vec3[]>
            {
                T(P(-a, 0, 0), P(a, 0, 0), P(a1, k, zk)),
                T(P(-a, 0, 0), P(a1, k, zk), P(-a1, k, zk)),
                T(P(a, 0, 0), P(f0, 0, rise), P(f1, k, rise)),
                T(P(a, 0, 0), P(f1, k, rise), P(a1, k, zk)),
                T(P(-f0, 0, rise), P(-a, 0, 0), P(-a1, k, zk)),
                T(P(-f0, 0, rise), P(-a1, k, zk), P(-f1, k, rise)),

                // the face of the flares towards the carriageway
                T(P(a, 0, 0), P(f0, 0, 0), P(f0, 0, rise)),
                T(P(-f0, 0, 0), P(-a, 0, 0), P(-f0, 0, rise)),
            };
            return (surface, kerb);
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
