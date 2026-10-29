using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Labels;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Quantities;
using SiteDesignPro.Core.Symbols;

namespace SiteDesignPro.AutoCAD.Drawing
{
    /// <summary>
    /// Drawing annotations. All sizes derive from the project label settings (plotted text height x scale),
    /// so annotation is consistent for the chosen drawing scale. Created objects are native AutoCAD entities.
    /// </summary>
    internal sealed class Annotations
    {
        private readonly Database db;
        private readonly Transaction tr;
        private readonly ElementService svc;
        private readonly ProjectSettings settings;

        public Annotations(Database db, Transaction tr, ElementService svc)
        {
            this.db = db;
            this.tr = tr;
            this.svc = svc;
            settings = svc.Settings;
        }

        private double K => settings.Scale.DrawingUnitsPerMeter;

        /// <summary>Model-space text height in drawing units.</summary>
        public double TextHeight => settings.Labels.ModelTextHeightMeters * K;

        private CultureInfo C => CultureInfo.InvariantCulture;

        public static string LabelText(SiteElement e, ElementCatalog catalog)
        {
            ElementType? t = catalog.FindType(e.TypeId);
            string first = string.IsNullOrEmpty(e.Label) ? (t?.Name ?? e.TypeId) : e.Label!;
            string botanical = e.Property("botanicalName");
            if (botanical.Length > 0)
            {
                return first + "\\P" + botanical;
            }

            string code = e.Parameters.Text("fixtureCode", string.Empty);
            if (code.Length > 0)
            {
                return first + "\\P" + code;
            }

            return first;
        }

        // ------------------------------------------------------------------ primitives

        private ObjectId Add(Entity e, string layerKey)
        {
            e.SetDatabaseDefaults(db);
            e.Layer = svc.Layers.Ensure(layerKey);
            BlockTableRecord ms = svc.ModelSpace(OpenMode.ForWrite);
            ObjectId id = ms.AppendEntity(e);
            tr.AddNewlyCreatedDBObject(e, true);
            return id;
        }

        public ObjectId Text(Point3d at, string text, double height, string layerKey, double rotation = 0, TextHorizontalMode h = TextHorizontalMode.TextLeft, TextVerticalMode v = TextVerticalMode.TextBase)
        {
            var t = new DBText { TextString = text, Height = height, Rotation = rotation, Position = at, HorizontalMode = h, VerticalMode = v };
            if (h != TextHorizontalMode.TextLeft || v != TextVerticalMode.TextBase)
            {
                t.AlignmentPoint = at;
            }

            ObjectId id = Add(t, layerKey);
            t.AdjustAlignment(db);
            return id;
        }

        public ObjectId Poly(IEnumerable<Point2d> pts, bool closed, string layerKey, double elevation = 0, double width = 0)
        {
            var pl = new Polyline();
            int i = 0;
            foreach (Point2d p in pts)
            {
                pl.AddVertexAt(i++, p, 0, width, width);
            }

            pl.Closed = closed;
            pl.Elevation = elevation;
            return Add(pl, layerKey);
        }

        public ObjectId Solid(IList<Point2d> pts, string layerKey)
        {
            var h = new Hatch();
            ObjectId id = Add(h, layerKey);
            h.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
            var loop = new Point2dCollection(pts.Append(pts[0]).ToArray());
            h.AppendLoop(HatchLoopTypes.External, loop, new DoubleCollection(Enumerable.Repeat(0.0, loop.Count).ToArray()));
            h.EvaluateHatch(true);
            return id;
        }

        // ------------------------------------------------------------------ element labels

        /// <summary>Creates a multileader label for an element (or updates an existing one).</summary>
        public ObjectId Label(ManagedElement m, ElementCatalog catalog)
        {
            SiteElement e = m.Element!;
            svc.EraseAux(e.Id, "label");
            Vec2 anchor = LabelNumbering.Anchor(e);
            double h = TextHeight;
            var target = new Point3d(anchor.X, anchor.Y, 0);
            var text = new MText { Contents = LabelText(e, catalog), TextHeight = h, Location = target + new Vector3d(h * 4, h * 4, 0) };
            var ml = new MLeader();
            ObjectId id = Add(ml, "anno_labels");
            ml.ContentType = ContentType.MTextContent;
            ml.MText = text;
            ml.AddLeaderLine(target);
            ml.TextLocation = target + new Vector3d(h * 4, h * 4, 0);
            ml.ArrowSize = h * 0.8;
            ml.LandingGap = h * 0.3;
            ElementStore.WriteAux(ml, e.Id, "label", tr);
            return id;
        }

