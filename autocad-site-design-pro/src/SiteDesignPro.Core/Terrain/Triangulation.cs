using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Geometry;

namespace SiteDesignPro.Core.Terrain
{
    /// <summary>
    /// Constrained Delaunay triangulation in the plan (XY), with Z carried per vertex.
    /// Incremental insertion with Lawson flips; constraint edges (breaklines, contour segments, boundary) are
    /// recovered by edge flips (Sloan 1993) and never flipped afterwards. Coordinates are shifted to the
    /// centroid internally so projected coordinates (millions of metres) keep full precision.
    /// </summary>
    public sealed class Triangulation
    {
        private struct Tri
        {
            public int V0, V1, V2;
            public int N0, N1, N2; // neighbour across the edge opposite V0 / V1 / V2 (-1 = none)
#pragma warning disable CS0649 // triangles are re-used in place and never removed; the flag keeps loops explicit
            public bool Dead;
#pragma warning restore CS0649

            public int V(int i) => i == 0 ? V0 : (i == 1 ? V1 : V2);

            public int N(int i) => i == 0 ? N0 : (i == 1 ? N1 : N2);

            public void SetN(int i, int t)
            {
                if (i == 0) { N0 = t; } else if (i == 1) { N1 = t; } else { N2 = t; }
            }
        }

        private readonly List<double> xs = new List<double>();
        private readonly List<double> ys = new List<double>();
        private readonly List<double> zs = new List<double>();
        private readonly List<Tri> tris = new List<Tri>();
        private readonly HashSet<long> constrained = new HashSet<long>();
        private double ox;
        private double oy;
        private int lastTri;

        /// <summary>Vertices (original coordinates).</summary>
        public List<Vec3> Vertices { get; } = new List<Vec3>();

        /// <summary>Final triangles as vertex index triples (counter-clockwise).</summary>
        public List<(int A, int B, int C)> Triangles { get; } = new List<(int A, int B, int C)>();

        /// <summary>Points dropped because another point already exists within tolerance (index kept, Z difference).</summary>
        public List<(Vec3 Point, int KeptIndex, double DeltaZ)> Duplicates { get; } = new List<(Vec3, int, double)>();

        /// <summary>Constraint segments that could not be recovered (should not happen with valid input).</summary>
        public List<(Vec3 A, Vec3 B)> UnrecoveredConstraints { get; } = new List<(Vec3, Vec3)>();

        /// <summary>Constraint segments that cross each other (a modelling error in the input).</summary>
        public List<(Vec3 A, Vec3 B)> CrossingConstraints { get; } = new List<(Vec3, Vec3)>();

        public double Tolerance { get; }

        private Triangulation(double tolerance) => Tolerance = tolerance;

