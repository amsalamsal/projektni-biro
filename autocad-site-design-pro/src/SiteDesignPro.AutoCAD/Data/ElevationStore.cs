using System;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using SiteDesignPro.Core.Elevation;

namespace SiteDesignPro.AutoCAD.Data
{
    /// <summary>
    /// Elevation of a single object. Elements keep their assignment in the element record; other source objects
    /// (polylines, blocks of the original DWG) get XData "SITEDESIGNPRO_KOTA" so the assignment stays traceable.
    /// </summary>
    internal static class ElevationStore
    {
        public static void Write(Entity e, ElevationAssignment a, double relative, Transaction tr)
        {
            ElementStore.EnsureRegApps(e.Database, tr);
            if (!e.IsWriteEnabled)
            {
                e.UpgradeOpen();
            }

            e.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, ElementStore.ElevationApp),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, a.Mode.ToString()),
                new TypedValue((int)DxfCode.ExtendedDataReal, a.Value),
                new TypedValue((int)DxfCode.ExtendedDataReal, relative),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, a.Source));
        }

        public static (ElevationAssignment Assignment, double Relative)? Read(Entity e)
        {
            using (ResultBuffer? rb = e.GetXDataForApplication(ElementStore.ElevationApp))
            {
                if (rb == null)
                {
                    return null;
                }

                TypedValue[] v = rb.AsArray();
                if (v.Length < 5 || !Enum.TryParse((string)v[1].Value, out ElevationMode mode))
                {
                    return null;
                }

                return (new ElevationAssignment { Mode = mode, Value = Convert.ToDouble(v[2].Value, CultureInfo.InvariantCulture), Source = (string)v[4].Value },
                        Convert.ToDouble(v[3].Value, CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Current Z of an object in drawing units (first vertex for curves), or null when it has none.</summary>
        public static double? CurrentZ(Entity e, Transaction tr)
        {
            switch (e)
            {
                case Polyline pl: return pl.NumberOfVertices > 0 ? pl.GetPoint3dAt(0).Z : pl.Elevation;
                case Polyline2d p2: return p2.Elevation;
                case Polyline3d p3:
                    foreach (ObjectId vid in p3)
                    {
                        return ((PolylineVertex3d)tr.GetObject(vid, OpenMode.ForRead)).Position.Z;
                    }

                    return null;
                case BlockReference br: return br.Position.Z;
                case DBPoint p: return p.Position.Z;
                case Line l: return l.StartPoint.Z;
                case Circle c: return c.Center.Z;
                case Arc a: return a.Center.Z;
                case Hatch h: return h.Elevation;
                default: return null;
            }
        }

        /// <summary>Moves an object to a constant elevation (drawing units). Returns false for unsupported objects.</summary>
        public static bool SetZ(Entity e, double z, Transaction tr)
        {
            if (!e.IsWriteEnabled)
            {
                e.UpgradeOpen();
            }

            switch (e)
            {
                case Polyline pl:
                    pl.Elevation = z;
                    return true;
                case Polyline2d p2:
                    p2.Elevation = z;
                    return true;
                case Polyline3d p3:
                    foreach (ObjectId vid in p3)
                    {
                        var v = (PolylineVertex3d)tr.GetObject(vid, OpenMode.ForWrite);
                        v.Position = new Point3d(v.Position.X, v.Position.Y, z);
                    }

                    return true;
                case BlockReference br:
                    br.Position = new Point3d(br.Position.X, br.Position.Y, z);
                    return true;
                case DBPoint p:
                    p.Position = new Point3d(p.Position.X, p.Position.Y, z);
                    return true;
                case Line l:
                    l.StartPoint = new Point3d(l.StartPoint.X, l.StartPoint.Y, z);
                    l.EndPoint = new Point3d(l.EndPoint.X, l.EndPoint.Y, z);
                    return true;
                case Circle c:
                    c.Center = new Point3d(c.Center.X, c.Center.Y, z);
                    return true;
                case Arc a:
                    a.Center = new Point3d(a.Center.X, a.Center.Y, z);
                    return true;
                case Hatch h:
                    h.Elevation = z;
                    return true;
                default:
                    return false;
            }
        }
    }
}
