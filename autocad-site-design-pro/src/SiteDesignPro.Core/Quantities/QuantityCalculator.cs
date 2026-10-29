using System;
using System.Linq;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Quantities
{
    /// <summary>Measured quantity of one element (metres-based units).</summary>
    public readonly record struct ElementQuantity(double Value, string Unit, double? Secondary, string? SecondaryUnit, bool SecondaryApproximate);

    /// <summary>
    /// Computes element quantities from geometry (package frame, metres) and parameters, following the
    /// builder's quantity definition (count, length or area, with optional secondary volume/area/perimeter).
    /// </summary>
    public static class QuantityCalculator
    {
        public static ElementQuantity For(SiteElement e, ElementType type)
        {
            QuantityDefinition q = type.Builder.Quantity;
            ParameterSet p = e.Parameters;
            double N(string key) => p.Contains(key) ? p.Number(key) : type.Parameter(key)?.Default.AsNumber() ?? double.NaN;

            double value;
            switch (q.Measure)
            {
                case "length":
                    value = e.Geometry is PathGeometry path ? GeometryMath.Length(path.Ring, path.Closed)
                        : e.Geometry is PolygonGeometry poly ? GeometryMath.Length(poly.Outer, true) : 0;
                    break;
                case "area":
                    value = e.Geometry is PolygonGeometry pg ? GeometryMath.Area(pg) : 0;
                    break;
                default:
                    value = 1;
                    if (q.CountParam != null)
                    {
                        value = Math.Max(0, Math.Round(N(q.CountParam)));
                    }
                    else if (q.CountExpression != null)
                    {
                        value = q.CountExpression.Split('*').Select(k => N(k.Trim())).Aggregate(1.0, (acc, x) => double.IsFinite(x) ? acc * Math.Round(x) : acc);
                    }

                    break;
            }

            double? secondary = null;
            string? secondaryUnit = null;
            bool approx = false;
            switch (q.Secondary)
            {
                case "volume":
                    secondaryUnit = "m3";
                    if (e.Geometry is PolygonGeometry vp)
                    {
                        double area = GeometryMath.Area(vp);
                        switch (type.Builder.Id)
                        {
                            case "slab":
                                secondary = area * N("thickness");
                                break;
                            case "pool":
                                secondary = area * Math.Max(0, N("depth") - N("waterLevelOffset"));
                                break;
                            case "basin":
                                {
                                    double depth = N("depth");
                                    double inset = N("sideSlope") * depth;
                                    double perimeter = GeometryMath.Length(vp.Outer, true);
                                    double bottom = Math.Max(0, area - (perimeter * inset) + (Math.PI * inset * inset));
                                    secondary = depth / 3.0 * (area + bottom + Math.Sqrt(area * bottom));
                                    approx = true;
                                    break;
                                }
                        }
                    }

                    break;
                case "area":
                    secondaryUnit = "m2";
                    if (e.Geometry is PathGeometry ap)
                    {
                        secondary = GeometryMath.Length(ap.Ring, ap.Closed) * N("width");
                        approx = ap.Ring.Count > 2; // mitred joins make the true strip area differ slightly
                    }
                    else if (e.Geometry is PolygonGeometry aq)
                    {
                        secondary = GeometryMath.Area(aq);
                    }

                    break;
                case "perimeter":
                    secondaryUnit = "m";
                    if (e.Geometry is PolygonGeometry per)
                    {
                        secondary = GeometryMath.Length(per.Outer, true);
                    }

                    break;
            }

            return new ElementQuantity(value, q.Unit, secondary, secondaryUnit, approx);
        }
    }
}
