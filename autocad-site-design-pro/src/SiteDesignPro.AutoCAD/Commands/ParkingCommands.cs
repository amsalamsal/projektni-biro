using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Layout;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>
    /// PARKING MJESTA U NIZU (ribbon panel "Parking mjesta"): perpendicular, parallel and 45° rows drawn as in the
    /// project standard – numbers, the lines with T heads and hooks, the hatched area and kerb ramp of an accessible
    /// bay (always before bay 1), the hatched triangle and the 45° of an angled row and the arrows in the aisle. No
    /// dimensions. Each bay is a parking bay element (counted, labelled and exported to SketchUp like any other).
    /// Used again, the command asks whether the numbers go on or start from 1.
    /// </summary>
    public sealed class ParkingCommands
    {
        [CommandMethod("SITEDESIGNPRO", "SDPPARKINGOKOMITO", CommandFlags.Modal)]
        public void Perpendicular() => Row(ParkingRows.Perpendicular, "SDPPARKINGOKOMITO");

        [CommandMethod("SITEDESIGNPRO", "SDPPARKINGPARALELNO", CommandFlags.Modal)]
        public void Parallel() => Row(ParkingRows.Parallel, "SDPPARKINGPARALELNO");

        [CommandMethod("SITEDESIGNPRO", "SDPPARKINGUGAO", CommandFlags.Modal)]
        public void Angled() => Row(ParkingRows.Angled, "SDPPARKINGUGAO");

        private static string Title(string kind) => kind == ParkingRows.Parallel ? "paralelno" : kind == ParkingRows.Angled ? "pod uglom 45°" : "okomito";

        private static void Row(string kind, string command) => CommandContext.RunMajor(command, "Parking mjesta u nizu (" + command + ")", ctx =>
        {
            double k = ctx.K;
            (double w, double depth) = ParkingRows.Size(kind);
            ctx.Write("Parking mjesta u nizu – " + Title(kind) + ": " + Bs.Num(w, 2) + " × " + Bs.Num(depth, 2) + " m" +
                      (kind == ParkingRows.Perpendicular ? "; mjesto za osobe s invaliditetom 2,50 m + šrafura 1,40 m i rampa za trotoar, uvijek ispred broja 1." : kind == ParkingRows.Angled ? ", prolaz 3,00 m." : ", prolaz 3,00 m."));
            int count = ctx.Integer("Broj parking mjesta u nizu", 5, 1);
            int accessible = 0;
            if (kind == ParkingRows.Perpendicular)
            {
                accessible = ctx.Integer("Broj mjesta za osobe s invaliditetom u nizu (0 = nema)", 1, 0);
                if (accessible > count)
                {
                    accessible = count;
                    ctx.Write("Mjesta za osobe s invaliditetom: " + accessible + " (koliko ima mjesta u nizu).");
                }
            }

            // numbering: on, or from 1 again
            List<SiteElement> existing = ctx.ExistingElements();
            int last = existing.Where(e => e.TypeId.StartsWith("road.parking_bay", StringComparison.Ordinal) && e.Parameters.Contains("bayNumber"))
                .Select(e => int.TryParse(e.Parameters.Text("bayNumber"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0)
                .DefaultIfEmpty(0).Max();
            int first = 1;
            if (last > 0 && count > accessible)
            {
                string answer = ctx.Keyword("U crtežu su parking mjesta do broja " + last + ". Numeracija [Nastavi/Od1]", new[] { "Next|Nastavi", "One|Od1" }, "Next");
                first = answer == "Next" ? last + 1 : 1;
            }

            Point3d a = ctx.Point("Početak niza na ivici trotoara / granici (strana suprotna prolazu)")!.Value;
            Vec2 M(Point3d p) => new Vec2(p.X / k, p.Y / k);
            double length = ParkingRows.Length(kind, count, accessible);
            Point3d b = ctx.PointPreview("Smjer niza (dužina " + Bs.Num(length, 2) + " m)", a, false, cur =>
            {
                Vec2 dir = M(cur) - M(a);
                if (dir.Length < 0.01)
                {
                    return new List<PreviewShape>();
                }

                Vec2 u = dir.Normalized();
                return new List<PreviewShape> { Shape(k, 7, false, M(a), M(a) + (u * length)) };
            })!.Value;
            if (M(b).DistanceTo(M(a)) < 0.01)
            {
                ctx.Write("Smjer niza nije zadan.");
                return;
            }

            Point3d side = ctx.PointPreview("Strana prolaza (kliknite na strani gdje je kolovoz/prolaz)", b, false, cur => Outline(k, M(a), M(b), M(cur), kind, count, accessible))!.Value;

            List<ParkingRowBay> bays = ParkingRows.Bays(M(a), M(b), M(side), kind, count, accessible, first);
            ElementCatalog cat = ctx.Catalog;
            ElementType normal = cat.RequireType("road.parking_bay");
            ElementType disabled = cat.FindType("road.parking_bay_accessible") ?? normal;
            foreach (ParkingRowBay bay in bays)
            {
                ElementType type = bay.Accessible ? disabled : normal;
                SiteElement e = ctx.NewElement(type, new PointGeometry(new Vec3(bay.Position.X * k, bay.Position.Y * k, a.Z), bay.Rotation), existing);
                ParameterSet p = e.Parameters;
                p["layout"] = ParamValue.Of(kind);
                p["width"] = ParamValue.Of(bay.Width);
                p["length"] = ParamValue.Of(bay.Depth);
                p["lineWidth"] = ParamValue.Of(Core.Symbols.ParkingSymbols.LineWidth);
                p["rowDir"] = ParamValue.Of((double)bay.RowDir);
                p["rowStart"] = ParamValue.Of(bay.First);
                p["rowEnd"] = ParamValue.Of(bay.Last);
                p["accessible"] = ParamValue.Of(bay.Accessible);
                p["hatchWidth"] = ParamValue.Of(bay.Hatch);
                p["bayNumber"] = ParamValue.Of(bay.Number);
                p["markingMaterial"] = ParamValue.Of(bay.Accessible ? "line_paint_yellow" : "line_paint_white");
                e.Name = bay.Accessible ? "Parking mjesto za osobe s invaliditetom" : "Parking mjesto " + bay.Number;
                ctx.Svc.Create(e);
                existing.Add(e);
            }

            // arrows in the aisle (parallel and 45°)
            ElementType? arrowType = cat.FindType("road.arrow");
            List<ParkingArrow> arrows = ParkingRows.Arrows(M(a), M(b), M(side), kind, count);
            double arrowLength = ParkingRows.ArrowSize(kind, count);
            if (arrowType != null)
            {
                foreach (ParkingArrow ar in arrows)
                {
                    SiteElement e = ctx.NewElement(arrowType, new PointGeometry(new Vec3(ar.Position.X * k, ar.Position.Y * k, a.Z), ar.Rotation), existing);
                    e.Parameters["length"] = ParamValue.Of(arrowLength);
                    e.Parameters["width"] = ParamValue.Of(ParkingRows.ArrowWidth);
                    e.Parameters["shaftWidth"] = ParamValue.Of(ParkingRows.ArrowShaft);
                    e.Name = "Strelica smjera na parkingu";
                    ctx.Svc.Create(e);
                    existing.Add(e);
                }
            }

            int numbered = count - accessible;
            ctx.Write("Napravljeno: " + count + " parking mjesta " + Title(kind) +
                      (accessible > 0 ? " (" + accessible + " za osobe s invaliditetom)" : string.Empty) +
                      (numbered > 0 ? ", brojevi " + first + "–" + (first + numbered - 1) : string.Empty) +
                      (arrows.Count > 0 && arrowType != null ? ", strelica u prolazu: " + arrows.Count : string.Empty) +
                      ", dužina niza " + Bs.Num(length, 2) + " m.");
        });

        /// <summary>The outlines of the bays while the aisle side is picked.</summary>
        private static List<PreviewShape> Outline(double k, Vec2 start, Vec2 along, Vec2 side, string kind, int count, int accessible)
        {
            var shapes = new List<PreviewShape>();
            if (along.DistanceTo(start) < 0.01)
            {
                return shapes;
            }

            foreach (ParkingRowBay bay in ParkingRows.Bays(start, along, side, kind, count, accessible, 1))
            {
                Vec2 x = new Vec2(Math.Cos(bay.Rotation), Math.Sin(bay.Rotation)) * bay.RowDir, y = new Vec2(-Math.Sin(bay.Rotation), Math.Cos(bay.Rotation));
                double h = bay.Width / 2, t = kind == ParkingRows.Angled ? bay.Depth : 0;
                Vec2 p0 = bay.Position - (x * h), p1 = bay.Position + (x * h);
                shapes.Add(Shape(k, (short)(bay.Accessible ? 2 : 7), true, p0, p1, p1 + (x * t) + (y * bay.Depth), p0 + (x * t) + (y * bay.Depth)));
            }

            return shapes;
        }

        private static PreviewShape Shape(double k, short color, bool closed, params Vec2[] pts)
        {
            var s = new PreviewShape { Closed = closed, Color = color };
            foreach (Vec2 p in pts)
            {
                s.Points.Add(new Point2d(p.X * k, p.Y * k));
                s.Bulges.Add(0);
            }

            return s;
        }
    }
}
