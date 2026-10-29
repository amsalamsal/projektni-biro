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
            var o = new JigPromptPointOptions("\nInsertion point [Enter to finish]: ")
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
                ElementType type = ctx.ChooseType("Element type", null);
                if (type.Geometry != GeometryKind.Point)
                {
                    ctx.Write(type.Name + " is drawn as a " + ElementGeometry.KindCode(type.Geometry) + ".");
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

                    rotation = ctx.Angle("Rotation <" + CommandContext.F(rotation * 180 / Math.PI, 1) + ">", jig.Position, rotation);
                    SiteElement element = ctx.NewElement(type, new PointGeometry(new Vec3(jig.Position.X, jig.Position.Y, jig.Position.Z), rotation), existing);
                    ctx.Svc.Create(element);
                    existing.Add(element);
                    placed++;
                    ctx.Tr.TransactionManager.QueueForGraphicsFlush();
                }

                ctx.Write(placed + " " + type.Name + " placed.");
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
            CommandContext.Run("SDPDRAW", ctx => type = ctx.ChooseType("Element type", null, GeometryKind.Path, GeometryKind.Polygon));
            if (type != null)
            {
                DrawInteractive(type);
            }
        }

        /// <summary>
        /// Native drawing runs between transactions: the user draws with PLINE or RECTANG (snaps, polar
        /// tracking, arcs), then the new object is converted in a separate transaction.
        /// </summary>
        private static void DrawInteractive(ElementType type)
        {
            string? method = null;
            var picked = new List<ObjectId>();
            CommandContext.Run("SDPDRAW", ctx =>
            {
                method = type.Geometry == GeometryKind.Polygon
                    ? ctx.Keyword("Boundary [Polyline/Rectangle/Pick/Select]", new[] { "Polyline", "Rectangle", "Pick", "Select" }, "Polyline")
                    : ctx.Keyword("Alignment [Polyline/Select]", new[] { "Polyline", "Select" }, "Polyline");
                if (method == "Select")
                {
                    picked.AddRange(SelectCurves(ctx));
                }
                else if (method == "Pick")
                {
                    while (ctx.Point("Pick inside the area [Enter to finish]", null, true) is Point3d inside)
                    {
                        DBObjectCollection found = ctx.Ed.TraceBoundary(inside, false);
                        Curve? outer = found.Cast<DBObject>().OfType<Curve>().OrderByDescending(c => c.GeometricExtents.MaxPoint.DistanceTo(c.GeometricExtents.MinPoint)).FirstOrDefault();
                        if (outer == null)
                        {
                            ctx.Write("No closed boundary found around that point.");
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
                Editor ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
                ObjectId before = Autodesk.AutoCAD.Internal.Utils.EntLast();
                ed.Command(method == "Rectangle" ? "_.RECTANG" : "_.PLINE");
                while ((Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE"), CultureInfo.InvariantCulture) & 1) == 1)
                {
                    ed.Command(Editor.PauseToken);
                }

                ObjectId last = Autodesk.AutoCAD.Internal.Utils.EntLast();
                if (last.IsNull || last == before)
                {
                    ed.WriteMessage("\nNothing was drawn.");
                    return;
                }

                picked.Add(last);
            }

            if (picked.Count > 0)
            {
                CommandContext.Run("SDPDRAW", ctx => ctx.Write(ConvertCurves(ctx, type, picked) + " " + type.Name + " created."));
            }
        }

        /// <summary>Converts existing lines, arcs, circles and polylines into managed elements.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPCONVERT", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void ConvertCommand() => CommandContext.Run("SDPCONVERT", ctx =>
        {
            List<ObjectId> curves = SelectCurves(ctx);
            ElementType type = ctx.ChooseType("Element type for the selected objects", null, GeometryKind.Path, GeometryKind.Polygon);
            ctx.Write(ConvertCurves(ctx, type, curves) + " objects converted to " + type.Name + ".");
        });

        private static List<ObjectId> SelectCurves(CommandContext ctx)
        {
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE,LINE,ARC,CIRCLE") });
            PromptSelectionResult implied = ctx.Ed.SelectImplied();
            if (implied.Status == PromptStatus.OK)
            {
                return implied.Value.GetObjectIds().ToList();
            }

            PromptSelectionResult r = ctx.Ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nSelect polylines, lines, arcs or circles: " }, filter);
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
                    ctx.Write("Object " + entity.Handle + " is already a Site Design Pro element; use SDPEDIT.");
                    continue;
                }

                ElementGeometry g;
                try
                {
                    g = ctx.Svc.ReadGeometry(entity, type.Geometry, string.Empty);
                }
                catch (InvalidOperationException ex)
                {
                    ctx.Write("Skipped " + entity.Handle + ": " + ex.Message + (type.Geometry == GeometryKind.Polygon ? " Close the polyline (PEDIT Close) or use the Pick option." : string.Empty));
                    continue;
                }

                SiteElement element = ctx.NewElement(type, g, existing);
                ValidationReport check = new ProjectValidator(ctx.Catalog, ctx.Settings.Checks).Validate(new[] { Core.Exchange.PackageWriter.ToPackageFrame(element, ctx.Settings) });
                foreach (ValidationIssue issue in check.Issues.Where(i => i.Severity == Severity.Error && i.Kind == IssueKind.GeometricError))
                {
                    ctx.Write("Warning for " + element.Label + ": " + issue.Message + " " + issue.Resolution);
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
            List<ManagedElement> sel = ctx.SelectElements("Select elements to edit", false, m => m.Element != null && m.Type != null && !m.IsCopy);
            if (sel.Count == 0)
            {
                ctx.Write("No editable Site Design Pro elements selected.");
                return;
            }

            if (sel.Select(m => m.Type!.Id).Distinct().Count() > 1)
            {
                ctx.Write("Selected elements have different types; only parameters they share can be changed.");
            }

            ElementType type = sel[0].Type!;
            List<ParameterDefinition> shared = type.Parameters.Where(p => sel.All(m => m.Type!.Parameter(p.Key) != null)).ToList();
            while (true)
            {
                SiteElement first = sel[0].Element!;
                ctx.Write(type.Name + (sel.Count > 1 ? " (" + sel.Count + " elements)" : " " + first.Label) + ":");
                foreach (ParameterDefinition p in shared)
                {
                    IEnumerable<string> values = sel.Select(m => ElementCatalog.FormatValue(p, m.Element!.Parameters.Contains(p.Key) ? m.Element.Parameters[p.Key] : p.Default)).Distinct();
                    ctx.Write("  " + p.Key.PadRight(24) + string.Join(" | ", values.Take(3)) + (values.Count() > 3 ? " | ..." : string.Empty) + "   (" + p.Label + ")");
                }

                string[] keys = shared.Select(p => p.Key).Concat(new[] { "Label", "Property", "eXit" }).ToArray();
                string key = ctx.Keyword("Parameter to change", keys, "eXit");
                if (key == "eXit")
                {
                    break;
                }

                if (key == "Label")
                {
                    if (sel.Count > 1)
                    {
                        ctx.Write("Labels are unique; select a single element or use SDPNUMBER.");
                        continue;
                    }

                    first.Label = ctx.Text("Label", first.Label ?? string.Empty, false);
                }
                else if (key == "Property")
                {
                    string pk = ctx.Text("Property name (e.g. botanicalName, manufacturer, notes)", null, false);
                    string pv = ctx.Text("Value (- to clear)", "-");
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
                        ctx.Write("Not applied to " + m.Element!.Label + ": " + err.Message);
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
                    return ParamValue.Of(ctx.Keyword(def.Label + " [Yes/No]", new[] { "Yes", "No" }, current.AsBool() ? "Yes" : "No") == "Yes");
                case ParameterType.Enum:
                    return ParamValue.Of(ctx.Keyword(def.Label + " [" + string.Join("/", def.Options) + "]", def.Options.ToArray(), current.AsText()));
                case ParameterType.Integer:
                    return ParamValue.Of((double)ctx.Integer(def.Label, (int)current.AsNumber(def.Min ?? 1), (int)(def.Min ?? 0)));
                case ParameterType.Length:
                case ParameterType.Number:
                case ParameterType.Angle:
                    return ParamValue.Of(ctx.Number(def.Label + " (" + def.DisplayUnit + ")", current.IsNull ? (def.Min ?? 0) : current.AsNumber(), (def.Min ?? 0) < 0, (def.Min ?? 0) <= 0));
                case ParameterType.Material:
                    {
                        string v = ctx.Text(def.Label + " (material key, ? to list)", current.AsText(), false);
                        if (v == "?")
                        {
                            foreach (MaterialDefinition m in ctx.Catalog.Materials)
                            {
                                ctx.Write("  " + m.Key.PadRight(20) + m.Name);
                            }

                            v = ctx.Text(def.Label + " (material key)", current.AsText(), false);
                        }

                        return ParamValue.Of(v);
                    }

                default:
                    {
                        string v = ctx.Text(def.Label + " (- to clear)", current.IsNull ? "-" : current.AsText());
                        return v == "-" ? ParamValue.Null : ParamValue.Of(v);
                    }
            }
        }

        /// <summary>Scriptable parameter change used by the palette: handle(s), key, value.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPSETPARAM", CommandFlags.Modal)]
        public void SetParam() => CommandContext.Run("SDPSETPARAM", ctx =>
        {
            string handles = ctx.Text("Element handles (comma separated)", null, false);
            string key = ctx.Text("Parameter", null, false);
            string raw = ctx.Text("Value", null, true);
            foreach (string h in handles.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!ctx.Db.TryGetObjectId(new Handle(Convert.ToInt64(h, 16)), out ObjectId id) || id.IsErased)
                {
                    ctx.Write("Element " + h + " not found.");
                    continue;
                }

                ManagedElement? m = ctx.Svc.Read((Entity)ctx.Tr.GetObject(id, OpenMode.ForRead));
                ParameterDefinition? def = m?.Type?.Parameter(key);
                if (m?.Element == null || def == null)
                {
                    ctx.Write("Parameter '" + key + "' is not defined for element " + h + ".");
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
                    ctx.Write("Not applied: " + err.Message);
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
                    return ParamValue.Of(raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw.Equals("yes", StringComparison.OrdinalIgnoreCase) || raw == "1");
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
            string mode = ctx.Keyword("Select by [Type/Category/Label/Id/Material/All]", new[] { "Type", "Category", "Label", "Id", "Material", "All" }, "Type");
            Func<ManagedElement, bool> predicate;
            switch (mode)
            {
                case "Type":
                    {
                        ElementType t = ctx.ChooseType("Element type", null);
                        predicate = m => m.Record.TypeId == t.Id;
                        break;
                    }

                case "Category":
                    {
                        string[] cats = ctx.Catalog.Categories.Select(c => c.Id).ToArray();
                        string c = ctx.Keyword("Category [" + string.Join("/", cats) + "]", cats);
                        predicate = m => m.Type?.Category == c;
                        break;
                    }

                case "Label":
                    {
                        string q = ctx.Text("Label or prefix (e.g. T- or T-012)", null, false);
                        predicate = m => (m.Record.Label ?? string.Empty).StartsWith(q, StringComparison.OrdinalIgnoreCase);
                        break;
                    }

                case "Id":
                    {
                        string q = ctx.Text("Element id (or its start)", null, false).ToLowerInvariant();
                        predicate = m => m.Record.Id.StartsWith(q, StringComparison.Ordinal);
                        break;
                    }

                case "Material":
                    {
                        string q = ctx.Text("Material key", null, false);
                        predicate = m => m.Record.Parameters.Any(kv => kv.Value.Kind == ParamKind.Text && kv.Value.AsText() == q && m.Type?.Parameter(kv.Key)?.Type == ParameterType.Material);
                        break;
                    }

                default:
                    predicate = _ => true;
                    break;
            }

            ObjectId[] ids = ctx.Svc.ReadAll().Where(predicate).Select(m => m.ObjectId).ToArray();
            ctx.Ed.SetImpliedSelection(ids);
            ctx.Write(ids.Length + " elements selected.");
        });

        /// <summary>Zooms to an element by handle (used by the validation palette).</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPZOOM", CommandFlags.Modal | CommandFlags.Redraw)]
        public void Zoom() => CommandContext.Run("SDPZOOM", ctx =>
        {
            string h = ctx.Text("Element handle", null, false);
            if (!ctx.Db.TryGetObjectId(new Handle(Convert.ToInt64(h, 16)), out ObjectId id) || id.IsErased)
            {
                ctx.Write("Element not found.");
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
        public void PathArray() => CommandContext.Run("SDPPATHARRAY", ctx =>
        {
            ObjectId cid = ctx.Entity("Select path (polyline, line or arc)", typeof(Curve));
            (Ring ring, bool closed) = ElementService.ReadCurve((Entity)ctx.Tr.GetObject(cid, OpenMode.ForRead));
            ElementType type = ctx.ChooseType("Element type to repeat", null, GeometryKind.Point);
            double spacing = ctx.Number("Spacing (m)", 8.0);
            SpacingMode mode = ctx.Keyword("Spacing mode [Fixed/Fit]", new[] { "Fixed", "Fit" }, "Fit") == "Fit" ? SpacingMode.Fit : SpacingMode.Fixed;
            double start = ctx.Number("Start offset (m)", 0, false, true);
            double end = ctx.Number("End offset (m)", 0, false, true);
            double lateral = ctx.Number("Offset to the left of the path, negative = right (m)", 0, true, true);
            string align = ctx.Keyword("Rotation [Align/Fixed]", new[] { "Align", "Fixed" }, "Align");
            double extra = ctx.Number(align == "Align" ? "Additional rotation (deg)" : "Rotation (deg)", 0, true, true) * Math.PI / 180;
            double k = ctx.K;
            List<Placement> placements = PathPlacement.Distribute(ring, closed, spacing * k, start * k, end * k, mode, lateral * k, align == "Align" ? extra : 0);
            List<SiteElement> existing = ctx.ExistingElements();
            foreach (Placement p in placements)
            {
                SiteElement e = ctx.NewElement(type, new PointGeometry(p.Position, align == "Align" ? p.Rotation : extra), existing);
                ctx.Svc.Create(e);
                existing.Add(e);
            }

            ctx.Write(placements.Count + " " + type.Name + " placed along the path.");
        });

        /// <summary>Generates a row of parking bays along an aisle edge.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPARKING", CommandFlags.Modal)]
        public void Parking() => CommandContext.Run("SDPPARKING", ctx =>
        {
            ElementType type = ctx.ChooseType("Bay type", "road.parking_bay", GeometryKind.Point);
            if (type.Builder.Id != "parking_bay")
            {
                type = ctx.ChooseType("Bay type (parking_bay builder)", null, GeometryKind.Point);
            }

            Point3d a = ctx.Point("Start of the aisle edge")!.Value;
            Point3d b = ctx.Point("End of the aisle edge", a)!.Value;
            Point3d side = ctx.Point("Pick a point on the side where the bays go", b)!.Value;
            double angle = ctx.Number("Bay angle to the aisle (deg)", 90);
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

            ctx.Write(row.Count + " bays created (" + CommandContext.F(width) + " m wide, " + CommandContext.F(angle, 1) + " deg).");
        });

        /// <summary>Fills a closed boundary with a planting grid of point elements.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPGRID", CommandFlags.Modal)]
        public void Grid() => CommandContext.Run("SDPGRID", ctx =>
        {
            ObjectId cid = ctx.Entity("Select closed boundary", typeof(Curve));
            (Ring ring, bool closed) = ElementService.ReadCurve((Entity)ctx.Tr.GetObject(cid, OpenMode.ForRead));
            if (!closed)
            {
                ctx.Write("The boundary must be closed.");
                return;
            }

            ElementType type = ctx.ChooseType("Element type", null, GeometryKind.Point);
            double sx = ctx.Number("Spacing along rows (m)", 1.0);
            double sy = ctx.Number("Row spacing (m)", sx);
            bool stagger = ctx.Keyword("Staggered (triangular) [Yes/No]", new[] { "Yes", "No" }, "Yes") == "Yes";
            double margin = ctx.Number("Distance from the boundary (m)", sx / 2, false, true);
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

            ctx.Write(pts.Count + " " + type.Name + " placed.");
        });
    }
}
