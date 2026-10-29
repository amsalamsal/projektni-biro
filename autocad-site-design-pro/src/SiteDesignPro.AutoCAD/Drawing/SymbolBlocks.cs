using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Symbols;
using SiteDesignPro.Core.Units;

namespace SiteDesignPro.AutoCAD.Drawing
{
    /// <summary>
    /// Turns Core symbol drawings (metres) into block definitions in drawing units. Blocks are named by the
    /// symbol hash, so equal parameters share one definition. Block contents are on layer 0 with ByBlock colour
    /// and linetype, so references display with their element layer's properties.
    /// </summary>
    internal static class SymbolBlocks
    {
        public static ObjectId Ensure(Database db, Transaction tr, SymbolDrawing symbol, UnitScale scale)
        {
            string name = symbol.BlockName(UnitConversion.Code(scale.DrawingUnit));
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (bt.Has(name))
            {
                return bt[name];
            }

            bt.UpgradeOpen();
            var btr = new BlockTableRecord { Name = name, Origin = Point3d.Origin, Comments = "Site Design Pro symbol (" + symbol.Symbol + ")" };
            ObjectId id = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);
            double k = scale.DrawingUnitsPerMeter;
            foreach (SymbolPrimitive p in symbol.Items)
            {
                foreach (Entity e in Create(p, k))
                {
                    e.Layer = "0";
                    e.ColorIndex = 0; // ByBlock
                    e.Linetype = "BYBLOCK";
                    e.LineWeight = LineWeight.ByBlock;
                    btr.AppendEntity(e);
                    tr.AddNewlyCreatedDBObject(e, true);
                    if (e is Hatch h)
                    {
                        FillHatch(h, (SymFill)p, k);
                    }
                    else if (e is DBText text)
                    {
                        text.AdjustAlignment(db);
                    }
                }
            }

            return id;
        }

        private static Point2d P(Vec2 v, double k) => new Point2d(v.X * k, v.Y * k);

        private static Point3d P3(Vec2 v, double k) => new Point3d(v.X * k, v.Y * k, 0);

        private static IEnumerable<Entity> Create(SymbolPrimitive p, double k)
        {
            switch (p)
            {
                case SymLine l:
                    yield return new Line(P3(l.A, k), P3(l.B, k));
                    break;
                case SymCircle c:
                    yield return new Circle(P3(c.Center, k), Vector3d.ZAxis, c.Radius * k);
                    break;
                case SymArc a:
                    yield return new Arc(P3(a.Center, k), a.Radius * k, a.StartAngle, a.EndAngle);
                    break;
                case SymPolyline pl:
                    {
                        var poly = new Polyline();
                        for (int i = 0; i < pl.Points.Count; i++)
                        {
                            double bulge = pl.Bulges != null && i < pl.Bulges.Count ? pl.Bulges[i] : 0;
                            poly.AddVertexAt(i, P(pl.Points[i], k), bulge, pl.Width * k, pl.Width * k);
                        }

                        poly.Closed = pl.Closed;
                        yield return poly;
                        break;
                    }

                case SymFill:
                    yield return new Hatch();
                    break;
                case SymText t:
                    yield return new DBText
                    {
                        TextString = t.Text,
                        Height = t.Height * k,
                        HorizontalMode = TextHorizontalMode.TextCenter,
                        VerticalMode = TextVerticalMode.TextVerticalMid,
                        AlignmentPoint = P3(t.Position, k),
                        Position = P3(t.Position, k),
                    };
                    break;
            }
        }

        private static void FillHatch(Hatch h, SymFill fill, double k)
        {
            h.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
            var pts = new Point2dCollection(fill.Points.Select(v => P(v, k)).Append(P(fill.Points[0], k)).ToArray());
            var bulges = new DoubleCollection(Enumerable.Repeat(0.0, pts.Count).ToArray());
            h.AppendLoop(HatchLoopTypes.External, pts, bulges);
            h.EvaluateHatch(true);
        }
    }
}
