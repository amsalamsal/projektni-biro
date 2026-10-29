using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Input;

namespace SiteDesignPro.Core.Samples
{
    /// <summary>
    /// A synthetic "as received" source drawing for the sample project, in the same millimetre WCS as the sample:
    /// parcel boundary, two building footprints, regulation-plan road edges, a parterre line, elevation labels and
    /// a survey (3D points and one 3D breakline) on typical Bosnian layer names. It lets the acceptance workflow
    /// (layer mapping → input validation → datum → elevation assignment → terrain) run without a real project DWG.
    /// The data is invented for demonstration and is not a survey of a real site.
    /// </summary>
    public static class SampleSourceDrawing
    {
        public const string ParcelLayer = "KP_GRANICA";
        public const string BuildingLayer = "OBJEKAT_TLOCRT";
        public const string RoadLayer = "SAOBRACAJNICE_RP";
        public const string ParterreLayer = "LINIJA_PARTERA";
        public const string LabelLayer = "KOTE";
        public const string SurveyLayer = "GEO_TACKE";
        public const string BreaklineLayer = "GEO_PRELOMNICE";

        /// <summary>Mapping a user would set up for this drawing.</summary>
        public static LayerMapping Mapping()
        {
            var m = new LayerMapping { Name = "Primjer – izvorni crtež" };
            m.Set(SourceRole.ParcelBoundary, new[] { ParcelLayer });
            m.Set(SourceRole.BuildingFootprint, new[] { BuildingLayer });
            m.Set(SourceRole.Roads, new[] { RoadLayer });
            m.Set(SourceRole.ParterreLine, new[] { ParterreLayer });
            m.Set(SourceRole.ElevationLabels, new[] { LabelLayer });
            m.Set(SourceRole.TerrainPoints, new[] { SurveyLayer });
            m.Set(SourceRole.Breaklines, new[] { BreaklineLayer });
            return m;
        }

        /// <summary>Objects in drawing units (mm, absolute Z in mm) - same frame as <see cref="SampleResidentialComplex.CreateDrawing"/>.</summary>
        public static List<SourceObject> Create()
        {
            Vec3 o = SampleResidentialComplex.OriginMeters;
            const double k = 1000.0;
            int n = 0;
            SourceObject Poly(string layer, bool closed, params (double X, double Y)[] pts) => new SourceObject
            {
                Handle = "S" + (++n).ToString(CultureInfo.InvariantCulture),
                Layer = layer,
                Kind = SourceKind.Polyline,
                Closed = closed,
                Vertices = pts.Select(p => new Vec3((o.X + p.X) * k, (o.Y + p.Y) * k, 0)).ToList(),
            };

            SourceObject Text(string text, double x, double y) => new SourceObject
            {
                Handle = "S" + (++n).ToString(CultureInfo.InvariantCulture),
                Layer = LabelLayer,
                Kind = SourceKind.Text,
                Text = text,
                Vertices = { new Vec3((o.X + x) * k, (o.Y + y) * k, 0) },
            };

            var list = new List<SourceObject>
            {
                Poly(ParcelLayer, true, (0, 0), (120, 0), (120, 80), (0, 80)),
                Poly(BuildingLayer, true, (15, 45), (55, 45), (55, 63), (15, 63)),
                Poly(BuildingLayer, true, (70, 45), (105, 45), (105, 63), (70, 63)),
                Poly(RoadLayer, true, (0, 27), (120, 27), (120, 33), (0, 33)),
                Poly(ParterreLayer, true, (5, 34), (115, 34), (115, 64), (5, 64)),
                Text("±0,00", 30, 55),
                Text("±0,00", 85, 55),
                Text("+0,00", 60, 40),
                Text("-0,15", 40, 30),
                Text("KT 520,00", 100, 31),
            };

            // survey: same synthetic surface as SampleResidentialComplex.CreateTerrainSources, absolute Z in mm
            Terrain.TerrainSources ts = SampleResidentialComplex.CreateTerrainSources();
            foreach (Vec3 p in ts.Points)
            {
                list.Add(new SourceObject
                {
                    Handle = "S" + (++n).ToString(CultureInfo.InvariantCulture),
                    Layer = SurveyLayer,
                    Kind = SourceKind.Point,
                    Vertices = { new Vec3((o.X + p.X) * k, (o.Y + p.Y) * k, (o.Z + p.Z) * k) },
                });
            }

            foreach (IReadOnlyList<Vec3> b in ts.Breaklines)
            {
                list.Add(new SourceObject
                {
                    Handle = "S" + (++n).ToString(CultureInfo.InvariantCulture),
                    Layer = BreaklineLayer,
                    Kind = SourceKind.Polyline,
                    Vertices = b.Select(p => new Vec3((o.X + p.X) * k, (o.Y + p.Y) * k, (o.Z + p.Z) * k)).ToList(),
                });
            }

            return list;
        }
    }
}
