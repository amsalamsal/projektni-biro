using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Roads;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.Core.Validation
{
    /// <summary>A design that breaks a technical minimum: the element is not drawn and the command says why.</summary>
    public sealed class DesignRuleException : InvalidOperationException
    {
        public DesignRuleException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// The technical minimums of every kind of ramp. A ramp below them cannot be designed: the tools refuse it (in
    /// AutoCAD and in SketchUp) and the validator reports one that is already in a drawing as an error.
    /// Garage ramps: see <see cref="GarageRamp.Violations"/>. Pedestrian ramps (level.ramp): slope at most 8,33 %
    /// (1 : 12) and clear width at least 1,20 m.
    /// </summary>
    public static class RampMinimums
    {
        public const double MaxPedestrianSlope = 1.0 / 12.0;

        public const double MinPedestrianWidth = 1.20;

        public const string PedestrianRampBuilder = "ramp";

        /// <summary>True for the element types the minimums apply to.</summary>
        public static bool Applies(ElementType? type) => type != null && (type.Id == GarageRamp.Type || type.Builder.Id == PedestrianRampBuilder);

        /// <summary>The broken minimums of an element (empty: none). Geometry in drawing units (<paramref name="unitsPerMeter"/>).</summary>
        public static List<string> Check(SiteElement e, ElementCatalog catalog, double unitsPerMeter = 1.0)
        {
            ElementType? type = catalog.FindType(e.TypeId);
            if (!Applies(type))
            {
                return new List<string>();
            }

            if (type!.Id == GarageRamp.Type)
            {
                GarageRamp? ramp = GarageRamp.From(e, unitsPerMeter);
                return ramp == null || ramp.Length < 1e-6 ? new List<string>() : ramp.Violations();
            }

            var list = new List<string>();
            if (e.Geometry is PolygonGeometry pg && pg.Outer.Vertices.Count >= 3)
            {
                double k = unitsPerMeter > 0 ? unitsPerMeter : 1;
                OrientedRect r = GeometryMath.OrientedBoundingRect(pg.Outer);
                double length = r.LengthV / k, width = r.LengthU / k;
                double rise = e.Parameters.Contains("rise") ? e.Parameters.Number("rise") : type.Parameter("rise")?.Default.AsNumber() ?? 0;
                double slope = length > 1e-9 ? Math.Abs(rise) / length : double.PositiveInfinity;
                if (slope > MaxPedestrianSlope + 1e-9)
                {
                    double need = Math.Abs(rise) / MaxPedestrianSlope;
                    list.Add("nagib " + Bs.Num(slope * 100, 2) + " % je veći od dozvoljenih 8,33 % (1 : 12); za visinu " + Bs.Num(Math.Abs(rise), 2) + " m rampa mora biti duga najmanje " + Bs.Num(need, 2) + " m");
                }

                if (width < MinPedestrianWidth - 1e-6)
                {
                    list.Add("svijetla širina " + Bs.Num(width, 2) + " m je manja od najmanjih " + Bs.Num(MinPedestrianWidth, 2) + " m");
                }
            }

            return list;
        }

        /// <summary>Throws a <see cref="DesignRuleException"/> naming the element and every broken minimum.</summary>
        public static void Require(SiteElement e, ElementCatalog catalog, double unitsPerMeter = 1.0)
        {
            List<string> broken = Check(e, catalog, unitsPerMeter);
            if (broken.Count > 0)
            {
                throw new DesignRuleException(Message(e, catalog, broken));
            }
        }

        public static string Message(SiteElement e, ElementCatalog catalog, IEnumerable<string> broken)
        {
            string name = catalog.FindType(e.TypeId)?.Name ?? e.TypeId;
            string label = string.IsNullOrEmpty(e.Label) ? string.Empty : " " + e.Label;
            return "Rampa ispod tehničkog minimuma – " + name + label + " nije nacrtana/izmijenjena: " + string.Join("; ", broken) + ".";
        }
    }
}