        // ------------------------------------------------------------------ site annotation symbols

        public void NorthArrow(Point3d at)
        {
            double s = TextHeight * 6;
            double a = settings.TrueNorthDegrees * Math.PI / 180; // clockwise from +Y
            Point2d P(double x, double y)
            {
                double c = Math.Cos(-a), si = Math.Sin(-a);
                return new Point2d(at.X + (x * c) - (y * si), at.Y + (x * si) + (y * c));
            }

            Add(new Circle(at, Vector3d.ZAxis, s * 0.6), "anno_symbols");
            Solid(new[] { P(0, s * 0.55), P(-s * 0.2, -s * 0.4), P(0, -s * 0.2) }, "anno_symbols");
            Poly(new[] { P(0, s * 0.55), P(s * 0.2, -s * 0.4), P(0, -s * 0.2) }, true, "anno_symbols");
            Point2d n = P(0, s * 0.8);
            Text(new Point3d(n.X, n.Y, 0), "N", TextHeight * 1.4, "anno_symbols", -a, TextHorizontalMode.TextCenter, TextVerticalMode.TextBottom);
        }

        public void ScaleBar(Point3d at, double lengthMeters, int segments)
        {
            double h = TextHeight;
            double seg = lengthMeters / segments * K;
            for (int i = 0; i < segments; i++)
            {
                var pts = new[] { new Point2d(at.X + (i * seg), at.Y), new Point2d(at.X + ((i + 1) * seg), at.Y), new Point2d(at.X + ((i + 1) * seg), at.Y + (h * 0.8)), new Point2d(at.X + (i * seg), at.Y + (h * 0.8)) };
                if (i % 2 == 0)
                {
                    Solid(pts, "anno_symbols");
                }

                Poly(pts, true, "anno_symbols");
                Text(new Point3d(at.X + (i * seg), at.Y - (h * 0.4), 0), (lengthMeters / segments * i).ToString("0.##", C), h * 0.8, "anno_symbols", 0, TextHorizontalMode.TextCenter, TextVerticalMode.TextTop);
            }

            Text(new Point3d(at.X + (segments * seg), at.Y - (h * 0.4), 0), lengthMeters.ToString("0.##", C) + " m", h * 0.8, "anno_symbols", 0, TextHorizontalMode.TextCenter, TextVerticalMode.TextTop);
            Text(new Point3d(at.X, at.Y + (h * 1.6), 0), "1:" + settings.Labels.ScaleDenominator.ToString("0", C), h, "anno_symbols");
        }

        /// <summary>Spot level marker; the level is absolute (project origin elevation + point Z), in metres.</summary>
        public void SpotLevel(Point3d at, double? overrideLevel)
        {
            double h = TextHeight;
            double level = overrideLevel ?? (at.Z / K);
            var tri = new[] { new Point2d(at.X, at.Y), new Point2d(at.X - (h * 0.6), at.Y + h), new Point2d(at.X + (h * 0.6), at.Y + h) };
            Poly(tri, true, "anno_levels");
            Add(new Line(new Point3d(at.X - (h * 0.6), at.Y + h, 0), new Point3d(at.X + (h * 6), at.Y + h, 0)), "anno_levels");
            Text(new Point3d(at.X + (h * 0.8), at.Y + (h * 1.25), 0), (level >= 0 ? "+" : string.Empty) + level.ToString("0.00", C), h, "anno_levels");
        }

