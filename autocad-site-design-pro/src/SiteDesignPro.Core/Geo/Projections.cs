using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.Core.Geo
{
    public sealed class Ellipsoid
    {
        public Ellipsoid(string name, double a, double inverseFlattening)
        {
            Name = name;
            A = a;
            F = 1.0 / inverseFlattening;
            E2 = F * (2 - F);
        }

        public static readonly Ellipsoid Bessel1841 = new Ellipsoid("Bessel 1841", 6377397.155, 299.1528128);
        public static readonly Ellipsoid Grs80 = new Ellipsoid("GRS 1980", 6378137.0, 298.257222101);
        public static readonly Ellipsoid Wgs84 = new Ellipsoid("WGS 84", 6378137.0, 298.257223563);

        public string Name { get; }

        public double A { get; }

        public double F { get; }

        public double E2 { get; }
    }

    /// <summary>7-parameter Helmert transformation to WGS 84 (position-vector convention; arc-seconds, ppm).</summary>
    public sealed class Helmert
    {
        public double Tx { get; set; }

        public double Ty { get; set; }

        public double Tz { get; set; }

        public double Rx { get; set; }

        public double Ry { get; set; }

        public double Rz { get; set; }

        public double Ppm { get; set; }

        public bool IsIdentity => Tx == 0 && Ty == 0 && Tz == 0 && Rx == 0 && Ry == 0 && Rz == 0 && Ppm == 0;

        public (double X, double Y, double Z) Apply(double x, double y, double z)
        {
            const double sec = Math.PI / (180.0 * 3600.0);
            double rx = Rx * sec, ry = Ry * sec, rz = Rz * sec, m = 1 + (Ppm * 1e-6);
            return (Tx + (m * (x - (rz * y) + (ry * z))),
                    Ty + (m * ((rz * x) + y - (rx * z))),
                    Tz + (m * ((-ry * x) + (rx * y) + z)));
        }
    }

    /// <summary>A projected coordinate system: Transverse Mercator on an ellipsoid plus a datum shift to WGS 84.</summary>
    public sealed class ProjectedCrs
    {
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public Ellipsoid Ellipsoid { get; set; } = Ellipsoid.Grs80;

        public double CentralMeridian { get; set; }

        public double ScaleFactor { get; set; } = 0.9996;

        public double FalseEasting { get; set; } = 500000;

        public double FalseNorthing { get; set; }

        public Helmert ToWgs84 { get; set; } = new Helmert();

        /// <summary>Stated accuracy of the datum shift (metres); 0 = the datum is WGS 84 compatible.</summary>
        public double DatumAccuracyMeters { get; set; }

        public string DatumNote { get; set; } = string.Empty;

        /// <summary>Projected (E, N) → WGS 84 longitude/latitude in degrees.</summary>
        public (double Lon, double Lat) ToWgs84Geographic(double easting, double northing)
        {
            (double lat, double lon) = TransverseMercator.Inverse(Ellipsoid, CentralMeridian, ScaleFactor, FalseEasting, FalseNorthing, easting, northing);
            if (ToWgs84.IsIdentity)
            {
                return (lon, lat);
            }

            (double x, double y, double z) = Geocentric.ToCartesian(Ellipsoid, lat, lon, 0);
            (double x2, double y2, double z2) = ToWgs84.Apply(x, y, z);
            (double lat2, double lon2, _) = Geocentric.FromCartesian(Ellipsoid.Wgs84, x2, y2, z2);
            return (lon2, lat2);
        }

        public (double E, double N) FromGeographicOnOwnDatum(double lon, double lat) =>
            TransverseMercator.Forward(Ellipsoid, CentralMeridian, ScaleFactor, FalseEasting, FalseNorthing, lat, lon);
    }

    /// <summary>Coordinate systems used in Bosnia and Herzegovina and neighbouring countries.</summary>
    public static class CrsRegistry
    {
        private static readonly Helmert Mgi1901ToWgs84 = new Helmert { Tx = 682, Ty = -203, Tz = 480 };
        private const string MgiNote = "MGI 1901 → WGS 84 (EPSG transformacija \"MGI 1901 to WGS 84 (1)\", tačnost oko 5 m). Za geodetsku tačnost koristite lokalne parametre.";

        public static IReadOnlyList<ProjectedCrs> All { get; } = Build();

        private static List<ProjectedCrs> Build()
        {
            var list = new List<ProjectedCrs>();
            foreach ((int zone, string code) in new[] { (5, "EPSG:3907"), (6, "EPSG:3908"), (7, "EPSG:3909") })
            {
                list.Add(new ProjectedCrs
                {
                    Code = code,
                    Name = "MGI 1901 / Balkans zone " + zone + " (Gauss-Krüger, zona " + zone + ")",
                    Ellipsoid = Ellipsoid.Bessel1841,
                    CentralMeridian = zone * 3,
                    ScaleFactor = 0.9999,
                    FalseEasting = (zone * 1000000) + 500000,
                    ToWgs84 = Mgi1901ToWgs84,
                    DatumAccuracyMeters = 5,
                    DatumNote = MgiNote,
                });
            }

            // older EPSG codes of the same projections (datum MGI); same parameters are used here
            foreach ((int zone, string code) in new[] { (5, "EPSG:31275"), (6, "EPSG:31276"), (7, "EPSG:31277") })
            {
                ProjectedCrs b = list.First(c => c.Code == "EPSG:390" + (zone + 2));
                list.Add(new ProjectedCrs
                {
                    Code = code,
                    Name = "MGI / Balkans zone " + zone + " (Gauss-Krüger, zona " + zone + ")",
                    Ellipsoid = b.Ellipsoid,
                    CentralMeridian = b.CentralMeridian,
                    ScaleFactor = b.ScaleFactor,
                    FalseEasting = b.FalseEasting,
                    ToWgs84 = Mgi1901ToWgs84,
                    DatumAccuracyMeters = 5,
                    DatumNote = MgiNote,
                });
            }

            foreach (int zone in new[] { 33, 34 })
            {
                list.Add(new ProjectedCrs
                {
                    Code = "EPSG:258" + zone,
                    Name = "ETRS89 / UTM zone " + zone + "N",
                    Ellipsoid = Ellipsoid.Grs80,
                    CentralMeridian = (zone * 6) - 183,
                    DatumNote = "ETRS89 se za potrebe preklapanja smatra jednakim WGS 84 (razlika manja od 1 m).",
                });
                list.Add(new ProjectedCrs
                {
                    Code = "EPSG:326" + zone,
                    Name = "WGS 84 / UTM zone " + zone + "N",
                    Ellipsoid = Ellipsoid.Wgs84,
                    CentralMeridian = (zone * 6) - 183,
                });
            }

            return list;
        }

        public static ProjectedCrs? Find(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            string c = code!.Trim().ToUpperInvariant();
            if (!c.StartsWith("EPSG:", StringComparison.Ordinal))
            {
                c = "EPSG:" + c;
            }

            return All.FirstOrDefault(x => x.Code == c);
        }

        public static string Supported() => string.Join(", ", All.Select(c => c.Code));
    }

    public static class Geocentric
    {
        public static (double X, double Y, double Z) ToCartesian(Ellipsoid e, double latDeg, double lonDeg, double h)
        {
            double lat = latDeg * Math.PI / 180, lon = lonDeg * Math.PI / 180;
            double n = e.A / Math.Sqrt(1 - (e.E2 * Math.Sin(lat) * Math.Sin(lat)));
            return ((n + h) * Math.Cos(lat) * Math.Cos(lon), (n + h) * Math.Cos(lat) * Math.Sin(lon), ((n * (1 - e.E2)) + h) * Math.Sin(lat));
        }

        public static (double Lat, double Lon, double H) FromCartesian(Ellipsoid e, double x, double y, double z)
        {
            double lon = Math.Atan2(y, x);
            double p = Math.Sqrt((x * x) + (y * y));
            double lat = Math.Atan2(z, p * (1 - e.E2));
            double h = 0;
            for (int i = 0; i < 10; i++)
            {
                double n = e.A / Math.Sqrt(1 - (e.E2 * Math.Sin(lat) * Math.Sin(lat)));
                h = (p / Math.Cos(lat)) - n;
                lat = Math.Atan2(z, p * (1 - (e.E2 * n / (n + h))));
            }

            return (lat * 180 / Math.PI, lon * 180 / Math.PI, h);
        }
    }

    /// <summary>Transverse Mercator (Krüger series to 6th order in n; sub-millimetre within a zone).</summary>
    public static class TransverseMercator
    {
        private static (double[] Alpha, double[] Beta, double Ar) Coefficients(Ellipsoid e)
        {
            double n = e.F / (2 - e.F);
            double n2 = n * n, n3 = n2 * n, n4 = n3 * n, n5 = n4 * n, n6 = n5 * n;
            double ar = e.A / (1 + n) * (1 + (n2 / 4) + (n4 / 64) + (n6 / 256));
            double[] alpha =
            {
                0,
                (n / 2) - (2 * n2 / 3) + (5 * n3 / 16) + (41 * n4 / 180) - (127 * n5 / 288) + (7891 * n6 / 37800),
                (13 * n2 / 48) - (3 * n3 / 5) + (557 * n4 / 1440) + (281 * n5 / 630) - (1983433 * n6 / 1935360),
                (61 * n3 / 240) - (103 * n4 / 140) + (15061 * n5 / 26880) + (167603 * n6 / 181440),
                (49561 * n4 / 161280) - (179 * n5 / 168) + (6601661 * n6 / 7257600),
                (34729 * n5 / 80640) - (3418889 * n6 / 1995840),
                212378941 * n6 / 319334400,
            };
            double[] beta =
            {
                0,
                (n / 2) - (2 * n2 / 3) + (37 * n3 / 96) - (n4 / 360) - (81 * n5 / 512) + (96199 * n6 / 604800),
                (n2 / 48) + (n3 / 15) - (437 * n4 / 1440) + (46 * n5 / 105) - (1118711 * n6 / 3870720),
                (17 * n3 / 480) - (37 * n4 / 840) - (209 * n5 / 4480) + (5569 * n6 / 90720),
                (4397 * n4 / 161280) - (11 * n5 / 504) - (830251 * n6 / 7257600),
                (4583 * n5 / 161280) - (108847 * n6 / 3991680),
                20648693 * n6 / 638668800,
            };
            return (alpha, beta, ar);
        }

        public static (double E, double N) Forward(Ellipsoid ell, double lon0Deg, double k0, double fe, double fn, double latDeg, double lonDeg)
        {
            (double[] alpha, _, double ar) = Coefficients(ell);
            double e = Math.Sqrt(ell.E2);
            double phi = latDeg * Math.PI / 180, lam = (lonDeg - lon0Deg) * Math.PI / 180;
            double tau = Math.Tan(phi);
            double sigma = Math.Sinh(e * Atanh(e * tau / Math.Sqrt(1 + (tau * tau))));
            double taup = (tau * Math.Sqrt(1 + (sigma * sigma))) - (sigma * Math.Sqrt(1 + (tau * tau)));
            double xip = Math.Atan2(taup, Math.Cos(lam));
            double etap = Asinh(Math.Sin(lam) / Math.Sqrt((taup * taup) + (Math.Cos(lam) * Math.Cos(lam))));
            double xi = xip, eta = etap;
            for (int j = 1; j <= 6; j++)
            {
                xi += alpha[j] * Math.Sin(2 * j * xip) * Math.Cosh(2 * j * etap);
                eta += alpha[j] * Math.Cos(2 * j * xip) * Math.Sinh(2 * j * etap);
            }

            return (fe + (k0 * ar * eta), fn + (k0 * ar * xi));
        }

        public static (double Lat, double Lon) Inverse(Ellipsoid ell, double lon0Deg, double k0, double fe, double fn, double easting, double northing)
        {
            (_, double[] beta, double ar) = Coefficients(ell);
            double e = Math.Sqrt(ell.E2);
            double xi = (northing - fn) / (k0 * ar), eta = (easting - fe) / (k0 * ar);
            double xip = xi, etap = eta;
            for (int j = 1; j <= 6; j++)
            {
                xip -= beta[j] * Math.Sin(2 * j * xi) * Math.Cosh(2 * j * eta);
                etap -= beta[j] * Math.Cos(2 * j * xi) * Math.Sinh(2 * j * eta);
            }

            double sinhEtap = Math.Sinh(etap), sinXip = Math.Sin(xip), cosXip = Math.Cos(xip);
            double taup = sinXip / Math.Sqrt((sinhEtap * sinhEtap) + (cosXip * cosXip));
            double lam = Math.Atan2(sinhEtap, cosXip);
            double tau = taup;
            for (int i = 0; i < 12; i++)
            {
                double sigma = Math.Sinh(e * Atanh(e * tau / Math.Sqrt(1 + (tau * tau))));
                double taui = (tau * Math.Sqrt(1 + (sigma * sigma))) - (sigma * Math.Sqrt(1 + (tau * tau)));
                double dtau = (taup - taui) / Math.Sqrt(1 + (taui * taui)) * (1 + ((1 - ell.E2) * tau * tau)) / ((1 - ell.E2) * Math.Sqrt(1 + (tau * tau)));
                tau += dtau;
                if (Math.Abs(dtau) < 1e-14)
                {
                    break;
                }
            }

            return (Math.Atan(tau) * 180 / Math.PI, lon0Deg + (lam * 180 / Math.PI));
        }

        private static double Atanh(double x) => 0.5 * Math.Log((1 + x) / (1 - x));

        private static double Asinh(double x) => Math.Log(x + Math.Sqrt((x * x) + 1));
    }

    /// <summary>Writes a KML overlay (site outlines in WGS 84) for checking the position in Google Earth.</summary>
    public static class KmlWriter
    {
        public static string Write(string documentName, IEnumerable<(string Name, IReadOnlyList<(double Lon, double Lat)> Ring, bool Closed)> features, string note)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document>");
            sb.AppendLine("<name>" + Esc(documentName) + "</name>");
            sb.AppendLine("<description>" + Esc(note) + "</description>");
            sb.AppendLine("<Style id=\"sdp\"><LineStyle><color>ff00a5ff</color><width>2</width></LineStyle><PolyStyle><fill>0</fill></PolyStyle></Style>");
            foreach ((string name, IReadOnlyList<(double Lon, double Lat)> ring, bool closed) in features)
            {
                IEnumerable<(double Lon, double Lat)> pts = closed && ring.Count > 0 ? ring.Concat(new[] { ring[0] }) : ring;
                string coords = string.Join(" ", pts
                    .Select(p => p.Lon.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) + "," + p.Lat.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) + ",0"));
                sb.AppendLine("<Placemark><name>" + Esc(name) + "</name><styleUrl>#sdp</styleUrl><LineString><tessellate>1</tessellate><coordinates>" + coords + "</coordinates></LineString></Placemark>");
            }

            sb.AppendLine("</Document></kml>");
            return sb.ToString();
        }

        private static string Esc(string s) => System.Security.SecurityElement.Escape(s) ?? string.Empty;

        public static string AccuracyNote(ProjectedCrs crs) =>
            Bs.T("Preklop za vizuelnu provjeru položaja. Koordinatni sistem: {0}. {1} Google Earth snimci nemaju geodetsku tačnost.", crs.Name, crs.DatumNote);
    }
}
