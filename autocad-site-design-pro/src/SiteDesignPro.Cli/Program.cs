using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Exchange;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Quantities;
using SiteDesignPro.Core.Samples;
using SiteDesignPro.Core.Validation;

namespace SiteDesignPro.Cli
{
    /// <summary>Headless Site Design Pro tool (no AutoCAD required).</summary>
    public static class Program
    {
        private const string Usage = @"Site Design Pro command-line tool

Usage:
  sdp sample   --out <folder|file.sdpkg> [--catalog <dir>]   Generate the sample residential complex package
  sdp validate <package> [--catalog <dir>] [--json]           Read a package and run all validation rules
  sdp schedule <package> [--id <schedule>] [--csv <folder>]   Print quantity schedules (all applicable by default)
  sdp catalog  [--catalog <dir>] [--types]                    Summarise the element catalog
  sdp stress   --out <path> --count <n> [--catalog <dir>]     Generate a large synthetic package for performance tests

Exit codes: 0 success, 1 validation errors, 2 usage or I/O error.";

        public static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
            {
                Console.WriteLine(Usage);
                return args.Length == 0 ? 2 : 0;
            }

            try
            {
                var opts = Options.Parse(args.Skip(1).ToArray());
                ElementCatalog catalog = ElementCatalog.LoadWithOverlays(opts.Get("catalog"), opts.Get("user-catalog"));
                switch (args[0])
                {
                    case "sample": return Sample(opts, catalog);
                    case "validate": return Validate(opts, catalog);
                    case "schedule": return Schedule(opts, catalog);
                    case "catalog": return CatalogInfo(opts, catalog);
                    case "stress": return Stress(opts, catalog);
                    default:
                        Console.Error.WriteLine("Unknown command '" + args[0] + "'.");
                        Console.Error.WriteLine(Usage);
                        return 2;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is FormatException || ex is UnauthorizedAccessException || ex is KeyNotFoundException)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                return 2;
            }
        }