        /// <summary>
        /// Builds the triangulation. <paramref name="constraints"/> are polylines whose segments must appear as edges.
        /// When <paramref name="boundary"/> is given, triangles outside it are removed (the boundary becomes a constraint).
        /// </summary>
        public static Triangulation Build(IEnumerable<Vec3> points, IEnumerable<IReadOnlyList<Vec3>>? constraints = null, IReadOnlyList<Vec2>? boundary = null, double tolerance = 0.001)
        {
            var t = new Triangulation(tolerance);
            List<Vec3> pts = points.ToList();
            List<IReadOnlyList<Vec3>> cons = constraints?.ToList() ?? new List<IReadOnlyList<Vec3>>();
            IEnumerable<Vec3> all = pts.Concat(cons.SelectMany(c => c));
            List<Vec3> allList = all.ToList();
            if (allList.Count < 3)
            {
                return t;
            }

            t.ox = allList.Average(p => p.X);
            t.oy = allList.Average(p => p.Y);
            t.InitSuper(allList);

            var index = new Dictionary<(long, long), int>();
            int Insert(Vec3 p)
            {
                (long, long) key = (Key(p.X - t.ox, tolerance), Key(p.Y - t.oy, tolerance));
                if (index.TryGetValue(key, out int existing))
                {
                    double dz = p.Z - t.Vertices[existing].Z;
                    if (Math.Abs(dz) > 1e-9)
                    {
                        t.Duplicates.Add((p, existing, dz));
                    }

                    return existing;
                }

                int vi = t.AddVertex(p);
                if (!t.InsertVertex(vi))
                {
                    // numerically on an existing vertex (tolerance cell boundary): reuse the nearest
                    int nearest = t.Nearest(p);
                    t.Duplicates.Add((p, nearest, p.Z - t.Vertices[nearest].Z));
                    index[key] = nearest;
                    return nearest;
                }

                index[key] = vi;
                return vi;
            }

            foreach (Vec3 p in pts)
            {
                Insert(p);
            }

            var segments = new List<(int, int)>();
            foreach (IReadOnlyList<Vec3> line in cons)
            {
                for (int i = 0; i + 1 < line.Count; i++)
                {
                    int a = Insert(line[i]), b = Insert(line[i + 1]);
                    if (a != b)
                    {
                        segments.Add((a, b));
                    }
                }
            }

            if (boundary != null && boundary.Count >= 3)
            {
                // boundary vertices take Z by interpolation later; only XY matters for clipping
                var bIdx = new List<int>();
                foreach (Vec2 b in boundary)
                {
                    double z = t.InterpolateInternal(b.X - t.ox, b.Y - t.oy) ?? double.NaN;
                    bIdx.Add(Insert(new Vec3(b.X, b.Y, z)));
                }

                for (int i = 0; i < bIdx.Count; i++)
                {
                    int a = bIdx[i], b = bIdx[(i + 1) % bIdx.Count];
                    if (a != b)
                    {
                        segments.Add((a, b));
                    }
                }
            }

            t.DetectCrossings(segments);
            foreach ((int a, int b) in segments)
            {
                t.InsertConstraint(a, b);
            }

            t.Finish(boundary);
            return t;
        }

        private static long Key(double v, double tol) => (long)Math.Round(v / tol);

        private int AddVertex(Vec3 p)
        {
            Vertices.Add(p);
            xs.Add(p.X - ox);
            ys.Add(p.Y - oy);
            zs.Add(p.Z);
            return Vertices.Count - 1;
        }

        private int Nearest(Vec3 p)
        {
            int best = 0;
            double bd = double.MaxValue;
            for (int i = 0; i < Vertices.Count; i++)
            {
                double d = Math.Pow(xs[i] - (p.X - ox), 2) + Math.Pow(ys[i] - (p.Y - oy), 2);
                if (d < bd)
                {
                    bd = d;
                    best = i;
                }
            }

            return best;
        }

        private void InitSuper(List<Vec3> pts)
        {
            double minX = pts.Min(p => p.X) - ox, maxX = pts.Max(p => p.X) - ox, minY = pts.Min(p => p.Y) - oy, maxY = pts.Max(p => p.Y) - oy;
            double d = Math.Max(Math.Max(maxX - minX, maxY - minY), 1.0) * 100;
            double cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;

            // the three super vertices use negative indices (-1, -2, -3) so user vertex indices are never shifted
            superX = new[] { cx - (2 * d), cx + (2 * d), cx };
            superY = new[] { cy - d, cy - d, cy + (2 * d) };
            tris.Add(new Tri { V0 = -1, V1 = -2, V2 = -3, N0 = -1, N1 = -1, N2 = -1 });
        }

        private double[] superX = Array.Empty<double>();
        private double[] superY = Array.Empty<double>();

        private double X(int v) => v >= 0 ? xs[v] : superX[-v - 1];

        private double Y(int v) => v >= 0 ? ys[v] : superY[-v - 1];

        private double Orient(int a, int b, int c) => ((X(b) - X(a)) * (Y(c) - Y(a))) - ((Y(b) - Y(a)) * (X(c) - X(a)));