        /// <summary>Slope arrow from high to low point with gradient in percent (from Z values or given).</summary>
        public void SlopeArrow(Point3d high, Point3d low, double? percent)
        {
            double h = TextHeight;
            double run = new Vector2d(low.X - high.X, low.Y - high.Y).Length;
            double pct = percent ?? (run > 0 ? (high.Z - low.Z) / run * 100 : 0);
            Vector2d d = new Vector2d(low.X - high.X, low.Y - high.Y).GetNormal();
            Vector2d n = d.GetPerpendicularVector();
            Add(new Line(new Point3d(high.X, high.Y, 0), new Point3d(low.X, low.Y, 0)), "anno_levels");
            var tip = new Point2d(low.X, low.Y);
            Solid(new[] { tip, tip - (d * h * 1.4) + (n * h * 0.45), tip - (d * h * 1.4) - (n * h * 0.45) }, "anno_levels");
            var mid = new Point3d((high.X + low.X) / 2, (high.Y + low.Y) / 2, 0) + (new Vector3d(n.X, n.Y, 0) * h * 0.6);
            double ang = Math.Atan2(d.Y, d.X);
            if (ang > Math.PI / 2 || ang < -Math.PI / 2)
            {
                ang += Math.PI;
            }

            Text(mid, pct.ToString("0.0", C) + " %", h, "anno_levels", ang, TextHorizontalMode.TextCenter, TextVerticalMode.TextBottom);
        }

        /// <summary>Coordinate marker with easting/northing in metres (WCS includes the project origin).</summary>
        public void Coordinate(Point3d at)
        {
            double h = TextHeight;
            Add(new Line(at - new Vector3d(h, 0, 0), at + new Vector3d(h, 0, 0)), "anno_symbols");
            Add(new Line(at - new Vector3d(0, h, 0), at + new Vector3d(0, h, 0)), "anno_symbols");
            Text(at + new Vector3d(h * 1.3, h * 0.3, 0), "E " + (at.X / K).ToString("0.000", C), h * 0.8, "anno_symbols");
            Text(at + new Vector3d(h * 1.3, -h * 1.1, 0), "N " + (at.Y / K).ToString("0.000", C), h * 0.8, "anno_symbols");
        }

        public void SectionMarker(Point3d a, Point3d b, string name)
        {
            double h = TextHeight;
            Add(new Line(a, b) { LinetypeScale = 1 }, "anno_symbols");
            Vector3d d = (b - a).GetNormal();
            Vector3d n = new Vector3d(-d.Y, d.X, 0);
            foreach (Point3d p in new[] { a, b })
            {
                Add(new Circle(p, Vector3d.ZAxis, h * 1.2), "anno_symbols");
                Text(p, name, h, "anno_symbols", 0, TextHorizontalMode.TextCenter, TextVerticalMode.TextVerticalMid);
                Point3d t = p + (n * h * 1.2);
                Solid(new[] { new Point2d(t.X, t.Y) + new Vector2d(n.X, n.Y) * h, new Point2d(t.X, t.Y) + new Vector2d(d.X, d.Y) * h * 0.6, new Point2d(t.X, t.Y) - new Vector2d(d.X, d.Y) * h * 0.6 }, "anno_symbols");
            }
        }

        public void DetailReference(Point3d at, string detail, string sheet)
        {
            double h = TextHeight;
            Add(new Circle(at, Vector3d.ZAxis, h * 1.6), "anno_symbols");
            Add(new Line(at - new Vector3d(h * 1.6, 0, 0), at + new Vector3d(h * 1.6, 0, 0)), "anno_symbols");
            Text(at + new Vector3d(0, h * 0.25, 0), detail, h, "anno_symbols", 0, TextHorizontalMode.TextCenter, TextVerticalMode.TextBottom);
            Text(at - new Vector3d(0, h * 0.25, 0), sheet, h * 0.8, "anno_symbols", 0, TextHorizontalMode.TextCenter, TextVerticalMode.TextTop);
        }

        public void AreaLabel(ManagedElement m, ElementCatalog catalog)
        {
            if (!(m.Element?.Geometry is PolygonGeometry))
            {
                return;
            }

            SiteElement local = Core.Exchange.PackageWriter.ToPackageFrame(m.Element, settings);
            double area = GeometryMath.Area((PolygonGeometry)local.Geometry);
            Vec2 anchor = LabelNumbering.Anchor(m.Element);
            ElementType? t = catalog.FindType(m.Element.TypeId);
            string code = m.Element.Parameters.Text("surfaceCode", string.Empty);
            string line1 = (code.Length > 0 ? code + " " : string.Empty) + (t?.Name ?? m.Element.TypeId);
            svc.EraseAux(m.Element.Id, "area");
            double h = TextHeight;
            ObjectId a = Text(new Point3d(anchor.X, anchor.Y + (h * 0.7), 0), line1, h, "anno_labels", 0, TextHorizontalMode.TextCenter, TextVerticalMode.TextBottom);
            ObjectId b = Text(new Point3d(anchor.X, anchor.Y - (h * 0.2), 0), area.ToString("0." + new string('0', settings.Labels.AreaPrecision), C) + " m²", h, "anno_labels", 0, TextHorizontalMode.TextCenter, TextVerticalMode.TextTop);
            ElementStore.WriteAux((Entity)tr.GetObject(a, OpenMode.ForWrite), m.Element.Id, "area", tr);
            ElementStore.WriteAux((Entity)tr.GetObject(b, OpenMode.ForWrite), m.Element.Id, "area", tr);
        }

