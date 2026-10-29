# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Polygon generators for closed solids. Every polygon is a list of [x, y, z] points (metres) ordered
    # counter-clockwise when seen from outside, so the right-hand-rule normal points outward. A polygon with
    # openings is a Hash { outer: [...], holes: [[...], ...] } (holes clockwise when seen from outside).
    module Mesh
      module_function

      def segments_for(lod, base = 16)
        case lod.to_s
        when 'conceptual' then [base / 2, 6].max
        when 'presentation' then base * 2
        else base
        end
      end

      # Vertical prism between z0 and z1 over a plan ring (any orientation) with optional holes.
      def prism(ring, z0, z1, holes = [])
        z0, z1 = z1, z0 if z1 < z0
        outer = Geometry.ccw(Geometry.dedupe(ring.map { |p| p[0, 2] }, true))
        hole_rings = holes.map { |h| Geometry.ccw(Geometry.dedupe(h.map { |p| p[0, 2] }, true)).reverse }
        polys = []
        top_outer = outer.map { |p| [p[0], p[1], z1] }
        bottom_outer = outer.reverse.map { |p| [p[0], p[1], z0] }
        if hole_rings.empty?
          polys << top_outer << bottom_outer
        else
          polys << { outer: top_outer, holes: hole_rings.map { |h| h.map { |p| [p[0], p[1], z1] } } }
          polys << { outer: bottom_outer, holes: hole_rings.map { |h| h.reverse.map { |p| [p[0], p[1], z0] } } }
        end
        ([outer] + hole_rings).each { |r| polys.concat(walls(r, z0, z1)) }
        polys
      end

      def walls(ring, z0, z1)
        ring.each_with_index.map do |a, i|
          b = ring[(i + 1) % ring.length]
          [[a[0], a[1], z0], [b[0], b[1], z0], [b[0], b[1], z1], [a[0], a[1], z1]]
        end
      end

      # Rectangle centred on (cx, cy) rotated by angle (radians), as a ring.
      def rect_ring(cx, cy, sx, sy, angle = 0.0)
        hx = sx / 2.0
        hy = sy / 2.0
        [[-hx, -hy], [hx, -hy], [hx, hy], [-hx, hy]].map do |p|
          r = Geometry.rotate(p, angle)
          [cx + r[0], cy + r[1]]
        end
      end

      # Box centred in plan on (cx, cy), from z0 to z0 + sz.
      def box(cx, cy, z0, sx, sy, sz, angle = 0.0)
        prism(rect_ring(cx, cy, sx, sy, angle), z0, z0 + sz)
      end

      # Parallelepiped from a corner and three edge vectors (any orientation; made right-handed).
      def block(corner, xv, yv, zv)
        if triple(xv, yv, zv).negative?
          corner = add3(corner, xv)
          xv = neg3(xv)
        end
        c = corner
        p = lambda do |i, j, k|
          [c[0] + (xv[0] * i) + (yv[0] * j) + (zv[0] * k),
           c[1] + (xv[1] * i) + (yv[1] * j) + (zv[1] * k),
           c[2] + (xv[2] * i) + (yv[2] * j) + (zv[2] * k)]
        end
        [
          [p[0, 0, 0], p[0, 1, 0], p[1, 1, 0], p[1, 0, 0]], # bottom (-z)
          [p[0, 0, 1], p[1, 0, 1], p[1, 1, 1], p[0, 1, 1]], # top (+z)
          [p[0, 0, 0], p[1, 0, 0], p[1, 0, 1], p[0, 0, 1]], # -y
          [p[1, 1, 0], p[0, 1, 0], p[0, 1, 1], p[1, 1, 1]], # +y
          [p[0, 1, 0], p[0, 0, 0], p[0, 0, 1], p[0, 1, 1]], # -x
          [p[1, 0, 0], p[1, 1, 0], p[1, 1, 1], p[1, 0, 1]]  # +x
        ]
      end

      def circle_ring(cx, cy, r, n)
        Array.new(n) { |i| t = 2 * Math::PI * i / n; [cx + (Math.cos(t) * r), cy + (Math.sin(t) * r)] }
      end

      def cylinder(base, r, h, n)
        prism(circle_ring(base[0], base[1], r, n), base[2], base[2] + h)
      end

      # Hollow vertical ring (round chamber walls, fountain basins).
      def ring_wall(base, r_out, r_in, h, n)
        prism(circle_ring(base[0], base[1], r_out, n), base[2], base[2] + h, [circle_ring(base[0], base[1], r_in, n)])
      end

      def cone(base, r, h, n)
        ring = circle_ring(base[0], base[1], r, n)
        apex = [base[0], base[1], base[2] + h]
        polys = [ring.reverse.map { |p| [p[0], p[1], base[2]] }]
        ring.each_with_index do |a, i|
          b = ring[(i + 1) % n]
          polys << [[a[0], a[1], base[2]], [b[0], b[1], base[2]], apex]
        end
        polys
      end

      # Cylinder between two arbitrary points.
      def tube(p1, p2, r, n)
        axis = sub3(p2, p1)
        l = len3(axis)
        return [] if l < 1e-9

        w = scale3(axis, 1.0 / l)
        helper = w[2].abs < 0.9 ? [0.0, 0.0, 1.0] : [1.0, 0.0, 0.0]
        u = normalize3(cross3(helper, w))
        v = cross3(w, u)
        ring = Array.new(n) do |i|
          t = 2 * Math::PI * i / n
          add3(scale3(u, Math.cos(t) * r), scale3(v, Math.sin(t) * r))
        end
        bottom = ring.map { |o| add3(p1, o) }
        top = ring.map { |o| add3(p2, o) }
        polys = [bottom.reverse, top]
        n.times do |i|
          j = (i + 1) % n
          polys << [bottom[i], bottom[j], top[j], top[i]]
        end
        polys
      end

      # Ellipsoid (UV sphere) centred on c.
      def ellipsoid(c, rx, ry, rz, n, rings)
        rings = [rings, 2].max
        pts = (1...rings).map do |k|
          phi = Math::PI * k / rings
          Array.new(n) do |i|
            t = 2 * Math::PI * i / n
            [c[0] + (rx * Math.sin(phi) * Math.cos(t)), c[1] + (ry * Math.sin(phi) * Math.sin(t)), c[2] + (rz * Math.cos(phi))]
          end
        end
        top = [c[0], c[1], c[2] + rz]
        bottom = [c[0], c[1], c[2] - rz]
        polys = []
        n.times do |i|
          j = (i + 1) % n
          polys << [pts.first[i], pts.first[j], top]
          polys << [pts.last[j], pts.last[i], bottom]
          (0...(pts.length - 1)).each do |k|
            polys << [pts[k + 1][i], pts[k + 1][j], pts[k][j], pts[k][i]]
          end
        end
        polys
      end

      # Closed solid between two parallel, vertically offset copies of the same 3D polyline strip.
      # left/right are matching point lists (same length, travel direction), top at z + dz_top, bottom at z + dz_bottom.
      def strip(left, right, dz_bottom, dz_top)
        n = left.length
        raise ArgumentError, 'strip sides must match' unless n == right.length && n >= 2

        lt = left.map { |p| [p[0], p[1], p[2] + dz_top] }
        rt = right.map { |p| [p[0], p[1], p[2] + dz_top] }
        lb = left.map { |p| [p[0], p[1], p[2] + dz_bottom] }
        rb = right.map { |p| [p[0], p[1], p[2] + dz_bottom] }
        polys = []
        (n - 1).times do |i|
          polys << [rt[i], rt[i + 1], lt[i + 1], lt[i]]   # top (+z)
          polys << [lb[i], lb[i + 1], rb[i + 1], rb[i]]   # bottom (-z)
          polys << [lb[i], lt[i], lt[i + 1], lb[i + 1]]   # left side
          polys << [rb[i + 1], rt[i + 1], rt[i], rb[i]]   # right side
        end
        polys << [lb[0], rb[0], rt[0], lt[0]]                        # start cap
        polys << [rb[n - 1], lb[n - 1], lt[n - 1], rt[n - 1]]        # end cap
        polys
      end

      # Sweeps a closed 2D profile along a 3D polyline with mitred joints. Profile points are [u, w]
      # where u is the offset to the left of travel and w the height above the path. Returns a closed solid.
      # For closed paths the sweep wraps around without caps.
      def sweep(path, profile, closed = false)
        path = Geometry.dedupe(path, closed)
        n = path.length
        raise ArgumentError, 'sweep path needs at least two distinct points' if n < 2

        # profile must run clockwise in the (u to the right, w up) view so side normals point outward
        prof = Geometry.signed_area(profile).positive? ? profile.reverse : profile
        frames = Array.new(n) do |i|
          prev_dir = i.positive? || closed ? Geometry.norm(Geometry.sub(path[i], path[(i - 1) % n])) : nil
          next_dir = i < n - 1 || closed ? Geometry.norm(Geometry.sub(path[(i + 1) % n], path[i])) : nil
          d1 = prev_dir || next_dir
          d2 = next_dir || prev_dir
          left = Geometry.norm(Geometry.add(Geometry.perp(d1), Geometry.perp(d2)))
          left = Geometry.perp(d1) if Geometry.len(left) < 1e-9
          k = Geometry.dot(left, Geometry.perp(d1))
          k = k.abs < 0.2 ? 5.0 : 1.0 / k
          [left, k]
        end
        rings = path.each_with_index.map do |p, i|
          left, k = frames[i]
          prof.map { |(u, w)| [p[0] + (left[0] * u * k), p[1] + (left[1] * u * k), (p[2] || 0.0) + w] }
        end
        m = prof.length
        polys = []
        segs = closed ? n : n - 1
        segs.times do |i|
          a = rings[i]
          b = rings[(i + 1) % n]
          m.times do |j|
            jj = (j + 1) % m
            polys << [a[j], b[j], b[jj], a[jj]]
          end
        end
        unless closed
          start_cap = rings[0].reverse
          end_cap = rings[n - 1]
          fwd = Geometry.sub(path[1], path[0])
          start_cap = start_cap.reverse if dot3(newell(start_cap), [fwd[0], fwd[1], 0.0]).positive?
          fwd_end = Geometry.sub(path[n - 1], path[n - 2])
          end_cap = end_cap.reverse if dot3(newell(end_cap), [fwd_end[0], fwd_end[1], 0.0]).negative?
          polys << start_cap << end_cap
        end
        polys
      end

      # Extrudes a profile given in a vertical plane: profile points [s, h] map to origin + v*s + z*h;
      # the solid extends along u by length. Used for steps and ramps.
      def extrude_profile(origin, u, v, profile, length)
        prof = Geometry.signed_area(profile).negative? ? profile.reverse : profile
        map = lambda do |(s, h), t|
          [origin[0] + (v[0] * s) + (u[0] * t), origin[1] + (v[1] * s) + (u[1] * t), origin[2] + h]
        end
        a = prof.map { |pt| map.call(pt, 0.0) }
        b = prof.map { |pt| map.call(pt, length) }
        polys = [a, b.reverse]
        n = prof.length
        n.times do |i|
          j = (i + 1) % n
          polys << [a[i], b[i], b[j], a[j]]
        end
        fix_orientation(polys)
      end

      # Flips every polygon of a closed solid if the solid is inside-out (negative signed volume).
      def fix_orientation(polys)
        vol = polys.sum do |poly|
          pts = poly.is_a?(Hash) ? poly[:outer] : poly
          (1...(pts.length - 1)).sum { |i| triple(pts[0], pts[i], pts[i + 1]) }
        end
        return polys unless vol.negative?

        polys.map { |poly| poly.is_a?(Hash) ? { outer: poly[:outer].reverse, holes: poly[:holes].map(&:reverse) } : poly.reverse }
      end

      # Newell normal of a polygon (not normalised).
      def newell(points)
        nx = ny = nz = 0.0
        points.each_with_index do |a, i|
          b = points[(i + 1) % points.length]
          nx += (a[1] - b[1]) * (a[2] + b[2])
          ny += (a[2] - b[2]) * (a[0] + b[0])
          nz += (a[0] - b[0]) * (a[1] + b[1])
        end
        [nx, ny, nz]
      end

      def planar?(points, tol = 1e-6)
        return true if points.length <= 3

        n = normalize3(newell(points))
        d = dot3(n, points[0])
        points.all? { |p| (dot3(n, p) - d).abs <= tol }
      end

      def add3(a, b)
        [a[0] + b[0], a[1] + b[1], a[2] + b[2]]
      end

      def sub3(a, b)
        [a[0] - b[0], a[1] - b[1], a[2] - b[2]]
      end

      def neg3(a)
        [-a[0], -a[1], -a[2]]
      end

      def scale3(a, s)
        [a[0] * s, a[1] * s, a[2] * s]
      end

      def dot3(a, b)
        (a[0] * b[0]) + (a[1] * b[1]) + (a[2] * b[2])
      end

      def cross3(a, b)
        [(a[1] * b[2]) - (a[2] * b[1]), (a[2] * b[0]) - (a[0] * b[2]), (a[0] * b[1]) - (a[1] * b[0])]
      end

      def len3(a)
        Math.sqrt(dot3(a, a))
      end

      def normalize3(a)
        l = len3(a)
        l < 1e-15 ? [0.0, 0.0, 0.0] : scale3(a, 1.0 / l)
      end

      def triple(a, b, c)
        dot3(a, cross3(b, c))
      end
    end
  end
end