        private static int Sample(Options o, ElementCatalog catalog)
        {
            string output = o.Require("out");
            (ProjectSettings settings, List<SiteElement> drawing) = SampleResidentialComplex.CreateDrawing(catalog);
            List<SiteElement> local = drawing.Select(e => PackageWriter.ToPackageFrame(e, settings)).ToList();
            ValidationReport report = new ProjectValidator(catalog, settings.Checks).Validate(local);
            PackageWriteResult result = PackageWriter.Write(output, settings, drawing, catalog, new PackageWriteOptions
            {
                Host = "sdp command-line tool",
                SourceDocument = "SampleResidentialComplex (generated)",
                Validation = report,
                PackageId = SampleResidentialComplex.DeterministicId("package"),
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            Console.WriteLine("Wrote {0} elements to {1}", result.ElementCount, result.Path);
            Console.WriteLine("Validation: {0} errors, {1} warnings, {2} review items", report.ErrorCount, report.WarningCount, report.ReviewCount);
            foreach (string s in result.Skipped)
            {
                Console.WriteLine("Skipped: " + s);
            }

            return report.HasErrors ? 1 : 0;
        }

        private static int Validate(Options o, ElementCatalog catalog)
        {
            string path = o.Positional(0) ?? throw new ArgumentException("Package path required.");
            var sw = Stopwatch.StartNew();
            PackageReadResult read = PackageReader.Read(path);
            var report = new ValidationReport();
            report.Issues.AddRange(read.Report.Issues);
            if (read.Package != null)
            {
                foreach (PackageElement pe in read.Package.Elements)
                {
                    ElementType? t = catalog.FindType(pe.Element.TypeId);
                    if (t != null)
                    {
                        ProjectValidator.FillDefaults(pe.Element, t);
                    }
                }

                ValidationReport v = new ProjectValidator(catalog).Validate(read.Package.Elements.Select(p => p.Element).ToList());
                report.Issues.AddRange(v.Issues);
                report.ElementCount = read.Package.Elements.Count;
            }

            sw.Stop();
            if (o.Has("json"))
            {
                Console.WriteLine(report.ToJson().ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.Write(report.ToText());
                Console.WriteLine("Checked in {0} ms.", sw.ElapsedMilliseconds);
            }

            return report.HasErrors ? 1 : 0;
        }

        private static int Schedule(Options o, ElementCatalog catalog)
        {
            string path = o.Positional(0) ?? throw new ArgumentException("Package path required.");
            PackageReadResult read = PackageReader.Read(path);
            if (read.Package == null)
            {
                Console.Error.Write(read.Report.ToText());
                return 2;
            }

            List<SiteElement> elements = read.Package.Elements.Select(p => p.Element).ToList();
            var builder = new ScheduleBuilder(catalog);
            IEnumerable<ScheduleDefinition> defs = o.Get("id") is string id
                ? new[] { catalog.FindSchedule(id) ?? throw new ArgumentException("Unknown schedule '" + id + "'.") }
                : builder.Applicable(elements);
            string? csvDir = o.Get("csv");
            foreach (ScheduleDefinition def in defs)
            {
                ScheduleTable table = builder.Build(def, elements);
                Console.WriteLine("== " + table.Title + " ==");
                Console.WriteLine(Render(table));
                if (csvDir != null)
                {
                    Directory.CreateDirectory(csvDir);
                    string file = Path.Combine(csvDir, def.Id + ".csv");
                    File.WriteAllText(file, table.ToCsv(), new System.Text.UTF8Encoding(true));
                    Console.WriteLine("CSV: " + file);
                }

                Console.WriteLine();
            }

            return 0;
        }

        private static string Render(ScheduleTable t)
        {
            var rows = new List<string[]> { t.Columns.Select(c => c.Title).ToArray() };
            rows.AddRange(t.Rows.Select(r => r.Cells.Select(c => c.Length > 40 ? c.Substring(0, 37) + "..." : c).ToArray()));
            int[] widths = Enumerable.Range(0, t.Columns.Count).Select(i => rows.Max(r => r[i].Length)).ToArray();
            var sb = new System.Text.StringBuilder();
            for (int r = 0; r < rows.Count; r++)
            {
                sb.AppendLine(string.Join(" | ", rows[r].Select((c, i) => c.PadRight(widths[i]))).TrimEnd());
                if (r == 0)
                {
                    sb.AppendLine(string.Join("-+-", widths.Select(w => new string('-', w))));
                }
            }

            foreach (KeyValuePair<string, double> tot in t.Totals)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Total: {0:0.##} {1}", tot.Value, tot.Key));
            }

            return sb.ToString();
        }

        private static int CatalogInfo(Options o, ElementCatalog catalog)
        {
            Console.WriteLine("Catalog {0} v{1}: {2} element types, {3} builders, {4} layers, {5} materials, {6} schedules",
                catalog.CatalogId, catalog.Version, catalog.Types.Count(), catalog.Builders.Count, catalog.Layers.Count(), catalog.Materials.Count(), catalog.Schedules.Count);
            foreach (CategoryDefinition c in catalog.Categories)
            {
                List<ElementType> types = catalog.Types.Where(t => t.Category == c.Id).ToList();
                Console.WriteLine("  {0,-28} {1,3} types", c.Name, types.Count);
                if (o.Has("types"))
                {
                    foreach (ElementType t in types)
                    {
                        Console.WriteLine("      {0,-42} {1,-22} {2}", t.Id, t.Builder.Id, t.Name);
                    }
                }
            }

            foreach (string m in catalog.LoadMessages)
            {
                Console.WriteLine("  note: " + m);
            }

            return 0;
        }

        private static int Stress(Options o, ElementCatalog catalog)
        {
            string output = o.Require("out");
            int count = int.Parse(o.Require("count"), CultureInfo.InvariantCulture);
            var settings = new ProjectSettings { Name = "Stress test " + count, DrawingUnit = "m" };
            var elements = new List<SiteElement>(count);
            string[] types = { "landscape.tree_deciduous", "lighting.pedestrian_light", "furniture.bench", "landscape.shrub", "road.bollard" };
            int side = (int)Math.Ceiling(Math.Sqrt(count));
            for (int i = 0; i < count; i++)
            {
                ElementType t = catalog.RequireType(types[i % types.Length]);
                double x = (i % side) * 6.0, y = (i / side) * 6.0;
                elements.Add(new SiteElement(SampleResidentialComplex.DeterministicId("stress-" + i.ToString(CultureInfo.InvariantCulture)), t.Id, new PointGeometry(new Vec3(x, y, 0), (i % 8) * Math.PI / 4))
                {
                    Name = t.Name,
                    Parameters = t.CreateDefaultParameters(),
                });
            }

            for (int i = 0; i < Math.Max(1, count / 50); i++)
            {
                double y = i * 6.0 + 3;
                elements.Add(new SiteElement(SampleResidentialComplex.DeterministicId("stress-path-" + i.ToString(CultureInfo.InvariantCulture)), "wall.mesh_fence",
                    new PathGeometry(new Ring(new[] { new Vec3(-5, y, 0), new Vec3(side * 6.0, y, 0) }), false))
                {
                    Name = "Fence",
                    Parameters = catalog.RequireType("wall.mesh_fence").CreateDefaultParameters(),
                });
            }

            var sw = Stopwatch.StartNew();
            PackageWriteResult result = PackageWriter.Write(output, settings, elements, catalog, new PackageWriteOptions { Host = "sdp stress" });
            sw.Stop();
            Console.WriteLine("Wrote {0} elements to {1} in {2} ms", result.ElementCount, result.Path, sw.ElapsedMilliseconds);
            return 0;
        }

        private sealed class Options
        {
            private readonly Dictionary<string, string?> named = new Dictionary<string, string?>(StringComparer.Ordinal);
            private readonly List<string> positional = new List<string>();

            public static Options Parse(string[] args)
            {
                var o = new Options();
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        string key = args[i].Substring(2);
                        string? value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : null;
                        o.named[key] = value;
                    }
                    else
                    {
                        o.positional.Add(args[i]);
                    }
                }

                return o;
            }

            public bool Has(string key) => named.ContainsKey(key);

            public string? Get(string key) => named.TryGetValue(key, out string? v) ? v : null;

            public string Require(string key) => Get(key) ?? throw new ArgumentException("Option --" + key + " is required.");

            public string? Positional(int i) => i < positional.Count ? positional[i] : null;
        }
    }
}
