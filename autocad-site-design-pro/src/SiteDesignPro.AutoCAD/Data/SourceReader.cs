using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Input;

namespace SiteDesignPro.AutoCAD.Data
{
    /// <summary>
    /// Reads model-space objects into host-neutral <see cref="SourceObject"/>s (drawing units, WCS) for input
    /// validation, elevation assignment and terrain building. Reading never modifies the drawing.
    /// </summary>
    internal static class SourceReader
    {
        /// <summary>All model-space objects, optionally restricted to layers accepted by <paramref name="layerFilter"/>.</summary>
        public static List<SourceObject> Read(Database db, Transaction tr, Func<string, bool>? layerFilter = null, bool skipManaged = true)
        {
            var result = new List<SourceObject>();
            var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is Entity e) || (layerFilter != null && !layerFilter(e.Layer)))
                {
                    continue;
                }

                if (skipManaged && (ElementStore.IsManaged(e) || HasAux(e)))
                {
                    continue; // objects created by this extension are not source data
                }

                SourceObject? o = Convert(e, tr);
                if (o != null)
                {
                    result.Add(o);
                }
            }

            return result;
        }

        /// <summary>Layer names in use in model space with object counts (for the mapping dialog).</summary>
        public static SortedDictionary<string, int> LayerUsage(Database db, Transaction tr)
        {
            var usage = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (ObjectId lid in lt)
            {
                var l = (LayerTableRecord)tr.GetObject(lid, OpenMode.ForRead);
                usage[l.Name] = 0;
            }

            var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is Entity e)
                {
                    usage[e.Layer] = usage.TryGetValue(e.Layer, out int n) ? n + 1 : 1;
                }
            }

            return usage;
        }

        public static SourceObject? Convert(Entity e, Transaction tr)
        {
            var o = new SourceObject { Handle = e.Handle.ToString(), Layer = e.Layer };
            switch (e)
            {
                case Polyline pl:
                    {
                        o.Kind = SourceKind.Polyline;
                        o.NonPlanar = !pl.Normal.IsParallelTo(Vector3d.ZAxis);
                        double sign = pl.Normal.Z < 0 ? -1 : 1;
                        o.Bulges = new List<double>();
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            o.Vertices.Add(V(pl.GetPoint3dAt(i)));
                            o.Bulges.Add(pl.GetBulgeAt(i) * sign);
                        }

                        o.Closed = pl.Closed || (o.Vertices.Count > 2 && (o.Vertices[0] - o.Vertices[o.Vertices.Count - 1]).Length < 1e-9);
                        return o;
                    }

                case Polyline3d p3:
                    o.Kind = SourceKind.Polyline;
                    foreach (ObjectId vid in p3)
                    {
                        var v = (PolylineVertex3d)tr.GetObject(vid, OpenMode.ForRead);
                        o.Vertices.Add(V(v.Position));
                    }

                    o.Closed = p3.Closed;
                    return o;
                case Polyline2d p2:
                    o.Kind = SourceKind.Polyline;
                    o.NonPlanar = !p2.Normal.IsParallelTo(Vector3d.ZAxis);
                    o.Bulges = new List<double>();
                    foreach (ObjectId vid in p2)
                    {
                        var v = (Vertex2d)tr.GetObject(vid, OpenMode.ForRead);
                        Point3d wcs = p2.VertexPosition(v);
                        o.Vertices.Add(V(wcs));
                        o.Bulges.Add(v.Bulge * (p2.Normal.Z < 0 ? -1 : 1));
                    }

                    o.Closed = p2.Closed;
                    return o;
                case Line ln:
                    o.Kind = SourceKind.Line;
                    o.Vertices.Add(V(ln.StartPoint));
                    o.Vertices.Add(V(ln.EndPoint));
                    return o;
                case Arc arc:
                    {
                        o.Kind = SourceKind.Arc;
                        o.NonPlanar = !arc.Normal.IsParallelTo(Vector3d.ZAxis);
                        double sweep = arc.EndAngle - arc.StartAngle;
                        if (sweep < 0)
                        {
                            sweep += 2 * Math.PI;
                        }

                        o.Vertices.Add(V(arc.StartPoint));
                        o.Vertices.Add(V(arc.EndPoint));
                        o.Bulges = new List<double> { Math.Tan(sweep / 4) * (arc.Normal.Z < 0 ? -1 : 1), 0 };
                        return o;
                    }

                case Circle c:
                    {
                        o.Kind = SourceKind.Circle;
                        o.NonPlanar = !c.Normal.IsParallelTo(Vector3d.ZAxis);
                        o.Closed = true;
                        o.Vertices.Add(new Vec3(c.Center.X + c.Radius, c.Center.Y, c.Center.Z));
                        o.Vertices.Add(new Vec3(c.Center.X - c.Radius, c.Center.Y, c.Center.Z));
                        o.Bulges = new List<double> { 1, 1 };
                        return o;
                    }

                case DBText t:
                    o.Kind = SourceKind.Text;
                    o.Text = t.TextString;
                    o.Vertices.Add(V(t.HorizontalMode == TextHorizontalMode.TextLeft && t.VerticalMode == TextVerticalMode.TextBase ? t.Position : t.AlignmentPoint));
                    return o;
                case MText mt:
                    o.Kind = SourceKind.Text;
                    o.Text = mt.Contents;
                    o.Vertices.Add(V(mt.Location));
                    return o;
                case MLeader ml when ml.ContentType == ContentType.MTextContent && ml.MText != null:
                    o.Kind = SourceKind.Text;
                    o.Text = ml.MText.Contents;
                    o.Vertices.Add(V(ml.MText.Location));
                    try
                    {
                        // first leader line, first vertex = arrow head (points at the object)
                        System.Collections.ArrayList leaders = ml.GetLeaderIndexes();
                        if (leaders.Count > 0)
                        {
                            System.Collections.ArrayList lines = ml.GetLeaderLineIndexes((int)leaders[0]!);
                            if (lines.Count > 0)
                            {
                                o.Target = V(ml.GetFirstVertex((int)lines[0]!));
                            }
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception)
                    {
                        // leader geometry unavailable: the text position is used
                    }

                    return o;
                case DBPoint p:
                    o.Kind = SourceKind.Point;
                    o.Vertices.Add(V(p.Position));
                    return o;
                case BlockReference br:
                    o.Kind = SourceKind.Block;
                    o.Vertices.Add(V(br.Position));
                    o.BlockName = BlockName(br, tr);
                    o.Text = string.Join(" ", br.AttributeCollection.Cast<ObjectId>()
                        .Select(aid => tr.GetObject(aid, OpenMode.ForRead) as AttributeReference)
                        .Where(a => a != null && !a.Invisible)
                        .Select(a => a!.TextString));
                    return o;
                default:
                    o.Kind = SourceKind.Other;
                    return o;
            }
        }

        private static bool HasAux(Entity e)
        {
            using (ResultBuffer? rb = e.GetXDataForApplication(ElementStore.AuxApp))
            {
                return rb != null;
            }
        }

        private static string BlockName(BlockReference br, Transaction tr)
        {
            ObjectId btrId = br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord;
            return ((BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead)).Name;
        }

        private static Vec3 V(Point3d p) => new Vec3(p.X, p.Y, p.Z);
    }
}
