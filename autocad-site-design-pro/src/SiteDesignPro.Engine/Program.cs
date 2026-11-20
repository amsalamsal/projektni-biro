using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Exchange;

namespace SiteDesignPro.Engine
{
    /// <summary>
    /// sdp-ceste: the road engine of the SketchUp road tools.
    ///   sdp-ceste build --in request.json --out folder          package (manifest.json, geometry.json) + result.json
    ///   sdp-ceste export --in request.json --out roads.sdpkg --result result.json   the roads as a package for AutoCAD
    ///   sdp-ceste roundabout --in options.json --out parts.json  the parts of a roundabout as source elements
    ///   sdp-ceste version --out version.json
    /// Exit code 0 on success, 1 on an error (the error is written to the output: result.json or the given file).
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            string command = args.Length > 0 ? args[0] : string.Empty;
            Dictionary<string, string> o = Options(args);
            o.TryGetValue("out", out string? output);
            try
            {
                ElementCatalog catalog = ElementCatalog.LoadEmbedded();
                string version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
                switch (command)
                {
                    case "build":
                        {
                            string folder = Require(o, "out");
                            RoadEngineRequest request = RoadEngine.ParseRequest(ReadText(Require(o, "in")), catalog);
                            JsonObject result = RoadEngine.Build(request, folder, catalog, version);
                            RoadEngine.WriteResult(folder, result);
                            return 0;
                        }

                    case "export":
                        {
                            RoadEngineRequest request = RoadEngine.ParseRequest(ReadText(Require(o, "in")), catalog);
                            JsonObject result = RoadEngine.Export(request, Require(o, "out"), catalog, version);
                            Write(Require(o, "result"), result);
                            return 0;
                        }

                    case "roundabout":
                        {
                            JsonObject parts = RoadEngine.RoundaboutParts(ReadText(Require(o, "in")), catalog);
                            parts["ok"] = true;
                            Write(Require(o, "out"), parts);
                            return 0;
                        }

                    case "version":
                        Write(Require(o, "out"), new JsonObject { ["ok"] = true, ["version"] = version, ["catalog"] = catalog.Version, ["runtime"] = Environment.Version.ToString() });
                        return 0;

                    default:
                        throw new ArgumentException("Nepoznata naredba '" + command + "' (build, export, roundabout, version).");
                }
            }
            catch (Exception ex)
            {
                // the host reads the error from the output (no console: WinExe)
                if (!string.IsNullOrEmpty(output))
                {
                    try
                    {
                        var error = new JsonObject { ["ok"] = false, ["error"] = ex.GetType().Name + ": " + ex.Message, ["detail"] = ex.ToString() };
                        if (command == "build")
                        {
                            RoadEngine.WriteResult(output!, error);
                        }
                        else if (command == "export" && o.TryGetValue("result", out string? resultFile))
                        {
                            Write(resultFile, error);
                        }
                        else
                        {
                            Write(output!, error);
                        }
                    }
                    catch (Exception)
                    {
                        // nothing more can be reported
                    }
                }

                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static Dictionary<string, string> Options(string[] args)
        {
            var o = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i + 1 < args.Length; i++)
            {
                if (args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    o[args[i].Substring(2)] = args[i + 1];
                    i++;
                }
            }

            return o;
        }

        private static string Require(Dictionary<string, string> o, string key) =>
            o.TryGetValue(key, out string? v) && !string.IsNullOrEmpty(v) ? v : throw new ArgumentException("Nedostaje --" + key + ".");

        private static string ReadText(string path) => File.ReadAllText(path, Encoding.UTF8);

        private static void Write(string path, JsonObject o)
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, o.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }), new UTF8Encoding(false));
        }
    }
}
