using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteDesignPro.Core.Elevation;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Input;
using SiteDesignPro.Core.Safety;
using SiteDesignPro.Core.Units;
using SiteDesignPro.Core.Validation;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    public class LayerMappingTests
    {
        [Fact]
        public void WildcardsAndCaseInsensitiveNames()
        {
            Assert.True(LayerMapping.Matches("KP_*", "kp_granica"));
            Assert.True(LayerMapping.Matches("Kota?", "KOTA1"));
            Assert.False(LayerMapping.Matches("Kota?", "KOTA12"));
            Assert.True(LayerMapping.Matches("Parcela", "PARCELA"));
        }

        [Fact]
        public void Suggestion_RecognisesCommonLayerNames_ButIsOnlyASuggestion()
        {
            LayerMapping m = LayerMapping.Suggest(new[] { "KP_GRANICA", "OBJEKAT_TLOCRT", "Saobracajnice-RP", "LINIJA PARTERA", "KOTE", "IZOHIPSE", "0", "Defpoints" });
            Assert.Equal(new[] { "KP_GRANICA" }, m.Patterns(SourceRole.ParcelBoundary));
            Assert.Equal(new[] { "OBJEKAT_TLOCRT" }, m.Patterns(SourceRole.BuildingFootprint));
            Assert.Equal(new[] { "Saobracajnice-RP" }, m.Patterns(SourceRole.Roads));
            Assert.Equal(new[] { "LINIJA PARTERA" }, m.Patterns(SourceRole.ParterreLine));
            Assert.Equal(new[] { "KOTE" }, m.Patterns(SourceRole.ElevationLabels));
            Assert.Equal(new[] { "IZOHIPSE" }, m.Patterns(SourceRole.Contours));
            Assert.Empty(m.RolesOf("0"));
        }

        [Fact]
        public void Presets_RoundTrip()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sdp-map-" + Guid.NewGuid().ToString("N"));
            try
            {
                var m = new LayerMapping { Name = "Biro: standard/2026" };
                m.Set(SourceRole.ParcelBoundary, new[] { "KP_*" });
                m.Set(SourceRole.ElevationLabels, new[] { "KOTE", "kote" }); // duplicates collapse
                string path = LayerMappingPresets.Save(dir, m);
                Assert.EndsWith("Biro_ standard_2026.sdpmap.json", path, StringComparison.Ordinal);
                File.WriteAllText(Path.Combine(dir, "pokvaren.sdpmap.json"), "{ nije json");
                var problems = new List<string>();
                List<(string Path, LayerMapping Mapping)> loaded = LayerMappingPresets.Load(dir, problems);
                Assert.Single(loaded);
                Assert.Single(problems);
                Assert.Equal(new[] { "KOTE" }, loaded[0].Mapping.Patterns(SourceRole.ElevationLabels));
                Assert.Contains("\"ParcelBoundary\"", File.ReadAllText(path), StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }

    public class InputValidatorTests
    {
        private static SourceObject Poly(string layer, bool closed, params (double X, double Y)[] pts) => new SourceObject
        {
            Handle = layer + pts.Length + (closed ? "c" : "o") + pts[0].X,
            Layer = layer,
            Kind = SourceKind.Polyline,
            Closed = closed,
            Vertices = pts.Select(p => new Vec3(p.X, p.Y, 0)).ToList(),
        };

        private static SourceObject Text(string layer, string text, double x, double y) => new SourceObject
        {
            Handle = "T" + x + "_" + y + text.GetHashCode(),
            Layer = layer,
            Kind = SourceKind.Text,
            Text = text,
            Vertices = new List<Vec3> { new Vec3(x, y, 0) },
        };

        private static LayerMapping StandardMapping()
        {
            var m = new LayerMapping();
            m.Set(SourceRole.ParcelBoundary, new[] { "KP" });
            m.Set(SourceRole.BuildingFootprint, new[] { "OBJ" });
            m.Set(SourceRole.Roads, new[] { "SAOB" });
            m.Set(SourceRole.ParterreLine, new[] { "PARTER" });
            m.Set(SourceRole.ElevationLabels, new[] { "KOTE" });
            return m;
        }

        private static InputSettings Metres(double? datum = 518.56) => new InputSettings
        {
            DrawingUnit = LengthUnit.Meter,
            Crs = "EPSG:3908",
            Datum = new ElevationDatum { BuildingZeroAbsolute = datum, VerticalDatum = "Trst" },
        };

        [Fact]
        public void CompleteDrawing_HasNoErrors()
        {
            var objs = new List<SourceObject>
            {
                Poly("KP", true, (0, 0), (60, 0), (60, 40), (0, 40)),
                Poly("OBJ", true, (10, 10), (30, 10), (30, 25), (10, 25)),
                Poly("SAOB", false, (0, -5), (60, -5)),
                Poly("PARTER", true, (5, 5), (55, 5), (55, 35), (5, 35)),
                Text("KOTE", "±0,00", 20, 15),
                Text("KOTE", "-0,15", 40, 30),
                Text("KOTE", "KT 518,41", 50, 30),
            };
            (ValidationReport r, InputSummary s) = InputValidator.Validate(objs, StandardMapping(), Metres());
            Assert.False(r.HasErrors, string.Join("\n", r.Issues));
            Assert.Equal(1, s.ParcelCount);
            Assert.Equal(1, s.BuildingCount);
            Assert.Equal(3, s.UsableElevationLabels);
            Assert.Equal(2400, s.ParcelAreaM2, 6);
        }

        [Fact]
        public void MissingLayersAndElevations_AreErrors_NothingInvented()
        {
            var m = new LayerMapping();
            m.Set(SourceRole.ParcelBoundary, new[] { "NE_POSTOJI" });
            (ValidationReport r, _) = InputValidator.Validate(new List<SourceObject> { Poly("X", true, (0, 0), (1, 0), (1, 1)) }, m, Metres(null));
            string codes = string.Join(",", r.Issues.Select(i => i.Code));
            Assert.Contains("SDP-I001", codes, StringComparison.Ordinal); // mapped layer missing
            Assert.Contains("SDP-I011", codes, StringComparison.Ordinal); // no parcel geometry
            Assert.Contains("SDP-I020", codes, StringComparison.Ordinal); // no building
            Assert.Contains("SDP-I046", codes, StringComparison.Ordinal); // no elevation data
            Assert.Contains("SDP-I047", codes, StringComparison.Ordinal); // datum not defined
        }

        [Fact]
        public void OpenAlmostClosedSelfIntersectingAndDuplicateBoundaries()
        {
            var objs = new List<SourceObject>
            {
                Poly("KP", false, (0, 0), (60, 0), (60, 40), (0, 40), (0, 0.02)),        // almost closed (2 cm)
                Poly("KP", false, (100, 0), (160, 0), (160, 40), (100, 40), (100, 10)), // open (10 m)
                Poly("OBJ", true, (10, 10), (30, 25), (30, 10), (10, 25)),              // bow-tie
                Poly("OBJ", true, (40, 10), (50, 10), (50, 20), (40, 20)),
                Poly("OBJ", true, (50, 20), (40, 20), (40, 10), (50, 10)),              // same footprint, other start/direction
                Text("KOTE", "+0,10", 1, 1),
            };
            (ValidationReport r, _) = InputValidator.Validate(objs, StandardMapping(), Metres());
            Assert.Contains(r.Issues, i => i.Code == "SDP-I006");
            Assert.Contains(r.Issues, i => i.Code == "SDP-I007" && i.Severity == Severity.Error);
            Assert.Contains(r.Issues, i => i.Code == "SDP-I004");
            Assert.Contains(r.Issues, i => i.Code == "SDP-I008");
        }

        [Fact]
        public void MarkerOnly_RequiresExplicitWorkflow()
        {
            var objs = new List<SourceObject>
            {
                Poly("KP", true, (0, 0), (60, 0), (60, 40), (0, 40)),
                new SourceObject { Handle = "B1", Layer = "OBJ", Kind = SourceKind.Block, BlockName = "ZGRADA", Vertices = { new Vec3(20, 20, 0) } },
                Text("KOTE", "+0,10", 1, 1),
            };
            (ValidationReport strict, InputSummary s) = InputValidator.Validate(objs, StandardMapping(), Metres());
            Assert.Contains(strict.Issues, i => i.Code == "SDP-I021" && i.Severity == Severity.Error);
            Assert.Equal(0, s.BuildingCount);
            Assert.Equal(1, s.MarkerCount);

            LayerMapping m = StandardMapping();
            m.AllowMarkerWorkflow = true;
            (ValidationReport relaxed, _) = InputValidator.Validate(objs, m, Metres());
            Assert.Contains(relaxed.Issues, i => i.Code == "SDP-I021" && i.Severity == Severity.Warning);
        }

        [Fact]
        public void BuildingOutsideParcel_AndUnitSuspicion()
        {
            var objs = new List<SourceObject>
            {
                Poly("KP", true, (0, 0), (60, 0), (60, 40), (0, 40)),
                Poly("OBJ", true, (50, 10), (70, 10), (70, 20), (50, 20)),
                Text("KOTE", "+0,10", 1, 1),
            };
            (ValidationReport r, _) = InputValidator.Validate(objs, StandardMapping(), Metres());
            Assert.Contains(r.Issues, i => i.Code == "SDP-I022");

            // the same numbers read as millimetres give a 0.0024 m² parcel
            InputSettings mm = Metres();
            mm.DrawingUnit = LengthUnit.Millimeter;
            (ValidationReport r2, _) = InputValidator.Validate(objs, StandardMapping(), mm);
            Assert.Contains(r2.Issues, i => i.Code == "SDP-I012");
        }

        [Fact]
        public void ElevationLabelProblems()
        {
            var objs = new List<SourceObject>
            {
                Poly("KP", true, (0, 0), (60, 0), (60, 40), (0, 40)),
                Poly("OBJ", true, (10, 10), (30, 10), (30, 25), (10, 25)),
                Text("KOTE", "nivo", 1, 1),
                Text("KOTE", "0,45", 2, 2),
                Text("KOTE", "+0,10", 5, 5),
                Text("KOTE", "+0,30", 5.05, 5),
                Text("KOTE", "518,81 m n.v. / +0,20", 20, 20),
            };
            (ValidationReport r, _) = InputValidator.Validate(objs, StandardMapping(), Metres());
            Assert.Contains(r.Issues, i => i.Code == "SDP-I040");
            Assert.Contains(r.Issues, i => i.Code == "SDP-I041");
            Assert.Contains(r.Issues, i => i.Code == "SDP-I043");
            Assert.Contains(r.Issues, i => i.Code == "SDP-I042");
        }

        [Fact]
        public void AmbiguousLayerRoles_AreReported()
        {
            LayerMapping m = StandardMapping();
            m.Set(SourceRole.Roads, new[] { "KP" });
            (ValidationReport r, _) = InputValidator.Validate(new List<SourceObject> { Poly("KP", true, (0, 0), (60, 0), (60, 40), (0, 40)) }, m, Metres());
            Assert.Contains(r.Issues, i => i.Code == "SDP-I002");
        }
    }

    public class SafetyTests
    {
        private static string TempDir()
        {
            string d = Path.Combine(Path.GetTempPath(), "sdp-safe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        [Fact]
        public void AtomicWrite_ReplacesContentAndLeavesNoTempFiles()
        {
            string d = TempDir();
            try
            {
                string f = Path.Combine(d, "a.json");
                SafeFile.WriteAllTextAtomic(f, "prvo");
                SafeFile.WriteAllTextAtomic(f, "drugo čćžšđ");
                Assert.Equal("drugo čćžšđ", File.ReadAllText(f));
                Assert.Single(Directory.GetFiles(d));
            }
            finally
            {
                Directory.Delete(d, true);
            }
        }

        [Fact]
        public void Backups_AreTimestampedVerifiedAndPrunedButNewestSurvives()
        {
            string d = TempDir();
            try
            {
                string src = Path.Combine(d, "Projekat.dwg");
                File.WriteAllText(src, "v1");
                DateTime now = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
                var mgr = new BackupManager(new BackupPolicy { KeepPerFile = 3, MaxAgeDays = 10 }, () => now);
                var made = new List<string>();
                for (int i = 0; i < 5; i++)
                {
                    made.Add(mgr.BackupFile(src, "Uvoz DWG-a")!);
                    now = now.AddMinutes(1);
                }

                Assert.All(made, m => Assert.Contains("_uvoz-dwg-a.dwg", m, StringComparison.Ordinal));
                List<BackupInfo> left = mgr.List(src);
                Assert.Equal(3, left.Count);
                Assert.Equal(made[4], left[0].Path);
                Assert.Equal("v1", File.ReadAllText(src)); // source untouched

                // everything is older than MaxAgeDays now: only the newest survives
                now = now.AddDays(30);
                mgr.Prune(src);
                Assert.Single(mgr.List(src));

                string restored = mgr.RestoreAsCopy(mgr.List(src)[0], src);
                Assert.NotEqual(src, restored);
                Assert.Equal("v1", File.ReadAllText(restored));
                Assert.Throws<IOException>(() => mgr.RestoreAsCopy(mgr.List(src)[0], src)); // never overwrites
            }
            finally
            {
                Directory.Delete(d, true);
            }
        }

        [Fact]
        public void DisabledBackups_DoNothing_MissingSourceThrows()
        {
            var mgr = new BackupManager(new BackupPolicy { Enabled = false });
            Assert.Null(mgr.BackupFile("/nema/ove/datoteke.dwg", "x"));
            Assert.Throws<FileNotFoundException>(() => new BackupManager(new BackupPolicy()).BackupFile("/nema/ove/datoteke.dwg", "x"));
        }

        [Fact]
        public void Journal_FindsInterruptedOperations_AndSurvivesATornLine()
        {
            string d = TempDir();
            try
            {
                var j = new RecoveryJournal(Path.Combine(d, "journal.jsonl"));
                string a = j.Begin("Uvoz paketa", "plan.dwg", "kopija.dwg");
                string b = j.Begin("Teren", "plan.dwg", null);
                j.End(a, JournalState.Completed);
                File.AppendAllText(j.Path, "{\"OperationId\":\"tor"); // crash in the middle of a write
                List<JournalEntry> open = j.Unfinished("PLAN.dwg");
                Assert.Single(open);
                Assert.Equal(b, open[0].OperationId);
                j.Acknowledge(b, "korisnik je pregledao");
                Assert.Empty(j.Unfinished());
                Assert.Contains(j.Read(), e => e.OperationId == b && e.State == JournalState.RolledBack && e.Operation == "Teren");
            }
            finally
            {
                Directory.Delete(d, true);
            }
        }

        [Fact]
        public void Journal_Compacts_KeepingUnfinished()
        {
            string d = TempDir();
            try
            {
                var j = new RecoveryJournal(Path.Combine(d, "j.jsonl"));
                string keep = j.Begin("Dugotrajna", "x.dwg", null);
                for (int i = 0; i < 30; i++)
                {
                    j.End(j.Begin("op" + i, "x.dwg", null), JournalState.Completed);
                }

                j.Compact(10);
                Assert.True(j.Read().Count <= 11);
                Assert.Single(j.Unfinished());
                Assert.Equal(keep, j.Unfinished()[0].OperationId);
            }
            finally
            {
                Directory.Delete(d, true);
            }
        }
    }
}
