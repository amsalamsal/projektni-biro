using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Roads;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.Core.Exchange
{
    /// <summary>The road elements a host without its own road engine (SketchUp) sends to be built.</summary>
    public sealed class RoadEngineRequest
    {
        public const string FormatId = "sdp-roads-request";

        public string ProjectId { get; set; } = string.Empty;

        public string ProjectName { get; set; } = "Saobraćajnice";

        /// <summary>Base elevation of the roads (m above sea level); null when not set.</summary>
        public double? BaseLevel { get; set; }

        /// <summary>Source elements in metres (model frame). Road axes may carry heights in Z (become elevation marks).</summary>
        public List<SiteElement> Elements { get; } = new List<SiteElement>();

        /// <summary>
        /// Origin of the project frame (E, N, H in metres) the elements are given in: that of the AutoCAD project the
        /// SketchUp model was imported from, so an exported package lands in the drawing where it belongs.
        /// </summary>
        public double[] Origin { get; set; } = new double[3];

        /// <summary>Problems found while reading the request (an element that could not be read is left out).</summary>
        public List<string> Problems { get; } = new List<string>();

        /// <summary>Ids of road elements erased in the host since AutoCAD last had them (export only).</summary>
        public List<string> Removed { get; } = new List<string>();
    }

    /// <summary>
    /// Headless road engine for hosts without the .NET road network (the SketchUp road tools): reads the streets,
    /// roads, crossings, elevation marks, kerb ramps, garage ramps and roundabout parts drawn in the host, builds the
    /// network exactly as AutoCAD does, and writes an exchange package (the host imports it like any package) and a
    /// result file with the notes, warnings, ramp texts and longitudinal sections.
    /// </summary>
    public static class RoadEngine
    {
        public const string ResultFile = "result.json";

        /// <summary>Host written into the packages of the engine: AutoCAD recognises a package of the SketchUp road tools by it.</summary>
        public const string Host = "SketchUp – saobraćajnice (sdp-ceste)";

        /// <summary>Axis vertices whose heights differ less than this from 0 carry no height.</summary>
        public const double HeightTolerance = 0.0005;

        public static RoadEngineRequest ParseRequest(string json, ElementCatalog catalog)
        {
            var req = new RoadEngineRequest();
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (!root.TryGetProperty("format", out JsonElement f) || f.GetString() != RoadEngineRequest.FormatId)
            {
                throw new FormatException("Zahtjev nije zahtjev za saobraćajnice (polje format).");
            }

            if (root.TryGetProperty("project", out JsonElement p) && p.ValueKind == JsonValueKind.Object)
            {
                req.ProjectId = p.TryGetProperty("id", out JsonElement id) ? id.GetString() ?? string.Empty : string.Empty;
                req.ProjectName = p.TryGetProperty("name", out JsonElement name) ? name.GetString() ?? req.ProjectName : req.ProjectName;
            }

            if (!SiteElement.IsValidId(req.ProjectId))
            {
                req.ProjectId = RoadElements.Uuid("project|" + req.ProjectName);
            }

            if (root.TryGetProperty("base", out JsonElement b) && b.ValueKind == JsonValueKind.Number)
            {
                req.BaseLevel = b.GetDouble();
            }

            if (root.TryGetProperty("origin", out JsonElement or) && or.ValueKind == JsonValueKind.Array && or.GetArrayLength() >= 2)
            {
                req.Origin = new[] { or[0].GetDouble(), or[1].GetDouble(), or.GetArrayLength() >= 3 ? or[2].GetDouble() : 0 };
            }

            if (root.TryGetProperty("removed", out JsonElement removed) && removed.ValueKind == JsonValueKind.Array)
            {
                req.Removed.AddRange(removed.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Where(SiteElement.IsValidId));
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("elements", out JsonElement elements) && elements.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement e in elements.EnumerateArray())
                {
                    string eid = e.TryGetProperty("id", out JsonElement x) ? x.GetString() ?? string.Empty : string.Empty;
                    string type = e.TryGetProperty("type", out JsonElement t) ? t.GetString() ?? string.Empty : string.Empty;
                    if (!SiteElement.IsValidId(eid) || !seen.Add(eid))
                    {
                        req.Problems.Add("Element bez ispravnog ili s duplim identifikatorom je izostavljen (" + type + ").");
                        continue;
                    }

                    ElementType? et = catalog.FindType(type);
                    if (et == null)
                    {
                        req.Problems.Add("Nepoznat tip elementa '" + type + "' je izostavljen.");
                        continue;
                    }

                    ElementGeometry g;
                    try
                    {
                        g = GeometryJson.FromJson(e.GetProperty("geometry"));
                    }
                    catch (Exception ex) when (ex is FormatException || ex is KeyNotFoundException || ex is InvalidOperationException)
                    {
                        req.Problems.Add("Geometrija elementa " + eid + " nije ispravna: " + ex.Message);
                        continue;
                    }

                    if (g.Kind != et.Geometry)
                    {
                        req.Problems.Add("Element " + eid + ": vrsta geometrije ne odgovara tipu " + type + ".");
                        continue;
                    }

                    ParameterSet parameters = et.CreateDefaultParameters();
                    if (e.TryGetProperty("parameters", out JsonElement ps))
                    {
                        foreach (KeyValuePair<string, ParamValue> kv in ParameterSet.FromJson(ps))
                        {
                            parameters[kv.Key] = kv.Value;
                        }
                    }

                    req.Elements.Add(new SiteElement(eid, type, g)
                    {
                        Name = e.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? string.Empty : string.Empty,
                        Label = e.TryGetProperty("label", out JsonElement l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null,
                        Parameters = parameters,
                    });
                }
            }

            return req;
        }

        /// <summary>
        /// The elements as the road network takes them: all lie at Z = 0 (as in AutoCAD), and the heights of the
        /// vertices of road axes become elevation marks (stable ids, so repeated builds keep them).
        /// </summary>
        public static List<SiteElement> Prepare(IEnumerable<SiteElement> source, ElementCatalog catalog)
        {
            var list = new List<SiteElement>();
            var marks = new List<SiteElement>();
            ElementType? markType = catalog.FindType(RoadElements.LevelMarkType);
            foreach (SiteElement e in source)
            {
                ElementType? t = catalog.FindType(e.TypeId);
                if (markType != null && RoadElements.IsNetworkRoad(t) && e.Geometry is PathGeometry path && path.Ring.Vertices.Any(v => Math.Abs(v.Z) > HeightTolerance))
                {
                    for (int i = 0; i < path.Ring.Count; i++)
                    {
                        Vec3 v = path.Ring.Vertices[i];
                        var m = new SiteElement(RoadElements.Uuid("axis-level|" + e.Id + "|" + i), RoadElements.LevelMarkType, new PointGeometry(new Vec3(v.X, v.Y, 0), 0))
                        {
                            Name = "Kota osovine " + (string.IsNullOrEmpty(e.Label) ? e.Name : e.Label),
                            Parameters = markType.CreateDefaultParameters(),
                        };
                        m.Parameters["level"] = ParamValue.Of(Math.Round(v.Z, 4));
                        marks.Add(m);
                    }
                }

                // the roads lie in the plan (their heights are the marks); any other element keeps its height
                bool road = t != null && (RoadElements.SourceTypes.Contains(t.Id) || RoadElements.IsNetworkRoad(t));
                list.Add(new SiteElement(e.Id, e.TypeId, road ? e.Geometry.Transform(v => new Vec3(v.X, v.Y, 0)) : e.Geometry)
                {
                    Name = e.Name,
                    Label = e.Label,
                    Parameters = e.Parameters.Clone(),
                    Custom = e.Custom.Clone(),
                    Properties = new SortedDictionary<string, string>(e.Properties, StringComparer.Ordinal),
                });
            }

            // a mark drawn by hand at the same place wins over the height of the axis vertex
            List<Vec2> drawn = list.Where(x => x.TypeId == RoadElements.LevelMarkType && x.Geometry is PointGeometry).Select(x => ((PointGeometry)x.Geometry).Position.XY).ToList();
            list.AddRange(marks.Where(m => !drawn.Any(d => d.DistanceTo(((PointGeometry)m.Geometry).Position.XY) < 0.05)));
            return list;
        }

        /// <summary>Builds the roads of the request: package in <paramref name="outputFolder"/> and the result document.</summary>
        public static JsonObject Build(RoadEngineRequest request, string outputFolder, ElementCatalog catalog, string generatorVersion = "0.0.0")
        {
            List<SiteElement> elements = Prepare(request.Elements, catalog);
            var settings = new ProjectSettings
            {
                ProjectId = request.ProjectId,
                Name = request.ProjectName,
                DrawingUnit = "m",
                Origin = new double[] { 0, 0, 0 },
            };
            if (request.BaseLevel.HasValue)
            {
                settings.Elevations.BuildingZeroAbsolute = request.BaseLevel.Value;
                settings.Elevations.DrawingZ = Elevation.DrawingZMode.Relative;
            }

            var notes = new List<string>(request.Problems);
            var warnings = new List<string>();
            var result = new JsonObject { ["format"] = "sdp-roads-result", ["version"] = 1 };

            // the network: notes and the summary the AutoCAD command writes
            List<SiteElement> roads = elements.Where(e => RoadElements.IsNetworkRoad(catalog.FindType(e.TypeId))).ToList();
            RoadNetworkResult? net = null;
            if (roads.Count > 0)
            {
                net = RoadElements.Network(elements, catalog);
                if (net != null)
                {
                    notes.AddRange(net.Notes);
                    result["summary"] = Bs.T("Ulice i ceste ({0}): kolovoz {1}, biciklističke staze {2}, trotoari {3}; {4} oznaka traka, {5} strelica; rampi za invalide: {6}.",
                        roads.Count, Bs.SquareMeters(net.Area(RoadLevel.Carriageway), 1), Bs.SquareMeters(net.Area(RoadLevel.Bike), 1),
                        Bs.SquareMeters(net.Area(RoadLevel.Sidewalk), 1), net.Markings.Count, net.Arrows.Count, net.Ramps.Count);
                    result["areas"] = new JsonObject
                    {
                        ["carriageway"] = Math.Round(net.Area(RoadLevel.Carriageway), 2),
                        ["bike"] = Math.Round(net.Area(RoadLevel.Bike), 2),
                        ["kerb"] = Math.Round(net.Area(RoadLevel.Kerb), 2),
                        ["sidewalk"] = Math.Round(net.Area(RoadLevel.Sidewalk), 2),
                        ["edge"] = Math.Round(net.Area(RoadLevel.Edge), 2),
                    };
                }

                foreach (RoadSlope sl in RoadLevels.Slopes(RoadElements.Profiles(elements, catalog)).Where(x => x.TooSteep))
                {
                    warnings.Add("Uzdužni nagib saobraćajnice " + sl.RoadName + " između stacionaža " + Bs.Num(sl.From, 2) + " i " + Bs.Num(sl.To, 2) + " m je " +
                                 Bs.Num(sl.Slope * 100, 2) + " % (dozvoljeno najviše 10 %; garažne rampe 17 %).");
                }
            }

            // garage ramps: texts, slopes of the parts, warnings, the texts of the plan and the longitudinal sections
            List<GarageRamp> ramps = GarageRamp.All(elements);
            List<RoadSpec> specs = RoadElements.Specs(elements, catalog);
            var rampsJson = new JsonArray();
            var violations = new JsonArray();
            foreach (GarageRamp r in ramps)
            {
                List<string> broken = r.Violations();
                if (broken.Count > 0)
                {
                    violations.Add(new JsonObject
                    {
                        ["id"] = r.Id,
                        ["name"] = r.Name,
                        ["rules"] = new JsonArray(broken.Select(b => (JsonNode)JsonValue.Create(b)!).ToArray()),
                    });
                }

                var parts = new JsonArray();
                foreach ((double from, double to, double slope) in r.Parts())
                {
                    parts.Add(new JsonArray(Math.Round(from, 3), Math.Round(to, 3), Math.Round(slope, 5)));
                }

                if (r.TooSteep)
                {
                    warnings.Add("Uzdužni nagib garažne rampe " + r.Name + " je " + Bs.Num(r.Slope * 100, 2) + " % (dozvoljeno najviše 17 %).");
                }

                if (r.Curved && r.InnerRadius < 5.0)
                {
                    warnings.Add("Unutrašnji poluprečnik garažne rampe " + r.Name + " je " + Bs.Num(r.InnerRadius, 2) + " m (preporučeno najmanje 5 m).");
                }

                if (r.TooLow)
                {
                    warnings.Add("Svijetla visina pod pločom iznad rampe " + r.Name + " je " + Bs.Num(r.Clearance!.Value.Clearance, 2) + " m (potrebno " + Bs.Num(r.MinClearance, 2) + " m).");
                }

                List<(double From, double To, double Slope)> rp = r.Parts();
                if (rp.Count > 0 && r.FlatLength < 3)
                {
                    (double From, double To, double Slope) top = rp.FirstOrDefault(x => r.TopAtStart ? x.From < 1e-6 : x.To > r.Length - 1e-6);
                    if (top.Slope > GarageRamp.FlatSlope + 1e-9 && RoadElements.AtRoad(r, r.TopAtStart ? 0 : r.Length, specs))
                    {
                        notes.Add("PREPORUKA: rampa " + r.Name + " izlazi na ulicu s nagibom većim od 5 %; dodajte blagi dio pred ulicom (oko 5 m).");
                    }
                }

                rampsJson.Add(new JsonObject
                {
                    ["id"] = r.Id,
                    ["name"] = r.Name,
                    ["text"] = "L=" + Bs.Num(r.Length, 2) + " m, h=" + Bs.Num(Math.Abs(r.Rise), 2) + " m, " + Bs.Num(r.Slope * 100, 2) + " %",
                    ["length"] = Math.Round(r.Length, 3),
                    ["width"] = Math.Round(r.Width, 3),
                    ["slope"] = Math.Round(r.Slope, 5),
                    ["parts"] = parts,
                    ["texts"] = new JsonArray(r.Plan().Where(s => s.Role == "text").Select(s => (JsonNode)ShapeJson(s, Vec2.Zero)).ToArray()),
                });
            }

            var sections = new JsonArray();
            foreach (SiteElement e in elements.Where(x => x.TypeId == RoadElements.RampSectionType && x.Geometry is PointGeometry))
            {
                string reference = e.Parameters.Text("reference", string.Empty);
                GarageRamp? r = ramps.FirstOrDefault(x => x.Id == reference) ?? ramps.FirstOrDefault(x => x.Name == reference);
                if (r == null)
                {
                    notes.Add("Uzdužni presjek " + e.Id + " nema svoju rampu (" + reference + ").");
                    continue;
                }

                Vec2 origin = ((PointGeometry)e.Geometry).Position.XY;
                sections.Add(new JsonObject
                {
                    ["id"] = e.Id,
                    ["ramp"] = r.Id,
                    ["name"] = "Uzdužni presjek rampe " + r.Name,
                    ["shapes"] = new JsonArray(r.Section().Select(s => (JsonNode)ShapeJson(s, origin)).ToArray()),
                });
            }

            Directory.CreateDirectory(outputFolder);
            PackageWriteResult written = PackageWriter.Write(outputFolder, settings, elements, catalog, new PackageWriteOptions
            {
                GeneratorVersion = generatorVersion,
                Host = Host,
                SourceDocument = request.ProjectName,
            });
            notes.AddRange(written.Skipped.Select(s => "Preskočeno: " + s));

            result["ok"] = true;
            result["package"] = written.Path;
            result["elements"] = written.ElementCount;
            result["roads"] = roads.Count;
            result["notes"] = new JsonArray(notes.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray());
            result["warnings"] = new JsonArray(warnings.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray());
            result["ramps"] = rampsJson;
            result["violations"] = violations;
            result["sections"] = sections;
            return result;
        }

        /// <summary>
        /// Writes the roads of the request as a package for AutoCAD (.sdpkg): the source elements (streets, roads,
        /// roundabout parts, crossings, elevation marks – heights of axis vertices become marks –, kerb ramp marks,
        /// garage ramps, ramp sections) in the project frame of the request, with the generated elements flagged so
        /// AutoCAD leaves them out and draws its own network.
        /// </summary>
        public static JsonObject Export(RoadEngineRequest request, string packagePath, ElementCatalog catalog, string generatorVersion = "0.0.0")
        {
            List<SiteElement> elements = Prepare(request.Elements, catalog);
            var settings = new ProjectSettings
            {
                ProjectId = request.ProjectId,
                Name = request.ProjectName,
                DrawingUnit = "m",
                Origin = request.Origin,
            };
            if (request.BaseLevel.HasValue)
            {
                settings.Elevations.BuildingZeroAbsolute = request.BaseLevel.Value;
                settings.Elevations.DrawingZ = Elevation.DrawingZMode.Relative;
            }

            PackageWriteResult written = PackageWriter.WritePackageFrame(packagePath, settings, elements, catalog, new PackageWriteOptions
            {
                GeneratorVersion = generatorVersion,
                Host = Host,
                SourceDocument = request.ProjectName,
                RemovedElements = request.Removed.Where(id => !elements.Any(e => e.Id == id)).ToList(),
            });
            var notes = new List<string>(request.Problems);
            notes.AddRange(written.Skipped.Select(x => "Preskočeno: " + x));
            return new JsonObject
            {
                ["format"] = "sdp-roads-export",
                ["version"] = 1,
                ["ok"] = true,
                ["package"] = written.Path,
                ["elements"] = elements.Count,
                ["roads"] = elements.Count(e => RoadElements.IsNetworkRoad(catalog.FindType(e.TypeId))),
                ["others"] = elements.Count(e => !RoadElements.SourceTypes.Contains(e.TypeId)),
                ["notes"] = new JsonArray(notes.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()),
            };
        }

        /// <summary>The parts of a roundabout as source elements (request element format) for the host to keep.</summary>
        public static JsonObject RoundaboutParts(string json, ElementCatalog catalog)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement o = doc.RootElement;
            double N(string key, double fallback) => o.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;
            Vec2 centre = o.TryGetProperty("centre", out JsonElement c) && c.ValueKind == JsonValueKind.Array && c.GetArrayLength() >= 2 ? new Vec2(c[0].GetDouble(), c[1].GetDouble()) : Vec2.Zero;
            double z = o.TryGetProperty("centre", out JsonElement cz) && cz.ValueKind == JsonValueKind.Array && cz.GetArrayLength() >= 3 ? cz[2].GetDouble() : 0;

            // the cross-section of the arms: the parameters of a street
            ElementType street = catalog.RequireType(RoadElements.StreetType);
            ParameterSet ps = street.CreateDefaultParameters();
            if (o.TryGetProperty("arm", out JsonElement arm))
            {
                foreach (KeyValuePair<string, ParamValue> kv in ParameterSet.FromJson(arm))
                {
                    ps[kv.Key] = kv.Value;
                }
            }

            var probe = new SiteElement(RoadElements.Uuid("probe"), RoadElements.StreetType, new PathGeometry(new Ring(new List<Vec3> { Vec3.Zero, new Vec3(10, 0, 0) }), false)) { Parameters = ps };
            RoadSpec spec = RoadElements.Spec(probe, street) ?? new RoadSpec();
            var options = new RoundaboutOptions
            {
                Centre = centre,
                IslandRadius = N("islandRadius", 15),
                Apron = N("apron", 1.2),
                Arms = (int)Math.Round(N("arms", 4)),
                ArmLength = N("armLength", 40),
                FirstArmAngle = N("firstArmAngle", 0),
                Arm = spec,
            };
            List<string> notes = Roundabout.Normalize(options);
            var list = new JsonArray();
            string seed = o.TryGetProperty("seed", out JsonElement s) ? s.GetString() ?? Guid.NewGuid().ToString("D") : Guid.NewGuid().ToString("D");
            int i = 0;
            foreach (RoundaboutPart part in Roundabout.Create(options))
            {
                ElementType t = catalog.RequireType(part.TypeId);
                ParameterSet parameters = t.CreateDefaultParameters();
                foreach (KeyValuePair<string, ParamValue> kv in part.Parameters)
                {
                    parameters[kv.Key] = kv.Value;
                }

                string id = RoadElements.Uuid("roundabout|" + seed + "|" + i++);
                ElementGeometry g = Math.Abs(z) > HeightTolerance && part.Geometry is PathGeometry && RoadElements.IsNetworkRoad(t)
                    ? part.Geometry.Transform(v => new Vec3(v.X, v.Y, z))
                    : part.Geometry;
                list.Add(new JsonObject
                {
                    ["id"] = id,
                    ["type"] = part.TypeId,
                    ["name"] = string.IsNullOrEmpty(part.Name) ? t.Name : part.Name,
                    ["role"] = part.Role,
                    ["parameters"] = parameters.ToJson(),
                    ["geometry"] = GeometryJson.ToJson(id, g),
                });
            }

            return new JsonObject
            {
                ["format"] = "sdp-roundabout",
                ["version"] = 1,
                ["outerRadius"] = Math.Round(Roundabout.OuterRadius(options), 3),
                ["islandRadius"] = options.IslandRadius,
                ["apron"] = options.Apron,
                ["arms"] = options.Arms,
                ["lanes"] = Math.Max(1, options.Arm.LanesLeft),
                ["laneWidth"] = options.Arm.LaneWidth,
                ["notes"] = new JsonArray(notes.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()),
                ["elements"] = list,
            };
        }

        /// <summary>A plan or section shape of a garage ramp (metres, moved by <paramref name="origin"/>).</summary>
        private static JsonObject ShapeJson(RampShape s, Vec2 origin)
        {
            var o = new JsonObject
            {
                ["role"] = s.Role,
                ["points"] = new JsonArray(s.Points.Select(p => (JsonNode)new JsonArray(Math.Round(p.X + origin.X, 4), Math.Round(p.Y + origin.Y, 4))).ToArray()),
                ["closed"] = s.Closed,
            };
            if (s.Bulges.Any(b => Math.Abs(b) > 1e-12))
            {
                o["bulges"] = new JsonArray(s.Bulges.Select(b => (JsonNode)JsonValue.Create(Math.Round(b, 6))!).ToArray());
            }

            if (s.Fill)
            {
                o["fill"] = true;
            }

            if (s.Width > 0)
            {
                o["width"] = Math.Round(s.Width, 4);
            }

            if (s.Text != null)
            {
                o["text"] = s.Text;
                o["position"] = new JsonArray(Math.Round(s.Position.X + origin.X, 4), Math.Round(s.Position.Y + origin.Y, 4));
                o["rotation"] = Math.Round(s.Rotation, 6);
                o["height"] = Math.Round(s.Height, 4);
                o["length"] = Math.Round(s.Length, 4);
            }

            if (s.Warning)
            {
                o["warning"] = true;
            }

            return o;
        }

        /// <summary>Writes the result document next to the package.</summary>
        public static void WriteResult(string outputFolder, JsonObject result)
        {
            Directory.CreateDirectory(outputFolder);
            File.WriteAllText(Path.Combine(outputFolder, ResultFile), result.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
        }

        /// <summary>The invariant number text used in messages.</summary>
        internal static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