        public void WidthLabel(ManagedElement m)
        {
            if (!(m.Element?.Geometry is PathGeometry path))
            {
                return;
            }

            ElementType? t = AutoCADCatalog.Type(m.Element.TypeId);
            string? key = t?.Builder.PolylineWidthParam;
            if (key == null)
            {
                return;
            }

            double w = m.Element.Parameters.Number(key, t!.Parameter(key)?.Default.AsNumber() ?? 0);
            double len = GeometryMath.Length(path.Ring, path.Closed);
            (Vec3 p, double angle) = GeometryMath.PointAtDistance(path.Ring, path.Closed, len / 2);
            if (angle > Math.PI / 2 && angle < 1.5 * Math.PI)
            {
                angle += Math.PI;
            }

            svc.EraseAux(m.Element.Id, "width");
            ObjectId id = Text(new Point3d(p.X, p.Y, 0), "w = " + w.ToString("0." + new string('0', settings.Labels.LinearPrecision), C) + " m", TextHeight * 0.9, "anno_dims", angle, TextHorizontalMode.TextCenter, TextVerticalMode.TextBottom);
            ElementStore.WriteAux((Entity)tr.GetObject(id, OpenMode.ForWrite), m.Element.Id, "width", tr);
        }

        /// <summary>Symbol key: one row per point type used in the drawing (symbol at real size scaled to fit) plus area/line types.</summary>
        public void Legend(Point3d at, IEnumerable<ManagedElement> elements, ElementCatalog catalog)
        {
            double h = TextHeight;
            double row = h * 3.2;
            Text(at, "LEGEND", h * 1.3, "anno_schedules");
            double y = at.Y - (row * 0.8);
            foreach (IGrouping<string, ManagedElement> g in elements.Where(e => e.Element != null && e.Type != null).GroupBy(e => e.Type!.Id).OrderBy(g => g.First().Type!.Category).ThenBy(g => g.Key))
            {
                ElementType t = g.First().Type!;
                var center = new Point3d(at.X + (h * 1.5), y, 0);
                if (t.Geometry == GeometryKind.Point)
                {
                    SymbolDrawing d = SymbolLibrary.Build(t.Builder.Id, t.CreateDefaultParameters());
                    Box2 b = d.Bounds();
                    double size = Math.Max(b.Width, b.Height) * K;
                    double s = size > 0 ? Math.Min(1.0, h * 2.2 / size) : 1.0;
                    ObjectId block = SymbolBlocks.Ensure(db, tr, d, settings.Scale);
                    var br = new BlockReference(center - new Vector3d(((b.MinX + b.MaxX) / 2) * K * s, ((b.MinY + b.MaxY) / 2) * K * s, 0), block) { ScaleFactors = new Scale3d(s) };
                    Add(br, t.LayerKey);
                }
                else if (t.Geometry == GeometryKind.Polygon)
                {
                    Poly(new[] { new Point2d(center.X - h, center.Y - (h * 0.6)), new Point2d(center.X + h, center.Y - (h * 0.6)), new Point2d(center.X + h, center.Y + (h * 0.6)), new Point2d(center.X - h, center.Y + (h * 0.6)) }, true, t.LayerKey);
                }
                else
                {
                    Poly(new[] { new Point2d(center.X - h, center.Y), new Point2d(center.X + h, center.Y) }, false, t.LayerKey);
                }

                Text(new Point3d(at.X + (h * 3.5), y - (h * 0.4), 0), t.Name + "  (" + g.Count().ToString(C) + ")", h, "anno_schedules");
                y -= row;
            }
        }

