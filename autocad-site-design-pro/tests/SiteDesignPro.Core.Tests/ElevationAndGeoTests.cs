using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Elevation;
using SiteDesignPro.Core.Geo;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Text;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    public class ElevationDatumTests
    {
        [Fact]
        public void NewDatum_HasNoElevation_AndRefusesAbsoluteConversion()
        {
            var d = new ElevationDatum();
            Assert.False(d.IsDefined);
            Assert.Null(d.BuildingZeroAbsolute);
            Assert.Throws<DatumNotDefinedException>(() => d.ToAbsolute(0.25));
            Assert.Contains(d.Problems(), p => p.Contains("nema definisanu apsolutnu", StringComparison.Ordinal));
        }

        [Fact]
        public void AbsoluteIsDatumPlusRelative()
        {
            var d = new ElevationDatum { BuildingZeroAbsolute = 518.56, VerticalDatum = "Trst" };
            Assert.Equal(518.81, d.ToAbsolute(0.25), 9);
            Assert.Equal(-0.15, d.ToRelative(518.41), 9);
            Assert.Equal(518.56, d.ParterreAbsolute!.Value, 9);
            Assert.Empty(d.Problems());
        }

        [Fact]
        public void ParterreFollowsBuildingZeroUnlessConfigured_RoadsUseUserOffset()
        {
            var d = new ElevationDatum { BuildingZeroAbsolute = 300.00 };
            Assert.Equal(0, d.ParterreRelativeElevation);
            Assert.Equal(-0.15, d.RoadRelative(0.15), 9);
            Assert.Null(d.DefaultRoadOffsetBelowParterre); // no built-in road offset

            d.ParterreEqualsBuildingZero = false;
            d.ParterreRelative = -0.30;
            Assert.Equal(299.70, d.ParterreAbsolute!.Value, 9);
            Assert.Equal(-0.42, d.RoadRelative(0.12), 9);
            Assert.Equal(-0.42, d.ResolveRelative(new ElevationAssignment { Mode = ElevationMode.RoadOffset, Value = 0.12 }), 9);
            Assert.Equal(-0.10, d.ResolveRelative(new ElevationAssignment { Mode = ElevationMode.ParterreOffset, Value = 0.20 }), 9);
            Assert.Equal(1.5, d.ResolveRelative(new ElevationAssignment { Mode = ElevationMode.Absolute, Value = 301.5 }), 9);
        }

        [Theory]
        [InlineData(0.0, "±0,00")]
        [InlineData(0.25, "+0,25")]
        [InlineData(-0.155, "-0,16")]
        [InlineData(-0.001, "±0,00")]
        public void RelativeFormatting(double v, string expected) => Assert.Equal(expected, ElevationDatum.FormatRelative(v));

        [Fact]
        public void AssignmentRoundTripsThroughProperties()
        {
            var props = new SortedDictionary<string, string>(StringComparer.Ordinal);
            new ElevationAssignment { Mode = ElevationMode.RoadOffset, Value = 0.15, Source = "label:2A3" }.WriteTo(props, -0.15);
            ElevationAssignment? back = ElevationAssignment.ReadFrom(props);
            Assert.NotNull(back);
            Assert.Equal(ElevationMode.RoadOffset, back!.Mode);
            Assert.Equal(0.15, back.Value);
            Assert.Equal("label:2A3", back.Source);
        }
    }

    public class ElevationLabelTests
    {
        [Theory]
        [InlineData("±0.00", LabelKind.Relative, 0.0)]
        [InlineData("%%p0,00", LabelKind.Relative, 0.0)]
        [InlineData("+0,25", LabelKind.Relative, 0.25)]
        [InlineData("-0.15", LabelKind.Relative, -0.15)]
        [InlineData("{\\fArial|b0;\\H2.5;+0,30}", LabelKind.Relative, 0.30)]
        public void RelativeLabels(string text, LabelKind kind, double value)
        {
            ParsedElevation p = ElevationLabelParser.Parse(text);
            Assert.Equal(kind, p.Kind);
            Assert.Equal(value, p.Relative!.Value, 9);
        }

        [Theory]
        [InlineData("KT 518,81", 518.81)]
        [InlineData("518.81 m n.v.", 518.81)]
        [InlineData("KT=1234,50", 1234.50)]
        public void AbsoluteLabels(string text, double value)
        {
            ParsedElevation p = ElevationLabelParser.Parse(text);
            Assert.Equal(LabelKind.Absolute, p.Kind);
            Assert.Equal(value, p.Absolute!.Value, 9);
        }

        [Fact]
        public void SmallUnsignedValue_IsAmbiguous_NeverGuessed()
        {
            ParsedElevation p = ElevationLabelParser.Parse("0.45");
            Assert.Equal(LabelKind.Ambiguous, p.Kind);
            Assert.Null(p.Relative);
            Assert.Null(p.Absolute);
        }

        [Fact]
        public void PairOfAbsoluteAndRelative_IsReadAsBoth()
        {
            ParsedElevation p = ElevationLabelParser.Parse("NK=518,56 / ±0,00");
            Assert.Equal(518.56, p.Absolute!.Value, 9);
            Assert.Equal(0.0, p.Relative!.Value, 9);
        }

        [Fact]
        public void TextWithoutNumber_IsInvalid() => Assert.Equal(LabelKind.Invalid, ElevationLabelParser.Parse("kota terena").Kind);
    }

    public class ElevationAssignerTests
    {
        private static AssignmentTarget Rect(string handle, double x0, double y0, double x1, double y1) => new AssignmentTarget
        {
            Handle = handle,
            Description = handle,
            Closed = true,
            Area = (x1 - x0) * (y1 - y0),
            Boundary = new Ring(new List<Vec3> { new Vec3(x0, y0, 0), new Vec3(x1, y0, 0), new Vec3(x1, y1, 0), new Vec3(x0, y1, 0) }),
        };

        [Fact]
        public void LabelGoesOnlyToTheSmallestContainingObject()
        {
            var datum = new ElevationDatum { BuildingZeroAbsolute = 518.56 };
            var targets = new List<AssignmentTarget> { Rect("parcel", 0, 0, 100, 100), Rect("terrace", 10, 10, 20, 20), Rect("road", 50, 0, 60, 100) };
            List<AssignmentProposal> p = ElevationAssigner.Propose(new[]
            {
                new SourceLabel { Handle = "L1", Text = "+0,25", Position = new Vec3(15, 15, 0) },
                new SourceLabel { Handle = "L2", Text = "KT 518,41", Position = new Vec3(55, 40, 0) },
                new SourceLabel { Handle = "L3", Text = "-0,15", Position = new Vec3(200, 200, 0) },
            }, targets, datum);

            Assert.Equal(ProposalStatus.Proposed, p[0].Status);
            Assert.Equal("terrace", p[0].Target!.Handle);
            Assert.Equal(0.25, p[0].RelativeElevation!.Value, 9);
            Assert.Equal("road", p[1].Target!.Handle);
            Assert.Equal(-0.15, p[1].RelativeElevation!.Value, 9);
            Assert.Equal(ProposalStatus.NoTarget, p[2].Status);
        }

        [Fact]
        public void LeaderTarget_WinsOverTextPosition()
        {
            var datum = new ElevationDatum { BuildingZeroAbsolute = 100 };
            var targets = new List<AssignmentTarget> { Rect("a", 0, 0, 10, 10), Rect("b", 20, 0, 30, 10) };
            List<AssignmentProposal> p = ElevationAssigner.Propose(new[] { new SourceLabel { Text = "+0,10", Position = new Vec3(5, 5, 0), Target = new Vec3(25, 5, 0) } }, targets, datum);
            Assert.Equal("b", p[0].Target!.Handle);
        }

        [Fact]
        public void AbsoluteLabelWithoutDatum_IsNotConverted()
        {
            List<AssignmentProposal> p = ElevationAssigner.Propose(new[] { new SourceLabel { Text = "518,80 m n.v.", Position = new Vec3(5, 5, 0) } },
                new List<AssignmentTarget> { Rect("a", 0, 0, 10, 10) }, new ElevationDatum());
            Assert.Equal(ProposalStatus.BadLabel, p[0].Status);
            Assert.Null(p[0].RelativeElevation);
        }

        [Fact]
        public void InconsistentPairAndConflictingLabels_AreBlocked()
        {
            var datum = new ElevationDatum { BuildingZeroAbsolute = 518.56 };
            var targets = new List<AssignmentTarget> { Rect("a", 0, 0, 10, 10), Rect("b", 20, 0, 30, 10) };
            List<AssignmentProposal> p = ElevationAssigner.Propose(new[]
            {
                new SourceLabel { Text = "518,81 m n.v. / +0,20", Position = new Vec3(5, 5, 0) },
                new SourceLabel { Text = "+0,10", Position = new Vec3(22, 5, 0) },
                new SourceLabel { Text = "+0,30", Position = new Vec3(28, 5, 0) },
            }, targets, datum);
            Assert.Equal(ProposalStatus.Conflict, p[0].Status);
            Assert.Equal(ProposalStatus.Conflict, p[1].Status);
            Assert.Equal(ProposalStatus.Conflict, p[2].Status);
        }

        [Fact]
        public void EqualOverlappingTargets_AreAmbiguous()
        {
            var datum = new ElevationDatum { BuildingZeroAbsolute = 100 };
            var targets = new List<AssignmentTarget> { Rect("a", 0, 0, 10, 10), Rect("a-copy", 0, 0, 10, 10) };
            List<AssignmentProposal> p = ElevationAssigner.Propose(new[] { new SourceLabel { Text = "+0,10", Position = new Vec3(5, 5, 0) } }, targets, datum);
            Assert.Equal(ProposalStatus.AmbiguousTarget, p[0].Status);
        }
    }

    public class BosnianFormatTests
    {
        [Theory]
        [InlineData(518.56, 2, "518,56")]
        [InlineData(-0.004, 2, "0,00")]
        [InlineData(1234.5, 1, "1234,5")]
        public void DecimalComma(double v, int d, string expected) => Assert.Equal(expected, Bs.Num(v, d));

        [Theory]
        [InlineData("518,56", 518.56)]
        [InlineData("518.56", 518.56)]
        [InlineData("1.234,5", 1234.5)]
        [InlineData("1,234.5", 1234.5)]
        [InlineData("±0,00", 0.0)]
        [InlineData("-0,15", -0.15)]
        public void ParsesCommaAndPoint(string text, double expected)
        {
            Assert.True(Bs.TryParse(text, out double v));
            Assert.Equal(expected, v, 9);
        }

        [Fact]
        public void Grouping_AndPlurals()
        {
            Assert.Equal("12.345,68", Bs.Grouped(12345.678, 2));
            Assert.Equal("1 element", Bs.Elements(1));
            Assert.Equal("3 elementa", Bs.Elements(3));
            Assert.Equal("12 elemenata", Bs.Elements(12));
            Assert.Equal("21 element", Bs.Elements(21));
        }
    }

    public class GeoreferenceTests
    {
        [Fact]
        public void OnePoint_WithoutRotation_IsRefused()
        {
            GeoreferenceResult r = GeoreferenceSolver.Solve(new[] { new ControlPoint { Name = "A", LocalX = 0, LocalY = 0, Easting = 6550000, Northing = 4860000 } }, false, null);
            Assert.False(r.Usable);
            Assert.Contains(r.Warnings, w => w.Contains("Rotacija nije poznata", StringComparison.Ordinal));
        }

        [Fact]
        public void OnePoint_WithGivenRotation_TranslatesAndWarns()
        {
            GeoreferenceResult r = GeoreferenceSolver.Solve(new[] { new ControlPoint { Name = "A", LocalX = 10, LocalY = 0, Easting = 6550000, Northing = 4860000 } }, false, 90);
            Assert.True(r.Usable);
            (double e, double n) = r.Transform.Apply(10, 0);
            Assert.Equal(6550000, e, 6);
            Assert.Equal(4860000, n, 6);
            (double e2, double n2) = r.Transform.Apply(10, 5); // +5 local Y rotated 90° CCW -> -5 E
            Assert.Equal(6549995, e2, 6);
            Assert.Equal(4860000, n2, 6);
            Assert.NotEmpty(r.Warnings);
        }

        [Fact]
        public void ThreePoints_RecoverRotationAndTranslation_WithResiduals()
        {
            var truth = new PlanTransform { Kind = TransformKind.Rigid, Tx = 6550123.4, Ty = 4860456.7, RotationDegrees = 23.5, Scale = 1 };
            var local = new[] { (0.0, 0.0), (80.0, 5.0), (30.0, 60.0) };
            List<ControlPoint> pts = local.Select((p, i) =>
            {
                (double e, double n) = truth.Apply(p.Item1, p.Item2);
                return new ControlPoint { Name = "P" + i, LocalX = p.Item1, LocalY = p.Item2, Easting = e, Northing = n };
            }).ToList();
            pts[2].Easting += 0.03; // a small survey error

            GeoreferenceResult r = GeoreferenceSolver.Solve(pts, false, null);
            Assert.True(r.Usable);
            Assert.Equal(23.5, r.Transform.RotationDegrees, 1);
            Assert.Equal(1.0, r.Transform.Scale);
            Assert.InRange(r.RmsError, 0.005, 0.03);
            (double x, double y) = r.Transform.Inverse(r.Transform.Apply(12, 34).E, r.Transform.Apply(12, 34).N);
            Assert.Equal(12, x, 9);
            Assert.Equal(34, y, 9);
        }

        [Fact]
        public void MillimetreDrawingAgainstMetreCoordinates_IsFlaggedAsScaleProblem()
        {
            var pts = new[]
            {
                new ControlPoint { Name = "A", LocalX = 0, LocalY = 0, Easting = 100, Northing = 100 },
                new ControlPoint { Name = "B", LocalX = 50000, LocalY = 0, Easting = 150, Northing = 100 },
            };
            GeoreferenceResult r = GeoreferenceSolver.Solve(pts, true, null);
            Assert.Equal(0.001, r.Transform.Scale, 9);
            Assert.Contains(r.Warnings, w => w.Contains("jedinice", StringComparison.Ordinal));
        }
    }

    public class ProjectionTests
    {
        // reference values: PROJ 9 via pyproj (MGI 1901 to WGS 84 (1); ETRS89 treated as WGS 84)
        [Theory]
        [InlineData("EPSG:3907", 5500000, 4900000, 14.995335362, 44.244349776)]
        [InlineData("EPSG:3907", 5450000, 4980000, 14.361492164, 44.962546162)]
        [InlineData("EPSG:3908", 6550000, 4860000, 18.617202057, 43.882785194)]
        [InlineData("EPSG:3908", 6420000, 4950000, 16.985615260, 44.689972464)]
        [InlineData("EPSG:3909", 7480000, 4750000, 20.749808856, 42.894125167)]
        [InlineData("EPSG:25834", 250000, 4860000, 17.889636179, 43.850753463)]
        [InlineData("EPSG:25834", 350000, 4900000, 19.121562759, 44.237790747)]
        [InlineData("EPSG:32633", 700000, 4900000, 17.504063776, 44.225784263)]
        public void ProjectedToWgs84_MatchesProj(string code, double e, double n, double lon, double lat)
        {
            ProjectedCrs crs = CrsRegistry.Find(code)!;
            (double lo, double la) = crs.ToWgs84Geographic(e, n);
            Assert.Equal(lon, lo, 7); // ~1 cm
            Assert.Equal(lat, la, 7);
        }

        [Fact]
        public void TransverseMercator_RoundTrips()
        {
            ProjectedCrs crs = CrsRegistry.Find("3908")!;
            (double lat, double lon) = TransverseMercator.Inverse(crs.Ellipsoid, crs.CentralMeridian, crs.ScaleFactor, crs.FalseEasting, crs.FalseNorthing, 6551234.567, 4861234.567);
            (double e, double n) = crs.FromGeographicOnOwnDatum(lon, lat);
            Assert.Equal(6551234.567, e, 4);
            Assert.Equal(4861234.567, n, 4);
            Assert.Equal(18.6223057247, TransverseMercator.Inverse(crs.Ellipsoid, 18, 0.9999, 6500000, 0, 6550000, 4860000).Lon, 9);
        }

        [Fact]
        public void KmlContainsClosedRingAndAccuracyNote()
        {
            ProjectedCrs crs = CrsRegistry.Find("EPSG:3908")!;
            var ring = new List<(double, double)> { (18.6, 43.8), (18.61, 43.8), (18.61, 43.81) };
            string kml = KmlWriter.Write("Test", new[] { ("Parcela", (IReadOnlyList<(double Lon, double Lat)>)ring, true) }, KmlWriter.AccuracyNote(crs));
            Assert.Contains("18.60000000,43.80000000,0 18.61000000,43.80000000,0 18.61000000,43.81000000,0 18.60000000,43.80000000,0", kml, StringComparison.Ordinal);
            Assert.Contains("geodetsku", kml, StringComparison.Ordinal);
        }
    }
}
