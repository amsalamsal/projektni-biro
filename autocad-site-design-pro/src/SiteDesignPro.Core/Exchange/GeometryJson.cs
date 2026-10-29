using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SiteDesignPro.Core.Geometry;

namespace SiteDesignPro.Core.Exchange
{
    /// <summary>Converts element geometry to and from the geometry.json representation.</summary>
    public static class GeometryJson
    {
        /// <summary>Coordinates are rounded to this many decimals of a metre (1e-9 m) to remove floating-point noise.</summary>
        public const int CoordinateDecimals = 9;

        public static JsonObject ToJson(string id, ElementGeometry g)
        {
            var o = new JsonObject { ["id"] = id, ["kind"] = ElementGeometry.KindCode(g.Kind) };
            switch (g)
            {
                case PointGeometry p:
                    o["position"] = Vec(p.Position);
                    o["rotation"] = Round(GeometryMath.NormalizeAngle(p.Rotation));
                    break;
                case PathGeometry path:
                    o["vertices"] = Vertices(path.Ring);
                    if (path.Ring.HasArcs)
                    {
                        o["bulges"] = Bulges(path.Ring, path.Closed);
                    }

                    o["closed"] = path.Closed;
                    break;
                case PolygonGeometry poly:
                    o["outer"] = RingJson(poly.Outer);
                    if (poly.Holes.Count > 0)
                    {
                        o["holes"] = new JsonArray(poly.Holes.Select(h => (JsonNode)RingJson(h)).ToArray());
                    }

                    break;
                default:
                    throw new NotSupportedException("Nepodržana vrsta geometrije " + g.GetType().Name);
            }

            return o;
        }

        public static ElementGeometry FromJson(JsonElement e)
        {
            string kind = e.GetProperty("kind").GetString() ?? string.Empty;
            switch (kind)
            {
                case "point":
                    return new PointGeometry(ReadVec(e.GetProperty("position")), e.GetProperty("rotation").GetDouble());
                case "path":
                    {
                        List<Vec3> verts = e.GetProperty("vertices").EnumerateArray().Select(ReadVec).ToList();
                        List<double>? bulges = e.TryGetProperty("bulges", out JsonElement b) ? b.EnumerateArray().Select(x => x.GetDouble()).ToList() : null;
                        return new PathGeometry(new Ring(verts, bulges), e.GetProperty("closed").GetBoolean());
                    }

                case "polygon":
                    {
                        Ring outer = ReadRing(e.GetProperty("outer"));
                        var holes = new List<Ring>();
                        if (e.TryGetProperty("holes", out JsonElement h))
                        {
                            holes.AddRange(h.EnumerateArray().Select(ReadRing));
                        }

                        return new PolygonGeometry(outer, holes);
                    }

                default:
                    throw new FormatException("Nepoznata vrsta geometrije '" + kind + "'.");
            }
        }

        private static Ring ReadRing(JsonElement r)
        {
            List<Vec3> verts = r.GetProperty("vertices").EnumerateArray().Select(ReadVec).ToList();
            List<double>? bulges = r.TryGetProperty("bulges", out JsonElement b) ? b.EnumerateArray().Select(x => x.GetDouble()).ToList() : null;
            return new Ring(verts, bulges);
        }

        private static Vec3 ReadVec(JsonElement a)
        {
            if (a.ValueKind != JsonValueKind.Array || a.GetArrayLength() != 3)
            {
                throw new FormatException("Koordinate moraju biti nizovi [x, y, z].");
            }

            var v = new Vec3(a[0].GetDouble(), a[1].GetDouble(), a[2].GetDouble());
            if (!v.IsFinite)
            {
                throw new FormatException("Koordinate moraju biti konačni brojevi.");
            }

            return v;
        }

        private static JsonObject RingJson(Ring ring)
        {
            var o = new JsonObject { ["vertices"] = Vertices(ring) };
            if (ring.HasArcs)
            {
                o["bulges"] = Bulges(ring, true);
            }

            return o;
        }

        private static JsonArray Vertices(Ring ring) => new JsonArray(ring.Vertices.Select(v => (JsonNode)Vec(v)).ToArray());

        private static JsonArray Bulges(Ring ring, bool closed)
        {
            int n = closed ? ring.Count : ring.Count - 1;
            var arr = new JsonArray();
            for (int i = 0; i < n; i++)
            {
                arr.Add(Round(ring.BulgeAt(i)));
            }

            return arr;
        }

        private static JsonArray Vec(Vec3 v) => new JsonArray(Round(v.X), Round(v.Y), Round(v.Z));

        public static double Round(double v)
        {
            double r = Math.Round(v, CoordinateDecimals, MidpointRounding.AwayFromZero);
            return r == 0 ? 0 : r; // normalise -0
        }

        /// <summary>Invariant, round-trippable number text used by canonical hashing.</summary>
        public static string NumberText(double v) => Round(v).ToString("R", CultureInfo.InvariantCulture);
    }
}
