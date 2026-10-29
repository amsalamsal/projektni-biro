using System;
using System.IO;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Tests
{
    internal static class TestSupport
    {
        private static ElementCatalog? catalog;

        public static ElementCatalog Catalog => catalog ??= ElementCatalog.LoadEmbedded();

        public static string SharedDir => Path.Combine(AppContext.BaseDirectory, "shared");

        public static string TempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sdp-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static SiteElement Element(string typeId, ElementGeometry g, Action<ParameterSet>? configure = null, string? label = null)
        {
            ElementType t = Catalog.RequireType(typeId);
            var e = new SiteElement(SiteElement.NewId(), typeId, g) { Name = t.Name, Parameters = t.CreateDefaultParameters(), Label = label };
            configure?.Invoke(e.Parameters);
            return e;
        }

        public static PointGeometry Pt(double x, double y, double z = 0, double rotDeg = 0) => new PointGeometry(new Vec3(x, y, z), rotDeg * Math.PI / 180);

        public static PolygonGeometry Rect(double x0, double y0, double x1, double y1, double z = 0)
            => new PolygonGeometry(new Ring(new[] { new Vec3(x0, y0, z), new Vec3(x1, y0, z), new Vec3(x1, y1, z), new Vec3(x0, y1, z) }));

        public static PathGeometry Line(params double[] xy)
        {
            var pts = new System.Collections.Generic.List<Vec3>();
            for (int i = 0; i + 1 < xy.Length; i += 2)
            {
                pts.Add(new Vec3(xy[i], xy[i + 1], 0));
            }

            return new PathGeometry(new Ring(pts), false);
        }
    }
}