        /// <summary>ISO sheet border and title block at the project scale. Returns the sheet extents.</summary>
        public void TitleBlock(Point3d corner, string sheetSize, string title, string sheetNumber)
        {
            (double wMm, double hMm) = sheetSize.ToUpperInvariant() switch
            {
                "A0" => (1189.0, 841.0),
                "A1" => (841.0, 594.0),
                "A2" => (594.0, 420.0),
                "A4" => (297.0, 210.0),
                _ => (420.0, 297.0),
            };
            double f = settings.Labels.ScaleDenominator / 1000.0 * K; // paper mm -> drawing units
            double w = wMm * f, hh = hMm * f, m = 10 * f;
            Point2d P(double x, double y) => new Point2d(corner.X + x, corner.Y + y);
            Poly(new[] { P(0, 0), P(w, 0), P(w, hh), P(0, hh) }, true, "anno_titleblock");
            Poly(new[] { P(m, m), P(w - m, m), P(w - m, hh - m), P(m, hh - m) }, true, "anno_titleblock", 0, 0.5 * f);
            double tbW = 180 * f, tbH = 56 * f;
            double x0 = w - m - tbW, y0 = m;
            Poly(new[] { P(x0, y0), P(x0 + tbW, y0), P(x0 + tbW, y0 + tbH), P(x0, y0 + tbH) }, true, "anno_titleblock");
            double rowH = tbH / 4;
            for (int i = 1; i < 4; i++)
            {
                Poly(new[] { P(x0, y0 + (i * rowH)), P(x0 + tbW, y0 + (i * rowH)) }, false, "anno_titleblock");
            }

            double th = 3.5 * f, sh = 2.5 * f;
            string[] rows =
            {
                "Project: " + settings.Name + (settings.Number.Length > 0 ? "  (" + settings.Number + ")" : string.Empty),
                "Drawing: " + title,
                "Client: " + settings.Client + "   Location: " + settings.Location,
                "Scale 1:" + settings.Labels.ScaleDenominator.ToString("0", C) + "   Sheet " + sheetNumber + "   Rev. " + settings.Revision + "   " + DateTime.Now.ToString("yyyy-MM-dd", C) + (settings.Author.Length > 0 ? "   " + settings.Author : string.Empty),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                Text(new Point3d(corner.X + x0 + (3 * f), corner.Y + y0 + tbH - ((i + 1) * rowH) + (rowH * 0.35), 0), rows[i], i == 1 ? th : sh, "anno_titleblock");
            }
        }

        /// <summary>Revision cloud around a rectangle plus a revision note.</summary>
        public void RevisionCloud(Point3d a, Point3d b, string note)
        {
            double arc = TextHeight * 2.5;
            double x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X), y0 = Math.Min(a.Y, b.Y), y1 = Math.Max(a.Y, b.Y);
            var corners = new[] { new Point2d(x0, y0), new Point2d(x1, y0), new Point2d(x1, y1), new Point2d(x0, y1) };
            var pl = new Polyline();
            int idx = 0;
            for (int i = 0; i < 4; i++)
            {
                Point2d s = corners[i], e = corners[(i + 1) % 4];
                int n = Math.Max(1, (int)Math.Round(s.GetDistanceTo(e) / arc));
                for (int k = 0; k < n; k++)
                {
                    // counter-clockwise outline: a positive bulge arcs to the right of travel, i.e. outward
                    pl.AddVertexAt(idx++, s + ((e - s) * k / n), 0.5, 0, 0);
                }
            }

            pl.Closed = true;
            Add(pl, "anno_revision");
            double h = TextHeight;
            var tri = new Point3d(x1 + (h * 1.5), y1 + (h * 1.5), 0);
            Poly(new[] { new Point2d(tri.X - h, tri.Y - (h * 0.8)), new Point2d(tri.X + h, tri.Y - (h * 0.8)), new Point2d(tri.X, tri.Y + h) }, true, "anno_revision");
            Text(tri, settings.Revision, h * 0.9, "anno_revision", 0, TextHorizontalMode.TextCenter, TextVerticalMode.TextVerticalMid);
            if (!string.IsNullOrWhiteSpace(note))
            {
                Text(tri + new Vector3d(h * 1.8, -h * 0.4, 0), note, h, "anno_revision");
            }
        }

