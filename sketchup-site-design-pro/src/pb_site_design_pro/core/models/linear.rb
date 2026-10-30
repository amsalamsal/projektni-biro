# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Models
      # Linear elements drawn along the element path (package frame): walls, kerbs, hedges and fences. Profiles
      # use u = offset to the left of the path direction and w = height above the path point, so every model
      # follows the path elevations exactly like the parametric builder it replaces.
      module Lin
        module_function

        def sweep(k, pts, profile, closed, material, name, smooth: false)
          k.mesh(Mesh.sweep(pts, profile, closed), material, name, smooth: smooth)
        end

        def rect(left, right, bottom, top)
          [[left, bottom], [left, top], [-right, top], [-right, bottom]]
        end

        # Left/right extent of a wall of thickness t for the alignment parameter.
        def sides(e, t)
          case e.text('alignment', 'center')
          when 'left' then [t, 0.0]
          when 'right' then [0.0, t]
          else [t / 2, t / 2]
          end
        end

        # Points every `spacing` metres along a polyline (both ends included): [[point, unit dir, left normal], ...]
        def along(pts, closed, spacing, ends: true)
          ring = closed ? pts + [pts.first] : pts
          out = []
          ring.each_cons(2) do |a, b|
            d = Geometry.dist(a, b)
            next if d < 1e-6

            dir = Geometry.norm(Geometry.sub(b, a))
            n = [(d / spacing).round, 1].max
            (0...n).each do |i|
              t = (i + 0.5) / n
              t = i.to_f / n if ends
              out << [Geometry.lerp3(a, b, t), dir, Geometry.perp(dir)]
            end
          end
          if ends && !closed && ring.length > 1
            a, b = ring[-2], ring[-1]
            dir = Geometry.norm(Geometry.sub(b, a))
            out << [b, dir, Geometry.perp(dir)]
          end
          out
        end

        def at(p, nrm, u, w)
          [p[0] + (nrm[0] * u), p[1] + (nrm[1] * u), (p[2] || 0.0) + w]
        end

        # Wall with coping and footing. The body keeps the builder contract ('Zid' between the alignment sides,
        # 'Temelj' centred under it); the coping sits within the wall height.
        def wall(k, e, coping: 0.0, overhang: 0.03, coping_mat: 'concrete_precast', piers: nil)
          pts = k.path
          closed = e.geometry[:closed]
          t = e.num('thickness', 0.2)
          h = e.num('height', 1.0)
          z0 = e.num('baseOffset', 0.0)
          left, right = sides(e, t)
          cap = coping.positive? && h > coping * 2 ? coping : 0.0
          sweep(k, pts, rect(left, right, z0, z0 + h - cap), closed, k.mat('material', 'concrete'), 'Zid')
          sweep(k, pts, rect(left + overhang, right + overhang, z0 + h - cap, z0 + h), closed, coping_mat, 'Pokrivnica') if cap.positive?
          if piers
            along(pts, closed, piers).each do |p, dir, nrm|
              c = at(p, nrm, (left - right) / 2.0, 0)
              k.box(c[0], c[1], c[2] + z0, t + 0.12, t + 0.12, h + 0.06, k.mat('material', 'brick'), 'Stub zida', angle: Math.atan2(dir[1], dir[0]))
            end
          end
          fw = e.num('footingWidth', 0.0)
          fd = e.num('footingDepth', 0.0)
          return unless fw.positive? && fd.positive?

          centre = (left - right) / 2.0
          sweep(k, pts, rect(centre + (fw / 2), (fw / 2) - centre, z0 - fd, z0), closed, 'concrete', 'Temelj')
        end

        # Gabion: stone fill in welded mesh baskets (1 m long, 0.5 m layers) with visible wires on both faces.
        def gabion(k, e)
          wall(k, e)
          return unless k.detail.positive?

          pts = k.path
          closed = e.geometry[:closed]
          t = e.num('thickness', 0.5)
          h = e.num('height', 1.0)
          z0 = e.num('baseOffset', 0.0)
          left, right = sides(e, t)
          wire = 0.004
          polys = []
          along(pts, closed, 1.0).each do |p, _dir, nrm|
            [left + wire, -right - wire].each do |u|
              a = at(p, nrm, u, z0)
              polys.concat(Kit.square_tube(a, [a[0], a[1], a[2] + h], wire * 2, wire * 2))
            end
          end
          layers = [(h / 0.5).round, 1].max
          (0..layers).each do |i|
            z = z0 + (i * h / layers)
            [left + wire, -right - wire].each do |u|
              polys.concat(Mesh.sweep(pts, rect(u + wire, -u + wire, z - wire, z + wire), closed))
            end
          end
          k.mesh(polys, 'steel_galv', 'Žičana korpa')
        end

        # Kerbs and edges with their real cross-sections (road side to the right of the path).
        def kerb(k, e, kind)
          pts = k.path
          closed = e.geometry[:closed]
          t = e.num('thickness', 0.15)
          h = e.num('height', 0.3)
          z0 = e.num('baseOffset', 0.0)
          left, right = sides(e, t)
          m = k.mat('material', 'concrete_precast')
          prof = case kind
                 when :kerb # chamfered face towards the road
                   [[left, z0], [left, z0 + h], [-right + 0.04, z0 + h], [-right, z0 + h - 0.06], [-right, z0]]
                 when :flush # rounded top edges
                   [[left, z0], [left, z0 + h - 0.015], [left - 0.015, z0 + h], [-right + 0.015, z0 + h], [-right, z0 + h - 0.015], [-right, z0]]
                 when :l_edge # precast L-element: stem and foot
                   foot = [h * 0.6, 0.3].min
                   [[left, z0], [left, z0 + h], [left - t, z0 + h], [left - t, z0 + 0.1], [-right - foot, z0 + 0.1], [-right - foot, z0]]
                 else
                   rect(left, right, z0, z0 + h)
                 end
          sweep(k, pts, prof, closed, m, 'Zid')
          sweep(k, pts, rect(left + 0.08, right + 0.08, z0 - 0.1, z0), closed, 'concrete', 'Betonsko ležište') if %i[kerb flush].include?(kind)
          return unless kind == :edging && k.detail.positive?

          along(pts, closed, 1.0).each do |p, _dir, nrm|
            a = at(p, nrm, (left - right) / 2.0 - 0.01, z0 - 0.15)
            k.bar(a, [a[0], a[1], z0 + h - 0.03], 0.006, 'steel_dark', 'Ankerni klin', n: 6)
          end
        end

        # Clipped hedge: rounded section plus leaf clusters breaking up the faces.
        def hedge(k, e)
          pts = k.path
          closed = e.geometry[:closed]
          t = e.num('thickness', 0.8)
          h = e.num('height', 1.5)
          z0 = e.num('baseOffset', 0.0)
          left, right = sides(e, t)
          m = k.mat('material', 'foliage_evergreen')
          c = (left - right) / 2.0
          hw = t / 2
          prof = [[c + (hw * 0.82), z0], [c + hw, z0 + (h * 0.35)], [c + (hw * 0.96), z0 + (h * 0.88)], [c + (hw * 0.7), z0 + h],
                  [c - (hw * 0.7), z0 + h], [c - (hw * 0.96), z0 + (h * 0.88)], [c - hw, z0 + (h * 0.35)], [c - (hw * 0.82), z0]]
          sweep(k, pts, prof, closed, m, 'Zid', smooth: true)
          return unless k.detail.positive?

          step = k.detail >= 2 ? 0.45 : 0.8
          specs = []
          along(pts, closed, step, ends: false).each do |p, _dir, nrm|
            [hw * 0.92, -hw * 0.92].each do |u|
              specs << [at(p, nrm, c + u, z0 + k.rand(h * 0.3, h * 0.8)), 0.18, 0.18, 0.2]
            end
            specs << [at(p, nrm, c + k.rand(-hw * 0.5, hw * 0.5), z0 + h - 0.04), 0.22, 0.22, 0.12]
          end
          k.blobs(specs, m, 'Lisna masa', rough: 0.3, level: 1)
          along(pts, closed, 0.9, ends: false).each do |p, _dir, nrm|
            a = at(p, nrm, c, z0)
            k.bar(a, [a[0], a[1], a[2] + 0.35], 0.03, 'bark', 'Stablo', n: 6)
          end
        end

        # Green wall: steel support frame with a planted face (plant cushions on a grid) on the -side.
        def green_wall(k, e)
          pts = k.path
          closed = e.geometry[:closed]
          t = e.num('thickness', 0.2)
          h = e.num('height', 3.0)
          z0 = e.num('baseOffset', 0.0)
          left, right = sides(e, t)
          sweep(k, pts, rect(left, -left + 0.06, z0, z0 + h), closed, 'steel_dark', 'Nosiva konstrukcija')
          sweep(k, pts, rect(left - 0.06, right, z0 + 0.15, z0 + h - 0.1), closed, k.mat('material', 'foliage'), 'Zid')
          sweep(k, pts, rect(left - 0.06, right + 0.05, z0, z0 + 0.15), closed, 'steel_dark', 'Žlijeb za drenažu')
          return unless k.detail.positive?

          rows = [((h - 0.3) / 0.35).floor, 1].max
          mats = %w[foliage foliage_dark foliage_light planting_flowers]
          along(pts, closed, 0.35, ends: false).each_with_index do |(p, _dir, nrm), i|
            rows.times do |r|
              z = z0 + 0.3 + (r * (h - 0.4) / rows)
              k.blob(at(p, nrm, -right - 0.04, z), 0.16, 0.16, 0.16, mats[(i + (r * 3)) % mats.length], 'Biljke', rough: 0.35, level: k.detail >= 2 ? 1 : 0)
            end
          end
        end

        # Mural: rendered substrate wall with a painted composition (thin paint layers on the -side face).
        def mural(k, e)
          pts = k.path
          closed = e.geometry[:closed]
          h = e.num('height', 2.5)
          z0 = e.num('baseOffset', 0.0)
          t = [e.num('thickness', 0.01), 0.01].max
          left, right = sides(e, t)
          sweep(k, pts, rect(left, right, z0, z0 + h), closed, 'render_white', 'Zid')
          colours = %w[flower_red flower_yellow plastic_blue foliage flower_violet flower_pink]
          i = 0
          along(pts, closed, 1.2, ends: false).each do |p, dir, nrm|
            2.times do |r|
              c = at(p, nrm, -right - 0.002, 0)
              w = k.rand(0.6, 1.2)
              hh = k.rand(0.5, h * 0.45)
              z = z0 + 0.15 + (r * h * 0.45) + k.rand(0, 0.1)
              k.mesh(Mesh.block([c[0] - (dir[0] * w / 2), c[1] - (dir[1] * w / 2), c[2] + z], [dir[0] * w, dir[1] * w, 0.0],
                                [-nrm[0] * 0.003, -nrm[1] * 0.003, 0.0], [0.0, 0.0, [hh, z0 + h - z - 0.05].min]),
                     colours[i % colours.length], 'Oslik')
              i += 1
            end
          end
        end

        # Water wall: stone wall with a spill lip, water film on the -side face and a catch trough.
        def cascade(k, e)
          wall(k, e, coping: 0.06, coping_mat: 'stainless', overhang: 0.06)
          pts = k.path
          closed = e.geometry[:closed]
          t = e.num('thickness', 0.3)
          h = e.num('height', 1.2)
          z0 = e.num('baseOffset', 0.0)
          left, right = sides(e, t)
          sweep(k, pts, rect(-right - 0.005, right + 0.02, z0 + 0.25, z0 + h - 0.06), closed, 'water', 'Vodeni zastor')
          sweep(k, pts, rect(-right, right + 0.45, z0, z0 + 0.3), closed, 'stone_wall', 'Prihvatno korito')
          sweep(k, pts, rect(-right - 0.05, right + 0.4, z0 + 0.3, z0 + 0.32), closed, 'water', 'Voda u koritu') if left >= 0
        end

        # Fence: parametric posts and panels plus caps, handrails, finials and glass clamps.
        def fence(k, e, cap: :flat, handrail: false, finials: false, clamps: false)
          k.builder_draw
          return if k.detail.zero?

          g = e.geometry
          h = e.num('height', 1.8)
          post = e.num('postSize', 0.06)
          stations = Geometry.stations(g[:vertices], g[:bulges], g[:closed], e.num('postSpacing', 2.5))
          pm = k.mat('postMaterial', 'steel_galv')
          stations.each do |s|
            z = (s[2] || 0.0) + h + 0.02
            case cap
            when :ball then k.sphere([s[0], s[1], z + (post * 0.45)], post * 0.6, pm, 'Kapa stuba', n: 10)
            when :pyramid then k.mesh(Mesh.fix_orientation(Kit.lathe_polys([[0, 0], [post * 0.75, 0], [0, post * 0.9]], 0, 0, 4, false)).map { |pl| pl.map { |q| [q[0] + s[0], q[1] + s[1], q[2] + z] } }, pm, 'Kapa stuba')
            else k.box(s[0], s[1], z - 0.015, post + 0.02, post + 0.02, 0.015, pm, 'Kapa stuba') # within the post height
            end
          end
          if handrail
            pts = stations.map { |s| [s[0], s[1], (s[2] || 0.0) + h + 0.04] }
            pts << pts.first if g[:closed] && pts.length > 2
            k.rail(pts, 0.021, 'stainless', 'Rukohvat')
          end
          if clamps
            stations.each do |s|
              [0.15, h - 0.2].each { |z| k.box(s[0], s[1], (s[2] || 0.0) + z, post + 0.03, post + 0.03, 0.05, 'stainless', 'Stezaljka stakla') }
            end
          end
          return unless finials && k.detail >= 2

          tops = []
          stations.each_cons(2) do |a, b|
            d = Geometry.dist(a, b)
            n = [(d / 0.12).floor, 1].max
            n.times do |i|
              t = (i + 0.5) / n
              p = Geometry.lerp3(a, b, t)
              tops << [p[0], p[1], (p[2] || 0.0) + h - 0.01]
            end
          end
          k.mesh(tops.flat_map { |p| Mesh.fix_orientation(Kit.lathe_polys([[0, 0], [0.018, 0], [0, 0.07]], 0, 0, 4, false)).map { |pl| pl.map { |q| [q[0] + p[0], q[1] + p[1], q[2] + p[2]] } } }, pm, 'Vrh šipke')
        end
      end

      define('wall.concrete', geometry: 'path') { |k, e| Lin.wall(k, e, coping: 0.05) }
      define('wall.stone', geometry: 'path') { |k, e| Lin.wall(k, e, coping: 0.07, coping_mat: 'natural_stone', overhang: 0.04) }
      define('wall.retaining', geometry: 'path') { |k, e| Lin.wall(k, e, coping: 0.06) }
      define('wall.planter_wall', geometry: 'path') { |k, e| Lin.wall(k, e, coping: 0.05, coping_mat: 'natural_stone') }
      define('decorative.decorative_wall', geometry: 'path') { |k, e| Lin.wall(k, e, coping: 0.06, coping_mat: 'natural_stone', piers: 3.0) }
      define('wall.gabion', geometry: 'path') { |k, e| Lin.gabion(k, e) }
      define('water.cascade_wall', geometry: 'path') { |k, e| Lin.cascade(k, e) }
      define('decorative.mural', geometry: 'path') { |k, e| Lin.mural(k, e) }
      define('landscape.hedge', geometry: 'path') { |k, e| Lin.hedge(k, e) }
      define('landscape.green_wall', geometry: 'path') { |k, e| Lin.green_wall(k, e) }
      define('edge.kerb', geometry: 'path') { |k, e| Lin.kerb(k, e, :kerb) }
      define('edge.flush_kerb', geometry: 'path') { |k, e| Lin.kerb(k, e, :flush) }
      define('edge.retaining_edge', geometry: 'path') { |k, e| Lin.kerb(k, e, :l_edge) }
      define('edge.edging', geometry: 'path') { |k, e| Lin.kerb(k, e, :edging) }

      define('wall.metal_fence', geometry: 'path') { |k, e| Lin.fence(k, e, cap: :pyramid) }
      define('wall.decorative_fence', geometry: 'path') { |k, e| Lin.fence(k, e, cap: :ball, finials: true) }
      define('wall.mesh_fence', geometry: 'path') { |k, e| Lin.fence(k, e) }
      define('landscape.tree_protection_barrier', geometry: 'path') { |k, e| Lin.fence(k, e) }
      define('wall.timber_fence', geometry: 'path') { |k, e| Lin.fence(k, e, cap: :pyramid) }
      define('wall.glass_railing', geometry: 'path') { |k, e| Lin.fence(k, e, handrail: true, clamps: true) }
      define('wall.safety_railing', geometry: 'path') { |k, e| Lin.fence(k, e, handrail: true) }
      define('climate.wind_screen', geometry: 'path') { |k, e| Lin.fence(k, e, clamps: true) }
      define('decorative.screen', geometry: 'path') { |k, e| Lin.fence(k, e) }
    end
  end
end
