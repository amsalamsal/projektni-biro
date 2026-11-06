using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.AutoCAD.Drawing;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Exchange;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Layout;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Presentation;
using SiteDesignPro.Core.Quantities;
using SiteDesignPro.Core.Symbols;
using SiteDesignPro.Core.Urban;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>Urban indicators, the colour presentation plan and the vehicle swept-path check.</summary>
    public sealed class AnalysisCommands
    {
        private const string UrbanOwner = "urban";
        private const string PresentationOwner = "presentation";
        private const string SweptOwner = "swept";

        // ------------------------------------------------------------------ urban indicators

        /// <summary>Table of coverage, floor area ratio, green and permeable share and parking against the regulation plan.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPOKAZATELJI", CommandFlags.Modal)]
        public void UrbanIndicatorsCommand() => CommandContext.Run("SDPPOKAZATELJI", ctx =>
        {
            UrbanRequirements req = ctx.Settings.Urban ?? new UrbanRequirements();
            string mode = ctx.Keyword("Urbanistički pokazatelji [Tabela/Zahtjevi]", new[] { "Table|Tabela", "Requirements|Zahtjevi" }, req.IsEmpty ? "Requirements" : "Table");
            if (mode == "Requirements")
            {
                UrbanRequirements? edited = CommandContext.Interactive ? UI.UrbanDialog.Ask(req) : AskRequirements(ctx, req);
                if (edited == null)
                {
                    return;
                }

                ctx.Settings.Urban = edited;
                ctx.SaveSettings();
                req = edited;
            }

            List<SiteElement> elements = PackageFrame(ctx);
            UrbanMeasures m = UrbanIndicators.Measure(elements, ctx.Catalog);
            List<UrbanIndicator> rows = UrbanIndicators.Compute(m, req);
            foreach (UrbanIndicator i in rows)
            {
                ctx.Write("  " + i.Name.PadRight(48) + i.Value.PadLeft(22) + "   " + (i.Requirement.Length > 0 ? i.Requirement.PadRight(28) + i.Verdict : string.Empty));
            }

            int failed = rows.Count(r => r.Met == false);
            if (m.Parcel <= 0)
            {
                ctx.Write("Nema parcelne granice: koeficijenti i procenti se ne mogu izračunati. Nacrtajte parcelu (Parcela i objekti → Parcelna granica).");
            }

            ctx.Svc.EraseAux(UrbanOwner, UrbanOwner);
            Point3d? at = ctx.Point("Gornji lijevi ugao tabele [Enter = bez tabele]", null, true);
            if (at != null)
            {
                double h = new Annotations(ctx.Db, ctx.Tr, ctx.Svc).TextHeight;
                ObjectId id = ScheduleTables.Insert(ctx.Db, ctx.Tr, ctx.Svc, UrbanIndicators.ToTable(rows), at.Value, h);
                var table = (Table)ctx.Tr.GetObject(id, OpenMode.ForWrite);
                for (int r = 0; r < rows.Count; r++)
                {
                    if (rows[r].Met.HasValue)
                    {
                        table.Cells[r + 2, 3].ContentColor = Color.FromColorIndex(ColorMethod.ByAci, rows[r].Met.Value ? (short)3 : (short)1);
                    }
                }

                ElementStore.WriteAux(table, UrbanOwner, UrbanOwner, ctx.Tr);
            }

            ctx.Write(failed == 0
                ? "Svi zadani zahtjevi plana su ispunjeni."
                : "Nije ispunjeno zahtjeva: " + failed + ". Zahtjeve mijenjate opcijom Zahtjevi.");
        });

        private static UrbanRequirements AskRequirements(CommandContext ctx, UrbanRequirements r)
        {
            double? Opt(string what, double? current)
            {
                string t = ctx.Text(what + " (- = bez zahtjeva)", current.HasValue ? current.Value.ToString(CultureInfo.InvariantCulture) : "-", false);
                return t == "-" || t.Length == 0 ? (double?)null : Core.Text.Bs.TryParse(t, out double v) ? v : current;
            }

            return new UrbanRequirements
            {
                MaxCoverage = Opt("Koeficijent izgrađenosti najviše", r.MaxCoverage),
                MaxFloorAreaRatio = Opt("Koeficijent iskorištenosti najviše", r.MaxFloorAreaRatio),
                MinGreenPercent = Opt("Zelene površine najmanje (%)", r.MinGreenPercent),
                MinPermeablePercent = Opt("Vodopropusne površine najmanje (%)", r.MinPermeablePercent),
                Dwellings = (int?)Opt("Broj stanova", r.Dwellings),
                ParkingPerDwelling = Opt("Parking mjesta po stanu", r.ParkingPerDwelling),
                MinParking = (int?)Opt("Parking mjesta najmanje (ukupno)", r.MinParking),
                MinAccessiblePercent = Opt("Mjesta za osobe s invaliditetom najmanje (%)", r.MinAccessiblePercent),
                MinEvSpaces = (int?)Opt("Mjesta s punjačem najmanje", r.MinEvSpaces),
            };
        }

        private static List<SiteElement> PackageFrame(CommandContext ctx)
            => ctx.Svc.ReadAll().Where(m => m.Element != null && !m.IsCopy && m.Type != null)
                .Select(m => PackageWriter.ToPackageFrame(m.Element!, ctx.Settings)).ToList();

        // ------------------------------------------------------------------ presentation plan

        /// <summary>Colour presentation plan: material colours, green planting, blue water, tree canopies and shadows.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPREZENTACIJA", CommandFlags.Modal)]
        public void PresentationCommand() => CommandContext.Run("SDPPREZENTACIJA", ctx =>
        {
            string mode = ctx.Keyword("Prezentacijski plan [Napravi/Ukloni]", new[] { "Make|Napravi", "Remove|Ukloni" }, "Make");
            ctx.Svc.EraseAux(PresentationOwner, PresentationOwner);
            if (mode == "Remove")
            {
                ctx.Write("Prezentacijske boje su uklonjene.");
                return;
            }

            double k = ctx.K;
            ObjectId fillLayer = Layer(ctx, "presentation_fill", "VU-PREZ-POVRSINE", 8);
            ObjectId shadowLayer = Layer(ctx, "presentation_shadow", "VU-PREZ-SJENE", 250);
            ObjectId canopyLayer = Layer(ctx, "presentation_canopy", "VU-PREZ-KROSNJE", 3);
            (double sx, double sy) = PresentationStyle.ShadowDirection(ctx.Settings.TrueNorthDegrees);
            var stack = new SortedDictionary<int, List<ObjectId>>();
            void Push(int level, ObjectId id)
            {
                if (!stack.TryGetValue(level, out List<ObjectId>? list))
                {
                    list = new List<ObjectId>();
                    stack[level] = list;
                }

                list.Add(id);
            }

            BlockTableRecord ms = ctx.Svc.ModelSpace(OpenMode.ForWrite);
            ObjectId Add(Entity e, ObjectId layer, int r, int g, int b, int transparency)
            {
                e.SetDatabaseDefaults(ctx.Db);
                e.LayerId = layer;
                e.Color = Color.FromRgb((byte)r, (byte)g, (byte)b);
                ObjectId id = ms.AppendEntity(e);
                ctx.Tr.AddNewlyCreatedDBObject(e, true);
                if (transparency > 0)
                {
                    e.Transparency = new Transparency((byte)Math.Round(255 * (100 - transparency) / 100.0));
                }

                ElementStore.WriteAux(e, PresentationOwner, PresentationOwner, ctx.Tr);
                return id;
            }

            int painted = 0;
            foreach (ManagedElement m in ctx.Svc.ReadAll().Where(x => x.Element != null && x.Type != null && !x.IsCopy))
            {
                SiteElement e = m.Element!;
                ElementType t = m.Type!;
                PresentationPaint paint = PresentationStyle.For(t, e, ctx.Catalog);
                switch (paint.Kind)
                {
                    case PresentationKind.Fill when e.Geometry is PolygonGeometry pg:
                        {
                            Hatch? h = SolidHatch(pg.Outer, pg.Holes, k);
                            if (h != null)
                            {
                                Push(paint.Layer, Add(h, fillLayer, paint.R, paint.G, paint.B, paint.Transparency));
                                painted++;
                            }

                            break;
                        }

                    case PresentationKind.Building when e.Geometry is PolygonGeometry bg:
                        {
                            double height = Num(e, t, "height", 12);
                            double len = Math.Min(Math.Max(height * 0.35, 1.5), 12) * k;
                            Ring shifted = bg.Outer.Map(v => new Vec3(v.X + (sx * len), v.Y + (sy * len), v.Z));
                            Hatch? shadow = SolidHatch(shifted, Array.Empty<Ring>(), k);
                            if (shadow != null)
                            {
                                (int r, int g, int b) = PresentationStyle.Shadow;
                                Push(3, Add(shadow, shadowLayer, r, g, b, 55));
                            }

                            Hatch? h = SolidHatch(bg.Outer, bg.Holes, k);
                            if (h != null)
                            {
                                Push(4, Add(h, fillLayer, paint.R, paint.G, paint.B, 0));
                                painted++;
                            }

                            break;
                        }

                    case PresentationKind.Band when e.Geometry is PathGeometry path:
                        {
                            double w = Num(e, t, t.Builder.PolylineWidthParam ?? "width", 0);
                            if (w <= 0)
                            {
                                break;
                            }

                            List<Vec3> pts = GeometryMath.Tessellate(path.Ring, path.Closed, 0.02 * k);
                            var pl = new Polyline();
                            for (int i = 0; i < pts.Count; i++)
                            {
                                pl.AddVertexAt(i, new Point2d(pts[i].X, pts[i].Y), 0, 0, 0);
                            }

                            pl.Closed = path.Closed;
                            pl.ConstantWidth = w * k;
                            Push(paint.Layer, Add(pl, fillLayer, paint.R, paint.G, paint.B, paint.Transparency));
                            painted++;
                            break;
                        }

                    case PresentationKind.Canopy when e.Geometry is PointGeometry pt:
                        {
                            double d = t.Builder.Id == "tree" ? Num(e, t, "canopyDiameter", 5) : Num(e, t, "diameter", 1.2);
                            double r = Math.Max(d, 0.3) / 2 * k;
                            double height = t.Builder.Id == "tree" ? Num(e, t, "height", 8) : Num(e, t, "height", 1.2);
                            double off = Math.Min(Math.Max(height * 0.25, r / k * 0.4), 6) * k;
                            (int sr, int sg, int sb) = PresentationStyle.Shadow;
                            Push(3, Add(Disc(pt.Position.X + (sx * off), pt.Position.Y + (sy * off), r), shadowLayer, sr, sg, sb, 60));
                            Push(5, Add(Disc(pt.Position.X, pt.Position.Y, r), canopyLayer, paint.R, paint.G, paint.B, paint.Transparency));
                            Push(5, Add(Disc(pt.Position.X - (r * 0.25), pt.Position.Y - (r * 0.25), r * 0.45), canopyLayer, Math.Min(255, paint.R + 40), Math.Min(255, paint.G + 40), Math.Min(255, paint.B + 30), 35));
                            painted++;
                            break;
                        }
                }
            }

            // stack from the ground up and push the whole presentation below the drawing's linework
            var order = new ObjectIdCollection();
            foreach (List<ObjectId> ids in stack.Values)
            {
                foreach (ObjectId id in ids)
                {
                    order.Add(id);
                }
            }

            if (order.Count > 0)
            {
                var dot = (DrawOrderTable)ctx.Tr.GetObject(ms.DrawOrderTableId, OpenMode.ForWrite);
                dot.MoveToBottom(order);
            }

            ctx.Write("Prezentacijski plan: obojeno elemenata " + painted + " (slojevi VU-PREZ-*). Boje se uklanjaju opcijom Ukloni, a ponovno pokretanje ih osvježava. Za štampu u boji koristite stil štampe bez pretvaranja u crno-bijelo.");
        });

        private static double Num(SiteElement e, ElementType t, string key, double fallback)
        {
            double v = e.Parameters.Number(key, double.NaN);
            if (Core.Compat.IsFinite(v))
            {
                return v;
            }

            double d = t.Parameter(key)?.Default.AsNumber() ?? double.NaN;
            return Core.Compat.IsFinite(d) ? d : fallback;
        }

        private static ObjectId Layer(CommandContext ctx, string key, string name, short color)
            => ctx.Svc.Layers.Ensure(new LayerDefinition { Key = key, Name = name, Color = color, Plot = true, Description = "Vanjsko uređenje PRO – prezentacija i analize" });

        private static Hatch? SolidHatch(Ring outer, IReadOnlyList<Ring> holes, double k)
        {
            List<Vec3> pts = GeometryMath.Tessellate(outer, true, 0.02 * k);
            if (pts.Count < 3)
            {
                return null;
            }

            var h = new Hatch();
            h.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
            h.Associative = false;
            h.AppendLoop(HatchLoopTypes.Polyline | HatchLoopTypes.External, Loop(pts));
            foreach (Ring hole in holes)
            {
                List<Vec3> hp = GeometryMath.Tessellate(hole, true, 0.02 * k);
                if (hp.Count >= 3)
                {
                    h.AppendLoop(HatchLoopTypes.Polyline, Loop(hp));
                }
            }

            h.EvaluateHatch(true);
            return h;
        }

        private static BulgeVertexCollection Loop(List<Vec3> pts)
        {
            var bv = new BulgeVertexCollection();
            foreach (Vec3 p in pts)
            {
                bv.Add(new BulgeVertex(new Point2d(p.X, p.Y), 0));
            }

            if (pts.Count > 1 && (pts[0].XY - pts[pts.Count - 1].XY).Length > 1e-9)
            {
                bv.Add(new BulgeVertex(new Point2d(pts[0].X, pts[0].Y), 0));
            }

            return bv;
        }

        private static Hatch Disc(double cx, double cy, double r)
        {
            var h = new Hatch();
            h.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
            h.Associative = false;
            var bv = new BulgeVertexCollection
            {
                new BulgeVertex(new Point2d(cx - r, cy), 1),
                new BulgeVertex(new Point2d(cx + r, cy), 1),
                new BulgeVertex(new Point2d(cx - r, cy), 0),
            };
            h.AppendLoop(HatchLoopTypes.Polyline | HatchLoopTypes.External, bv);
            h.EvaluateHatch(true);
            return h;
        }

        // ------------------------------------------------------------------ swept path

        /// <summary>
        /// Swept path of a fire engine, refuse truck, van or car along a drawn route: body envelope, wheel tracks,
        /// vehicle outlines and red marks where a bend is too tight or the body hits an obstacle.
        /// </summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPROHODNOST", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void SweptPathCommand() => CommandContext.Run("SDPPROHODNOST", ctx =>
        {
            string mode = ctx.Keyword("Prohodnost vozila [Provjeri/Ukloni]", new[] { "Check|Provjeri", "Remove|Ukloni" }, "Check");
            if (mode == "Remove")
            {
                ctx.Svc.EraseAux(SweptOwner, SweptOwner);
                ctx.Write("Crteži prohodnosti su uklonjeni.");
                return;
            }

            string vk = ctx.Keyword("Vozilo [Vatrogasno/Smece/Dostavno/Putnicko]", new[] { "fire|Vatrogasno", "refuse|Smece", "van|Dostavno", "car|Putnicko" }, "fire");
            DesignVehicle v = DesignVehicle.Standard.First(x => x.Key == vk);
            string how = ctx.Keyword("Putanja vozila (osovina prednjih točkova) [Odaberi/Nacrtaj]", new[] { "Select|Odaberi", "Draw|Nacrtaj" }, "Select");
            Ring ring;
            bool closed;
            if (how == "Draw")
            {
                ObjectId drawn = PlacementCommands.DrawPolyline(ctx, false);
                if (drawn.IsNull)
                {
                    return;
                }

                (ring, closed) = ElementService.ReadCurve((Entity)ctx.Tr.GetObject(drawn, OpenMode.ForRead));
                ElementStore.WriteAux((Entity)ctx.Tr.GetObject(drawn, OpenMode.ForWrite), SweptOwner, SweptOwner, ctx.Tr);
            }
            else
            {
                ObjectId id = ctx.Entity("Odaberite putanju (osovina ceste, polilinija, linija ili luk)", typeof(Curve));
                (ring, closed) = ElementService.ReadCurve((Entity)ctx.Tr.GetObject(id, OpenMode.ForRead));
            }

            double k = ctx.K;
            List<Vec2> path = GeometryMath.Tessellate(ring, closed, 0.02 * k).Select(p => new Vec2(p.X / k, p.Y / k)).ToList();
            if (ctx.Keyword("Smjer vožnje [Naprijed/Obrnuto]", new[] { "Forward|Naprijed", "Reverse|Obrnuto" }, "Forward") == "Reverse")
            {
                path.Reverse();
            }

            List<SweptObstacle> obstacles = Obstacles(ctx, k);
            SweptPathResult res = SweptPath.Run(path, v, obstacles, 0.25, 0.2);
            if (res.Poses.Count < 2)
            {
                ctx.Write("Putanja je prekratka.");
                return;
            }

            ObjectId envLayer = Layer(ctx, "swept_envelope", "VU-PROHODNOST", 30);
            ObjectId badLayer = Layer(ctx, "swept_conflict", "VU-PROHODNOST-PROBLEM", 1);
            BlockTableRecord ms = ctx.Svc.ModelSpace(OpenMode.ForWrite);
            void Add(Entity e, ObjectId layer)
            {
                e.SetDatabaseDefaults(ctx.Db);
                e.LayerId = layer;
                ms.AppendEntity(e);
                ctx.Tr.AddNewlyCreatedDBObject(e, true);
                ElementStore.WriteAux(e, SweptOwner, SweptOwner, ctx.Tr);
            }

            Polyline Line(IEnumerable<Vec2> pts, bool close = false)
            {
                var pl = new Polyline();
                int i = 0;
                foreach (Vec2 p in pts)
                {
                    pl.AddVertexAt(i++, new Point2d(p.X * k, p.Y * k), 0, 0, 0);
                }

                pl.Closed = close;
                return pl;
            }

            // envelope: the outer corner traces; wheel tracks thinner
            for (int i = 0; i < res.Traces.Count; i++)
            {
                Polyline pl = Line(res.Traces[i]);
                if (i >= 4)
                {
                    pl.Color = Color.FromColorIndex(ColorMethod.ByAci, 8);
                }

                Add(pl, envLayer);
            }

            // vehicle outline at the start, the end and about every vehicle length
            int every = Math.Max(1, (int)Math.Round(v.Length / 0.25));
            for (int i = 0; i < res.Poses.Count; i += every)
            {
                Add(Line(res.Poses[i].Body(v), true), envLayer);
            }

            Add(Line(res.Poses[res.Poses.Count - 1].Body(v), true), envLayer);
            double h = new Annotations(ctx.Db, ctx.Tr, ctx.Svc).TextHeight;
            VehiclePose start = res.Poses[0];
            Add(new DBText { TextString = v.Name, Height = h, Position = new Point3d(start.Front.X * k, (start.Front.Y * k) + (v.Width * k), 0) }, envLayer);

            foreach (SweptConflict c in res.Conflicts)
            {
                Add(new Circle(new Point3d(c.At.X * k, c.At.Y * k, 0), Vector3d.ZAxis, 1.5 * k), badLayer);
                Add(new DBText { TextString = "!", Height = h * 2, Position = new Point3d((c.At.X * k) + (1.6 * k), c.At.Y * k, 0) }, badLayer);
                ctx.Write("  [" + c.Station.ToString("0.0", CultureInfo.InvariantCulture) + " m] " + c.Message);
            }

            ctx.Write(v.Name + ": dužina putanje " + res.Length.ToString("0.0", CultureInfo.InvariantCulture) + " m, najveći potreban zaokret " +
                Math.Round(res.MaxSteerUsed * 180 / Math.PI) + "° (dozvoljeno " + Math.Round(v.MaxSteer * 180 / Math.PI) + "°). " +
                (res.Conflicts.Count == 0 ? "Vozilo prolazi bez problema." : "Problema: " + res.Conflicts.Count + " (crveni krugovi, sloj VU-PROHODNOST-PROBLEM).") +
                " Provjera je orijentaciona (model krutog vozila pri maloj brzini) i ne zamjenjuje provjeru prema propisima.");
        });

        /// <summary>Things a vehicle body must not touch: trunks, posts, signs, furniture, equipment, buildings, walls and fences.</summary>
        private static List<SweptObstacle> Obstacles(CommandContext ctx, double k)
        {
            var list = new List<SweptObstacle>();
            foreach (ManagedElement m in ctx.Svc.ReadAll().Where(x => x.Element != null && x.Type != null && !x.IsCopy))
            {
                SiteElement e = m.Element!;
                ElementType t = m.Type!;
                string label = (string.IsNullOrEmpty(e.Label) ? string.Empty : e.Label + " ") + "(" + t.Name + ")";
                switch (e.Geometry)
                {
                    case PointGeometry p:
                        {
                            string b = t.Builder.Id;
                            if (b == "parking_bay" || b == "arrow_marking" || b == "marker" || b == "circle_zone" || b == "chamber" || b == "tank")
                            {
                                continue;
                            }

                            double r;
                            if (b == "tree")
                            {
                                r = (Num(e, t, "trunkDiameter", 0.25) / 2) + 0.3;
                            }
                            else
                            {
                                Box2 bb = SymbolLibrary.Build(b, e.Parameters, key => t.Parameter(key)?.Default.AsNumber() ?? double.NaN, t.Id).Bounds();
                                r = Math.Max(0.12, Math.Max(bb.Width, bb.Height) * 0.45);
                            }

                            list.Add(SweptObstacle.Circle(e.Id, label, new Vec2(p.Position.X / k, p.Position.Y / k), r));
                            break;
                        }

                    case PolygonGeometry pg when t.Category == "buildings" || t.Category == "structures" || t.Builder.Id == "pool":
                        list.Add(SweptObstacle.Area(e.Id, label, GeometryMath.Tessellate(pg.Outer, true, 0.05 * k).Select(v => new Vec2(v.X / k, v.Y / k)).ToList()));
                        break;
                    case PathGeometry path when t.Category == "walls":
                        {
                            List<Vec2> line = GeometryMath.Tessellate(path.Ring, path.Closed, 0.05 * k).Select(v => new Vec2(v.X / k, v.Y / k)).ToList();
                            var poly = new List<Vec2>(line);
                            for (int i = line.Count - 1; i >= 0; i--)
                            {
                                poly.Add(line[i]);
                            }

                            list.Add(SweptObstacle.Area(e.Id, label, poly));
                            break;
                        }
                }
            }

            return list;
        }
    }
}
