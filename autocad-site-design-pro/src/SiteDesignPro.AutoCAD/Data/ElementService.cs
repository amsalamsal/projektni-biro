using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using SiteDesignPro.AutoCAD.Drawing;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Symbols;
using AcGeom = Autodesk.AutoCAD.Geometry;

namespace SiteDesignPro.AutoCAD.Data
{
    /// <summary>A managed element found in the drawing.</summary>
    internal sealed class ManagedElement
    {
        public ObjectId ObjectId { get; set; }

        public ElementRecord Record { get; set; } = new ElementRecord();

        /// <summary>Element in the drawing frame (drawing units, WCS), or null when its geometry is unreadable.</summary>
        public SiteElement? Element { get; set; }

        public ElementType? Type { get; set; }

        /// <summary>True when the record was written for another entity (the entity is a copy).</summary>
        public bool IsCopy { get; set; }

        public string? GeometryProblem { get; set; }

        /// <summary>Set when a symbol block was scaled or mirrored in AutoCAD (the 3D model is driven by parameters).</summary>
        public string? SymbolTransformNote { get; set; }
    }

    /// <summary>
    /// Maps between drawing entities and Core elements inside one transaction: reads managed elements,
    /// creates entities for new elements, refreshes their representation after edits, and finds auxiliary
    /// entities (hatches, edges, labels, openings).
    /// </summary>
    internal sealed class ElementService
    {
        public const double NarrowBandWidth = 0.3; // metres: narrower strips are drawn as polyline width, wider as edges

        private readonly Database db;
        private readonly Transaction tr;
        private readonly ElementCatalog catalog;
        private readonly ProjectSettings settings;
        private readonly LayerManager layers;
        private Dictionary<string, List<ObjectId>>? auxIndex;

        public ElementService(Database db, Transaction tr, ElementCatalog catalog, ProjectSettings settings)
        {
            this.db = db;
            this.tr = tr;
            this.catalog = catalog;
            this.settings = settings;
            layers = new LayerManager(db, tr, catalog);
        }

        public LayerManager Layers => layers;

        public ProjectSettings Settings => settings;

        private double K => settings.Scale.DrawingUnitsPerMeter;

        public BlockTableRecord ModelSpace(OpenMode mode = OpenMode.ForRead)
            => (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), mode);

        // ------------------------------------------------------------------ reading

        public List<ManagedElement> ReadAll()
        {
            var result = new List<ManagedElement>();
            BuildAuxIndex();
            foreach (ObjectId id in ModelSpace())
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                if (ElementStore.IsManaged(e))
                {
                    ManagedElement? m = Read(e);
                    if (m != null)
                    {
                        result.Add(m);
                    }
                }
            }

