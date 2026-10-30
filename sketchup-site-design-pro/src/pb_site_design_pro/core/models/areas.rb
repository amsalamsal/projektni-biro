# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Models
      # Area and structure models drawn on the element's own outline (package frame): pools, ponds, basins,
      # planters, steps, ramps, pergolas, sails, decks and courts. Builder contracts (levels, envelopes, part
      # names) are kept; the models add the parts a real object has (coping, ladders, plants, handrails ...).
      module Area
        module_function

        def ring_ccw(points)
          Geometry.ccw(Geometry.simplify_collinear(points))
        end

        # ---- pools -------------------------------------------------------------------------------------------

        def pool(k, e, kind)
          inner, = k.outline
          inner = ring_ccw(inner)
          z = inner.first[2] || 0.0
          depth = e.num('depth', 1.5)
          ft = e.num('floorThickness', 0.25)
          wt = e.num('wallThickness', 0.25)
          finish = k.mat('finishMaterial', 'pool_finish')
          outer = Geometry.offset_ring(inner, wt)
          cap = kind == :pond ? 0.0 : 0.05
          k.prism(outer, z - depth - ft, z - cap, finish, 'Zidovi bazena', holes: [inner]) if outer
          k.prism(inner, z - depth - ft, z - depth, finish, 'Dno bazena')
          water_top = z - e.num('waterLevelOffset', 0.1)
          k.prism(inner, z - depth, water_top, k.mat('waterMaterial', 'water'), 'Voda') if water_top > z - depth + 1e-4
          return unless outer

          edges = inner.each_index.map { |i| [inner[i], inner[(i + 1) % inner.length]] }
          longest = edges.each_index.max_by { |i| Geometry.dist(*edges[i]) }
          case kind
          when :pond then pond_edge(k, e, inner, z, water_top)
          when :infinity then infinity_edge(k, e, inner, edges, longest, z, wt, water_top)
          else coping(k, inner, z, wt, kind == :reflecting ? 'natural_stone' : 'concrete_slab', cap)
          end
          return unless kind == :swimming && k.detail.positive?

          ladder(k, edges[longest], z, depth)
          lights(k, edges[(longest + 2) % edges.length], z, water_top)
        end

        # Coping slabs over the wall with 3 cm overhang into the pool (top = pool edge level).
        def coping(k, inner, z, wt, material, cap)
          lip = Geometry.offset_ring(inner, -0.03) || inner
          outer = Geometry.offset_ring(inner, wt)
          k.prism(outer, z - cap, z, material, 'Obloga ruba', holes: [lip]) if outer
        end

        # Infinity edge: coping on all sides except the longest, which is a weir at water level with an overflow
        # gutter outside it.
        def infinity_edge(k, e, inner, edges, longest, z, wt, water_top)
          path = (0...inner.length).map { |i| inner[(longest + 1 + i) % inner.length] }
          Lin.sweep(k, path, [[0.03, -0.05], [0.03, 0.0], [-wt, 0.0], [-wt, -0.05]], false, 'concrete_slab', 'Obloga ruba')
          a, b = edges[longest]
          weir = [a, b]
          Lin.sweep(k, weir, [[0.0, -0.05], [0.0, water_top - z], [-wt, water_top - z - 0.02], [-wt, -0.05]], false, 'pool_finish', 'Preljevni rub')
          Lin.sweep(k, weir, [[-wt, -0.6], [-wt, -0.05], [-wt - 0.45, -0.05], [-wt - 0.45, -0.6]], false, 'pool_finish', 'Preljevni kanal')
          Lin.sweep(k, weir, [[-wt - 0.02, -0.25], [-wt - 0.02, -0.12], [-wt - 0.43, -0.12], [-wt - 0.43, -0.25]], false, k.mat('waterMaterial', 'water'), 'Voda u kanalu')
        end

        # Natural pond edge: stones along the waterline, water lilies and reeds.
        def pond_edge(k, e, inner, z, water_top)
          stones = Lin.along(inner + [inner.first], false, 0.45, ends: false).map do |p, _d, n|
            s = k.rand(0.14, 0.26)
            [Lin.at(p, n, -s * 0.3, s * 0.15), s, s * k.rand(0.8, 1.1), s * 0.55]
          end
          k.blobs(stones, 'stone_wall', 'Kamenje uz rub', rough: 0.25, level: 1)
          return if k.detail.zero?

          xs = inner.map { |p| p[0] }
          ys = inner.map { |p| p[1] }
          pads = []
          6.times do
            x = k.rand(xs.min, xs.max)
            y = k.rand(ys.min, ys.max)
            next unless Geometry.point_in_polygon?([x, y], Geometry.offset_ring(inner, -0.4) || inner)

            pads << [x, y]
          end
          pads.each_with_index do |(x, y), i|
            r = k.rand(0.12, 0.2)
            k.cyl(x, y, water_top, r, 0.01, 'foliage_light', 'Lopoč', n: 10)
            k.sphere([x + (r * 0.3), y, water_top + 0.03], 0.04, i.even? ? 'flower_white' : 'flower_pink', 'Cvijet lopoča', n: 8) if k.detail >= 2
          end
          Lin.along(inner + [inner.first], false, 1.6, ends: false).first(5).each do |p, _d, n|
            at = Lin.at(p, n, 0.25, water_top - z)
            k.place('trska', at, k.rand(0, 6.28)) { |pk| Veg.grass_tuft(pk, 0.9, 0.18, 'grass_blade') }
          end
        end

        # Stainless pool ladder hanging over the coping at the middle of an edge.
        def ladder(k, edge, z, depth)
          a, b = edge
          m = Geometry.lerp3(a, b, 0.5)
          dir = Geometry.norm(Geometry.sub(b, a))
          n = Geometry.perp(dir) # into the pool (ring is counter-clockwise)
          [-0.25, 0.25].each do |s|
            base = [m[0] + (dir[0] * s), m[1] + (dir[1] * s)]
            pt = ->(u, w) { [base[0] + (n[0] * u), base[1] + (n[1] * u), z + w] }
            k.rail([pt.call(-0.35, 0.0), pt.call(-0.35, 0.75), pt.call(-0.1, 0.95), pt.call(0.12, 0.6), pt.call(0.12, -[depth - 0.4, 0.6].max)], 0.02, 'stainless', 'Ljestve')
          end
          3.times do |i|
            w = -0.3 - (i * 0.28)
            c = [m[0] + (n[0] * 0.14), m[1] + (n[1] * 0.14), z + w]
            k.box(c[0], c[1], c[2], 0.46, 0.08, 0.03, 'stainless', 'Gazište ljestvi', angle: Math.atan2(dir[1], dir[0]))
          end
        end

        def lights(k, edge, z, water_top)
          a, b = edge
          dir = Geometry.norm(Geometry.sub(b, a))
          n = Geometry.perp(dir)
          [0.3, 0.7].each do |t|
            p = Geometry.lerp3(a, b, t)
            c = [p[0] + (n[0] * 0.005), p[1] + (n[1] * 0.005), [water_top - 0.45, z - 0.6].min]
            k.bar(c, [c[0] + (n[0] * 0.02), c[1] + (n[1] * 0.02), c[2]], 0.09, 'luminaire', 'Podvodna svjetiljka', n: 16)
          end
        end

        # ---- basins ------------------------------------------------------------------------------------------

        def basin(k, e, kind)
          k.builder_draw
          return if k.detail.zero?

          top, = k.outline
          top = ring_ccw(top)
          z = top.first[2] || 0.0
          depth = e.num('depth', 0.5)
          slope = e.num('sideSlope', 3.0)
          bottom = [slope * depth, slope * depth * 0.5, slope * depth * 0.25].map { |d| Geometry.offset_ring(top, -d) }.find { |r| r && r.length == top.length }
          return unless bottom

          zb = z - depth
          case kind
          when :rain_garden
            pts = k.scatter(bottom, [], 0.6, inset: 0.15, limit: 80)
            pts.each_with_index do |(x, y), i|
              v = i % 3
              k.place("kisni-vrt-#{v}", [x, y, zb], k.rand(0, 6.28)) do |pk|
                case v
                when 0 then Veg.grass_tuft(pk, 0.6, 0.25, 'grass_blade')
                when 1 then Veg.flower_clump(pk, 0.25, 0.5, 'foliage', %w[flower_violet])
                else Veg.shrub(pk, 0.7, 0.6, 'foliage_dark')
                end
              end
            end
            stone_inlet(k, top, z, zb)
          when :bioswale
            k.scatter(bottom, [], 0.5, inset: 0.1, limit: 120).each_with_index do |(x, y), i|
              k.place("bioswale-#{i % 2}", [x, y, zb], k.rand(0, 6.28)) { |pk| Veg.grass_tuft(pk, i.even? ? 0.5 : 0.35, 0.2, i.even? ? 'grass_blade' : 'meadow') }
            end
            stone_inlet(k, top, z, zb)
          else
            stone_inlet(k, top, z, zb, headwall: true)
            k.scatter(bottom, [], 1.2, inset: 0.3, limit: 30).each do |x, y|
              k.place('retencija-trska', [x, y, zb + e.num('waterDepth', 0.0)], k.rand(0, 6.28)) { |pk| Veg.grass_tuft(pk, 0.8, 0.2, 'grass_blade') }
            end
          end
        end

        # Inlet on the first edge: rip-rap stones down the slope (plus a concrete headwall with pipe for basins).
        def stone_inlet(k, top, z, zb, headwall: false)
          a, b = top[0], top[1]
          m = Geometry.lerp3(a, b, 0.5)
          dir = Geometry.norm(Geometry.sub(b, a))
          n = Geometry.perp(dir) # into the basin
          stones = 9.times.map do |i|
            t = (i / 3) / 3.0
            s = (i % 3) - 1
            p = [m[0] + (n[0] * (0.2 + (t * 1.2))) + (dir[0] * s * 0.35), m[1] + (n[1] * (0.2 + (t * 1.2))) + (dir[1] * s * 0.35), z - ((z - zb) * t * 0.9)]
            [p, 0.18, 0.15, 0.1]
          end
          k.blobs(stones, 'stone_wall', 'Kamena obloga ulaza', rough: 0.3, level: 1)
          return unless headwall

          hz = [zb, z - 0.7].max
          k.box(m[0] - (n[0] * 0.1), m[1] - (n[1] * 0.1), hz, 1.2, 0.2, z + 0.1 - hz, 'concrete', 'Ulazna glava', angle: Math.atan2(dir[1], dir[0]))
          k.bar([m[0] - (n[0] * 0.3), m[1] - (n[1] * 0.3), hz + 0.25], [m[0] + (n[0] * 0.05), m[1] + (n[1] * 0.05), hz + 0.25], 0.15, 'concrete_precast', 'Ulazna cijev', n: 16)
        end

        # ---- planters ----------------------------------------------------------------------------------------

        def planter(k, e, coping_mat)
          outer, = k.outline
          outer = ring_ccw(outer)
          z0 = outer.first[2] || 0.0
          h = e.num('height', 0.6)
          wt = e.num('wallThickness', 0.1)
          inner = Geometry.offset_ring(outer, -wt)
          return k.builder_draw unless inner

          m = k.mat('material', 'concrete')
          cap = coping_mat && h > 0.2 ? 0.04 : 0.0
          k.prism(outer, z0, z0 + h - cap, m, 'Zid žardinjere', holes: [inner])
          if cap.positive?
            lip = Geometry.offset_ring(outer, 0.02) || outer
            k.prism(lip, z0 + h - cap, z0 + h, coping_mat, 'Pokrivnica', holes: [Geometry.offset_ring(inner, 0.01) || inner])
          end
          soil_top = z0 + h - e.num('soilDepthBelowTop', 0.05)
          k.prism(inner, z0, soil_top, k.mat('soilMaterial', 'soil'), 'Zemlja')
          ph = e.num('plantHeight', 0.5)
          return if ph <= 0 || k.detail.zero?

          spacing = k.detail >= 2 ? 0.45 : 0.6
          k.scatter(inner, [], spacing, inset: 0.15, limit: 150).each_with_index do |(x, y), i|
            v = i % 4
            k.place("zardinjera-#{v}-#{format('%.3g', ph)}", [x, y, soil_top], k.rand(0, 6.28)) do |pk|
              case v
              when 0 then Veg.shrub(pk, ph, ph * 0.9, 'foliage')
              when 1 then Veg.grass_tuft(pk, ph * 0.9, ph * 0.35, 'grass_blade')
              when 2 then Veg.flower_clump(pk, ph * 0.4, ph * 0.7, 'foliage', %w[flower_white flower_violet])
              else Veg.shrub(pk, ph * 0.8, ph * 0.8, 'foliage_dark')
              end
            end
          end
        end

        # ---- steps and ramps ---------------------------------------------------------------------------------

        # Handrail along the side at `a` (distance across), over a profile of [v, z] points; posts every <= 1.5 m.
        def handrail(k, r, a, profile, height, extension: 0.3)
          pts = profile.map { |(v, w)| Geometry.rect_point(r, a, v, r[:z] + w + height) }
          first = profile.first
          last = profile.last
          pts.unshift(Geometry.rect_point(r, a, first[0] - extension, r[:z] + first[1] + height)) if extension.positive?
          pts.push(Geometry.rect_point(r, a, last[0] + extension, r[:z] + last[1] + height)) if extension.positive?
          k.rail(pts, 0.021, 'stainless', 'Rukohvat')
          length = profile.last[0] - profile.first[0]
          n = [(length / 1.5).ceil, 1].max
          (0..n).each do |i|
            v = profile.first[0] + (i * length / n)
            w = interp(profile, v)
            base = Geometry.rect_point(r, a, v, r[:z] + w)
            k.bar(base, [base[0], base[1], base[2] + height], 0.02, 'stainless', 'Stub rukohvata', n: 8)
            k.cyl(base[0], base[1], base[2], 0.05, 0.008, 'stainless', 'Pločica stuba', n: 10) if k.detail >= 2
          end
        end

        def interp(profile, v)
          profile.each_cons(2) do |(v0, w0), (v1, w1)|
            next unless v.between?(v0 - 1e-9, v1 + 1e-9)

            return v1 - v0 < 1e-9 ? w1 : w0 + ((w1 - w0) * (v - v0) / (v1 - v0))
          end
          profile.last[1]
        end

        def steps(k, e)
          k.builder_draw
          return if k.detail.zero?

          outer, = k.outline
          r = Geometry.oriented_rect(outer)
          rise = e.num('totalRise', 0.6)
          n = [(rise / e.num('riserHeight', 0.15) - 1e-9).ceil, 1].max
          riser = rise / n
          going = r[:length_v] / n
          # contrasting nosing strips (accessibility) on every tread edge
          n.times do |i|
            p = Geometry.rect_point(r, 0.0, i * going, r[:z] + ((i + 1) * riser))
            k.mesh(Mesh.block(p, [r[:u][0] * r[:length_u], r[:u][1] * r[:length_u], 0.0], [r[:v][0] * 0.05, r[:v][1] * 0.05, 0.0], [0.0, 0.0, 0.004]), 'steel_dark', 'Kontrastna traka')
          end
          line = [[going * 0.5, riser]] + (1...n).map { |i| [(i + 0.5) * going, (i + 1) * riser] }
          line = [[0.0, riser], [r[:length_v], rise]] if n < 2
          [0.08, r[:length_u] - 0.08].each { |a| handrail(k, r, a, [[line.first[0], line.first[1]], [line.last[0], line.last[1]]], 0.9) }
        end

        def ramp(k, e)
          k.builder_draw
          return if k.detail.zero?

          outer, = k.outline
          r = Geometry.oriented_rect(outer)
          rise = e.num('rise', 0.3)
          lv = r[:length_v]
          [0.06, r[:length_u] - 0.06].each do |a|
            # 5 cm upstand (wheel guide) and handrails at 0.9 m and 0.7 m
            base = Geometry.rect_point(r, a, 0.0, r[:z])
            top = Geometry.rect_point(r, a, lv, r[:z] + rise)
            k.mesh(Mesh.block([base[0] - (r[:u][0] * 0.03), base[1] - (r[:u][1] * 0.03), base[2]], [r[:u][0] * 0.06, r[:u][1] * 0.06, 0.0],
                              [top[0] - base[0], top[1] - base[1], rise], [0.0, 0.0, 0.05]), 'concrete', 'Rubnik rampe')
            handrail(k, r, a, [[0.0, 0.0], [lv, rise]], 0.9)
            pts = [[0.0, 0.0], [lv, rise]].map { |(v, w)| Geometry.rect_point(r, a, v, r[:z] + w + 0.7) }
            k.rail(pts, 0.018, 'stainless', 'Donji rukohvat')
          end
        end

        # ---- pergolas, canopies ------------------------------------------------------------------------------

        def column_points(e, r)
          cs = e.num('columnSize', 0.15)
          st = lambda do |len|
            span = len - cs
            n = [(span / e.num('columnSpacing', 3.5) - 1e-9).ceil, 1].max
            Array.new(n + 1) { |i| (cs / 2) + (span * i / n) }
          end
          us = st.call(r[:length_u])
          vs = st.call(r[:length_v])
          pts = []
          us.each { |a| pts << [a, cs / 2] << [a, r[:length_v] - (cs / 2)] }
          vs[1...-1].each { |b| pts << [cs / 2, b] << [r[:length_u] - (cs / 2), b] } if vs.length > 2
          pts.uniq
        end

        def pergola(k, e, climbers: false, gutter: false)
          k.builder_draw
          return if k.detail.zero?

          outer, = k.outline
          r = Geometry.oriented_rect(outer)
          cs = e.num('columnSize', 0.15)
          h = e.num('height', 2.6)
          cols = column_points(e, r).map { |(a, b)| Geometry.rect_point(r, a, b) }
          bd = e.num('beamDepth', 0.25)
          centre = Geometry.rect_point(r, r[:length_u] / 2, r[:length_v] / 2)
          if climbers
            # climbers on every other column, kept inside the footprint and below the rafters
            cols.each_with_index do |p, i|
              next unless i.even?

              inward = Geometry.norm([centre[0] - p[0], centre[1] - p[1]])
              at = ->(d, z) { [p[0] + (inward[0] * d), p[1] + (inward[1] * d), p[2] + z] }
              n = k.detail >= 2 ? 9 : 5
              specs = Array.new(n) do |j|
                t = (j + 0.5) / n
                [at.call(cs * 0.5 + 0.12 + (0.04 * Math.sin(j * 2.1)), t * h), 0.11, 0.11, 0.18]
              end
              specs << [at.call(0.55, h + (bd * 0.5)), 0.35, 0.35, 0.12]
              k.blobs(specs, 'foliage', 'Puzavica', rough: 0.15, level: 1)
            end
          end
          return unless gutter

          a0 = Geometry.rect_point(r, -0.05, -0.08, r[:z] + h + bd)
          a1 = Geometry.rect_point(r, r[:length_u] + 0.05, -0.08, r[:z] + h + bd)
          k.bar(a0, a1, 0.05, 'steel_galv', 'Oluk', n: 10)
          k.bar([a0[0], a0[1], a0[2]], [a0[0], a0[1], r[:z] + 0.1], 0.04, 'steel_galv', 'Odvodna cijev', n: 10)
        end

        # ---- sails and membranes -----------------------------------------------------------------------------

        # Four-corner tensioned membrane: hypar surface with catenary edges pulled in by `sag` (closed thin solid).
        def membrane(k, corners, material, sag: 0.08, n: 10, thick: 0.004)
          p = lambda do |s, t|
            a = Geometry.lerp3(corners[0], corners[1], s)
            b = Geometry.lerp3(corners[3], corners[2], s)
            q = Geometry.lerp3(a, b, t)
            c = corners.map { |x| x[0] }.sum / 4.0
            d = corners.map { |x| x[1] }.sum / 4.0
            pull = sag * ((Math.sin(Math::PI * s) * (t.zero? || t == 1.0 ? 1 : 0)) + (Math.sin(Math::PI * t) * (s.zero? || s == 1.0 ? 1 : 0)))
            [q[0] + ((c - q[0]) * pull), q[1] + ((d - q[1]) * pull), q[2] - (0.08 * Math.sin(Math::PI * s) * Math.sin(Math::PI * t))]
          end
          grid = Array.new(n + 1) { |i| Array.new(n + 1) { |j| p.call(i.to_f / n, j.to_f / n) } }
          low = grid.map { |row| row.map { |q| [q[0], q[1], q[2] - thick] } }
          polys = []
          n.times do |i|
            n.times do |j|
              polys << [grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]]
              polys << [low[i][j + 1], low[i + 1][j + 1], low[i + 1][j], low[i][j]]
            end
          end
          ring = (0...n).map { |i| [i, 0] } + (0...n).map { |j| [n, j] } + (0...n).map { |i| [n - i, n] } + (0...n).map { |j| [0, n - j] }
          ring.each_with_index do |(i, j), idx|
            ni, nj = ring[(idx + 1) % ring.length]
            polys << [grid[i][j], low[i][j], low[ni][nj], grid[ni][nj]]
          end
          polys = polys.map(&:reverse) if Geometry.signed_area(corners).negative?
          k.mesh(polys, material, 'Jedro', smooth: true)
          [grid[0][0], grid[n][0], grid[n][n], grid[0][n]]
        end

        def sail(k, e)
          pts = Geometry.dedupe(e.geometry[:outer][:vertices], true)
          return k.builder_draw unless pts.length == 4 && !k.detail.zero?

          high = e.num('highHeight', 3.5)
          low = e.num('lowHeight', 2.6)
          md = e.num('mastDiameter', 0.12)
          mm = k.mat('mastMaterial', 'steel_galv')
          tops = pts.each_with_index.map { |p, i| [p[0], p[1], (p[2] || 0) + (i.even? ? high : low)] }
          c = [tops.map { |q| q[0] }.sum / 4, tops.map { |q| q[1] }.sum / 4]
          # sail corners 0.35 m inside the masts, connected with tensioners
          corners = tops.map { |q| d = Geometry.norm([c[0] - q[0], c[1] - q[1]]); [q[0] + (d[0] * 0.35), q[1] + (d[1] * 0.35), q[2] - 0.1] }
          membrane(k, corners, k.mat('material', 'fabric'))
          pts.each_with_index do |p, i|
            z = (p[2] || 0)
            mh = (i.even? ? high : low) + 0.2
            if md.positive?
              k.cyl(p[0], p[1], z, md / 2, mh, mm, 'Stub')
              k.cyl(p[0], p[1], z - 0.8, md * 2.5, 0.8, 'concrete', 'Temelj stuba') if k.detail >= 2
            end
            k.bar([tops[i][0], tops[i][1], tops[i][2]], corners[i], 0.006, 'stainless', 'Zatezač', n: 6)
            k.sphere(corners[i], 0.025, 'stainless', 'Ugaoni prsten', n: 8)
          end
        end

        # ---- other -------------------------------------------------------------------------------------------

        # Deck on joists and adjustable pedestals (within the deck thickness).
        def deck(k, e)
          outer, holes = k.outline
          top = (outer.first[2] || 0.0) + e.num('topOffset', 0.0)
          t = e.num('thickness', 0.15)
          board = [0.03, t * 0.3].min
          k.prism(outer, top - board, top, k.mat('material', 'timber_deck'), 'Površina', holes: holes)
          return if k.detail.zero?

          r = Geometry.oriented_rect(outer)
          joist = [t - board - 0.03, 0.04].max
          frame = k.mat('material', 'timber_deck').start_with?('timber') ? 'timber' : 'aluminium'
          n = [(r[:length_u] / 0.5).floor, 1].max
          (0..n).each do |i|
            a = [[i * r[:length_u] / n, 0.03].max, r[:length_u] - 0.03].min
            p0 = Geometry.rect_point(r, a, 0.05, top - board - joist)
            p1 = Geometry.rect_point(r, a, r[:length_v] - 0.05, top - board - joist)
            next unless Geometry.point_in_polygon?(Geometry.rect_point(r, a, r[:length_v] / 2), outer)

            k.mesh(Mesh.block([p0[0] - (r[:u][0] * 0.025), p0[1] - (r[:u][1] * 0.025), p0[2]], [r[:u][0] * 0.05, r[:u][1] * 0.05, 0.0],
                              [p1[0] - p0[0], p1[1] - p0[1], 0.0], [0.0, 0.0, joist]), frame, 'Podkonstrukcija')
            next if k.detail < 2

            m = [(r[:length_v] / 0.6).floor, 1].max
            (0..m).each do |j|
              q = Geometry.rect_point(r, a, 0.05 + (j * (r[:length_v] - 0.1) / m), top - t)
              k.cyl(q[0], q[1], q[2], 0.04, t - board - joist, 'plastic_dark', 'Podesivi oslonac', n: 8)
            end
          end
        end

        # Sports court: acrylic surface with inset white boundary and centre lines.
        def court(k, e)
          k.builder_draw
          outer, = k.outline
          r = Geometry.oriented_rect(outer)
          top = (outer.first[2] || 0.0) + e.num('topOffset', 0.0)
          lu = r[:length_u]
          lv = r[:length_v]
          inset = [[lu, lv].min * 0.08, 1.0].min
          w = 0.05
          line = lambda do |a0, b0, a1, b1|
            p = Geometry.rect_point(r, a0, b0, top)
            k.mesh(Mesh.block(p, [r[:u][0] * (a1 - a0), r[:u][1] * (a1 - a0), 0.0], [r[:v][0] * (b1 - b0), r[:v][1] * (b1 - b0), 0.0], [0.0, 0.0, 0.002]),
                   'line_paint_white', 'Linije terena')
          end
          line.call(inset, inset, lu - inset, inset + w)
          line.call(inset, lv - inset - w, lu - inset, lv - inset)
          line.call(inset, inset + w, inset + w, lv - inset - w)
          line.call(lu - inset - w, inset + w, lu - inset, lv - inset - w)
          line.call((lu - w) / 2, inset + w, (lu + w) / 2, lv - inset - w)
        end

        # Drainage channel with a slotted grate (bars across the channel).
        def channel(k, e)
          pts = k.path
          closed = e.geometry[:closed]
          w = e.num('width', 0.15)
          depth = e.num('depth', 0.25)
          g = 0.02
          Lin.sweep(k, pts, Lin.rect(w / 2, w / 2, -depth, -g), closed, k.mat('bodyMaterial', 'concrete_precast'), 'Tijelo kanala')
          gm = k.mat('grateMaterial', 'grate_steel')
          if k.detail.zero?
            Lin.sweep(k, pts, Lin.rect(w / 2, w / 2, -g, 0.0), closed, gm, 'Rešetka')
            return
          end
          Lin.sweep(k, pts, Lin.rect(w / 2, (w / 2) - 0.015, -g, 0.0), closed, gm, 'Okvir rešetke')
          Lin.sweep(k, pts, Lin.rect((w / 2) - 0.015, w / 2, -g, 0.0), closed, gm, 'Okvir rešetke')
          Lin.sweep(k, pts, Lin.rect((w / 2) - 0.015, (w / 2) - 0.015, -g, -g + 0.004), closed, 'cast_iron', 'Dno rešetke')
          polys = []
          Lin.along(pts, closed, k.detail >= 2 ? 0.025 : 0.05, ends: false).each do |p, dir, n|
            a = Lin.at(p, n, (w / 2) - 0.015, -g + 0.004)
            polys.concat(Mesh.block([a[0] - (dir[0] * 0.004), a[1] - (dir[1] * 0.004), a[2]], [dir[0] * 0.008, dir[1] * 0.008, 0.0],
                                    [-n[0] * (w - 0.03), -n[1] * (w - 0.03), 0.0], [0.0, 0.0, g - 0.004]))
          end
          k.mesh(polys, gm, 'Rešetka')
        end

        # Misting line: stainless pipe with nozzles every 0.6 m.
        def misting(k, e)
          k.builder_draw
          return if k.detail.zero?

          off = e.num('axisOffset', 2.6)
          r = e.num('diameter', 0.012) / 2
          Lin.along(k.path, e.geometry[:closed], 0.6, ends: false).each do |p, _d, _n|
            z = (p[2] || 0.0) + off
            k.bar([p[0], p[1], z - r], [p[0], p[1], z - r - 0.025], 0.005, 'stainless', 'Mlaznica', n: 6)
          end
        end

        # Zip line with a start platform, trolley with handle and an end buffer.
        def zip_line(k, e)
          k.builder_draw
          return if k.detail.zero?

          v = e.geometry[:vertices]
          a = v.first
          b = v.last
          ha = e.num('startHeight', 3.0)
          hb = e.num('endHeight', 2.0)
          dir = Geometry.norm(Geometry.sub(b, a))
          ang = Math.atan2(dir[1], dir[0])
          pz = (a[2] || 0) + [ha - 1.9, 0.4].max
          c = [a[0] + (dir[0] * 0.8), a[1] + (dir[1] * 0.8)]
          k.box(c[0], c[1], pz - 0.08, 1.4, 1.2, 0.08, 'timber_deck', 'Startna platforma', angle: ang)
          [[-0.6, -0.5], [0.6, -0.5], [-0.6, 0.5], [0.6, 0.5]].each do |du, dv|
            q = [c[0] + (dir[0] * du) - (dir[1] * dv), c[1] + (dir[1] * du) + (dir[0] * dv)]
            k.box(q[0], q[1], a[2] || 0, 0.09, 0.09, pz - 0.08 - (a[2] || 0), 'timber', 'Stub platforme', angle: ang)
          end
          t = Geometry.lerp3([a[0], a[1], (a[2] || 0) + ha - 0.15], [b[0], b[1], (b[2] || 0) + hb - 0.15], 0.15)
          k.box(t[0], t[1], t[2] - 0.08, 0.16, 0.06, 0.1, 'steel_galv', 'Kolica', angle: ang)
          k.bar([t[0], t[1], t[2] - 0.08], [t[0], t[1], t[2] - 1.0], 0.012, 'rope', 'Uže', n: 6)
          k.bar([t[0] - (dir[1] * 0.25), t[1] + (dir[0] * 0.25), t[2] - 1.0], [t[0] + (dir[1] * 0.25), t[1] - (dir[0] * 0.25), t[2] - 1.0], 0.02, 'rubber_black', 'Ručka', n: 8)
          e_ = Geometry.lerp3([a[0], a[1], (a[2] || 0) + hb], [b[0], b[1], (b[2] || 0) + hb - 0.15], 0.96)
          k.bar([e_[0], e_[1], e_[2]], [e_[0] - (dir[0] * 0.5), e_[1] - (dir[1] * 0.5), e_[2] - (0.5 * ((ha - hb) / [Geometry.dist(a, b), 1].max))], 0.06, 'rubber_black', 'Kočnica (amortizer)', n: 12)
        end

        # Recycling island: concrete pad with a row of bell containers (glass, paper, plastic, metal) on the long axis.
        def recycling_island(k, e)
          outer, holes = k.outline
          z = outer.first[2] || 0.0
          k.prism(outer, z - 0.12, z + 0.02, 'concrete', 'Betonska podloga', holes: holes)
          return if k.detail.zero?

          r = Geometry.oriented_rect(outer)
          long_u = r[:length_u] >= r[:length_v]
          len = long_u ? r[:length_u] : r[:length_v]
          n = [[(len / 1.9).floor, 1].max, 4].min
          kinds = [%w[plastic_green Staklo], %w[plastic_blue Papir], %w[plastic_yellow Plastika], %w[steel_galv Metal]]
          n.times do |i|
            t = (i + 0.5) * len / n
            a, b = long_u ? [t, r[:length_v] / 2] : [r[:length_u] / 2, t]
            p = Geometry.rect_point(r, a, b, z + 0.02)
            mat, label = kinds[i % kinds.length]
            k.place("zvono-#{mat}", p, Math.atan2(r[:u][1], r[:u][0])) do |pk|
              pk.lathe([[0, 0], [0.72, 0], [0.75, 0.08], [0.74, 1.05], [0.62, 1.38], [0.32, 1.55], [0, 1.58]], mat, "Kontejner – #{label}", n: pk.seg(20))
              pk.cyl(0, 0, 1.55, 0.06, 0.12, 'steel_galv', 'Kuka za pražnjenje', n: 8)
              pk.box(0, -0.66, 1.0, 0.24, 0.18, 0.12, 'plastic_dark', 'Otvor za ubacivanje', angle: 0.0) if pk.detail.positive?
            end
          end
        end

        # Cable trench (envelope from the catalog): sand bedding with cable ducts and a warning tape; the backfill
        # is ordinary soil and is not modelled, so the ducts stay visible in the underground view.
        def cable_trench(k, e)
          pts = k.path
          closed = e.geometry[:closed]
          w = e.num('width', 0.6)
          depth = e.num('thickness', 1.0)
          top = e.num('topOffset', 0.0)
          bottom = top - depth
          Lin.sweep(k, pts, Lin.rect(w / 2, w / 2, bottom, bottom + 0.35), closed, 'sand', 'Pješčana posteljica')
          Lin.sweep(k, pts, Lin.rect(w * 0.3, w * 0.3, top - 0.35, top - 0.348), closed, 'plastic_red', 'Upozoravajuća traka')
          ducts = [[-w / 4, bottom + 0.15], [w / 4, bottom + 0.15], [0.0, bottom + 0.27]]
          ducts.each { |(u, z)| Lin.sweep(k, pts, Array.new(12) { |i| a = 2 * Math::PI * i / 12; [u + (Math.cos(a) * 0.055), z + (Math.sin(a) * 0.055)] }, closed, 'plastic_dark', 'Kablovska cijev (110 mm)') }
        end

        # Underground service corridor: water, sewer, electric and telecom lines at their usual relative positions
        # within the corridor envelope (positions are representative, the real routes come from the utility design).
        def service_corridor(k, e)
          pts = k.path
          closed = e.geometry[:closed]
          w = e.num('width', 2.0)
          top = e.num('topOffset', -0.3)
          bottom = top - e.num('thickness', 1.5)
          circle = ->(u, z, r) { Array.new(14) { |i| a = 2 * Math::PI * i / 14; [u + (Math.cos(a) * r), z + (Math.sin(a) * r)] } }
          lines = [[-w * 0.35, bottom + 0.2, 0.1, 'concrete_precast', 'Kanalizacija (DN 200)'], [-w * 0.05, bottom + 0.5, 0.05, 'plastic_blue', 'Vodovod (DN 100)'],
                   [w * 0.2, top - 0.4, 0.055, 'plastic_red', 'Elektro kabl u cijevi'], [w * 0.35, top - 0.35, 0.055, 'plastic_green', 'Telekomunikacije u cijevi']]
          lines.each { |(u, z, r, m, name)| Lin.sweep(k, pts, circle.call(u, z, r), closed, m, name, smooth: true) }
          lines.each { |(u, _z, _r, _m, _n)| Lin.sweep(k, pts, Lin.rect(u + 0.1, -u + 0.1, top - 0.25, top - 0.248), closed, 'plastic_yellow', 'Upozoravajuća traka') }
          Lin.sweep(k, pts, Lin.rect(w / 2, w / 2, bottom, bottom + 0.1), closed, 'sand', 'Posteljica')
        end

        # Heated terrace: paving on a screed with an embedded heating cable loop over insulation.
        def heated_terrace(k, e)
          outer, holes = k.outline
          z = (outer.first[2] || 0.0) + e.num('baseOffset', 0.0)
          k.prism(outer, z - 0.02, z, 'porcelain', 'Površina', holes: holes)
          k.prism(outer, z - 0.08, z - 0.02, 'concrete', 'Estrih s grijaćim kablom', holes: holes)
          k.prism(outer, z - 0.13, z - 0.08, 'plastic_dark', 'Toplotna izolacija', holes: holes)
          return if k.detail.zero?

          inner = Geometry.offset_ring(Area.ring_ccw(outer), -0.15)
          return unless inner

          r = Geometry.oriented_rect(inner)
          n = [(r[:length_v] / 0.15).floor, 2].max
          pts = []
          n.times do |i|
            b = 0.05 + (i * (r[:length_v] - 0.1) / (n - 1))
            row = [Geometry.rect_point(r, 0.05, b, z - 0.05), Geometry.rect_point(r, r[:length_u] - 0.05, b, z - 0.05)]
            pts.concat(i.even? ? row : row.reverse)
          end
          k.rail(pts, 0.004, 'plastic_red', 'Grijaći kabl', n: 6)
        end

        # Lawn: topsoil with a turf layer.
        def lawn_area(k, e)
          outer, holes = k.outline
          z = (outer.first[2] || 0.0) + e.num('topOffset', 0.0)
          soil = e.num('soilThickness', 0.3)
          k.prism(outer, z - soil, z - 0.03, k.mat('soilMaterial', 'soil'), 'Zemlja', holes: holes)
          k.prism(outer, z - 0.03, z, 'grass', 'Travnjak', holes: holes)
        end
      end

      define('water.swimming_pool', geometry: 'polygon') { |k, e| Area.pool(k, e, :swimming) }
      define('water.infinity_pool', geometry: 'polygon') { |k, e| Area.pool(k, e, :infinity) }
      define('water.decorative_pond', geometry: 'polygon') { |k, e| Area.pool(k, e, :pond) }
      define('water.reflecting_pool', geometry: 'polygon') { |k, e| Area.pool(k, e, :reflecting) }
      define('water.retention_basin', geometry: 'polygon') { |k, e| Area.basin(k, e, :retention) }
      define('water.rain_garden', geometry: 'polygon') { |k, e| Area.basin(k, e, :rain_garden) }
      define('water.bioswale', geometry: 'polygon') { |k, e| Area.basin(k, e, :bioswale) }
      define('landscape.raised_planter', geometry: 'polygon') { |k, e| Area.planter(k, e, 'concrete_precast') }
      define('decorative.feature_planter', geometry: 'polygon') { |k, e| Area.planter(k, e, nil) }
      define('level.steps', geometry: 'polygon') { |k, e| Area.steps(k, e) }
      define('level.ramp', geometry: 'polygon') { |k, e| Area.ramp(k, e) }
      define('structure.pergola', geometry: 'polygon') { |k, e| Area.pergola(k, e, climbers: true) }
      define('structure.bioclimatic_pergola', geometry: 'polygon') { |k, e| Area.pergola(k, e, gutter: true) }
      define('structure.retractable_shade', geometry: 'polygon') { |k, e| Area.pergola(k, e) }
      define('structure.carport', geometry: 'polygon') { |k, e| Area.pergola(k, e, gutter: true) }
      define('structure.solar_canopy', geometry: 'polygon') { |k, e| Area.pergola(k, e, gutter: true) }
      define('structure.terrace_roof', geometry: 'polygon') { |k, e| Area.pergola(k, e, gutter: true) }
      define('climate.shade_sail', geometry: 'polygon') { |k, e| Area.sail(k, e) }
      define('structure.tensile_membrane', geometry: 'polygon') { |k, e| Area.sail(k, e) }
      define('surface.timber_deck', geometry: 'polygon') { |k, e| Area.deck(k, e) }
      define('surface.raised_terrace', geometry: 'polygon') { |k, e| Area.deck(k, e) }
      define('recreation.platform', geometry: 'polygon') { |k, e| Area.deck(k, e) }
      define('surface.sports_court', geometry: 'polygon') { |k, e| Area.court(k, e) }
      define('landscape.lawn_area', geometry: 'polygon') { |k, e| Area.lawn_area(k, e) }
      define('utility.recycling_island', geometry: 'polygon') { |k, e| Area.recycling_island(k, e) }
      define('climate.heated_zone', geometry: 'polygon') { |k, e| Area.heated_terrace(k, e) }
      define('utility.trench', geometry: 'path') { |k, e| Area.cable_trench(k, e) }
      define('utility.service_corridor', geometry: 'path') { |k, e| Area.service_corridor(k, e) }
      %w[water.drainage_channel water.slot_drain water.overflow_channel].each { |id| define(id, geometry: 'path') { |k, e| Area.channel(k, e) } }
      define('climate.misting_line', geometry: 'path') { |k, e| Area.misting(k, e) }
      define('recreation.zip_line', geometry: 'path') { |k, e| Area.zip_line(k, e) }
    end
  end
end
