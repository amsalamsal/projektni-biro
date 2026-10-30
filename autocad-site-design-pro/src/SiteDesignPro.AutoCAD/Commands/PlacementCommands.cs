using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Layout;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Text;
using SiteDesignPro.Core.Validation;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>Drag preview of a symbol block while picking the insertion point (native snaps, ortho, polar).</summary>
    internal sealed class PlacementJig : EntityJig
    {
        private Point3d position;

        public PlacementJig(BlockReference br)
            : base(br)
        {
            position = br.Position;
        }

        public Point3d Position => position;

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var o = new JigPromptPointOptions("\nTačka umetanja [Enter za kraj]: ")
            {
                UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted,
            };
            PromptPointResult r = prompts.AcquirePoint(o);
            if (r.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (r.Value.DistanceTo(position) < 1e-9)
            {
                return SamplerStatus.NoChange;
            }

            position = r.Value;
            return SamplerStatus.OK;
        }

        protected override bool Update()
        {
            ((BlockReference)Entity).Position = position;
            return true;
        }
    }

    /// <summary>Element placement, drawing, conversion, editing, selection and layout tools.</summary>
    public sealed class PlacementCommands
    {
        /// <summary>Places point elements with a live symbol preview; repeats until Enter.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPLACE", CommandFlags.Modal)]
        public void Place()
        {
            ElementType? drawType = null;
            CommandContext.Run("SDPPLACE", ctx =>
            {
                ElementType type = ctx.ChooseType("Tip elementa", null);
                if (type.Geometry != GeometryKind.Point)
                {
                    ctx.Write(type.Name + " se crta kao " + (type.Geometry == GeometryKind.Path ? "trasa" : "površina") + " (SDPDRAW).");
                    drawType = type;
                    return;
                }

                List<SiteElement> existing = ctx.ExistingElements();
                int placed = 0;
                double rotation = 0;
                ObjectId block = ctx.Svc.SymbolBlockFor(type, type.CreateDefaultParameters());
                while (true)
                {
                    // the preview reference is not database resident; the element is created after the pick
                    using var preview = new BlockReference(Point3d.Origin, block) { Rotation = rotation };
                    var jig = new PlacementJig(preview);
                    PromptResult r = ctx.Ed.Drag(jig);
                    if (r.Status != PromptStatus.OK)
                    {
                        break;
                    }

                    rotation = ctx.Angle("Rotacija <" + CommandContext.F(rotation * 180 / Math.PI, 1) + "°>", jig.Position, rotation);
                    SiteElement element = ctx.NewElement(type, new PointGeometry(new Vec3(jig.Position.X, jig.Position.Y, jig.Position.Z), rotation), existing);
                    ctx.Svc.Create(element);
                    existing.Add(element);
                    placed++;
                    ctx.Tr.TransactionManager.QueueForGraphicsFlush();
                }

                ctx.Write("Postavljeno: " + placed + " × " + type.Name + ".");
            });
            if (drawType != null)
            {
                DrawInteractive(drawType);
            }
        }

        /// <summary>Draws a path or area element with native PLINE / RECTANG, a picked boundary or selected curves.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPDRAW", CommandFlags.Modal)]
        public void DrawCommand()
        {
            ElementType? type = null;
            CommandContext.Run("SDPDRAW", ctx => type = ctx.ChooseType("Tip elementa", null, GeometryKind.Path, GeometryKind.Polygon));
            if (type != null)
            {
                DrawInteractive(type);
            }
        }

        /// <summary>Polyline by points: Enter ends, [Zatvori] closes, [Poništi] removes the last point.</summary>
        private static ObjectId DrawPolyline(CommandContext ctx, bool closeArea)
        {
            var pts = new List<Point3d>();
            bool closed = false;
            Point3d? first = ctx.Point("Prva tačka");
            if (first == null)
            {
                return ObjectId.Null;
            }

            pts.Add(first.Value);
            while (true)
            {
                var o = new PromptPointOptions(pts.Count >= 2 ? "\nSljedeća tačka [Zatvori/Poništi] <kraj>" : "\nSljedeća tačka [Poništi] <kraj>")
                {
                    AllowNone = true,
                    UseBasePoint = true,
                    BasePoint = pts[pts.Count - 1].TransformBy(ctx.Ed.CurrentUserCoordinateSystem.Inverse()),
                };
                if (pts.Count >= 2)
                {
                    o.Keywords.Add("Close", "Zatvori", "Zatvori");
                }

                o.Keywords.Add("Undo", "Poništi", "Poništi");
                PromptPointResult r = ctx.Ed.GetPoint(o);
                if (r.Status == PromptStatus.Keyword && r.StringResult == "Close")
                {
                    closed = true;
                    break;
                }

                if (r.Status == PromptStatus.Keyword && r.StringResult == "Undo")
                {
                    if (pts.Count > 1)
                    {
                        pts.RemoveAt(pts.Count - 1);
                        ctx.Ed.Regen();
                        for (int i = 1; i < pts.Count; i++)
                        {
                            ctx.Ed.DrawVector(pts[i - 1].TransformBy(ctx.Ed.CurrentUserCoordinateSystem.Inverse()), pts[i].TransformBy(ctx.Ed.CurrentUserCoordinateSystem.Inverse()), 1, false);
                        }
                    }

                    continue;
                }

                if (r.Status == PromptStatus.None)
                {
                    break;
                }

                if (r.Status != PromptStatus.OK)
                {
                    ctx.Ed.Regen();
                    throw new CommandCancelled();
                }

                Point3d p = r.Value.TransformBy(ctx.Ed.CurrentUserCoordinateSystem);
                ctx.Ed.DrawVector(o.BasePoint, r.Value, 1, false);
                pts.Add(p);
            }

            ctx.Ed.Regen();
            int needed = closeArea ? 3 : 2;
            if (pts.Count < needed)
            {
                ctx.Write(closeArea ? "Za površinu su potrebne najmanje 3 tačke." : "Za trasu su potrebne najmanje 2 tačke.");
                return ObjectId.Null;
            }

            return AddPolyline(ctx, pts, closed || closeArea);
        }

        private static ObjectId DrawRectangle(CommandContext ctx)
        {
            Point3d? a = ctx.Point("Prvi ugao pravougaonika");
            if (a == null)
            {
                return ObjectId.Null;
            }

            PromptPointResult r = ctx.Ed.GetCorner(new PromptCornerOptions("\nSuprotni ugao", a.Value.TransformBy(ctx.Ed.CurrentUserCoordinateSystem.Inverse())));
            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            // corners in UCS so the rectangle follows the current UCS like RECTANG
            Matrix3d ucs = ctx.Ed.CurrentUserCoordinateSystem;
            Point3d p1 = a.Value.TransformBy(ucs.Inverse());
            Point3d p3 = r.Value;
            var pts = new List<Point3d>
            {
                p1.TransformBy(ucs),
                new Point3d(p3.X, p1.Y, p1.Z).TransformBy(ucs),
                new Point3d(p3.X, p3.Y, p1.Z).TransformBy(ucs),
                new Point3d(p1.X, p3.Y, p1.Z).TransformBy(ucs),
            };
            if (pts[0].DistanceTo(pts[2]) < 1e-9)
            {
                return ObjectId.Null;
            }

            return AddPolyline(ctx, pts, true);
        }

        private static ObjectId AddPolyline(CommandContext ctx, List<Point3d> pts, bool closed)
        {
            var pl = new Polyline();
            pl.SetDatabaseDefaults();
            Vector3d normal = Vector3d.ZAxis;
            Matrix3d toPlane = Matrix3d.WorldToPlane(new Plane(Point3d.Origin, normal));
            for (int i = 0; i < pts.Count; i++)
            {
                Point3d q = pts[i].TransformBy(toPlane);
                pl.AddVertexAt(i, new Point2d(q.X, q.Y), 0, 0, 0);
            }

            pl.Elevation = pts[0].Z;
            pl.Closed = closed;
            BlockTableRecord ms = ctx.Svc.ModelSpace(OpenMode.ForWrite);
            ObjectId id = ms.AppendEntity(pl);
            ctx.Tr.AddNewlyCreatedDBObject(pl, true);
            return id;
        }

        /// <summary>
        /// Interactive drawing: the user draws a polyline or rectangle with our own prompts (snaps and polar tracking
        /// work), picks inside a boundary or selects curves; the result is converted in a separate transaction.
        /// </summary>
        private static void DrawInteractive(ElementType type)
        {
            string? method = null;
            var picked = new List<ObjectId>();
            CommandContext.Run("SDPDRAW", ctx =>
            {
                method = type.Geometry == GeometryKind.Polygon
                    ? ctx.Keyword("Granica [Polilinija/Pravougaonik/Unutar/Odaberi]", new[] { "Polyline|Polilinija", "Rectangle|Pravougaonik", "Pick|Unutar", "Select|Odaberi" }, "Polyline")
                    : ctx.Keyword("Trasa [Polilinija/Odaberi]", new[] { "Polyline|Polilinija", "Select|Odaberi" }, "Polyline");
                if (method == "Select")
                {
                    picked.AddRange(SelectCurves(ctx));
                }
                else if (method == "Pick")
                {
                    while (ctx.Point("Kliknite unutar površine [Enter za kraj]", null, true) is Point3d inside)
                    {
                        DBObjectCollection found = ctx.Ed.TraceBoundary(inside, false);
                        Curve? outer = found.Cast<DBObject>().OfType<Curve>().OrderByDescending(c => c.GeometricExtents.MaxPoint.DistanceTo(c.GeometricExtents.MinPoint)).FirstOrDefault();
                        if (outer == null)
                        {
                            ctx.Write("Oko te tačke nije pronađena zatvorena granica.");
                            continue;
                        }

                        BlockTableRecord ms = ctx.Svc.ModelSpace(OpenMode.ForWrite);
                        picked.Add(ms.AppendEntity(outer));
                        ctx.Tr.AddNewlyCreatedDBObject(outer, true);
                    }
                }
            });
            if (method == null)
            {
                return;
            }

            if (method == "Polyline" || method == "Rectangle")
            {
                // Drawn with our own prompts (no nested PLINE/RECTANG): Editor.Command inside a command fails with
                // eInvalidInput on some AutoCAD versions and setups.
                ObjectId drawn = ObjectId.Null;
                CommandContext.Run("SDPDRAW", ctx => drawn = method == "Rectangle" ? DrawRectangle(ctx) : DrawPolyline(ctx, type.Geometry == GeometryKind.Polygon));
                if (drawn.IsNull)
                {
                    AcApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nNišta nije nacrtano.");
                    return;
                }

                picked.Add(drawn);
            }

            if (picked.Count > 0)
            {
                CommandContext.Run("SDPDRAW", ctx => ctx.Write("Napravljeno: " + ConvertCurves(ctx, type, picked) + " × " + type.Name + "."));
            }
        }

        /// <summary>Converts existing lines, arcs, circles and polylines into managed elements.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPCONVERT", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void ConvertCommand() => CommandContext.Run("SDPCONVERT", ctx =>
        {
            List<ObjectId> curves = SelectCurves(ctx);
            ElementType type = ctx.ChooseType("Tip elementa za odabrane objekte", null, GeometryKind.Path, GeometryKind.Polygon);
            ctx.Write("Pretvoreno objekata u " + type.Name + ": " + ConvertCurves(ctx, type, curves) + ".");
        });

        private static List<ObjectId> SelectCurves(CommandContext ctx)
        {
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE,LINE,ARC,CIRCLE") });
            PromptSelectionResult implied = ctx.Ed.SelectImplied();
            if (implied.Status == PromptStatus.OK)
            {
                return implied.Value.GetObjectIds().ToList();
            }

            PromptSelectionResult r = ctx.Ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nOdaberite polilinije, linije, lukove ili krugove: " }, filter);
            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            return r.Value.GetObjectIds().ToList();
        }

        private static int ConvertCurves(CommandContext ctx, ElementType type, IEnumerable<ObjectId> ids)
        {
            List<SiteElement> existing = ctx.ExistingElements();
            int count = 0;
            foreach (ObjectId id in ids)
            {
                var entity = (Entity)ctx.Tr.GetObject(id, OpenMode.ForWrite);
                if (ElementStore.IsManaged(entity))
                {
                    ctx.Write("Objekat " + entity.Handle + " je već element; koristite SDPEDIT.");
                    continue;
                }

                ElementGeometry g;
                try
                {
                    g = ctx.Svc.ReadGeometry(entity, type.Geometry, string.Empty);
                }
                catch (InvalidOperationException ex)
                {
                    ctx.Write("Preskočeno " + entity.Handle + ": " + ex.Message + (type.Geometry == GeometryKind.Polygon ? " Zatvorite poliliniju (PEDIT > Close) ili koristite opciju Unutar." : string.Empty));
                    continue;
                }

                SiteElement element = ctx.NewElement(type, g, existing);
                ValidationReport check = new ProjectValidator(ctx.Catalog, ctx.Settings.Checks).Validate(new[] { Core.Exchange.PackageWriter.ToPackageFrame(element, ctx.Settings) });
                foreach (ValidationIssue issue in check.Issues.Where(i => i.Severity == Severity.Error && i.Kind == IssueKind.GeometricError))
                {
                    ctx.Write("Upozorenje za " + element.Label + ": " + issue.Message + " " + issue.Resolution);
                }

                entity.Layer = ctx.Svc.Layers.Ensure(type.LayerKey);
                ElementStore.Write(entity, ctx.Svc.ToRecord(element), ctx.Tr);
                ctx.Svc.ApplyRepresentation(entity, element, type);
                existing.Add(element);
                count++;
            }

            return count;
        }

        /// <summary>Edits parameters, properties or the label of one or more elements of the same type.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPEDIT", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void Edit() => CommandContext.Run("SDPEDIT", ctx =>
        {
            List<ManagedElement> sel = ctx.SelectElements("Odaberite elemente za izmjenu", false, m => m.Element != null && m.Type != null && !m.IsCopy);
            if (sel.Count == 0)
            {
                ctx.Write("Nije odabran nijedan element koji se može mijenjati.");
                return;
            }

            if (sel.Select(m => m.Type!.Id).Distinct().Count() > 1)
            {
                ctx.Write("Odabrani elementi su različitih tipova; mijenjaju se samo zajednički parametri.");
            }

            ElementType type = sel[0].Type!;
            List<ParameterDefinition> shared = type.Parameters.Where(p => sel.All(m => m.Type!.Parameter(p.Key) != null)).ToList();
            while (true)
            {
                SiteElement first = sel[0].Element!;
                ctx.Write(type.Name + (sel.Count > 1 ? " (" + Bs.Elements(sel.Count) + ")" : " " + first.Label) + ":");
                int n = 0;
                foreach (ParameterDefinition p in shared)
                {
                    IEnumerable<string> values = sel.Select(m => ElementCatalog.FormatValue(p, m.Element!.Parameters.Contains(p.Key) ? m.Element.Parameters[p.Key] : p.Default, 3, ctx.Catalog)).Distinct();
                    ctx.Write("  P" + (++n).ToString(CultureInfo.InvariantCulture).PadRight(4) + p.Label.PadRight(44) + string.Join(" | ", values.Take(3)) + (values.Count() > 3 ? " | ..." : string.Empty));
                }

                string[] keys = shared.Select((p, i) => p.Key + "|P" + (i + 1).ToString(CultureInfo.InvariantCulture)).Concat(new[] { "Label|Oznaka", "Property|Svojstvo", "eXit|Izlaz" }).ToArray();
                string key = ctx.Keyword("Parametar za izmjenu [P1...P" + shared.Count + "/Oznaka/Svojstvo/Izlaz]", keys, "eXit");
                if (key == "eXit")
                {
                    break;
                }

                if (key == "Label")
                {
                    if (sel.Count > 1)
                    {
                        ctx.Write("Oznake su jedinstvene; odaberite jedan element ili koristite SDPNUMBER.");
                        continue;
                    }

                    first.Label = ctx.Text("Oznaka", first.Label ?? string.Empty, false);
                }
                else if (key == "Property")
                {
                    string pk = ctx.Text("Naziv svojstva (npr. botanicalName, manufacturer, notes)", null, false);
                    string pv = ctx.Text("Vrijednost (- za brisanje)", "-");
                    foreach (ManagedElement m in sel)
                    {
                        if (pv == "-")
                        {
                            m.Element!.Properties.Remove(pk);
                        }
                        else
                        {
                            m.Element!.Properties[pk] = pv;
                        }
                    }
                }
                else
                {
                    ParameterDefinition def = type.Parameter(key)!;
                    ParamValue current = first.Parameters.Contains(key) ? first.Parameters[key] : def.Default;
                    ParamValue value = AskValue(ctx, def, current);
                    foreach (ManagedElement m in sel)
                    {
                        m.Element!.Parameters[key] = value;
                    }
                }

                foreach (ManagedElement m in sel)
                {
                    ValidationReport check = new ProjectValidator(ctx.Catalog, ctx.Settings.Checks).Validate(new[] { Core.Exchange.PackageWriter.ToPackageFrame(m.Element!, ctx.Settings) });
                    ValidationIssue? err = check.Issues.FirstOrDefault(i => i.Severity == Severity.Error && i.Code.StartsWith("SDP-P", StringComparison.Ordinal));
                    if (err != null)
                    {
                        ctx.Write("Nije primijenjeno na " + m.Element!.Label + ": " + err.Message);
                        ManagedElement? fresh = ctx.Svc.Read((Entity)ctx.Tr.GetObject(m.ObjectId, OpenMode.ForRead));
                        m.Element = fresh?.Element;
                        continue;
                    }

                    ctx.Svc.Update(m, m.Element!);
                }

                sel = sel.Where(m => m.Element != null).ToList();
                if (sel.Count == 0)
                {
                    break;
                }
            }
        });

        internal static ParamValue AskValue(CommandContext ctx, ParameterDefinition def, ParamValue current)
        {
            switch (def.Type)
            {
                case ParameterType.Bool:
                    return ParamValue.Of(ctx.Keyword(def.Label + " [Da/Ne]", new[] { "Yes|Da", "No|Ne" }, current.AsBool() ? "Yes" : "No") == "Yes");
                case ParameterType.Enum:
                    {
                        for (int i = 0; i < def.Options.Count; i++)
                        {
                            ctx.Write("  " + (i + 1).ToString(CultureInfo.InvariantCulture) + " = " + ctx.Catalog.OptionLabel(def.Options[i]));
                        }

                        int cur = Math.Max(0, def.Options.ToList().IndexOf(current.AsText()));
                        int choice = ctx.Integer(def.Label + " (redni broj opcije)", cur + 1, 1);
                        return ParamValue.Of(def.Options[Math.Min(choice, def.Options.Count) - 1]);
                    }
                case ParameterType.Integer:
                    return ParamValue.Of((double)ctx.Integer(def.Label, (int)current.AsNumber(def.Min ?? 1), (int)(def.Min ?? 0)));
                case ParameterType.Length:
                case ParameterType.Number:
                case ParameterType.Angle:
                    return ParamValue.Of(ctx.Number(def.Label + " (" + def.DisplayUnit + ")", current.IsNull ? (def.Min ?? 0) : current.AsNumber(), (def.Min ?? 0) < 0, (def.Min ?? 0) <= 0));
                case ParameterType.Material:
                    {
                        string v = ctx.Text(def.Label + " (ključ materijala, ? za popis)", current.AsText(), false);
                        if (v == "?")
                        {
                            foreach (MaterialDefinition m in ctx.Catalog.Materials)
                            {
                                ctx.Write("  " + m.Key.PadRight(20) + m.Name);
                            }

                            v = ctx.Text(def.Label + " (ključ materijala)", current.AsText(), false);
                        }

                        return ParamValue.Of(v);
                    }

                default:
                    {
                        string v = ctx.Text(def.Label + " (- za brisanje)", current.IsNull ? "-" : current.AsText());
                        return v == "-" ? ParamValue.Null : ParamValue.Of(v);
                    }
            }
        }

        /// <summary>Scriptable parameter change used by the palette: handle(s), key, value.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPSETPARAM", CommandFlags.Modal)]
        public void SetParam() => CommandContext.Run("SDPSETPARAM", ctx =>
        {
            string handles = ctx.Text("Handle elemenata (odvojeni zarezom)", null, false);
            string key = ctx.Text("Parametar", null, false);
            string raw = ctx.Text("Vrijednost", null, true);
            foreach (string h in handles.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!ctx.Db.TryGetObjectId(new Handle(Convert.ToInt64(h, 16)), out ObjectId id) || id.IsErased)
                {
                    ctx.Write("Element " + h + " nije pronađen.");
                    continue;
                }

                ManagedElement? m = ctx.Svc.Read((Entity)ctx.Tr.GetObject(id, OpenMode.ForRead));
                ParameterDefinition? def = m?.Type?.Parameter(key);
                if (m?.Element == null || def == null)
                {
                    ctx.Write("Parametar '" + key + "' nije definisan za element " + h + ".");
                    continue;
                }

                ParamValue value = Parse(def, raw);
                SiteElement probe = m.Element;
                ParamValue old = probe.Parameters[key];
                probe.Parameters[key] = value;
                ValidationIssue? err = new ProjectValidator(ctx.Catalog).Validate(new[] { Core.Exchange.PackageWriter.ToPackageFrame(probe, ctx.Settings) })
                    .Issues.FirstOrDefault(i => i.Severity == Severity.Error && i.Code.StartsWith("SDP-P", StringComparison.Ordinal));
                if (err != null)
                {
                    probe.Parameters[key] = old;
                    ctx.Write("Nije primijenjeno: " + err.Message);
                    continue;
                }

                ctx.Svc.Update(m, probe);
            }
        });

        internal static ParamValue Parse(ParameterDefinition def, string raw)
        {
            raw = raw.Trim();
            if (raw.Length == 0 || raw == "-")
            {
                return ParamValue.Null;
            }

            switch (def.Type)
            {
                case ParameterType.Bool:
                    return ParamValue.Of(raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw.Equals("yes", StringComparison.OrdinalIgnoreCase) || raw.Equals("da", StringComparison.OrdinalIgnoreCase) || raw == "1");
                case ParameterType.Length:
                case ParameterType.Number:
                case ParameterType.Integer:
                case ParameterType.Angle:
                    return ParamValue.Of(double.Parse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture));
                default:
                    return ParamValue.Of(raw);
            }
        }

        [CommandMethod("SITEDESIGNPRO", "SDPSELECT", CommandFlags.Modal | CommandFlags.Redraw)]
        public void Select() => CommandContext.Run("SDPSELECT", ctx =>
        {
            string mode = ctx.Keyword("Odabir po [Tipu/Kategoriji/Oznaci/Id/Materijalu/Sve]", new[] { "Type|Tipu", "Category|Kategoriji", "Label|Oznaci", "Id|Id", "Material|Materijalu", "All|Sve" }, "Type");
            Func<ManagedElement, bool> predicate;
            switch (mode)
            {
                case "Type":
                    {
                        ElementType t = ctx.ChooseType("Tip elementa", null);
                        predicate = m => m.Record.TypeId == t.Id;
                        break;
                    }

                case "Category":
                    {
                        string[] cats = ctx.Catalog.Categories.Select(c => c.Id).ToArray();
                        string c = ctx.Keyword("Kategorija [" + string.Join("/", cats) + "]", cats);
                        predicate = m => m.Type?.Category == c;
                        break;
                    }

                case "Label":
                    {
                        string q = ctx.Text("Oznaka ili prefiks (npr. T- ili T-012)", null, false);
                        predicate = m => (m.Record.Label ?? string.Empty).StartsWith(q, StringComparison.OrdinalIgnoreCase);
                        break;
                    }

                case "Id":
                    {
                        string q = ctx.Text("Id elementa (ili njegov početak)", null, false).ToLowerInvariant();
                        predicate = m => m.Record.Id.StartsWith(q, StringComparison.Ordinal);
                        break;
                    }

                case "Material":
                    {
                        string q = ctx.Text("Ključ materijala", null, false);
                        predicate = m => m.Record.Parameters.Any(kv => kv.Value.Kind == ParamKind.Text && kv.Value.AsText() == q && m.Type?.Parameter(kv.Key)?.Type == ParameterType.Material);
                        break;
                    }

                default:
                    predicate = _ => true;
                    break;
            }

            ObjectId[] ids = ctx.Svc.ReadAll().Where(predicate).Select(m => m.ObjectId).ToArray();
            ctx.Ed.SetImpliedSelection(ids);
            ctx.Write("Odabrano: " + Bs.Elements(ids.Length) + ".");
        });

        /// <summary>Zooms to an element by handle (used by the validation palette).</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPZOOM", CommandFlags.Modal | CommandFlags.Redraw)]
        public void Zoom() => CommandContext.Run("SDPZOOM", ctx =>
        {
            string h = ctx.Text("Handle elementa", null, false);
            if (!ctx.Db.TryGetObjectId(new Handle(Convert.ToInt64(h, 16)), out ObjectId id) || id.IsErased)
            {
                ctx.Write("Element nije pronađen.");
                return;
            }

            var e = (Entity)ctx.Tr.GetObject(id, OpenMode.ForRead);
            Extents3d ext = e.GeometricExtents;
            double pad = Math.Max(ext.MaxPoint.DistanceTo(ext.MinPoint), 10 * ctx.K) * 0.6;
            using ViewTableRecord view = ctx.Ed.GetCurrentView();
            Matrix3d wcsToDcs = Matrix3d.WorldToPlane(view.ViewDirection) * Matrix3d.Displacement(view.Target - Point3d.Origin).Inverse();
            Point3d c = new Point3d((ext.MinPoint.X + ext.MaxPoint.X) / 2, (ext.MinPoint.Y + ext.MaxPoint.Y) / 2, (ext.MinPoint.Z + ext.MaxPoint.Z) / 2).TransformBy(wcsToDcs);
            view.CenterPoint = new Point2d(c.X, c.Y);
            view.Height = pad * 2;
            view.Width = pad * 2;
            ctx.Ed.SetCurrentView(view);
            ctx.Ed.SetImpliedSelection(new[] { id });
        });

        /// <summary>Repeats a point element along a curve (trees, lights, bollards, benches).</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPATHARRAY", CommandFlags.Modal)]
        public void PathArray() => CommandContext.RunMajor("SDPPATHARRAY", "Masovno postavljanje (SDPPATHARRAY)", ctx =>
        {
            ObjectId cid = ctx.Entity("Odaberite trasu (polilinija, linija ili luk)", typeof(Curve));
            (Ring ring, bool closed) = ElementService.ReadCurve((Entity)ctx.Tr.GetObject(cid, OpenMode.ForRead));
            ElementType type = ctx.ChooseType("Tip elementa koji se ponavlja", null, GeometryKind.Point);
            double spacing = ctx.Number("Razmak (m)", 8.0);
            SpacingMode mode = ctx.Keyword("Način razmaka [Fiksni/Uklopi]", new[] { "Fixed|Fiksni", "Fit|Uklopi" }, "Fit") == "Fit" ? SpacingMode.Fit : SpacingMode.Fixed;
            double start = ctx.Number("Odmak od početka (m)", 0, false, true);
            double end = ctx.Number("Odmak od kraja (m)", 0, false, true);
            double lateral = ctx.Number("Bočni odmak lijevo od trase, negativno = desno (m)", 0, true, true);
            string align = ctx.Keyword("Rotacija [Poravnaj/Fiksna]", new[] { "Align|Poravnaj", "Fixed|Fiksna" }, "Align");
            double extra = ctx.Number(align == "Align" ? "Dodatna rotacija (°)" : "Rotacija (°)", 0, true, true) * Math.PI / 180;
            double k = ctx.K;
            List<Placement> placements = PathPlacement.Distribute(ring, closed, spacing * k, start * k, end * k, mode, lateral * k, align == "Align" ? extra : 0);
            List<SiteElement> existing = ctx.ExistingElements();
            foreach (Placement p in placements)
            {
                SiteElement e = ctx.NewElement(type, new PointGeometry(p.Position, align == "Align" ? p.Rotation : extra), existing);
                ctx.Svc.Create(e);
                existing.Add(e);
            }

            ctx.Write("Duž trase postavljeno: " + placements.Count + " × " + type.Name + ".");
        });

        /// <summary>Generates a row of parking bays along an aisle edge.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPARKING", CommandFlags.Modal)]
        public void Parking() => CommandContext.RunMajor("SDPPARKING", "Masovno postavljanje (SDPPARKING)", ctx =>
        {
            ElementType type = ctx.ChooseType("Tip parking mjesta", "road.parking_bay", GeometryKind.Point);
            if (type.Builder.Id != "parking_bay")
            {
                type = ctx.ChooseType("Tip parking mjesta (generator parking_bay)", null, GeometryKind.Point);
            }

            Point3d a = ctx.Point("Početak ivice prolaza")!.Value;
            Point3d b = ctx.Point("Kraj ivice prolaza", a)!.Value;
            Point3d side = ctx.Point("Tačka na strani gdje su parking mjesta", b)!.Value;
            double angle = ctx.Number("Ugao parking mjesta prema prolazu (°)", 90);
            double width = type.Parameter("width")!.Default.AsNumber();
            Vector3d dir = b - a;
            bool left = (dir.X * (side.Y - a.Y)) - (dir.Y * (side.X - a.X)) > 0;
            List<Placement> row = ParkingLayout.Row(new Vec3(a.X, a.Y, a.Z), new Vec3(b.X, b.Y, b.Z), width * ctx.K, left, angle);
            List<SiteElement> existing = ctx.ExistingElements();
            int n = 0;
            foreach (Placement p in row)
            {
                SiteElement e = ctx.NewElement(type, new PointGeometry(p.Position, p.Rotation), existing);
                e.Parameters["bayNumber"] = ParamValue.Of((++n).ToString(CultureInfo.InvariantCulture));
                ctx.Svc.Create(e);
                existing.Add(e);
            }

            ctx.Write("Napravljeno parking mjesta: " + row.Count + " (širina " + CommandContext.F(width) + " m, ugao " + CommandContext.F(angle, 1) + "°).");
        });

        /// <summary>Fills a closed boundary with a planting grid of point elements.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPGRID", CommandFlags.Modal)]
        public void Grid() => CommandContext.RunMajor("SDPGRID", "Masovno postavljanje (SDPGRID)", ctx =>
        {
            ObjectId cid = ctx.Entity("Odaberite zatvorenu granicu", typeof(Curve));
            (Ring ring, bool closed) = ElementService.ReadCurve((Entity)ctx.Tr.GetObject(cid, OpenMode.ForRead));
            if (!closed)
            {
                ctx.Write("Granica mora biti zatvorena.");
                return;
            }

            ElementType type = ctx.ChooseType("Tip elementa", null, GeometryKind.Point);
            double sx = ctx.Number("Razmak u redu (m)", 1.0);
            double sy = ctx.Number("Razmak redova (m)", sx);
            bool stagger = ctx.Keyword("Smaknuti raspored (trouglasti) [Da/Ne]", new[] { "Yes|Da", "No|Ne" }, "Yes") == "Yes";
            double margin = ctx.Number("Udaljenost od granice (m)", sx / 2, false, true);
            double k = ctx.K;
            List<Vec2> outline = GeometryMath.Tessellate(ring, true, 0.05 * k).Select(v => v.XY).ToList();
            List<Vec2> pts = GridLayout.InPolygon(outline, sx * k, sy * k, (ring.Vertices[1].XY - ring.Vertices[0].XY).Angle, margin * k, stagger);
            List<SiteElement> existing = ctx.ExistingElements();
            foreach (Vec2 p in pts)
            {
                SiteElement e = ctx.NewElement(type, new PointGeometry(p.WithZ(ring.Vertices[0].Z), 0), existing);
                ctx.Svc.Create(e);
                existing.Add(e);
            }

            ctx.Write("Postavljeno: " + pts.Count + " × " + type.Name + ".");
        });
    }
}