        /// <summary>Grid lines with lettered/numbered bubbles over a rectangle at the given spacing (metres).</summary>
        public void GridLines(Point3d a, Point3d b, double spacingMeters)
        {
            double s = spacingMeters * K, h = TextHeight;
            double x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X), y0 = Math.Min(a.Y, b.Y), y1 = Math.Max(a.Y, b.Y);
            int col = 0;
            for (double x = x0; x <= x1 + 1e-6; x += s, col++)
            {
                Add(new Line(new Point3d(x, y0, 0), new Point3d(x, y1, 0)), "anno_symbols");
                Add(new Circle(new Point3d(x, y1 + (h * 1.6), 0), Vector3d.ZAxis, h * 1.2), "anno_symbols");
                Text(new Point3d(x, y1 + (h * 1.6), 0), ((col % 26) + 1).ToString(C), h, "anno_symbols", 0, TextHorizontalMode.TextCenter, TextVerticalMode.TextVerticalMid);
            }

            int row = 0;
            for (double y = y0; y <= y1 + 1e-6; y += s, row++)
            {
                Add(new Line(new Point3d(x0, y, 0), new Point3d(x1, y, 0)), "anno_symbols");
                Add(new Circle(new Point3d(x0 - (h * 1.6), y, 0), Vector3d.ZAxis, h * 1.2), "anno_symbols");
                Text(new Point3d(x0 - (h * 1.6), y, 0), ((char)('A' + (row % 26))).ToString(), h, "anno_symbols", 0, TextHorizontalMode.TextCenter, TextVerticalMode.TextVerticalMid);
            }
        }

        /// <summary>Clearance envelope (service, safety, lifting) around a point element's footprint.</summary>
        public int Clearances(ManagedElement m)
        {
            if (!(m.Element?.Geometry is PointGeometry) || m.Type == null)
            {
                return 0;
            }

            svc.EraseAux(m.Element.Id, "clearance");
            int count = 0;
            SiteElement local = Core.Exchange.PackageWriter.ToPackageFrame(m.Element, settings);
            foreach (string key in new[] { "serviceClearance", "safetyClearance", "liftingClearance" })
            {
                double c = m.Element.Parameters.Number(key, double.NaN);
                if (!(c > 0))
                {
                    continue;
                }

                foreach (Core.Validation.Footprint f in Core.Validation.Footprints.For(m.Type.Builder.Id, (PointGeometry)local.Geometry, m.Element.Parameters).Where(f => f.Zone != Core.Validation.FootprintZone.Underground))
                {
                    Core.Validation.Footprint z = f.Inflate(c);
                    Vec3 o = settings.OriginMeters;
                    IEnumerable<Point2d> pts = z.Corners().Select(p => new Point2d((p.X + o.X) * K, (p.Y + o.Y) * K));
                    ObjectId id = Poly(pts, true, "anno_clearance");
                    ElementStore.WriteAux((Entity)tr.GetObject(id, OpenMode.ForWrite), m.Element.Id, "clearance", tr);
                    count++;
                }
            }

            return count;
        }

        /// <summary>Illustrative coverage circle for a light (not a photometric calculation).</summary>
        public bool LightCoverage(ManagedElement m, double defaultRadius)
        {
            if (!(m.Element?.Geometry is PointGeometry p))
            {
                return false;
            }

            double r = m.Element.Parameters.Number("coverageRadius", defaultRadius);
            if (!(r > 0))
            {
                return false;
            }

            svc.EraseAux(m.Element.Id, "coverage");
            ObjectId c = Add(new Circle(new Point3d(p.Position.X, p.Position.Y, 0), Vector3d.ZAxis, r * K), "anno_clearance");
            ElementStore.WriteAux((Entity)tr.GetObject(c, OpenMode.ForWrite), m.Element.Id, "coverage", tr);
            ObjectId t = Text(new Point3d(p.Position.X, p.Position.Y - (r * K) - (TextHeight * 0.3), 0), "illustrative r=" + r.ToString("0.#", C) + " m", TextHeight * 0.7, "anno_clearance", 0, TextHorizontalMode.TextCenter, TextVerticalMode.TextTop);
            ElementStore.WriteAux((Entity)tr.GetObject(t, OpenMode.ForWrite), m.Element.Id, "coverage", tr);
            return true;
        }
    }

    /// <summary>Shortcut to the process catalog for drawing helpers.</summary>
    internal static class AutoCADCatalog
    {
        public static ElementType? Type(string id) => Infrastructure.AcadEnv.Catalog.FindType(id);
    }
}