        private static double Orient(double ax, double ay, double bx, double by, double cx, double cy) => ((bx - ax) * (cy - ay)) - ((by - ay) * (cx - ax));

        /// <summary>True when d lies strictly inside the circumcircle of the CCW triangle a,b,c.</summary>
        private bool InCircle(int a, int b, int c, int d)
        {
            // super vertices take part with their real (distant) coordinates; a super vertex is never pulled in
            if (d < 0)
            {
                return false;
            }

            double adx = X(a) - X(d), ady = Y(a) - Y(d);
            double bdx = X(b) - X(d), bdy = Y(b) - Y(d);
            double cdx = X(c) - X(d), cdy = Y(c) - Y(d);
            double det = (((adx * adx) + (ady * ady)) * ((bdx * cdy) - (cdx * bdy)))
                       - (((bdx * bdx) + (bdy * bdy)) * ((adx * cdy) - (cdx * ady)))
                       + (((cdx * cdx) + (cdy * cdy)) * ((adx * bdy) - (bdx * ady)));
            double scale = ((adx * adx) + (ady * ady) + (bdx * bdx) + (bdy * bdy) + (cdx * cdx) + (cdy * cdy));
            return det > 1e-12 * scale * scale;
        }

        private int Locate(double px, double py)
        {
            int t = lastTri >= 0 && lastTri < tris.Count && !tris[lastTri].Dead ? lastTri : tris.FindIndex(x => !x.Dead);
            for (int steps = 0; steps < tris.Count * 4 + 100; steps++)
            {
                Tri tr = tris[t];
                bool moved = false;
                for (int i = 0; i < 3; i++)
                {
                    int a = tr.V((i + 1) % 3), b = tr.V((i + 2) % 3);
                    if (Orient(X(a), Y(a), X(b), Y(b), px, py) < -1e-12 * Scale2(a, b) && tr.N(i) >= 0)
                    {
                        t = tr.N(i);
                        moved = true;
                        break;
                    }
                }

                if (!moved)
                {
                    lastTri = t;
                    return t;
                }
            }

            // walking failed (degenerate cycle): brute force
            for (int i = 0; i < tris.Count; i++)
            {
                if (!tris[i].Dead && Contains(i, px, py))
                {
                    return i;
                }
            }

            return -1;
        }

        private double Scale2(int a, int b) => Math.Max(1.0, Math.Abs(X(b) - X(a)) + Math.Abs(Y(b) - Y(a)));

        private bool Contains(int t, double px, double py)
        {
            Tri tr = tris[t];
            for (int i = 0; i < 3; i++)
            {
                int a = tr.V((i + 1) % 3), b = tr.V((i + 2) % 3);
                if (Orient(X(a), Y(a), X(b), Y(b), px, py) < -1e-9)
                {
                    return false;
                }
            }

            return true;
        }

