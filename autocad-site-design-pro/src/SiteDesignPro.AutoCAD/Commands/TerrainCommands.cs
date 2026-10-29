using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Elevation;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Input;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Terrain;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>Project datum, object elevations, elevations from labels, terrain surface, profiles and terrain checks.</summary>
    public sealed class TerrainCommands
    {
        private const string TerrainRole = "terrain";
        private const string AnalysisRole = "terrainAnalysis";
        private const string PreviewOwner = "kota-pregled";

        // ------------------------------------------------------------------ datum

        /// <summary>Building zero (±0,00), parterre, road levels, vertical datum and the meaning of drawing Z.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPDATUM", CommandFlags.Modal)]
        public void Datum() => CommandContext.Run("SDPDATUM", ctx =>
        {
            ElevationDatum d = ctx.Settings.Elevations;
            while (true)
            {
                ctx.Write("Visinski sistem projekta: " + d.Describe());
                foreach (string p in d.Problems())
                {
                    ctx.Write("  ! " + p);
                }

                string k = ctx.Keyword("Izmjena [NultaKota/Parter/Saobracajnice/Datum/Zkoordinate/Izlaz]",
                    new[] { "NultaKota", "Parter", "Saobracajnice", "Datum", "Zkoordinate", "Izlaz" }, "Izlaz");
                switch (k)
                {
                    case "NultaKota":
                        d.BuildingZeroAbsolute = InputCommands.NumberText(ctx, "Apsolutna nadmorska visina nulte kote objekta ±0,00 (m n.v.)", d.BuildingZeroAbsolute);
                        if (d.DrawingZ == DrawingZMode.Absolute && ctx.Keyword("Postaviti visinu ishodišta projekta na nultu kotu (u SketchUp-u je tada ±0,00 = 0)? [Da/Ne]", new[] { "Da", "Ne" }, "Da") == "Da")
                        {
                            double[] o = ctx.Settings.Origin;
                            ctx.Settings.Origin = new[] { o[0], o[1], d.BuildingZeroAbsolute.Value * ctx.K };
                        }

                        break;
                    case "Parter":
                        if (ctx.Keyword("Glavni parter [JednakNultoj/Razlicit]", new[] { "JednakNultoj", "Razlicit" }, d.ParterreEqualsBuildingZero ? "JednakNultoj" : "Razlicit") == "JednakNultoj")
                        {
                            d.ParterreEqualsBuildingZero = true;
                        }
                        else
                        {
                            d.ParterreEqualsBuildingZero = false;
                            d.ParterreRelative = InputCommands.NumberText(ctx, "Kota partera u odnosu na ±0,00 (m, npr. -0,15)", d.ParterreRelative);
                        }

                        break;
                    case "Saobracajnice":
                        {
                            string t = ctx.Text("Saobraćajnice ispod partera u cm (Enter = nije podešeno)", d.DefaultRoadOffsetBelowParterre.HasValue ? Bs.Num(d.DefaultRoadOffsetBelowParterre.Value * 100, 1) : string.Empty, false);
                            if (t.Trim().Length == 0)
                            {
                                d.DefaultRoadOffsetBelowParterre = null;
                            }
                            else if (Bs.TryParse(t, out double cm))
                            {
                                d.DefaultRoadOffsetBelowParterre = cm / 100.0;
                            }
                            else
                            {
                                ctx.Write("Neispravan broj.");
                            }

                            break;
                        }

                    case "Datum":
                        d.VerticalDatum = ctx.Text("Naziv visinskog datuma (npr. Trst 1900)", d.VerticalDatum, true);
                        break;
                    case "Zkoordinate":
                        d.DrawingZ = ctx.Keyword("Z koordinate crteža su [Apsolutne/Relativne] (apsolutne = m n.v., relativne = u odnosu na ±0,00)",
                            new[] { "Apsolutne", "Relativne" }, d.DrawingZ == DrawingZMode.Absolute ? "Apsolutne" : "Relativne") == "Apsolutne" ? DrawingZMode.Absolute : DrawingZMode.Relative;
                        break;
                    default:
                        ctx.SaveSettings();
                        return;
                }

                ctx.SaveSettings();
            }
        });

        // ------------------------------------------------------------------ elevation of one object

        /// <summary>Assigns an elevation to exactly one selected object, with preview and confirmation.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPKOTA", CommandFlags.Modal)]
        public void AssignElevation() => CommandContext.Run("SDPKOTA", ctx =>
        {
            ElevationDatum d = ctx.Settings.Elevations;
            ObjectId id = ctx.Entity("Odaberite objekat kojem se dodjeljuje kota (kota važi samo za taj objekat)");
            var e = (Entity)ctx.Tr.GetObject(id, OpenMode.ForRead);
            string k = ctx.Keyword("Način zadavanja kote [Relativna/Apsolutna/Parter/Saobracajnica]", new[] { "Relativna", "Apsolutna", "Parter", "Saobracajnica" }, "Relativna");
            var a = new ElevationAssignment { Source = "manual" };
            switch (k)
            {
                case "Apsolutna":
                    a.Mode = ElevationMode.Absolute;
                    a.Value = InputCommands.NumberText(ctx, "Apsolutna kota (m n.v.)");
                    break;
                case "Parter":
                    a.Mode = ElevationMode.ParterreOffset;
                    a.Value = InputCommands.NumberText(ctx, "Visina u odnosu na parter (m, + iznad, - ispod)", 0);
                    break;
                case "Saobracajnica":
                    a.Mode = ElevationMode.RoadOffset;
                    a.Value = InputCommands.NumberText(ctx, "Spuštanje ispod partera (cm)", d.DefaultRoadOffsetBelowParterre.HasValue ? d.DefaultRoadOffsetBelowParterre.Value * 100 : (double?)null) / 100.0;
                    break;
                default:
                    a.Mode = ElevationMode.Relative;
                    a.Value = InputCommands.NumberText(ctx, "Relativna kota u odnosu na ±0,00 (m, npr. +0,25)");
                    break;
            }

            double rel, z;
            try
            {
                rel = d.ResolveRelative(a);
                z = d.DrawingZFromRelative(rel);
            }
            catch (DatumNotDefinedException ex)
            {
                ctx.Write(ex.Message);
                return;
            }

            double? current = ElevationStore.CurrentZ(e, ctx.Tr);
            e.Highlight();
            try
            {
                ctx.Write(Bs.T("Objekat {0} (sloj {1}): Z {2} → {3} m | {4}{5}", e.Handle, e.Layer,
                    current.HasValue ? Bs.Num(current.Value / ctx.K, 3) : "-", Bs.Num(z, 3), ElevationDatum.FormatRelative(rel),
                    d.IsDefined ? " = " + ElevationDatum.FormatAbsolute(d.ToAbsolute(rel)) : string.Empty));
                if (ctx.Keyword("Primijeniti kotu na ovaj objekat? [Da/Ne]", new[] { "Da", "Ne" }, "Da") != "Da")
                {
                    throw new CommandCancelled();
                }
            }
            finally
            {
                e.Unhighlight();
            }

            ctx.Write(Apply(ctx, e, a, rel, z) ? "Kota dodijeljena. Poništavanje: UNDO." : "Ova vrsta objekta ne može dobiti kotu.");
        });

        /// <summary>Applies an elevation to one object; elements keep the assignment in their record.</summary>
        internal static bool Apply(CommandContext ctx, Entity e, ElevationAssignment a, double rel, double zMeters)
        {
            double z = zMeters * ctx.K;
            ManagedElement? m = ElementStore.IsManaged(e) ? ctx.Svc.Read(e) : null;
            if (m?.Element != null && !m.IsCopy && m.Type != null)
            {
                SiteElement el = m.Element;
                if (el.Geometry is PointGeometry p)
                {
                    el.Geometry = new PointGeometry(new Vec3(p.Position.X, p.Position.Y, z), p.Rotation);
                }
                else
                {
                    var w = (Entity)ctx.Tr.GetObject(e.ObjectId, OpenMode.ForWrite);
                    if (!ElevationStore.SetZ(w, z, ctx.Tr))
                    {
                        return false;
                    }

                    el.Geometry = ctx.Svc.ReadGeometry(w, m.Type.Geometry, el.Id);
                }

                a.WriteTo(el.Properties, rel);
                ctx.Svc.Update(m, el);
                return true;
            }

            var ent = (Entity)ctx.Tr.GetObject(e.ObjectId, OpenMode.ForWrite);
            if (!ElevationStore.SetZ(ent, z, ctx.Tr))
            {
                return false;
            }

            ElevationStore.Write(ent, a, rel, ctx.Tr);
            return true;
        }

        // ------------------------------------------------------------------ elevations from labels

        /// <summary>Proposes object elevations from elevation labels (smallest object containing the label), with preview.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPKOTEIZOZNAKA", CommandFlags.Modal)]
        public void ElevationsFromLabels() => CommandContext.RunMajor("SDPKOTEIZOZNAKA", "Dodjela kota iz oznaka", ctx =>
        {
            LayerMapping? mapping = InputCommands.RequireMapping(ctx);
            if (mapping == null)
            {
                return;
            }

            double mpu = InputCommands.MetersPerUnit(ctx);
            List<SourceObject> sources = InputCommands.MappedSources(ctx, mapping);
            List<SourceLabel> labels = SiteWorkflow.Labels(sources, mapping, mpu);
            List<AssignmentTarget> targets = SiteWorkflow.Targets(sources, mapping, mpu);
            if (labels.Count == 0)
            {
                ctx.Write("Na mapiranim slojevima oznaka kota nema tekstova.");
                return;
            }

            List<AssignmentProposal> proposals = ElevationAssigner.Propose(labels, targets, ctx.Settings.Elevations);
            int i = 0;
            foreach (AssignmentProposal p in proposals)
            {
                ctx.Write(Bs.T("  {0,3}. {1,-18} {2,-18} {3}", ++i, Short(p.Label.Text), ElevationAssigner.StatusText(p.Status), p.Message));
            }

            List<AssignmentProposal> ok = proposals.Where(p => p.Status == ProposalStatus.Proposed).ToList();
            ctx.Write(Bs.T("Prijedloga: {0} od {1}. Ostale oznake treba riješiti ručno (SDPKOTA).", ok.Count, proposals.Count));
            if (ok.Count == 0)
            {
                return;
            }

            // preview: a line from each label to the centre of its object (erased again before committing)
            var preview = new List<ObjectId>();
            var ms = ctx.Svc.ModelSpace(OpenMode.ForWrite);
            string layer = ctx.Svc.Layers.Ensure("elevation_assignments");
            foreach (AssignmentProposal p in ok)
            {
                Vec2 c = Centroid(p.Target!.Boundary);
                Vec3 from = p.Label.Target ?? p.Label.Position;
                var line = new Line(new Point3d(from.X / mpu, from.Y / mpu, 0), new Point3d(c.X / mpu, c.Y / mpu, 0)) { Layer = layer, ColorIndex = 4 };
                preview.Add(ms.AppendEntity(line));
                ctx.Tr.AddNewlyCreatedDBObject(line, true);
            }

            ctx.Tr.TransactionManager.QueueForGraphicsFlush();
            ctx.Ed.UpdateScreen();
            string answer = ctx.Keyword(Bs.T("Primijeniti {0} kota na označene objekte? [Da/Ne]", ok.Count), new[] { "Da", "Ne" }, "Ne");
            foreach (ObjectId pid in preview)
            {
                ctx.Tr.GetObject(pid, OpenMode.ForWrite).Erase();
            }

            if (answer != "Da")
            {
                throw new CommandCancelled();
            }

            int applied = 0;
            foreach (IGrouping<string, AssignmentProposal> g in ok.GroupBy(p => p.Target!.Handle))
            {
                AssignmentProposal p = g.First();
                Entity? target = InputCommands.EntityByHandle(ctx, g.Key);
                if (target == null)
                {
                    continue;
                }

                var a = new ElevationAssignment { Mode = ElevationMode.Relative, Value = p.RelativeElevation!.Value, Source = "label:" + p.Label.Handle, Note = p.Label.Text };
                if (Apply(ctx, target, a, p.RelativeElevation.Value, ctx.Settings.Elevations.DrawingZFromRelative(p.RelativeElevation.Value)))
                {
                    applied++;
                }
            }

            ctx.Write(Bs.T("Kote dodijeljene objektima: {0}. Cijela dodjela se poništava jednim UNDO.", applied));
        });

        // ------------------------------------------------------------------ terrain

        /// <summary>Builds the terrain in drawing metres from the mapped survey data; null with a message when impossible.</summary>
        internal static TerrainModel? BuildTerrain(CommandContext ctx, bool report)
        {
            LayerMapping? mapping = ctx.Settings.LayerMapping;
            if (mapping == null)
            {
                if (report)
                {
                    ctx.Write("Teren se gradi iz mapiranih slojeva (tačke terena, izohipse, prelomne linije). Pokrenite SDPMAPIRANJE.");
                }

                return null;
            }

            double mpu = InputCommands.MetersPerUnit(ctx);
            TerrainDefinition def = ctx.Settings.TerrainSource;
            IReadOnlyList<Vec2>? boundary = null;
            if (def.BoundaryHandle.Length > 0)
            {
                Entity? b = InputCommands.EntityByHandle(ctx, def.BoundaryHandle);
                if (b == null)
                {
                    ctx.Write("Granica terena više ne postoji; teren se gradi bez granice.");
                    def.BoundaryHandle = string.Empty;
                }
                else
                {
                    (Ring ring, _) = ElementService.ReadCurve(b);
                    boundary = GeometryMath.Tessellate(ring, true, 0.05 / mpu).Select(v => new Vec2(v.X * mpu, v.Y * mpu)).ToList();
                }
            }

            (TerrainSources src, List<string> notes) = SiteWorkflow.TerrainSourcesFrom(InputCommands.MappedSources(ctx, mapping), mapping, ctx.Settings.Elevations, mpu, def.UseLabelsAsSpotHeights, boundary);
            TerrainModel model = TerrainModel.Build(src, ctx.Settings.Terrain);
            if (report)
            {
                foreach (string n in notes)
                {
                    ctx.Write("  " + n);
                }

                foreach (TerrainIssue i in model.Issues)
                {
                    ctx.Write("  [" + (i.Severity == "error" ? "GREŠKA" : "UPOZORENJE") + "] " + i.Code + " " + i.Message);
                }

                if (model.IsUsable)
                {
                    ctx.Write(model.Summary());
                }
            }

            return model.IsUsable ? model : null;
        }

        /// <summary>Builds the terrain surface from the mapped survey layers and draws it as a mesh (VU-TEREN).</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPTEREN", CommandFlags.Modal)]
        public void Terrain() => CommandContext.RunMajor("SDPTEREN", "Izrada terena", ctx =>
        {
            TerrainDefinition def = ctx.Settings.TerrainSource;
            string b = ctx.Keyword("Granica terena [Odaberi/Bez/Zadrzi]", new[] { "Odaberi", "Bez", "Zadrzi" }, def.BoundaryHandle.Length > 0 ? "Zadrzi" : "Bez");
            if (b == "Odaberi")
            {
                ObjectId bid = ctx.Entity("Odaberite zatvorenu poliliniju granice terena", typeof(Polyline), typeof(Polyline2d), typeof(Polyline3d), typeof(Circle));
                def.BoundaryHandle = ctx.Tr.GetObject(bid, OpenMode.ForRead).Handle.ToString();
            }
            else if (b == "Bez")
            {
                def.BoundaryHandle = string.Empty;
            }

            def.UseLabelsAsSpotHeights = ctx.Keyword("Koristiti oznake kota kao visinske tačke terena? [Da/Ne]", new[] { "Da", "Ne" }, def.UseLabelsAsSpotHeights ? "Da" : "Ne") == "Da";
            TerrainModel? model = BuildTerrain(ctx, true);
            if (model == null)
            {
                def.Enabled = false;
                ctx.SaveSettings();
                ctx.Write("Teren nije izrađen: podaci nisu dovoljni (pogledajte poruke iznad). Ništa nije izmišljeno ni nacrtano.");
                return;
            }

            ctx.Svc.EraseAux(def.Id, TerrainRole);
            ctx.Svc.EraseAux(def.Id, AnalysisRole);
            int meshes = DrawMesh(ctx, model, def.Id);
            int markers = DrawLowPoints(ctx, model, def.Id);
            def.Enabled = true;
            ctx.SaveSettings();
            ctx.Write(Bs.T("Teren nacrtan na sloju {0} ({1} mreža), najnižih tačaka označeno: {2}. Teren se izvozi u SketchUp naredbom SDPEXPORT.",
                ctx.Svc.Layers.NameFor("terrain"), meshes, markers));
        });

        private static int DrawMesh(CommandContext ctx, TerrainModel model, string ownerId)
        {
            const int MaxVertices = 32000; // PolyFaceMesh face records use 16-bit vertex indices
            var ms = ctx.Svc.ModelSpace(OpenMode.ForWrite);
            string layer = ctx.Svc.Layers.Ensure("terrain");
            int count = 0;
            int t = 0;
            while (t < model.Triangles.Count)
            {
                var map = new Dictionary<int, short>();
                var pfm = new PolyFaceMesh();
                pfm.SetDatabaseDefaults(ctx.Db);
                pfm.Layer = layer;
                ms.AppendEntity(pfm);
                ctx.Tr.AddNewlyCreatedDBObject(pfm, true);
                var faces = new List<(short, short, short)>();
                while (t < model.Triangles.Count && map.Count + 3 <= MaxVertices)
                {
                    TriangleInfo tri = model.Triangles[t++];
                    short Index(int v)
                    {
                        if (!map.TryGetValue(v, out short i))
                        {
                            i = (short)(map.Count + 1);
                            map[v] = i;
                            Vec3 p = model.Vertices[v];
                            var vx = new PolyFaceMeshVertex(new Point3d(p.X * ctx.K, p.Y * ctx.K, p.Z * ctx.K));
                            pfm.AppendVertex(vx);
                            ctx.Tr.AddNewlyCreatedDBObject(vx, true);
                        }

                        return i;
                    }

                    faces.Add((Index(tri.A), Index(tri.B), Index(tri.C)));
                }

                foreach ((short a, short b, short c) in faces)
                {
                    var fr = new FaceRecord(a, b, c, 0);
                    pfm.AppendFaceRecord(fr);
                    ctx.Tr.AddNewlyCreatedDBObject(fr, true);
                }

                ElementStore.WriteAux(pfm, ownerId, TerrainRole, ctx.Tr);
                count++;
            }

            return count;
        }

        private static int DrawLowPoints(CommandContext ctx, TerrainModel model, string ownerId)
        {
            var ms = ctx.Svc.ModelSpace(OpenMode.ForWrite);
            string layer = ctx.Svc.Layers.Ensure("terrain_analysis");
            double h = ctx.Settings.Labels.ModelTextHeightMeters * ctx.K;
            int n = 0;
            foreach (Vec3 p in model.LowPoints(ctx.Settings.Terrain.LowPointTolerance))
            {
                var at = new Point3d(p.X * ctx.K, p.Y * ctx.K, p.Z * ctx.K);
                var c = new Circle(at, Vector3d.ZAxis, h * 1.5) { Layer = layer };
                var txt = new DBText { Position = new Point3d(at.X + (h * 2), at.Y, at.Z), Height = h, TextString = "Najniža tačka " + Bs.Num(p.Z, 2), Layer = layer };
                foreach (Entity e in new Entity[] { c, txt })
                {
                    ms.AppendEntity(e);
                    ctx.Tr.AddNewlyCreatedDBObject(e, true);
                    ElementStore.WriteAux(e, ownerId, AnalysisRole, ctx.Tr);
                }

                n++;
            }

            return n;
        }

        /// <summary>Terrain profile along a picked alignment, drawn at a chosen point with optional vertical exaggeration.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPROFIL", CommandFlags.Modal)]
        public void Profile() => CommandContext.Run("SDPPROFIL", ctx =>
        {
            TerrainModel? model = BuildTerrain(ctx, false);
            if (model == null)
            {
                ctx.Write("Profil zahtijeva teren. Izradite ga naredbom SDPTEREN (provjerite poruke o podacima).");
                return;
            }

            double mpu = InputCommands.MetersPerUnit(ctx);
            ObjectId cid = ctx.Entity("Odaberite liniju ili poliliniju trase profila", typeof(Curve));
            (Ring ring, bool closed) = ElementService.ReadCurve((Entity)ctx.Tr.GetObject(cid, OpenMode.ForRead));
            List<Vec2> line = GeometryMath.Tessellate(ring, closed, 0.05 / mpu).Select(v => new Vec2(v.X * mpu, v.Y * mpu)).ToList();
            List<(double Station, double Z)> prof = model.Profile(line);
            if (prof.Count < 2)
            {
                ctx.Write("Trasa ne prelazi preko terena.");
                return;
            }

            double ex = ctx.Number("Nadvišenje (vertikalno mjerilo / horizontalno)", 1);
            Point3d at = ctx.Point("Tačka umetanja profila (početak bazne linije)")!.Value;
            double baseZ = Math.Floor(prof.Min(p => p.Z) * 2) / 2 - 0.5;
            double k = ctx.K, h = ctx.Settings.Labels.ModelTextHeightMeters * k;
            var ann = new Drawing.Annotations(ctx.Db, ctx.Tr, ctx.Svc);
            ann.Poly(prof.Select(p => new Point2d(at.X + (p.Station * k), at.Y + ((p.Z - baseZ) * k * ex))), false, "anno_levels");
            double len = prof[prof.Count - 1].Station;
            ann.Poly(new[] { new Point2d(at.X, at.Y), new Point2d(at.X + (len * k), at.Y) }, false, "anno_levels");
            ann.Text(new Point3d(at.X, at.Y - (h * 3), 0), Bs.T("Profil terena – bazna kota {0} m, nadvišenje {1}", Bs.Num(baseZ, 2), Bs.Num(ex, 1)), h, "anno_levels");
            double step = len > 200 ? 20 : (len > 60 ? 10 : 5);
            for (double s = 0; s <= len + 1e-9; s += step)
            {
                double z = Interp(prof, s);
                ann.Poly(new[] { new Point2d(at.X + (s * k), at.Y), new Point2d(at.X + (s * k), at.Y + ((z - baseZ) * k * ex)) }, false, "anno_levels");
                ann.Text(new Point3d(at.X + (s * k), at.Y - (h * 1.5), 0), Bs.Num(s, 0), h * 0.8, "anno_levels", 0, TextHorizontalMode.TextCenter);
                ann.Text(new Point3d(at.X + (s * k) + (h * 0.3), at.Y + ((z - baseZ) * k * ex) + (h * 0.3), 0), Bs.Num(z, 2), h * 0.8, "anno_levels", Math.PI / 2);
            }

            ctx.Write(Bs.T("Profil nacrtan: dužina {0}, kote {1} – {2} m.", Bs.Meters(len, 2), Bs.Num(prof.Min(p => p.Z), 2), Bs.Num(prof.Max(p => p.Z), 2)));
        });

        /// <summary>Terrain checks: low points (ponding), flat and steep areas, sparse data. Markers on VU-TEREN-ANALIZA.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPANALIZATERENA", CommandFlags.Modal)]
        public void Analyse() => CommandContext.Run("SDPANALIZATERENA", ctx =>
        {
            TerrainModel? model = BuildTerrain(ctx, true);
            if (model == null)
            {
                return;
            }

            TerrainDefinition def = ctx.Settings.TerrainSource;
            ctx.Svc.EraseAux(def.Id, AnalysisRole);
            int n = DrawLowPoints(ctx, model, def.Id);
            double min = model.Triangles.Count == 0 ? 0 : model.Triangles.Min(t => t.SlopePercent);
            double max = model.Triangles.Count == 0 ? 0 : model.Triangles.Max(t => t.SlopePercent);
            ctx.Write(Bs.T("Nagibi terena: {0} – {1}. Označeno najnižih tačaka: {2}. Pravila projekta: najmanji pad {3}, najveći nagib {4}.",
                Bs.Percent(min, 1), Bs.Percent(max, 1), n, Bs.Percent(ctx.Settings.Terrain.MinDrainageSlopePercent), Bs.Percent(ctx.Settings.Terrain.MaxSlopePercent)));
            ctx.Write("Napomena: ovo je provjera geometrije terena, a ne hidraulički proračun odvodnje.");
        });

        private static double Interp(List<(double Station, double Z)> prof, double s)
        {
            for (int i = 0; i + 1 < prof.Count; i++)
            {
                if (s <= prof[i + 1].Station + 1e-9)
                {
                    double d = prof[i + 1].Station - prof[i].Station;
                    return d < 1e-12 ? prof[i].Z : prof[i].Z + ((prof[i + 1].Z - prof[i].Z) * (s - prof[i].Station) / d);
                }
            }

            return prof[prof.Count - 1].Z;
        }

        private static Vec2 Centroid(Ring ring)
        {
            List<Vec3> pts = GeometryMath.Tessellate(ring, true);
            return new Vec2(pts.Average(p => p.X), pts.Average(p => p.Y));
        }

        private static string Short(string s)
        {
            string c = ElevationLabelParser.Clean(s);
            return c.Length > 18 ? c.Substring(0, 17) + "…" : c;
        }
    }
}