            return result;
        }

        public ManagedElement? Read(Entity e)
        {
            ElementRecord? rec = ElementStore.Read(e, tr);
            if (rec == null)
            {
                return null;
            }

            var m = new ManagedElement { ObjectId = e.ObjectId, Record = rec, Type = catalog.FindType(rec.TypeId) };
            m.IsCopy = !string.IsNullOrEmpty(rec.OwnerHandle) && rec.OwnerHandle != e.Handle.ToString();
            ElementGeometry? geometry = null;
            if (m.Type != null)
            {
                try
                {
                    geometry = ReadGeometry(e, m.Type.Geometry, rec.Id);
                }
                catch (InvalidOperationException ex)
                {
                    m.GeometryProblem = ex.Message;
                }
            }

            if (geometry != null)
            {
                m.Element = new SiteElement(rec.Id, rec.TypeId, geometry)
                {
                    Name = rec.Name,
                    Label = rec.Label,
                    Parameters = rec.Parameters.Clone(),
                    Properties = new SortedDictionary<string, string>(rec.Properties, StringComparer.Ordinal),
                    Custom = rec.Custom.Clone(),
                    Revision = rec.Revision,
                    SourceHandle = e.Handle.ToString(),
                    AssetId = rec.AssetId,
                };
            }

            return m;
        }

        public ManagedElement? FindById(string elementId)
        {
            foreach (ObjectId id in ModelSpace())
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                (string Id, string Type)? key = ElementStore.ReadKey(e);
                if (key.HasValue && key.Value.Id == elementId)
                {
                    ManagedElement? m = Read(e);
                    if (m != null && !m.IsCopy)
                    {
                        return m;
                    }
                }
            }

            return null;
        }

        private void BuildAuxIndex()
        {
            auxIndex = new Dictionary<string, List<ObjectId>>(StringComparer.Ordinal);
            foreach (ObjectId id in ModelSpace())
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                (string OwnerId, string Role)? aux = ElementStore.ReadAux(e);
                if (aux.HasValue)
                {
                    if (!auxIndex.TryGetValue(aux.Value.OwnerId, out List<ObjectId>? list))
                    {
                        list = new List<ObjectId>();
                        auxIndex[aux.Value.OwnerId] = list;
                    }

                    list.Add(id);
                }
            }
        }

        public IEnumerable<(ObjectId Id, string Role)> Aux(string ownerId)
        {
            if (auxIndex == null)
            {
                BuildAuxIndex();
            }

            if (!auxIndex!.TryGetValue(ownerId, out List<ObjectId>? list))
            {
                yield break;
            }

            foreach (ObjectId id in list.Where(i => !i.IsErased))
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                (string OwnerId, string Role)? aux = ElementStore.ReadAux(e);
                if (aux.HasValue && aux.Value.OwnerId == ownerId)
                {
                    yield return (id, aux.Value.Role);
                }
            }
        }

        /// <summary>Reads geometry in drawing units (WCS). Throws InvalidOperationException for unusable geometry.</summary>
        public ElementGeometry ReadGeometry(Entity e, GeometryKind kind, string elementId)
        {
            switch (kind)
            {
                case GeometryKind.Point:
                    if (e is BlockReference br)
                    {
                        if (!br.Normal.IsParallelTo(Vector3d.ZAxis))
                        {
                            throw new InvalidOperationException("Simbol nije u ravni XY (zarotiran izvan tlocrta).");
                        }

                        // direction of the block's X axis in WCS (correct for UCS insertions and mirrored blocks)
                        Vector3d x = br.BlockTransform.CoordinateSystem3d.Xaxis;
                        double rot = Math.Atan2(x.Y, x.X);
                        return new PointGeometry(new Vec3(br.Position.X, br.Position.Y, br.Position.Z), GeometryMath.NormalizeAngle(rot));
                    }

                    if (e is DBPoint pt)
                    {
                        return new PointGeometry(new Vec3(pt.Position.X, pt.Position.Y, pt.Position.Z), 0);
                    }

                    throw new InvalidOperationException("Tačkasti elementi moraju biti reference bloka.");
                case GeometryKind.Path:
                    {
                        (Ring ring, bool closed) = ReadCurve(e);
                        return new PathGeometry(ring, closed);
                    }

                case GeometryKind.Polygon:
                    {
                        (Ring ring, bool closed) = ReadCurve(e);
                        if (!closed)
                        {
                            throw new InvalidOperationException("Granica površine nije zatvorena.");
                        }

                        var holes = new List<Ring>();
                        foreach ((ObjectId id, string role) in Aux(elementId))
                        {
                            if (role == "hole")
                            {
                                (Ring h, bool hc) = ReadCurve((Entity)tr.GetObject(id, OpenMode.ForRead));
                                if (hc)
                                {
                                    holes.Add(h);
                                }
                            }
                        }

                        return new PolygonGeometry(ring, holes);
                    }

                default:
                    throw new InvalidOperationException("Nepodržana vrsta geometrije.");
            }
        }

        public static (Ring Ring, bool Closed) ReadCurve(Entity e)
        {
            switch (e)
            {
                case Polyline pl:
                    {
                        if (!pl.Normal.IsParallelTo(Vector3d.ZAxis))
                        {
                            throw new InvalidOperationException("Polilinija nije u ravni XY (WCS).");
                        }

                        var verts = new List<Vec3>();
                        var bulges = new List<double>();
                        double sign = pl.Normal.Z < 0 ? -1 : 1;
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            Point3d p = pl.GetPoint3dAt(i); // WCS, includes elevation
                            verts.Add(new Vec3(p.X, p.Y, p.Z));
                            bulges.Add(pl.GetBulgeAt(i) * sign);
                        }

                        bool closed = pl.Closed || (verts.Count > 2 && (verts[0] - verts[verts.Count - 1]).Length < 1e-9);
                        return (GeometryMath.RemoveDuplicateVertices(new Ring(verts, bulges), closed), closed);
                    }

                case Polyline3d p3:
                    {
                        var verts = new List<Vec3>();
                        foreach (ObjectId vid in p3)
                        {
                            var v = (PolylineVertex3d)vid.GetObject(OpenMode.ForRead);
                            verts.Add(new Vec3(v.Position.X, v.Position.Y, v.Position.Z));
                        }

                        return (new Ring(verts), p3.Closed);
                    }

                case Line ln:
                    return (new Ring(new[] { new Vec3(ln.StartPoint.X, ln.StartPoint.Y, ln.StartPoint.Z), new Vec3(ln.EndPoint.X, ln.EndPoint.Y, ln.EndPoint.Z) }), false);
                case Arc arc when arc.Normal.IsParallelTo(Vector3d.ZAxis):
                    {
                        double sweep = arc.EndAngle - arc.StartAngle;
                        if (sweep < 0)
                        {
                            sweep += 2 * Math.PI;
                        }

                        double bulge = Math.Tan(sweep / 4) * (arc.Normal.Z < 0 ? -1 : 1);
                        return (new Ring(new[] { new Vec3(arc.StartPoint.X, arc.StartPoint.Y, arc.StartPoint.Z), new Vec3(arc.EndPoint.X, arc.EndPoint.Y, arc.EndPoint.Z) }, new[] { bulge }), false);
                    }

                case Circle c when c.Normal.IsParallelTo(Vector3d.ZAxis):
                    return (new Ring(new[] { new Vec3(c.Center.X - c.Radius, c.Center.Y, c.Center.Z), new Vec3(c.Center.X + c.Radius, c.Center.Y, c.Center.Z) }, new[] { 1.0, 1.0 }), true);
                default:
                    throw new InvalidOperationException("Nepodržana vrsta krive " + e.GetType().Name + "; koristite poliliniju.");
            }
        }

        // ------------------------------------------------------------------ creation

        /// <summary>Creates the drawing representation of a drawing-frame element and stores its data.</summary>
        public ObjectId Create(SiteElement element)
        {
            ElementType type = catalog.RequireType(element.TypeId);
            BlockTableRecord ms = ModelSpace(OpenMode.ForWrite);
            string layer = layers.Ensure(type.LayerKey);
            Entity entity;
            switch (element.Geometry)
            {
                case PointGeometry p:
                    entity = new BlockReference(new Point3d(p.Position.X, p.Position.Y, p.Position.Z), SymbolBlock(type, element.Parameters)) { Rotation = p.Rotation };
                    break;
                case PathGeometry path:
                    entity = CreateCurve(path.Ring, path.Closed);
                    break;
                case PolygonGeometry poly:
                    entity = CreateCurve(poly.Outer, true);
                    break;
                default:
                    throw new InvalidOperationException("Nepodržana geometrija.");
            }

            entity.SetDatabaseDefaults(db);
            entity.Layer = layer;
            ObjectId id = ms.AppendEntity(entity);
            tr.AddNewlyCreatedDBObject(entity, true);
            if (entity is BlockReference)
            {
                // nothing else: the block carries the plan symbol
            }

            ElementStore.Write(entity, ToRecord(element), tr);
            if (element.Geometry is PolygonGeometry pg)
            {
                foreach (Ring hole in pg.Holes)
                {
                    Entity h = CreateCurve(hole, true);
                    h.SetDatabaseDefaults(db);
                    h.Layer = layer;
                    ms.AppendEntity(h);
                    tr.AddNewlyCreatedDBObject(h, true);
                    ElementStore.WriteAux(h, element.Id, "hole", tr);
                    Remember(element.Id, h.ObjectId);
                }
            }

            ApplyRepresentation(entity, element, type);
            return id;
        }

        /// <summary>Lines up the entity's representation with its parameters (symbol, linetype, width, edges, hatch).</summary>
        public void ApplyRepresentation(Entity entity, SiteElement element, ElementType type)
        {
            if (!entity.IsWriteEnabled)
            {
                entity.UpgradeOpen();
            }

            switch (entity)
            {
                case BlockReference br:
                    {
                        ObjectId block = SymbolBlock(type, element.Parameters);
                        if (br.BlockTableRecord != block)
                        {
                            br.BlockTableRecord = block;
                        }

                        break;
                    }

                case Curve curve when type.Geometry == GeometryKind.Path:
                    ApplyPathStyle(curve, element, type);
                    break;
                case Curve curve when type.Geometry == GeometryKind.Polygon:
                    ApplyHatch(curve, element, type);
                    break;
            }
        }

        private void ApplyPathStyle(Curve curve, SiteElement element, ElementType type)
        {
            if (type.Builder.Linetype != null)
            {
                curve.LinetypeId = layers.Linetype(type.Builder.Linetype);
            }

            EraseAux(element.Id, "edge");
            string? widthParam = type.Builder.PolylineWidthParam;
            if (widthParam == null)
            {
                return;
            }

            double width = element.Parameters.Number(widthParam, type.Parameter(widthParam)?.Default.AsNumber() ?? 0);
            if (curve is Polyline pl)
            {
                pl.ConstantWidth = width <= NarrowBandWidth ? width * K : 0;
            }

            if (width <= NarrowBandWidth || !(width > 0))
            {
                return;
            }

            (double left, double right) = type.Builder.Id == "wall"
                ? element.Parameters.Text("alignment", "center") switch
                {
                    "left" => (width, 0.0),
                    "right" => (0.0, width),
                    _ => (width / 2, width / 2),
                }
                : (width / 2, width / 2);
            BlockTableRecord ms = ModelSpace(OpenMode.ForWrite);
            foreach ((double distance, bool toLeft) in new[] { (left, true), (right, false) })
            {
                if (distance < 1e-9)
                {
                    continue;
                }

                foreach (Entity edge in OffsetToSide(curve, distance * K, toLeft))
                {
                    edge.SetDatabaseDefaults(db);
                    edge.Layer = curve.Layer;
                    if (edge is Polyline ep)
                    {
                        ep.ConstantWidth = 0;
                    }

                    ms.AppendEntity(edge);
                    tr.AddNewlyCreatedDBObject(edge, true);
                    ElementStore.WriteAux(edge, element.Id, "edge", tr);
                    Remember(element.Id, edge.ObjectId);
                }
            }
        }

        /// <summary>
        /// Native offset of a curve to the left or right of its direction. The sign convention of
        /// Curve.GetOffsetCurves depends on the curve type, so the result side is verified and the
        /// opposite sign is used when needed.
        /// </summary>
        private IEnumerable<Entity> OffsetToSide(Curve curve, double distance, bool toLeft)
        {
            Vector3d dir = curve.GetFirstDerivative(curve.StartParam);
            foreach (double sign in new[] { 1.0, -1.0 })
            {
                DBObjectCollection offsets;
                try
                {
                    offsets = curve.GetOffsetCurves(sign * distance);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    Log.Warn("Odmak ivice nije uspio: " + ex.Message);
                    return Array.Empty<Entity>();
                }

                List<Entity> result = offsets.Cast<DBObject>().OfType<Entity>().ToList();
                if (result.Count == 0)
                {
                    continue;
                }

                var first = (Curve)result[0];
                Vector3d v = first.StartPoint - curve.StartPoint;
                bool left = (dir.X * v.Y) - (dir.Y * v.X) > 0;
                if (left == toLeft)
                {
                    return result;
                }

                foreach (Entity e in result)
                {
                    e.Dispose();
                }
            }

            return Array.Empty<Entity>();
        }

        private void ApplyHatch(Curve boundary, SiteElement element, ElementType type)
        {
            EraseAux(element.Id, "hatch");
            HatchDefinition? def = type.Hatch;
            if (def == null)
            {
                return;
            }

            BlockTableRecord ms = ModelSpace(OpenMode.ForWrite);
            var hatch = new Hatch();
            hatch.SetDatabaseDefaults(db);
            hatch.Layer = layers.Ensure("surfaces_pattern");
            ms.AppendEntity(hatch);
            tr.AddNewlyCreatedDBObject(hatch, true);
            double scale = def.Scale * K * (db.Measurement == MeasurementValue.Metric ? 1.0 : 1.0 / 25.4);
            hatch.PatternScale = Math.Max(1e-6, scale);
            hatch.PatternAngle = def.Angle * Math.PI / 180;
            try
            {
                hatch.SetHatchPattern(HatchPatternType.PreDefined, def.Pattern);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                Log.Warn("Šrafura " + def.Pattern + " nije dostupna; koristi se ANSI31.");
                hatch.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
            }

            hatch.Associative = true;
            hatch.AppendLoop(HatchLoopTypes.Outermost, new ObjectIdCollection(new[] { boundary.ObjectId }));
            foreach ((ObjectId id, string role) in Aux(element.Id).ToList())
            {
                if (role == "hole")
                {
                    hatch.AppendLoop(HatchLoopTypes.Default, new ObjectIdCollection(new[] { id }));
                }
            }

            hatch.EvaluateHatch(true);
            ElementStore.WriteAux(hatch, element.Id, "hatch", tr);
            Remember(element.Id, hatch.ObjectId);
            hatch.DowngradeOpen();
            hatch.Draworder(tr, boundary.ObjectId);
        }

        public void EraseAux(string ownerId, string role)
        {
            foreach ((ObjectId id, string r) in Aux(ownerId).ToList())
            {
                if (r == role)
                {
                    tr.GetObject(id, OpenMode.ForWrite).Erase();
                }
            }
        }

        private void Remember(string ownerId, ObjectId id)
        {
            if (auxIndex == null)
            {
                return;
            }

            if (!auxIndex.TryGetValue(ownerId, out List<ObjectId>? list))
            {
                list = new List<ObjectId>();
                auxIndex[ownerId] = list;
            }

            list.Add(id);
        }

        private Entity CreateCurve(Ring ring, bool closed)
        {
            bool flat = ring.Vertices.All(v => Math.Abs(v.Z - ring.Vertices[0].Z) < 1e-9);
            if (flat || closed)
            {
                var pl = new Polyline();
                for (int i = 0; i < ring.Count; i++)
                {
                    pl.AddVertexAt(i, new Point2d(ring.Vertices[i].X, ring.Vertices[i].Y), ring.BulgeAt(i), 0, 0);
                }

                pl.Closed = closed;
                pl.Elevation = ring.Vertices[0].Z;
                return pl;
            }

            // varying elevation: 3D polyline (arcs are approximated by short segments)
            List<Vec3> pts = GeometryMath.Tessellate(ring, false, 0.02 * K);
            var collection = new Point3dCollection(pts.Select(v => new Point3d(v.X, v.Y, v.Z)).ToArray());
            return new Polyline3d(Poly3dType.SimplePoly, collection, false);
        }

        public ObjectId SymbolBlockFor(ElementType type, ParameterSet parameters) => SymbolBlock(type, parameters);

        private ObjectId SymbolBlock(ElementType type, ParameterSet parameters)
        {
            SymbolDrawing drawing = SymbolLibrary.Build(type.Builder.Id, parameters, k => type.Parameter(k)?.Default.AsNumber() ?? double.NaN);
            return SymbolBlocks.Ensure(db, tr, drawing, settings.Scale);
        }

        public ElementRecord ToRecord(SiteElement e) => new ElementRecord
        {
            Id = e.Id,
            TypeId = e.TypeId,
            Name = e.Name,
            Label = e.Label,
            Revision = e.Revision,
            Parameters = e.Parameters.Clone(),
            Properties = new SortedDictionary<string, string>(e.Properties, StringComparer.Ordinal),
            Custom = e.Custom.Clone(),
            AssetId = e.AssetId,
        };

        // ------------------------------------------------------------------ updates

        /// <summary>Writes element data and geometry back to an existing entity (parameters, label, point placement).</summary>
        public void Update(ManagedElement m, SiteElement element)
        {
            var entity = (Entity)tr.GetObject(m.ObjectId, OpenMode.ForWrite);
            ElementType type = catalog.RequireType(element.TypeId);
            if (element.Geometry is PointGeometry p && entity is BlockReference br)
            {
                br.Position = new Point3d(p.Position.X, p.Position.Y, p.Position.Z);
                br.Rotation = p.Rotation;
            }

            ElementRecord rec = ToRecord(element);
            rec.LastExportHash = m.Record.LastExportHash;
            ElementStore.Write(entity, rec, tr);
            ApplyRepresentation(entity, element, type);
            UpdateLabels(element);
        }

        /// <summary>Updates the text of label leaders attached to an element.</summary>
        public void UpdateLabels(SiteElement element)
        {
            foreach ((ObjectId id, string role) in Aux(element.Id).ToList())
            {
                if (role != "label")
                {
                    continue;
                }

                var e = tr.GetObject(id, OpenMode.ForWrite);
                string text = Annotations.LabelText(element, catalog);
                if (e is MLeader ml && ml.ContentType == ContentType.MTextContent)
                {
                    MText mt = ml.MText;
                    mt.Contents = text;
                    ml.MText = mt;
                }
                else if (e is DBText t)
                {
                    t.TextString = text;
                }
            }
        }
    }

    internal static class DrawOrderExtensions
    {
        /// <summary>Moves a hatch below its boundary so the outline stays visible.</summary>
        public static void Draworder(this Hatch hatch, Transaction tr, ObjectId boundary)
        {
            var owner = (BlockTableRecord)tr.GetObject(hatch.OwnerId, OpenMode.ForRead);
            var dot = (DrawOrderTable)tr.GetObject(owner.DrawOrderTableId, OpenMode.ForWrite);
            dot.MoveBelow(new ObjectIdCollection(new[] { hatch.ObjectId }), boundary);
        }
    }
}