        private bool InsertVertex(int v)
        {
            double px = X(v), py = Y(v);
            int t = Locate(px, py);
            if (t < 0)
            {
                return false;
            }

            Tri tr = tris[t];
            // on an edge?
            for (int i = 0; i < 3; i++)
            {
                int a = tr.V((i + 1) % 3), b = tr.V((i + 2) % 3);
                double len = Math.Sqrt(Math.Pow(X(b) - X(a), 2) + Math.Pow(Y(b) - Y(a), 2));
                double dist = Orient(X(a), Y(a), X(b), Y(b), px, py) / Math.Max(len, 1e-12);
                if (Math.Abs(dist) < Tolerance * 1e-3)
                {
                    if ((Math.Abs(X(a) - px) < Tolerance && Math.Abs(Y(a) - py) < Tolerance) || (Math.Abs(X(b) - px) < Tolerance && Math.Abs(Y(b) - py) < Tolerance))
                    {
                        return false;
                    }

                    SplitEdge(t, i, v);
                    return true;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                int a = tr.V(i);
                if (Math.Abs(X(a) - px) < Tolerance && Math.Abs(Y(a) - py) < Tolerance)
                {
                    return false;
                }
            }

            SplitTriangle(t, v);
            return true;
        }

        private void SplitTriangle(int t, int v)
        {
            Tri o = tris[t];
            int t1 = tris.Count, t2 = tris.Count + 1;
            // t: (v, V1, V2) opposite edge V1-V2 neighbour N0; t1: (V0, v, V2) neighbour N1; t2: (V0, V1, v) neighbour N2
            var a = new Tri { V0 = v, V1 = o.V1, V2 = o.V2, N0 = o.N0, N1 = t1, N2 = t2 };
            var b = new Tri { V0 = o.V0, V1 = v, V2 = o.V2, N0 = t, N1 = o.N1, N2 = t2 };
            var c = new Tri { V0 = o.V0, V1 = o.V1, V2 = v, N0 = t, N1 = t1, N2 = o.N2 };
            tris[t] = a;
            tris.Add(b);
            tris.Add(c);
            ReplaceNeighbour(o.N1, t, t1);
            ReplaceNeighbour(o.N2, t, t2);
            var stack = new Stack<(int Tri, int Edge)>();
            stack.Push((t, 0));
            stack.Push((t1, 1));
            stack.Push((t2, 2));
            Legalize(stack);
        }

        private void SplitEdge(int t, int edge, int v)
        {
            // triangle t has edge opposite vertex index `edge`; split it and the neighbour across it
            Tri o = tris[t];
            int apex = o.V(edge), a = o.V((edge + 1) % 3), b = o.V((edge + 2) % 3);
            int nb = o.N(edge);
            int nA = o.N((edge + 1) % 3); // across edge b-apex
            int nB = o.N((edge + 2) % 3); // across edge apex-a
            bool wasConstrained = constrained.Contains(EdgeKey(a, b));

            // t  -> (apex, a, v) ; t1 -> (apex, v, b)
            int t1 = tris.Count;
            tris[t] = new Tri { V0 = apex, V1 = a, V2 = v, N0 = -1, N1 = t1, N2 = nB };
            tris.Add(new Tri { V0 = apex, V1 = v, V2 = b, N0 = -1, N1 = nA, N2 = t });
            ReplaceNeighbour(nA, t, t1);
            var stack = new Stack<(int Tri, int Edge)>();
            if (nb >= 0)
            {
                Tri q = tris[nb];
                int qe = EdgeIndex(q, b, a);
                int qApex = q.V(qe);
                int qnA = q.N((qe + 1) % 3); // across edge a-qApex  (q is (qApex, b, a) rotated)
                int qnB = q.N((qe + 2) % 3); // across edge qApex-b
                // nb -> (qApex, b, v) ; t3 -> (qApex, v, a)
                int t3 = tris.Count;
                tris[nb] = new Tri { V0 = qApex, V1 = b, V2 = v, N0 = t1, N1 = t3, N2 = qnB };
                tris.Add(new Tri { V0 = qApex, V1 = v, V2 = a, N0 = t, N1 = qnA, N2 = nb });
                ReplaceNeighbour(qnA, nb, t3);
                SetNeighbourAcross(t, a, v, t3);
                SetNeighbourAcross(t1, v, b, nb);
                stack.Push((nb, 2));
                stack.Push((t3, 1));
            }

            if (wasConstrained)
            {
                constrained.Remove(EdgeKey(a, b));
                constrained.Add(EdgeKey(a, v));
                constrained.Add(EdgeKey(v, b));
            }

            stack.Push((t, 2));
            stack.Push((t1, 1));
            Legalize(stack);
        }

        private static int EdgeIndex(Tri t, int a, int b)
        {
            for (int i = 0; i < 3; i++)
            {
                if (t.V((i + 1) % 3) == a && t.V((i + 2) % 3) == b)
                {
                    return i;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                if (t.V((i + 1) % 3) == b && t.V((i + 2) % 3) == a)
                {
                    return i;
                }
            }

            throw new InvalidOperationException("Edge not in triangle.");
        }

        private void SetNeighbourAcross(int t, int a, int b, int n)
        {
            Tri tr = tris[t];
            tr.SetN(EdgeIndex(tr, a, b), n);
            tris[t] = tr;
        }

        private void ReplaceNeighbour(int t, int oldN, int newN)
        {
            if (t < 0)
            {
                return;
            }

            Tri tr = tris[t];
            for (int i = 0; i < 3; i++)
            {
                if (tr.N(i) == oldN)
                {
                    tr.SetN(i, newN);
                }
            }

            tris[t] = tr;
        }

        private static long EdgeKey(int a, int b)
        {
            int lo = Math.Min(a, b), hi = Math.Max(a, b);
            return ((long)lo << 32) | (uint)hi;
        }

        /// <summary>Lawson flips: each entry is a triangle and the index of the edge to check.</summary>
        private void Legalize(Stack<(int Tri, int Edge)> stack)
        {
            int guard = 0;
            while (stack.Count > 0 && guard++ < 1_000_000)
            {
                (int t, int e) = stack.Pop();
                Tri tr = tris[t];
                int n = tr.N(e);
                if (n < 0)
                {
                    continue;
                }

                int a = tr.V((e + 1) % 3), b = tr.V((e + 2) % 3), p = tr.V(e);
                if (constrained.Contains(EdgeKey(a, b)))
                {
                    continue;
                }

                Tri q = tris[n];
                int qe = EdgeIndex(q, b, a);
                int d = q.V(qe);
                if (!InCircle(p, a, b, d))
                {
                    continue;
                }

                if (!Flip(t, e))
                {
                    continue;
                }

                // after flip t = (p, a, d), n = (p, d, b): check the two outer edges
                stack.Push((t, EdgeIndex(tris[t], a, d)));
                stack.Push((n, EdgeIndex(tris[n], d, b)));
            }
        }

        /// <summary>Flips the edge opposite vertex index e of triangle t. Returns false if the quad is not convex.</summary>
        private bool Flip(int t, int e)
        {
            Tri tr = tris[t];
            int n = tr.N(e);
            int p = tr.V(e), a = tr.V((e + 1) % 3), b = tr.V((e + 2) % 3);
            Tri q = tris[n];
            int qe = EdgeIndex(q, b, a);
            int d = q.V(qe);
            if (Orient(p, a, d) <= 0 || Orient(p, d, b) <= 0)
            {
                return false;
            }

            int nPA = tr.N((e + 2) % 3); // across p-a
            int nBP = tr.N((e + 1) % 3); // across b-p
            int nAD = q.N(EdgeIndex(q, a, d));
            int nDB = q.N(EdgeIndex(q, d, b));

            // t = (p, a, d), n = (p, d, b)
            tris[t] = new Tri { V0 = p, V1 = a, V2 = d, N0 = nAD, N1 = n, N2 = nPA };
            tris[n] = new Tri { V0 = p, V1 = d, V2 = b, N0 = nDB, N1 = nBP, N2 = t };
            ReplaceNeighbour(nAD, n, t);
            ReplaceNeighbour(nBP, t, n);
            lastTri = t;
            return true;
        }

        private void DetectCrossings(List<(int A, int B)> segs)
        {
            for (int i = 0; i < segs.Count; i++)
            {
                for (int j = i + 1; j < segs.Count; j++)
                {
                    (int a, int b) = segs[i];
                    (int c, int d) = segs[j];
                    if (a == c || a == d || b == c || b == d)
                    {
                        continue;
                    }

                    if (SegmentsCross(a, b, c, d))
                    {
                        CrossingConstraints.Add((Vertices[a], Vertices[b]));
                    }
                }
            }
        }

        private bool SegmentsCross(int a, int b, int c, int d)
        {
            double o1 = Orient(a, b, c), o2 = Orient(a, b, d), o3 = Orient(c, d, a), o4 = Orient(c, d, b);
            return ((o1 > 1e-12 && o2 < -1e-12) || (o1 < -1e-12 && o2 > 1e-12)) && ((o3 > 1e-12 && o4 < -1e-12) || (o3 < -1e-12 && o4 > 1e-12));
        }

        private bool HasEdge(int a, int b, out int tri)
        {
            for (int i = 0; i < tris.Count; i++)
            {
                if (tris[i].Dead)
                {
                    continue;
                }

                Tri t = tris[i];
                for (int k = 0; k < 3; k++)
                {
                    if (t.V((k + 1) % 3) == a && t.V((k + 2) % 3) == b)
                    {
                        tri = i;
                        return true;
                    }
                }
            }

            tri = -1;
            return false;
        }

        private void InsertConstraint(int a, int b)
        {
            // split at existing vertices lying on the segment
            for (int v = 0; v < Vertices.Count; v++)
            {
                if (v == a || v == b)
                {
                    continue;
                }

                double len = Math.Sqrt(Math.Pow(X(b) - X(a), 2) + Math.Pow(Y(b) - Y(a), 2));
                if (len < 1e-12)
                {
                    return;
                }

                double dist = Math.Abs(Orient(a, b, v)) / len;
                double tpar = (((X(v) - X(a)) * (X(b) - X(a))) + ((Y(v) - Y(a)) * (Y(b) - Y(a)))) / (len * len);
                if (dist < Tolerance && tpar > 1e-9 && tpar < 1 - 1e-9)
                {
                    InsertConstraint(a, v);
                    InsertConstraint(v, b);
                    return;
                }
            }

            if (HasEdge(a, b, out _) || HasEdge(b, a, out _))
            {
                constrained.Add(EdgeKey(a, b));
                return;
            }

            // edges crossing a-b
            var crossing = new Queue<(int, int)>();
            for (int i = 0; i < tris.Count; i++)
            {
                if (tris[i].Dead)
                {
                    continue;
                }

                Tri t = tris[i];
                for (int k = 0; k < 3; k++)
                {
                    int u = t.V((k + 1) % 3), w = t.V((k + 2) % 3);
                    if (u < w && SegmentsCross(a, b, u, w))
                    {
                        crossing.Enqueue((u, w));
                    }
                }
            }

            int guard = 0;
            var newEdges = new List<(int, int)>();
            while (crossing.Count > 0 && guard++ < 100000)
            {
                (int u, int w) = crossing.Dequeue();
                if (constrained.Contains(EdgeKey(u, w)))
                {
                    continue; // crossing constraints were reported already
                }

                if (!HasEdge(u, w, out int t))
                {
                    if (!HasEdge(w, u, out t))
                    {
                        continue;
                    }
                }

                Tri tr = tris[t];
                int e = -1;
                for (int k = 0; k < 3; k++)
                {
                    int x1 = tr.V((k + 1) % 3), x2 = tr.V((k + 2) % 3);
                    if ((x1 == u && x2 == w) || (x1 == w && x2 == u))
                    {
                        e = k;
                    }
                }

                if (e < 0 || tr.N(e) < 0)
                {
                    continue;
                }

                int p = tr.V(e);
                Tri q = tris[tr.N(e)];
                int d = q.V(EdgeIndex(q, tr.V((e + 2) % 3), tr.V((e + 1) % 3)));
                if (!Flip(t, e))
                {
                    crossing.Enqueue((u, w));
                    if (guard > 50000)
                    {
                        break;
                    }

                    continue;
                }

                int lo = Math.Min(p, d), hi = Math.Max(p, d);
                if (SegmentsCross(a, b, lo, hi))
                {
                    crossing.Enqueue((lo, hi));
                }
                else
                {
                    newEdges.Add((lo, hi));
                }
            }

            if (HasEdge(a, b, out _) || HasEdge(b, a, out _))
            {
                constrained.Add(EdgeKey(a, b));
            }
            else
            {
                UnrecoveredConstraints.Add((Vertices[a], Vertices[b]));
            }

            // restore Delaunay for the new (unconstrained) edges
            var stack = new Stack<(int Tri, int Edge)>();
            foreach ((int u, int w) in newEdges)
            {
                if ((HasEdge(u, w, out int t) || HasEdge(w, u, out t)) && !constrained.Contains(EdgeKey(u, w)))
                {
                    Tri tr = tris[t];
                    for (int k = 0; k < 3; k++)
                    {
                        int x1 = tr.V((k + 1) % 3), x2 = tr.V((k + 2) % 3);
                        if ((x1 == u && x2 == w) || (x1 == w && x2 == u))
                        {
                            stack.Push((t, k));
                        }
                    }
                }
            }

            Legalize(stack);
        }

        private double? InterpolateInternal(double px, double py)
        {
            if (Vertices.Count < 3)
            {
                return null;
            }

            int t = Locate(px, py);
            if (t < 0)
            {
                return null;
            }

            Tri tr = tris[t];
            if (tr.V0 < 0 || tr.V1 < 0 || tr.V2 < 0 || !Contains(t, px, py))
            {
                return null;
            }

            return Bary(tr.V0, tr.V1, tr.V2, px, py);
        }

        private double Bary(int a, int b, int c, double px, double py)
        {
            double det = Orient(a, b, c);
            double l1 = Orient(X(b), Y(b), X(c), Y(c), px, py) / det;
            double l2 = Orient(X(c), Y(c), X(a), Y(a), px, py) / det;
            double l3 = 1 - l1 - l2;
            return (l1 * zs[a]) + (l2 * zs[b]) + (l3 * zs[c]);
        }

        private void Finish(IReadOnlyList<Vec2>? boundary)
        {
            List<Vec2>? bpoly = boundary?.Select(b => new Vec2(b.X - ox, b.Y - oy)).ToList();
            for (int i = 0; i < tris.Count; i++)
            {
                Tri t = tris[i];
                if (t.Dead || t.V0 < 0 || t.V1 < 0 || t.V2 < 0)
                {
                    continue;
                }

                if (bpoly != null)
                {
                    var c = new Vec2((X(t.V0) + X(t.V1) + X(t.V2)) / 3, (Y(t.V0) + Y(t.V1) + Y(t.V2)) / 3);
                    if (!GeometryMath.PointInPolygon(c, bpoly))
                    {
                        continue;
                    }
                }

                if (Math.Abs(Orient(t.V0, t.V1, t.V2)) < 1e-12)
                {
                    continue; // sliver of zero area (collinear input)
                }

                Triangles.Add((t.V0, t.V1, t.V2));
            }

            // boundary vertices inserted without known Z get the interpolated value of neighbouring real vertices
            for (int v = 0; v < Vertices.Count; v++)
            {
                if (double.IsNaN(Vertices[v].Z))
                {
                    double sum = 0;
                    double wsum = 0;
                    foreach ((int a, int b, int c) in Triangles.Where(tr => tr.A == v || tr.B == v || tr.C == v))
                    {
                        foreach (int o in new[] { a, b, c })
                        {
                            if (o != v && !double.IsNaN(Vertices[o].Z))
                            {
                                double d = Math.Sqrt(Math.Pow(X(o) - X(v), 2) + Math.Pow(Y(o) - Y(v), 2));
                                double w = 1 / Math.Max(d, 1e-6);
                                sum += w * Vertices[o].Z;
                                wsum += w;
                            }
                        }
                    }

                    Vertices[v] = new Vec3(Vertices[v].X, Vertices[v].Y, wsum > 0 ? sum / wsum : double.NaN);
                    zs[v] = Vertices[v].Z;
                }
            }
        }

        public bool IsConstrainedEdge(int a, int b) => constrained.Contains(EdgeKey(a, b));
    }
}
