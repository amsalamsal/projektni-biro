using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.Core.Geo
{
    /// <summary>A point known both in drawing coordinates (metres) and in the target coordinate system.</summary>
    public sealed class ControlPoint
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Drawing coordinates in metres.</summary>
        public double LocalX { get; set; }

        public double LocalY { get; set; }

        /// <summary>Coordinates in the project coordinate system (easting / northing).</summary>
        public double Easting { get; set; }

        public double Northing { get; set; }

        public bool Enabled { get; set; } = true;
    }

    public enum TransformKind
    {
        /// <summary>No transformation: drawing coordinates already are project coordinates.</summary>
        Identity,

        /// <summary>Translation from one control point with a user-supplied rotation (scale 1).</summary>
        TranslationWithGivenRotation,

        /// <summary>Rigid fit (rotation + translation, scale fixed at 1) from at least two points.</summary>
        Rigid,

        /// <summary>Similarity fit (scale, rotation, translation) from at least two points.</summary>
        Similarity,
    }

    /// <summary>2D transformation local (metres) → project coordinates: E = tx + s(cosθ·x − sinθ·y), N = ty + s(sinθ·x + cosθ·y).</summary>
    public sealed class PlanTransform
    {
        public TransformKind Kind { get; set; } = TransformKind.Identity;

        public double Tx { get; set; }

        public double Ty { get; set; }

        /// <summary>Rotation in degrees, counter-clockwise.</summary>
        public double RotationDegrees { get; set; }

        public double Scale { get; set; } = 1.0;

        public (double E, double N) Apply(double x, double y)
        {
            double t = RotationDegrees * Math.PI / 180, c = Math.Cos(t) * Scale, s = Math.Sin(t) * Scale;
            return (Tx + (c * x) - (s * y), Ty + (s * x) + (c * y));
        }

        public (double X, double Y) Inverse(double e, double n)
        {
            double t = RotationDegrees * Math.PI / 180, c = Math.Cos(t) / Scale, s = Math.Sin(t) / Scale;
            double dx = e - Tx, dy = n - Ty;
            return ((c * dx) + (s * dy), (-s * dx) + (c * dy));
        }
    }

    public sealed class Residual
    {
        public string Name { get; set; } = string.Empty;

        public double DE { get; set; }

        public double DN { get; set; }

        public double Distance => Math.Sqrt((DE * DE) + (DN * DN));
    }

    public sealed class GeoreferenceResult
    {
        public PlanTransform Transform { get; set; } = new PlanTransform();

        public List<Residual> Residuals { get; set; } = new List<Residual>();

        public double RmsError { get; set; }

        /// <summary>Bosnian warnings: insufficient points, scale deviation, large residuals.</summary>
        public List<string> Warnings { get; set; } = new List<string>();

        public bool Usable { get; set; }
    }

    /// <summary>Record of a transformation applied to the project (kept in the project settings).</summary>
    public sealed class TransformRecord
    {
        public string Utc { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public PlanTransform Transform { get; set; } = new PlanTransform();

        public double RmsError { get; set; }

        public int ControlPointCount { get; set; }
    }

    /// <summary>Project georeference: coordinate system, control points, fitted transform and history.</summary>
    public sealed class GeoreferenceSettings
    {
        /// <summary>EPSG code of the project coordinate system, e.g. "EPSG:3908"; empty = unknown.</summary>
        public string Crs { get; set; } = string.Empty;

        public List<ControlPoint> ControlPoints { get; set; } = new List<ControlPoint>();

        /// <summary>Transform from drawing coordinates (m) to project coordinates; Identity when the drawing is already in the CRS.</summary>
        public PlanTransform Transform { get; set; } = new PlanTransform();

        /// <summary>Allowed scale deviation before a unit problem is suspected (parts per million).</summary>
        public double ScaleTolerancePpm { get; set; } = 1000;

        public List<TransformRecord> History { get; set; } = new List<TransformRecord>();
    }

    public static class GeoreferenceSolver
    {
        /// <summary>
        /// Fits a transformation. One point fixes only the position: rotation must be given (<paramref name="givenRotationDegrees"/>)
        /// and scale is 1. Two or more points determine rotation (and scale when <paramref name="fitScale"/>), with residuals.
        /// </summary>
        public static GeoreferenceResult Solve(IReadOnlyList<ControlPoint> all, bool fitScale, double? givenRotationDegrees, double scaleTolerancePpm = 1000)
        {
            var r = new GeoreferenceResult();
            List<ControlPoint> pts = all.Where(p => p.Enabled).ToList();
            if (pts.Count == 0)
            {
                r.Warnings.Add(Bs.T("Nema kontrolnih tačaka. Unesite najmanje jednu tačku s poznatim koordinatama (npr. ugao objekta)."));
                return r;
            }

            if (pts.Count == 1)
            {
                if (!givenRotationDegrees.HasValue)
                {
                    r.Warnings.Add(Bs.T("Jedna kontrolna tačka određuje samo položaj. Rotacija nije poznata: unesite drugu kontrolnu tačku ili poznatu rotaciju."));
                    return r;
                }

                ControlPoint p = pts[0];
                var t = new PlanTransform { Kind = TransformKind.TranslationWithGivenRotation, RotationDegrees = givenRotationDegrees.Value, Scale = 1 };
                (double e0, double n0) = t.Apply(p.LocalX, p.LocalY);
                t.Tx = p.Easting - e0;
                t.Ty = p.Northing - n0;
                r.Transform = t;
                r.Usable = true;
                r.Warnings.Add(Bs.T("Transformacija iz jedne tačke: rotacija je zadata ručno, mjerilo nije provjereno. Preporučuju se najmanje dvije, a za kontrolu tri tačke."));
                r.Residuals.Add(new Residual { Name = p.Name });
                return r;
            }

            // least-squares similarity (Helmert 2D) on centred coordinates
            double cx = pts.Average(p => p.LocalX), cy = pts.Average(p => p.LocalY);
            double ce = pts.Average(p => p.Easting), cn = pts.Average(p => p.Northing);
            double sxx = 0, a = 0, b = 0;
            foreach (ControlPoint p in pts)
            {
                double x = p.LocalX - cx, y = p.LocalY - cy, e = p.Easting - ce, n = p.Northing - cn;
                sxx += (x * x) + (y * y);
                a += (x * e) + (y * n);
                b += (x * n) - (y * e);
            }

            if (sxx < 1e-6)
            {
                r.Warnings.Add(Bs.T("Kontrolne tačke se poklapaju u crtežu; rotacija se ne može odrediti."));
                return r;
            }

            double theta = Math.Atan2(b, a);
            double scale = fitScale ? Math.Sqrt((a * a) + (b * b)) / sxx : 1.0;
            var tr = new PlanTransform { Kind = fitScale ? TransformKind.Similarity : TransformKind.Rigid, RotationDegrees = theta * 180 / Math.PI, Scale = scale };
            (double ec, double nc) = tr.Apply(cx, cy);
            tr.Tx = ce - ec;
            tr.Ty = cn - nc;
            r.Transform = tr;

            double sum = 0;
            foreach (ControlPoint p in pts)
            {
                (double e, double n) = tr.Apply(p.LocalX, p.LocalY);
                var res = new Residual { Name = p.Name, DE = p.Easting - e, DN = p.Northing - n };
                r.Residuals.Add(res);
                sum += res.Distance * res.Distance;
            }

            r.RmsError = Math.Sqrt(sum / pts.Count);
            r.Usable = true;

            double fittedScale = fitScale ? scale : Math.Sqrt((a * a) + (b * b)) / sxx;
            double ppm = (fittedScale - 1) * 1e6;
            if (Math.Abs(ppm) > scaleTolerancePpm)
            {
                r.Warnings.Add(Bs.T("Mjerilo između crteža i koordinata iznosi {0} (odstupanje {1} ppm). Provjerite jedinice crteža prije primjene.",
                    Bs.Num(fittedScale, 6), Bs.Num(ppm, 0)));
            }

            if (pts.Count == 2)
            {
                r.Warnings.Add(Bs.T("Dvije tačke nemaju kontrolu (ostaci su nula). Dodajte treću tačku za provjeru."));
            }

            Residual? worst = r.Residuals.OrderByDescending(x => x.Distance).FirstOrDefault();
            if (pts.Count > 2 && worst != null && worst.Distance > Math.Max(0.05, 3 * r.RmsError) && worst.Distance > 0.05)
            {
                r.Warnings.Add(Bs.T("Tačka \"{0}\" odstupa {1}; provjerite unesene koordinate.", worst.Name, Bs.Meters(worst.Distance, 3)));
            }

            return r;
        }

        /// <summary>Heuristic: do drawing coordinates look like a projected CRS of the region already?</summary>
        public static string? DescribeCoordinateMagnitude(Box2 boundsMeters)
        {
            double x = (boundsMeters.MinX + boundsMeters.MaxX) / 2, y = (boundsMeters.MinY + boundsMeters.MaxY) / 2;
            if (x > 5300000 && x < 7700000 && y > 4500000 && y < 5200000)
            {
                return Bs.T("Koordinate odgovaraju Gauss-Krüger zonama 5-7 (MGI / Balkans). Provjerite zonu u postavkama.");
            }

            if (x > 160000 && x < 840000 && y > 4500000 && y < 5200000)
            {
                return Bs.T("Koordinate odgovaraju UTM projekciji (npr. ETRS89 / UTM 34N). Provjerite zonu u postavkama.");
            }

            if (Math.Abs(x) < 100000 && Math.Abs(y) < 100000)
            {
                return Bs.T("Koordinate su lokalne (blizu ishodišta). Za položaj u prostoru potrebne su kontrolne tačke.");
            }

            return null;
        }
    }
}
