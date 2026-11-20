using System;
using System.Collections.Generic;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Layout;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Roads;

namespace SiteDesignPro.Core.Samples
{
    /// <summary>
    /// A small drawing with one example of every main tool, to learn on (SDPSAMPLE → Alati): a T junction of two
    /// streets, a roundabout, a straight garage ramp from a street, parking rows (perpendicular with an accessible
    /// bay, parallel, 45°), a pedestrian crossing and a row of trees along a path. Metres, around (0, 0).
    /// </summary>
    public static class ToolExamples
    {
        public static List<SiteElement> Create(ElementCatalog cat)
        {
            var list = new List<SiteElement>();
            SiteElement New(string type, ElementGeometry g, string name)
            {
                ElementType t = cat.RequireType(type);
                var e = new SiteElement(Guid.NewGuid().ToString("D"), type, g) { Parameters = t.CreateDefaultParameters(), Name = name };
                list.Add(e);
                return e;
            }

            PathGeometry Path(params double[] xy)
            {
                var pts = new List<Vec3>();
                for (int i = 0; i + 1 < xy.Length; i += 2)
                {
                    pts.Add(new Vec3(xy[i], xy[i + 1], 0));
                }

                return new PathGeometry(new Ring(pts), false);
            }

            // streets: a T junction
            New(RoadElements.StreetType, Path(0, 0, 140, 0), "Primjer: glavna ulica");
            New(RoadElements.StreetType, Path(70, 0, 70, 70), "Primjer: sporedna ulica");

            // a pedestrian crossing over the main street (bars 0,50 / 0,50 m across 2 lanes)
            SiteElement crossing = New(RoadElements.CrossingType, new PolygonGeometry(new Ring(new List<Vec3>
            {
                new Vec3(30, -3.25, 0), new Vec3(34, -3.25, 0), new Vec3(34, 3.25, 0), new Vec3(30, 3.25, 0),
            })), "Primjer: pješački prijelaz");
            crossing.Parameters["stripeWidth"] = ParamValue.Of(0.5);
            crossing.Parameters["gapWidth"] = ParamValue.Of(0.5);

            // a roundabout at the end of the main street
            var options = new RoundaboutOptions { Centre = new Vec2(200, 0), IslandRadius = 12, Arms = 3, ArmLength = 25, FirstArmAngle = Math.PI };
            Roundabout.Normalize(options);
            foreach (RoundaboutPart part in Roundabout.Create(options))
            {
                SiteElement e = New(part.TypeId, part.Geometry, string.IsNullOrEmpty(part.Name) ? "Primjer: kružni tok" : part.Name);
                foreach (KeyValuePair<string, ParamValue> kv in part.Parameters)
                {
                    e.Parameters[kv.Key] = kv.Value;
                }
            }

            // a straight garage ramp down from the side street (3 m over 22 m: 13,6 %)
            SiteElement ramp = New(GarageRamp.Type, Path(80, 40, 102, 40), "Primjer: garažna rampa");
            ramp.Parameters["kind"] = ParamValue.Of("straight");
            ramp.Parameters["direction"] = ParamValue.Of("one_way");
            ramp.Parameters["laneWidth"] = ParamValue.Of(3.0);
            ramp.Parameters["margin"] = ParamValue.Of(0.25);
            ramp.Parameters["startLevel"] = ParamValue.Of(0.0);
            ramp.Parameters["endLevel"] = ParamValue.Of(-3.0);
            ramp.Parameters["width"] = ParamValue.Of(3.5);

            // parking rows below the main street: perpendicular (one accessible bay), parallel and 45°
            list.AddRange(ParkingRows.Elements(cat, new Vec2(0, -40), new Vec2(20, -40), new Vec2(0, -30), ParkingRows.Perpendicular, 6, 1, 1));
            list.AddRange(ParkingRows.Elements(cat, new Vec2(0, -50), new Vec2(20, -50), new Vec2(0, -60), ParkingRows.Parallel, 4, 0, 6));
            list.AddRange(ParkingRows.Elements(cat, new Vec2(40, -80), new Vec2(60, -80), new Vec2(40, -70), ParkingRows.Angled, 5, 0, 10));

            // a row of trees along a path north of the main street
            ElementType? tree = cat.FindType("landscape.tree_deciduous");
            if (tree != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    New(tree.Id, new PointGeometry(new Vec3(5 + (i * 10), 14, 0), 0), "Primjer: drvored");
                }
            }

            return list;
        }
    }
}
