# frozen_string_literal: true

require 'zlib'

module ProjektniBiro
  module SiteDesignPro
    # Asset models: detailed, realistic 3D models per element type (asset ID = catalog type id). A model is
    # drawn in the element's local frame (insertion point at the origin, +X = element direction, metres) and
    # replaces the generic parametric builder of its type, in package import and in the model library alike.
    module Models
      @registry = {}

      Definition = Struct.new(:type_id, :geometry, :version, :block)

      class << self
        # Registers the model of a point type: Models.define('furniture.bench') { |k, e| ... }.
        def define(type_id, geometry: 'point', version: 1, &block)
          @registry[type_id] = Definition.new(type_id, geometry, version, block)
        end

        def for(type_id)
          @registry[type_id]
        end

        def ids
          @registry.keys.sort
        end
      end

      # Modelling toolkit bound to one canvas. Every helper emits one closed, outward-oriented solid
      # (planar faces only), so SketchUp gets clean solids and the recording canvas can verify them.
      class Kit
        attr_reader :ctx, :element, :rng

        def initialize(canvas, ctx, element)
          @c = canvas
          @ctx = ctx
          @element = element
          @rng = Random.new(Zlib.crc32(element.type_id.to_s))
        end

        # 0 = conceptual, 1 = standard, 2 = presentation
        def detail
          @ctx.conceptual? ? 0 : (@ctx.presentation? ? 2 : 1)
        end

        # Segment count for round parts, scaled by level of detail.
        def seg(base = 16)
          Mesh.segments_for(@ctx.lod, base)
        end

        def mat(key, fallback)
          v = @element.param(key)
          return fallback if v.nil? || v.to_s.empty?
          return v if @ctx.materials.empty? || @ctx.materials.key?(v)

          fallback
        end

        def num(key, fallback)
          @element.num(key, fallback)
        end

        def mesh(polys, material, name = nil, smooth: false)
          return if polys.empty?
          return @buffer << [polys, material, name, smooth] if @buffer

          @c.mesh(polys, material: material, name: name, smooth: smooth)
        end

        # Draws the block's meshes scaled so the result exactly fills the given envelope: plan width (largest
        # X and Y extents, scaled about the insertion axis) and top height (scaled up from z = 0).
        # Organic shapes (crowns, shrubs) stay irregular yet match the element's dimensions exactly.
        def fit(width: nil, height: nil)
          @buffer = []
          yield
          parts = @buffer
          @buffer = nil
          pts = parts.flat_map { |(polys, _, _, _)| polys.flat_map { |p| p.is_a?(Hash) ? p[:outer] : p } }
          return if pts.empty?

          sx = sy = 1.0
          if width
            ex = pts.map { |q| q[0] }.minmax
            ey = pts.map { |q| q[1] }.minmax
            sx = width / (ex[1] - ex[0]) if ex[1] - ex[0] > 1e-9
            sy = width / (ey[1] - ey[0]) if ey[1] - ey[0] > 1e-9
          end
          sz = height && pts.map { |q| q[2] }.max > 1e-9 ? height / pts.map { |q| q[2] }.max : 1.0
          map = ->(q) { [q[0] * sx, q[1] * sy, q[2] * sz] }
          parts.each do |(polys, material, name, smooth)|
            scaled = polys.map { |p| p.is_a?(Hash) ? { outer: p[:outer].map(&map), holes: p[:holes].map { |h| h.map(&map) } } : p.map(&map) }
            @c.mesh(scaled, material: material, name: name, smooth: smooth)
          end
        end

        # ---- primitives ------------------------------------------------------------------------------

        # Box centred in plan on (x, y), base at z, rotated about Z by angle.
        def box(x, y, z, sx, sy, sz, material, name = nil, angle: 0.0)
          mesh(Mesh.box(x, y, z, sx, sy, sz, angle), material, name)
        end

        # Box between two corners (axis aligned).
        def box2(min, max, material, name = nil)
          box((min[0] + max[0]) / 2.0, (min[1] + max[1]) / 2.0, min[2], (max[0] - min[0]).abs, (max[1] - min[1]).abs,
              (max[2] - min[2]).abs, material, name)
        end

        # Prism over a plan ring [[x, y], ...] between z0 and z1.
        def prism(ring, z0, z1, material, name = nil, holes: [])
          mesh(Mesh.prism(ring, z0, z1, holes), material, name)
        end

        def cyl(x, y, z, r, h, material, name = nil, n: seg(16))
          mesh(Mesh.cylinder([x, y, z], r, h, n), material, name, smooth: true)
        end

        # Solid of revolution about the vertical axis through (x, y). profile: [[r, z], ...] running from
        # the axis at the bottom, outwards and up, back to the axis at the top (counter-clockwise in r-z).
        def lathe(profile, material, name = nil, x: 0.0, y: 0.0, n: seg(16), closed_loop: false)
          mesh(Kit.lathe_polys(profile, x, y, n, closed_loop), material, name, smooth: true)
        end

        def frustum(x, y, z, r0, r1, h, material, name = nil, n: seg(16))
          lathe([[0.0, z], [r0, z], [r1, z + h], [0.0, z + h]], material, name, x: x, y: y, n: n)
        end

        def sphere(c, r, material, name = nil, n: seg(12))
          mesh(Mesh.ellipsoid(c, r, r, r, n, [n / 2, 3].max), material, name, smooth: true)
        end

        def ellipsoid(c, rx, ry, rz, material, name = nil, n: seg(12))
          mesh(Mesh.ellipsoid(c, rx, ry, rz, n, [n / 2, 3].max), material, name, smooth: true)
        end

        # Round bar between two points; r1 at p1, r2 at p2 (tapered when different).
        def bar(p1, p2, r1, material, name = nil, r2: nil, n: seg(8))
          mesh(Kit.tapered_tube(p1, p2, r1, r2 || r1, [n, 5].max), material, name, smooth: true)
        end

        # Round bar along a polyline, with a small ball at every inner joint.
        def rail(points, r, material, name = nil, n: seg(8))
          points.each_cons(2) { |a, b| bar(a, b, r, material, name, n: n) }
          points[1...-1].each { |p| sphere(p, r * 1.02, material, name, n: [n, 6].max) } if detail.positive?
        end

        # Square tube between two points (profile size s x s).
        def beam(p1, p2, s, material, name = nil)
          mesh(Kit.square_tube(p1, p2, s, s), material, name)
        end

        def beam_rect(p1, p2, w, h, material, name = nil)
          mesh(Kit.square_tube(p1, p2, w, h), material, name)
        end

        # Vertical plate: a 2D outline [[s, z], ...] in the plane through `origin` spanned by the plan
        # direction `dir` (unit [dx, dy]) and Z, extruded `thickness` to the left of dir.
        def plate(origin, dir, outline, thickness, material, name = nil)
          u = [-dir[1], dir[0], 0.0]
          mesh(Mesh.extrude_profile(origin, u, [dir[0], dir[1], 0.0], outline, thickness), material, name)
        end

        # Profile in the X-Z plane (x, z) extruded along +Y from y0 by depth. Good for slides, frames, signs.
        def side_extrude(outline, y0, depth, material, name = nil)
          plate([0.0, y0 + depth, 0.0], [1.0, 0.0], outline, depth, material, name)
        end

        def torus(c, big_r, small_r, material, name = nil, n: seg(16), m: seg(8))
          prof = Array.new(m) { |i| t = 2 * Math::PI * i / m; [big_r + (small_r * Math.cos(t)), c[2] + (small_r * Math.sin(t))] }
          mesh(Kit.lathe_polys(prof, c[0], c[1], n, true), material, name, smooth: true)
        end

        # Irregular rounded lump (foliage clusters, stones): a noisy icosphere, deterministic per seed.
        def blob(c, rx, ry, rz, material, name = nil, rough: 0.22, level: nil)
          lvl = level || (detail >= 2 ? 2 : 1)
          mesh(Kit.blob_polys(c, rx, ry, rz, rough, lvl, @rng), material, name, smooth: true)
        end

        # Several lumps as one mesh (one group in SketchUp): specs [[centre, rx, ry, rz], ...].
        def blobs(specs, material, name = nil, rough: 0.2, level: nil)
          lvl = level || (detail >= 2 ? 2 : 1)
          polys = specs.flat_map { |(c, rx, ry, rz)| Kit.blob_polys(c, rx, ry, rz, rough, lvl, @rng) }
          mesh(polys, material, name, smooth: true)
        end

        # Several bars as one mesh: specs [[p1, p2, r1, r2], ...].
        def bars(specs, material, name = nil, n: seg(6))
          polys = specs.flat_map { |(a, b, r1, r2)| Kit.tapered_tube(a, b, r1, r2 || r1, [n, 5].max) }
          mesh(polys, material, name, smooth: true)
        end

        # Component instance inside this model (repeated plants, slats ...): the block draws the definition.
        def place(key, origin, rotation = 0.0, name: nil)
          @c.instance("model-part:#{@element.type_id}|#{@ctx.lod}|#{key}", origin, rotation, name: name) do |dc|
            yield Kit.new(dc, @ctx, @element)
          end
        end

        # Draws the element with its parametric builder (for objects whose builder geometry already is the
        # real object, e.g. paint markings).
        def builder_draw(element = @element)
          Builders.for(element.builder).new(@ctx).build(element, @c)
        end

        def rand(a = 1.0, b = nil)
          b.nil? ? @rng.rand * a : a + (@rng.rand * (b - a))
        end

        # ---- element geometry (polygon / path models, package frame) -------------------------------------

        # [outer ring 3D, [holes]] of a polygon element.
        def outline
          g = @element.geometry
          tol = @ctx.presentation? ? 0.01 : 0.03
          outer = Geometry.dedupe(Geometry.tessellate(g[:outer][:vertices], g[:outer][:bulges], true, tol), true)
          holes = g[:holes].map { |h| Geometry.dedupe(Geometry.tessellate(h[:vertices], h[:bulges], true, tol), true) }
          [outer, holes.reject { |h| h.length < 3 }]
        end

        def path
          g = @element.geometry
          Geometry.dedupe(Geometry.tessellate(g[:vertices], g[:bulges], g[:closed], @ctx.presentation? ? 0.01 : 0.03), g[:closed])
        end

        # Jittered grid of points inside a polygon (with holes), at most `limit` points (spacing grows if needed).
        def scatter(outer, holes, spacing, inset: 0.0, limit: 600)
          xs = outer.map { |p| p[0] }
          ys = outer.map { |p| p[1] }
          area = Geometry.signed_area(outer).abs
          s = [spacing, Math.sqrt(area / limit)].max
          pts = []
          y = ys.min + (s / 2)
          row = 0
          while y < ys.max
            x = xs.min + (s / 2) + (row.odd? ? s / 2 : 0)
            while x < xs.max
              q = [x + ((@rng.rand - 0.5) * s * 0.5), y + ((@rng.rand - 0.5) * s * 0.5)]
              if Geometry.point_in_polygon?(q, outer) && holes.none? { |h| Geometry.point_in_polygon?(q, h) } &&
                 (inset <= 0 || Kit.dist_to_ring(q, outer) >= inset)
                pts << q
              end
              x += s
            end
            y += s * 0.87
            row += 1
          end
          pts
        end

        # ---- geometry builders (pure) ------------------------------------------------------------------

        def self.lathe_polys(profile, cx, cy, n, closed_loop)
          rings = profile.map do |(r, z)|
            Array.new(n) { |i| t = 2 * Math::PI * i / n; [cx + (Math.cos(t) * r), cy + (Math.sin(t) * r), z.to_f] }
          end
          polys = []
          segs = closed_loop ? profile.length : profile.length - 1
          segs.times do |k|
            k2 = (k + 1) % profile.length
            r1 = profile[k][0]
            r2 = profile[k2][0]
            next if r1 < 1e-9 && r2 < 1e-9

            n.times do |i|
              j = (i + 1) % n
              a = rings[k][i]
              b = rings[k][j]
              c = rings[k2][j]
              d = rings[k2][i]
              polys << if r1 < 1e-9 then [a, c, d]
                       elsif r2 < 1e-9 then [a, b, c]
                       else [a, b, c, d]
                       end
            end
          end
          Mesh.fix_orientation(polys)
        end

        def self.dist_to_ring(q, ring)
          ring.each_with_index.map do |a, i|
            b = ring[(i + 1) % ring.length]
            dx = b[0] - a[0]
            dy = b[1] - a[1]
            l2 = (dx * dx) + (dy * dy)
            t = l2 < 1e-12 ? 0.0 : [[(((q[0] - a[0]) * dx) + ((q[1] - a[1]) * dy)) / l2, 0.0].max, 1.0].min
            Math.hypot(q[0] - (a[0] + (t * dx)), q[1] - (a[1] + (t * dy)))
          end.min
        end

        def self.frame(p1, p2)
          axis = Mesh.sub3(p2, p1)
          l = Mesh.len3(axis)
          return nil if l < 1e-9

          w = Mesh.scale3(axis, 1.0 / l)
          helper = w[2].abs < 0.9 ? [0.0, 0.0, 1.0] : [1.0, 0.0, 0.0]
          u = Mesh.normalize3(Mesh.cross3(helper, w))
          [u, Mesh.cross3(w, u)]
        end

        def self.tapered_tube(p1, p2, r1, r2, n)
          f = frame(p1, p2)
          return [] unless f

          u, v = f
          ring = ->(p, r) { Array.new(n) { |i| t = 2 * Math::PI * i / n; Mesh.add3(p, Mesh.add3(Mesh.scale3(u, Math.cos(t) * r), Mesh.scale3(v, Math.sin(t) * r))) } }
          bottom = ring.call(p1, r1)
          top = ring.call(p2, r2)
          polys = [bottom.reverse, top]
          n.times do |i|
            j = (i + 1) % n
            polys << [bottom[i], bottom[j], top[j], top[i]]
          end
          Mesh.fix_orientation(polys)
        end

        def self.square_tube(p1, p2, w, h)
          f = frame(p1, p2)
          return [] unless f

          u, v = f
          # keep rectangular sections upright for horizontal members
          axis = Mesh.normalize3(Mesh.sub3(p2, p1))
          if axis[2].abs < 0.95
            u = Mesh.normalize3(Mesh.cross3([0.0, 0.0, 1.0], axis))
            v = Mesh.cross3(axis, u)
          end
          c = Mesh.sub3(Mesh.sub3(p1, Mesh.scale3(u, w / 2.0)), Mesh.scale3(v, h / 2.0))
          Mesh.block(c, Mesh.sub3(p2, p1), Mesh.scale3(u, w), Mesh.scale3(v, h))
        end

        ICO_T = (1.0 + Math.sqrt(5.0)) / 2.0
        ICO_V = [[-1, ICO_T, 0], [1, ICO_T, 0], [-1, -ICO_T, 0], [1, -ICO_T, 0], [0, -1, ICO_T], [0, 1, ICO_T], [0, -1, -ICO_T], [0, 1, -ICO_T],
                 [ICO_T, 0, -1], [ICO_T, 0, 1], [-ICO_T, 0, -1], [-ICO_T, 0, 1]].freeze
        ICO_F = [[0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11], [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6], [7, 1, 8],
                 [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9], [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1]].freeze

        # Unit icosphere subdivided `level` times: [vertices, faces].
        def self.icosphere(level)
          verts = ICO_V.map { |v| Mesh.normalize3(v.map(&:to_f)) }
          faces = ICO_F.map(&:dup)
          level.times do
            cache = {}
            mid = lambda do |a, b|
              key = a < b ? [a, b] : [b, a]
              cache[key] ||= begin
                verts << Mesh.normalize3(Mesh.scale3(Mesh.add3(verts[a], verts[b]), 0.5))
                verts.length - 1
              end
            end
            faces = faces.flat_map do |(a, b, c)|
              ab = mid.call(a, b)
              bc = mid.call(b, c)
              ca = mid.call(c, a)
              [[a, ab, ca], [b, bc, ab], [c, ca, bc], [ab, bc, ca]]
            end
          end
          [verts, faces]
        end

        def self.blob_polys(c, rx, ry, rz, rough, level, rng)
          verts, faces = icosphere(level)
          scaled = verts.map do |v|
            k = 1.0 + (rough * ((rng.rand * 2.0) - 1.0))
            [c[0] + (v[0] * rx * k), c[1] + (v[1] * ry * k), c[2] + (v[2] * rz * k)]
          end
          polys = faces.map do |(a, b, cc)|
            tri = [scaled[a], scaled[b], scaled[cc]]
            centroid = Mesh.scale3(Mesh.add3(Mesh.add3(tri[0], tri[1]), tri[2]), 1.0 / 3)
            Mesh.dot3(Mesh.newell(tri), Mesh.sub3(centroid, c)).negative? ? tri.reverse : tri
          end
          Mesh.fix_orientation(polys)
        end
      end
    end
  end
end
