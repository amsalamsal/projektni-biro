# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Models
      # Street furniture, play and sports equipment. Local frame: length along X, depth along Y, the
      # "back" (backrest, rear of equipment) on +Y, insertion point at the centre of the footprint on the ground.
      module Furn
        module_function

        # Bench / chair / lounger: cast side frames, timber slats, optional backrest and armrests.
        def seat(k, e, l: nil, recline: 0.0)
          l ||= e.num('length', 1.8)
          d = e.num('depth', 0.6)
          sh = e.num('seatHeight', 0.45)
          bh = e.num('backHeight', 0.8)
          back = e.bool('backrest', true) && bh > sh + 0.1
          wood = k.mat('material', 'timber')
          frame = k.mat('frameMaterial', 'steel_dark')
          fx = l > 0.9 ? (l / 2) - 0.18 : (l / 2) - 0.03
          y0 = -d / 2
          y1 = d / 2
          [-fx, fx].each do |x|
            outline = [[y0, 0.0], [y0 + 0.08, 0.0], [y0 + 0.1, sh - 0.12], [y1 - 0.12, sh - 0.12], [y1 - 0.08, 0.0], [y1, 0.0],
                       [y1 - 0.03, sh - 0.05], [y0 + 0.02, sh - 0.05]]
            outline = [[y0, 0.0], [y0 + 0.08, 0.0], [y0 + 0.1, sh - 0.12], [y1 - 0.2 - (recline * 0.3), sh - 0.12], [y1 - 0.14 - (recline * 0.3), 0.0], [y1 - 0.06 - (recline * 0.3), 0.0],
                       [y1, bh], [y1 - 0.06, bh], [y1 - 0.1 - (recline * 0.3), sh - 0.05], [y0 + 0.02, sh - 0.05]] if back
            k.plate([x + 0.025, 0.0, 0.0], [0.0, 1.0], outline, 0.05, frame, 'Bočni nosač')
          end
          n = [3, 5, 6][k.detail]
          sw = (d - 0.02 - ((n - 1) * 0.012)) / n
          n.times do |i|
            y = y0 + 0.01 + (sw / 2) + (i * (sw + 0.012))
            k.box(0, y, sh - 0.045, l, sw, 0.04, wood, 'Lamela sjedišta')
          end
          if back
            m = [2, 3, 4][k.detail]
            span = bh - sh - 0.12
            m.times do |i|
              t = (i + 0.5) / m
              z = sh + 0.08 + (t * span)
              y = y1 - 0.075 - (recline * 0.3 * (1 - t))
              k.box(0, y, z, l, 0.035, (span / m) * 0.72, wood, 'Naslon')
            end
          end
          return unless e.bool('armrests', false)

          [-l / 2 + 0.04, l / 2 - 0.04].each do |x|
            k.rail([[x, y0 + 0.06, sh - 0.05], [x, y0 + 0.08, sh + 0.24], [x, y1 - 0.12, sh + 0.24]], 0.022, frame, 'Rukohvat')
          end
        end

        def picnic_table(k, e)
          l = e.num('length', 2.0)
          w = e.num('width', 0.8)
          h = e.num('height', 0.75)
          wood = k.mat('material', 'timber')
          frame = k.mat('frameMaterial', 'steel_dark')
          benches = e.bool('benches', true)
          bw = e.num('benchWidth', 0.3)
          boards = [3, 5, 5][k.detail]
          bwid = (w - ((boards - 1) * 0.01)) / boards
          boards.times { |i| k.box(0, (-w / 2) + (bwid / 2) + (i * (bwid + 0.01)), h - 0.045, l, bwid, 0.045, wood, 'Daska stola') }
          by = (w / 2) + 0.22 + (bw / 2)
          lx = (l / 2) - 0.3
          [-lx, lx].each do |x|
            if benches
              # A-frame: legs from the table edge down and out, cross member carrying the seats
              [-1, 1].each do |s|
                k.beam([x, s * w * 0.15, h - 0.05], [x, s * (by + (bw * 0.3)), 0.0], 0.07, frame, 'Noga')
              end
              k.beam_rect([x, -(by + (bw / 2)), 0.36], [x, by + (bw / 2), 0.36], 0.07, 0.07, frame, 'Poprečna greda')
              k.beam_rect([x, -w / 2 + 0.05, h - 0.1], [x, w / 2 - 0.05, h - 0.1], 0.07, 0.05, frame, 'Podnožje ploče')
            else
              [-1, 1].each { |s| k.box(x, s * ((w / 2) - 0.1), 0, 0.07, 0.07, h - 0.045, frame, 'Noga') }
              k.beam_rect([x, -w / 2 + 0.1, h - 0.12], [x, w / 2 - 0.1, h - 0.12], 0.05, 0.08, frame, 'Okvir')
            end
          end
          k.beam_rect([-lx, 0, h - 0.12], [lx, 0, h - 0.12], 0.05, 0.08, frame, 'Uzdužna greda') unless benches
          return unless benches

          [-1, 1].each do |s|
            2.times { |i| k.box(0, s * (by - (bw / 4) + (i * bw / 2)), 0.40, l, (bw / 2) - 0.01, 0.04, wood, 'Daska klupe') }
          end
        end

        def ping_pong(k, e)
          l = e.num('length', 2.74)
          w = e.num('width', 1.525)
          h = e.num('height', 0.76)
          top = k.mat('material', 'concrete')
          k.box(0, 0, h - 0.06, l, w, 0.06, 'plastic_blue', 'Ploča stola')
          k.box(0, 0, h - 0.12, l - 0.06, w - 0.06, 0.06, top, 'Nosiva ploča')
          [-1, 1].each { |s| k.box(s * l * 0.28, 0, 0, 0.22, w * 0.62, h - 0.12, top, 'Nosač') }
          line = 0.02
          [-w / 2 + line / 2, w / 2 - line / 2].each { |y| k.box(0, y, h, l, line, 0.002, 'line_paint_white', 'Rubna linija') }
          [-l / 2 + line / 2, l / 2 - line / 2].each { |x| k.box(x, 0, h, line, w - (2 * line), 0.002, 'line_paint_white', 'Rubna linija') }
          k.box(0, 0, h, l - (2 * line), 0.003, 0.002, 'line_paint_white', 'Središnja linija')
          k.box(0, 0, h, 0.012, w + 0.15, 0.1525, 'steel_galv', 'Mreža')
          [-1, 1].each { |s| k.cyl(0, s * ((w / 2) + 0.075), h, 0.012, 0.16, 'steel_dark', 'Stub mreže', n: 8) }
        end

        def chess_table(k, e)
          l = e.num('length', 0.8)
          h = e.num('height', 0.75)
          top = k.mat('material', 'concrete')
          k.box(0, 0, h - 0.06, l, l, 0.06, top, 'Ploča')
          sq = (l * 0.8) / 8
          if k.detail.positive?
            8.times do |i|
              8.times do |j|
                k.box(((i - 3.5) * sq), ((j - 3.5) * sq), h, sq, sq, 0.004, (i + j).even? ? 'steel_dark' : 'stone_wall', 'Polje')
              end
            end
          end
          k.frustum(0, 0, 0, l * 0.3, l * 0.18, 0.08, top, 'Temelj', n: k.seg(12))
          k.cyl(0, 0, 0.08, l * 0.12, h - 0.14, top, 'Stub', n: k.seg(12))
          return unless e.bool('benches', true)

          [-1, 1].each do |s|
            y = s * ((l / 2) + 0.42)
            k.lathe([[0, 0], [0.2, 0], [0.16, 0.06], [0.14, 0.4], [0.2, 0.42], [0.2, 0.45], [0, 0.45]], top, 'Stolica', x: 0, y: y, n: k.seg(12))
          end
        end

        def waste_bin(k, e)
          r = e.num('diameter', 0.45) / 2
          h = e.num('height', 0.9)
          body = k.mat('material', 'steel_dark')
          k.cyl(0, 0, 0, r * 1.05, 0.03, body, 'Podnožje')
          k.lathe([[0, 0.03], [r * 0.92, 0.03], [r, h * 0.85], [r * 0.92, h * 0.85], [r * 0.9, h * 0.1], [0, h * 0.1]], body, 'Tijelo')
          if k.detail.positive?
            ribs = [8, 12, 16][k.detail]
            ribs.times do |i|
              a = 2 * Math::PI * i / ribs
              k.box(Math.cos(a) * (r + 0.008), Math.sin(a) * (r + 0.008), 0.06, 0.03, 0.012, h * 0.76, body, 'Rebro', angle: a + (Math::PI / 2))
            end
          end
          k.lathe([[0, h * 0.85], [r * 1.04, h * 0.85], [r * 1.04, h * 0.89], [r * 0.7, h], [0, h]], body, 'Poklopac')
          k.torus([0, 0, h * 0.93], r * 0.62, 0.015, 'steel_galv', 'Otvor za ubacivanje')
        end

        def ashtray(k, e)
          r = e.num('diameter', 0.25) / 2
          h = e.num('height', 1.0)
          m = k.mat('material', 'steel_galv')
          k.lathe([[0, 0], [r * 1.3, 0], [r * 1.3, 0.02], [r * 0.8, 0.05], [r * 0.8, h * 0.8], [r, h * 0.84], [r, h], [r * 0.85, h], [r * 0.8, h * 0.96], [0, h * 0.96]], m, 'Stub pepeljare')
          k.cyl(0, 0, h * 0.96, r * 0.78, 0.01, 'grate_steel', 'Rešetka', n: k.seg(12))
        end

        def planter(k, e)
          w = e.num('width', 1.2)
          d = e.num('depth', 0.6)
          h = e.num('height', 0.6)
          m = k.mat('material', 'steel_dark')
          k.prism(Mesh.rect_ring(0, 0, w, d), 0.05, h - 0.04, m, 'Posuda')
          k.prism(Mesh.rect_ring(0, 0, w + 0.04, d + 0.04), h - 0.04, h, m, 'Rub', holes: [Mesh.rect_ring(0, 0, w - 0.06, d - 0.06)])
          [[-1, -1], [1, -1], [1, 1], [-1, 1]].each { |sx, sy| k.box(sx * ((w / 2) - 0.06), sy * ((d / 2) - 0.06), 0, 0.06, 0.06, 0.05, m, 'Nogica') }
          k.box(0, 0, h - 0.06, w - 0.06, d - 0.06, 0.02, 'soil', 'Zemlja')
          return if k.detail.zero?

          n = [(w / 0.45).round, 1].max
          n.times { |i| Veg.shrub(k, 0.55, [d * 0.85, 0.5].min, i.even? ? 'foliage' : 'foliage_light', x: (-w / 2) + ((i + 0.5) * w / n), z: h - 0.04) }
        end

        def drinking_fountain(k, e)
          w = e.num('width', 0.4)
          h = e.num('height', 1.0)
          m = k.mat('material', 'steel_galv')
          k.box(0, 0, 0, w * 1.6, w * 1.6, 0.03, 'grate_steel', 'Odvodna rešetka')
          k.lathe([[0, 0.03], [w * 0.38, 0.03], [w * 0.32, 0.12], [w * 0.26, h - 0.16], [w * 0.3, h - 0.1], [0, h - 0.1]], m, 'Stub')
          k.lathe([[0, h - 0.12], [w * 0.15, h - 0.12], [w * 0.55, h - 0.02], [w * 0.55, h], [w * 0.5, h], [w * 0.14, h - 0.07], [0, h - 0.07]], 'stainless', 'Školjka')
          k.rail([[0, -w * 0.25, h - 0.06], [0, -w * 0.25, h + 0.06], [0, -w * 0.1, h + 0.1]], 0.012, 'stainless', 'Izljev')
          k.cyl(0, -w * 0.33, h - 0.25, 0.03, 0.05, 'steel_dark', 'Dugme', n: 10)
        end

        def shower(k, e)
          ph = e.num('poleHeight', 2.2)
          r = e.num('poleDiameter', 0.08) / 2
          arm = e.num('armLength', 0.3)
          k.box(0, 0, 0, 0.9, 0.9, 0.04, 'grate_steel', 'Odvodna podloga')
          pm = k.mat('poleMaterial', 'stainless')
          hm = k.mat('headMaterial', 'stainless')
          hr = e.num('headWidth', 0.2) / 2
          k.cyl(0, 0.3, 0.04, r * 1.8, 0.01, pm, 'Prirubnica', n: 12)
          pts = [[0, 0.3, 0.04], [0, 0.3, ph - 0.12], [0, 0.26, ph], [0, 0.3 - arm, ph]]
          k.rail(pts, r, pm, 'Stub tuša')
          k.frustum(0, 0.3 - arm, ph - 0.04, hr, hr * 0.3, [e.num('headHeight', 0.04), 0.02].max, hm, 'Glava tuša', n: k.seg(14))
          Light.solar_panel(k, ph, r, pm, size: 0.5) if e.bool('solarPanel', false)
          k.bar([0, 0.3, 1.1], [0, 0.3 - 0.1, 1.1], 0.012, 'steel_dark', 'Ventil')
          k.bar([0, 0.3, 0.45], [0, 0.3 - 0.12, 0.45], 0.012, 'steel_dark', 'Slavina za noge')
        end

        def pet_station(k, e)
          h = e.num('height', 1.3)
          m = k.mat('material', 'plastic_green')
          k.box(0, 0, 0, 0.08, 0.08, h, 'steel_dark', 'Stub')
          k.box(0, -0.08, h - 0.45, 0.28, 0.12, 0.32, m, 'Dozator vrećica')
          k.box(0, -0.06, h - 0.02, 0.36, 0.02, 0.24, 'sign_plate', 'Tabla')
          k.lathe([[0, 0.3], [0.17, 0.3], [0.19, 0.75], [0, 0.75]], m, 'Korpa', x: 0, y: -0.2)
          k.torus([0, -0.2, 0.75], 0.18, 0.012, 'steel_dark', 'Obruč korpe')
        end

        def fitness(k, e)
          w = e.num('width', 2.0)
          d = e.num('depth', 1.0)
          h = e.num('height', 2.2)
          col = k.mat('material', 'steel_dark')
          steel = 'steel_galv'
          x0 = -w / 2 + 0.1
          x1 = w / 2 - 0.1
          # pull-up bar between two posts
          [x0, x0 + (w * 0.4)].each { |x| k.cyl(x, d / 2 - 0.15, 0, 0.06, h, col, 'Stub', n: k.seg(12)) }
          k.bar([x0, d / 2 - 0.15, h - 0.12], [x0 + (w * 0.4), d / 2 - 0.15, h - 0.12], 0.02, steel, 'Šipka za zgibove')
          [x0, x0 + (w * 0.4)].each { |x| k.sphere([x, d / 2 - 0.15, h], 0.065, col, 'Kapa stuba') }
          # parallel bars
          px0 = x0 + (w * 0.5)
          [-0.25, 0.25].each do |y|
            [px0, x1].each { |x| k.cyl(x, y, 0, 0.04, 1.2, col, 'Stub razboja', n: k.seg(10)) }
            k.rail([[px0, y, 1.2], [x1, y, 1.2]], 0.022, steel, 'Razboj')
          end
          # sit-up bench
          k.beam_rect([x0 + 0.2, -d / 2 + 0.25, 0.25], [x0 + (w * 0.4) - 0.1, -d / 2 + 0.25, 0.55], 0.4, 0.05, 'plastic_dark', 'Kosa klupa')
          k.box(x0 + 0.2, -d / 2 + 0.25, 0, 0.06, 0.3, 0.25, steel, 'Nosač klupe')
          k.box(x0 + (w * 0.4) - 0.1, -d / 2 + 0.25, 0, 0.06, 0.3, 0.55, steel, 'Nosač klupe')
          k.bar([x0 + 0.12, -d / 2 + 0.1, 0.3], [x0 + 0.12, -d / 2 + 0.4, 0.3], 0.025, 'rubber_black', 'Oslonac za stopala')
          k.box(-w / 2 + 0.35, d / 2 - 0.02, 0, 0.5, 0.03, 1.4, 'sign_plate', 'Uputstvo za vježbe')
        end

        # Slide chute along a smooth profile from (x0, z0) down to the ground at x1: bed and side walls.
        def chute(k, x0, z0, x1, width, mat, y: 0.0)
          n = [4, 8, 14][k.detail]
          pts = Array.new(n + 1) do |i|
            t = i.to_f / n
            s = (1 - Math.cos(Math::PI * t)) / 2
            z = z0 - ((z0 - 0.3) * s)
            z = 0.3 if t > 0.92
            [x0 + ((x1 - x0) * t), y, z]
          end
          pts.each_cons(2) do |a, b|
            k.beam_rect(a, b, width, 0.03, mat, 'Kosina tobogana')
            [-1, 1].each { |s| k.beam_rect([a[0], a[1] + (s * width / 2), a[2] + 0.1], [b[0], b[1] + (s * width / 2), b[2] + 0.1], 0.03, 0.22, mat, 'Bočna stranica') }
          end
          k.box(x1 + 0.1, y, 0, 0.08, width * 0.8, 0.3, 'steel_galv', 'Nosač izlaza')
        end

        def playground(k, e)
          w = e.num('width', 4.0)
          d = e.num('depth', 3.0)
          h = e.num('height', 3.0)
          wood = k.mat('material', 'timber')
          t = [1.4, d * 0.6].min
          pz = 1.2
          cx = -w / 2 + (t / 2) + 0.4
          corners = [[-1, -1], [1, -1], [1, 1], [-1, 1]].map { |sx, sy| [cx + (sx * t / 2), sy * t / 2] }
          corners.each { |x, y| k.box(x, y, 0, 0.09, 0.09, h - 0.6, wood, 'Stub kule') }
          k.box(cx, 0, pz - 0.05, t, t, 0.05, wood, 'Platforma')
          # pyramid roof
          k.mesh(Mesh.fix_orientation(Kit.lathe_polys([[0, h - 0.6], [t * 0.78, h - 0.6], [0, h]], cx, 0, 4, false)).map { |p| p.map { |q| rot45(q, cx) } }, 'plastic_red', 'Krov')
          # panels and rails
          k.box(cx, -t / 2, pz, t - 0.09, 0.03, 0.7, 'plastic_blue', 'Zaštitni panel')
          k.box(cx - (t / 2), 0, pz, 0.03, t - 0.09, 0.7, 'plastic_yellow', 'Zaštitni panel')
          k.rail([[cx - (t / 2), t / 2, pz + 0.7], [cx + (t / 2), t / 2, pz + 0.7]], 0.02, 'steel_galv', 'Rukohvat')
          # ladder on +Y side
          [-0.22, 0.22].each { |dx| k.bar([cx + dx, (t / 2) + 0.7, 0], [cx + dx, t / 2, pz], 0.03, wood, 'Ljestve') }
          4.times { |i| tt = (i + 1) / 5.0; k.bar([cx - 0.22, (t / 2) + (0.7 * (1 - tt)), pz * tt], [cx + 0.22, (t / 2) + (0.7 * (1 - tt)), pz * tt], 0.02, 'steel_galv', 'Prečka') }
          # slide to +X
          chute(k, cx + (t / 2), pz + 0.05, [w / 2 - 0.3, cx + (t / 2) + 2.2].min, 0.5, 'plastic_red')
          # climbing net to -X
          nx = cx - (t / 2)
          ropes = []
          5.times do |i|
            y = (-t / 2) + (t * i / 4)
            ropes << [[nx, y, pz], [nx - 0.9, y, 0.0]]
          end
          4.times do |j|
            f = (j + 1) / 5.0
            ropes << [[nx - (0.9 * f), -t / 2, pz * (1 - f)], [nx - (0.9 * f), t / 2, pz * (1 - f)]]
          end
          k.bars(ropes.map { |a, b| [a, b, 0.012, 0.012] }, 'rope', 'Mreža za penjanje')
          k.beam([nx - 0.9, -t / 2, 0.05], [nx - 0.9, t / 2, 0.05], 0.08, wood, 'Prag mreže')
        end

        def rot45(q, cx)
          a = Math::PI / 4
          x = q[0] - cx
          [cx + (x * Math.cos(a)) - (q[1] * Math.sin(a)), (x * Math.sin(a)) + (q[1] * Math.cos(a)), q[2]]
        end

        def slide(k, e)
          w = e.num('width', 4.0)
          h = e.num('height', 2.0)
          m = k.mat('material', 'steel_galv')
          pz = h - 0.6
          x0 = -w / 2 + 0.6
          # platform with handrails and ladder
          [[-0.3, -0.3], [0.3, -0.3], [-0.3, 0.3], [0.3, 0.3]].each { |dx, dy| k.box(x0 + dx, dy, 0, 0.07, 0.07, h, 'plastic_blue', 'Stub') }
          k.box(x0, 0, pz - 0.04, 0.6, 0.6, 0.04, 'steel_galv', 'Platforma')
          [-0.3, 0.3].each { |dy| k.rail([[x0 - 0.3, dy, pz + 0.6], [x0 + 0.3, dy, pz + 0.6]], 0.02, 'steel_galv', 'Rukohvat') }
          [-0.22, 0.22].each { |dy| k.bar([x0 - 1.0, dy, 0], [x0 - 0.3, dy, pz], 0.03, 'plastic_blue', 'Ljestve') }
          5.times { |i| tt = (i + 1) / 6.0; k.bar([x0 - 1.0 + (0.7 * tt), -0.22, pz * tt], [x0 - 1.0 + (0.7 * tt), 0.22, pz * tt], 0.018, 'steel_galv', 'Prečka') }
          chute(k, x0 + 0.3, pz + 0.02, w / 2 - 0.2, 0.5, m)
        end

        def swing(k, e)
          w = e.num('width', 3.4)
          h = e.num('height', 2.4)
          spread = e.num('frameSpread', 1.8)
          seats = e.num('seats', 2).to_i
          sh = e.num('seatHeight', 0.45)
          fr = e.num('frameDiameter', 0.1) / 2
          m = k.mat('material', 'steel_dark')
          [-w / 2, w / 2].each do |x|
            [-1, 1].each { |s| k.bar([x, s * spread / 2, 0], [x, 0, h], fr, m, 'Noga') }
            k.bar([x, -spread * 0.3, h * 0.4], [x, spread * 0.3, h * 0.4], fr * 0.6, m, 'Ukruta')
            [-1, 1].each { |s| k.box(x, s * spread / 2, -0.05, 0.25, 0.25, 0.05, 'concrete', 'Temelj') }
          end
          k.bar([-w / 2 - 0.05, 0, h], [w / 2 + 0.05, 0, h], fr * 1.1, m, 'Greda')
          seats.times do |i|
            x = (-w / 2) + ((i + 0.5) * w / seats)
            [-0.2, 0.2].each do |dx|
              k.cyl(x + dx, 0, h - fr - 0.08, 0.03, 0.08, 'steel_galv', 'Ovjes', n: 8)
              k.bar([x + dx, 0, h - fr - 0.08], [x + dx, 0, sh + 0.02], 0.007, 'steel_galv', 'Lanac')
            end
            k.box(x, 0, sh, 0.45, 0.18, 0.03, 'rubber_black', 'Sjedalo')
          end
        end

        def climbing(k, e)
          w = e.num('width', 3.0)
          h = e.num('height', 2.5)
          r = w / 2
          k.cyl(0, 0, 0, 0.06, h + 0.2, 'steel_galv', 'Središnji jarbol', n: k.seg(10))
          n = [6, 8, 10][k.detail]
          base = Array.new(n) { |i| a = 2 * Math::PI * i / n; [Math.cos(a) * r, Math.sin(a) * r, 0.05] }
          ropes = base.map { |p| [[0, 0, h], p, 0.014, 0.014] }
          [0.25, 0.5, 0.75].each do |f|
            ring = base.map { |p| [p[0] * f, p[1] * f, h - ((h - 0.05) * f)] }
            ring.each_with_index { |p, i| ropes << [p, ring[(i + 1) % n], 0.012, 0.012] }
          end
          k.bars(ropes, 'rope', 'Mreža od užadi')
          base.each { |p| k.box(p[0], p[1], 0, 0.15, 0.15, 0.06, 'steel_dark', 'Sidro') }
          base.each_with_index { |p, i| q = base[(i + 1) % n]; k.bar(p, q, 0.04, k.mat('material', 'timber'), 'Obodna greda') }
        end

        def trampoline(k, e)
          r = e.num('diameter', 3.0) / 2
          k.lathe([[r * 0.82, 0.0], [r, 0.0], [r, 0.03], [r * 0.82, 0.03]], k.mat('material', 'plastic_dark'), 'Zaštitni obod', n: k.seg(24), closed_loop: true)
          k.cyl(0, 0, -0.02, r * 0.82, 0.015, 'rubber_black', 'Skočna površina', n: k.seg(24))
          k.torus([0, 0, -0.04], r * 0.9, 0.03, 'steel_galv', 'Okvir', n: k.seg(24), m: 6)
          k.cyl(0, 0, -0.9, r * 0.85, 0.05, 'gravel', 'Drenažno dno', n: k.seg(16))
        end

        def hoop(k, e)
          ph = e.num('postHeight', 3.95)
          pw = e.num('plateWidth', 1.8)
          plh = e.num('plateHeight', 1.05)
          pb = e.num('plateBottom', 2.9)
          pm = k.mat('postMaterial', 'steel_dark')
          ps = [e.num('postDiameter', 0.15), 0.08].max
          pt = [e.num('plateThickness', 0.03), 0.01].max
          k.box(0, 0.9, 0, 0.6, 0.6, 0.02, pm, 'Podložna ploča')
          k.box(0, 0.9, 0, ps, ps, ph - 0.3, pm, 'Stub')
          k.beam([0, 0.9, ph - 0.4], [0, 0.05, pb + (plh * 0.55)], ps * 0.66, pm, 'Krak')
          k.beam([0, 0.9, ph - 1.2], [0, 0.05, pb + (plh * 0.2)], ps * 0.53, pm, 'Kosnik')
          k.box(0, 0, pb, pw, pt, plh, k.mat('plateMaterial', 'glass'), 'Tabla')
          k.box(0, -0.016, pb + 0.15, 0.59, 0.003, 0.45, 'line_paint_white', 'Oznaka table')
          rim_z = 3.05
          k.torus([0, -0.3, rim_z], 0.225, 0.009, 'plastic_red', 'Obruč', m: 6)
          k.box(0, -0.06, rim_z - 0.06, 0.12, 0.12, 0.06, 'plastic_red', 'Nosač obruča')
          n = [6, 10, 12][k.detail]
          net = Array.new(n) do |i|
            a = 2 * Math::PI * i / n
            [[Math.cos(a) * 0.22, -0.3 + (Math.sin(a) * 0.22), rim_z], [Math.cos(a + 0.4) * 0.15, -0.3 + (Math.sin(a + 0.4) * 0.15), rim_z - 0.4], 0.005, 0.005]
          end
          k.bars(net, 'line_paint_white', 'Mrežica')
        end

        def goal(k, e)
          w = e.num('width', 3.0)
          d = e.num('depth', 1.0)
          h = e.num('height', 2.0)
          m = 'line_paint_white'
          s = 0.08
          k.box(-w / 2, 0, 0, s, s, h, m, 'Stativa')
          k.box(w / 2, 0, 0, s, s, h, m, 'Stativa')
          k.beam([-w / 2 - (s / 2), 0, h - (s / 2)], [w / 2 + (s / 2), 0, h - (s / 2)], s, m, 'Prečka')
          [-w / 2, w / 2].each do |x|
            k.bar([x, 0, h - 0.1], [x, d, 0.05], 0.025, 'steel_galv', 'Zadnji nosač')
            k.bar([x, 0, 0.03], [x, d, 0.03], 0.025, 'steel_galv', 'Donja cijev')
          end
          k.bar([-w / 2, d, 0.03], [w / 2, d, 0.03], 0.025, 'steel_galv', 'Donja cijev')
          return if k.detail.zero?

          net = []
          n = (w / 0.25).round
          (0..n).each { |i| x = (-w / 2) + (w * i / n); net << [[x, 0.04, h - 0.05], [x, d, 0.05], 0.004, 0.004] }
          4.times { |j| f = (j + 1) / 5.0; net << [[-w / 2, d * f, h - ((h - 0.05) * f)], [w / 2, d * f, h - ((h - 0.05) * f)], 0.004, 0.004] }
          k.bars(net, 'rope', 'Mreža')
        end
      end

      define('furniture.bench') { |k, e| Furn.seat(k, e) }
      define('furniture.bench_backless') { |k, e| Furn.seat(k, e) }
      define('furniture.accessible_bench') { |k, e| Furn.seat(k, e) }
      define('furniture.chair') { |k, e| Furn.seat(k, e) }
      define('furniture.lounger') { |k, e| Furn.seat(k, e, recline: 0.5) }
      define('furniture.picnic_table') { |k, e| Furn.picnic_table(k, e) }
      define('furniture.dining_table') { |k, e| Furn.picnic_table(k, e) }
      define('recreation.table_tennis') { |k, e| Furn.ping_pong(k, e) }
      define('recreation.chess_table') { |k, e| Furn.chess_table(k, e) }
      define('furniture.waste_bin') { |k, e| Furn.waste_bin(k, e) }
      define('furniture.ashtray') { |k, e| Furn.ashtray(k, e) }
      define('furniture.planter') { |k, e| Furn.planter(k, e) }
      define('furniture.drinking_fountain') { |k, e| Furn.drinking_fountain(k, e) }
      define('furniture.outdoor_shower') { |k, e| Furn.shower(k, e) }
      define('furniture.pet_waste_station') { |k, e| Furn.pet_station(k, e) }
      define('recreation.fitness_station') { |k, e| Furn.fitness(k, e) }
      define('recreation.playground_unit') { |k, e| Furn.playground(k, e) }
      define('recreation.slide') { |k, e| Furn.slide(k, e) }
      define('recreation.swing') { |k, e| Furn.swing(k, e) }
      define('recreation.climbing_structure') { |k, e| Furn.climbing(k, e) }
      define('recreation.trampoline') { |k, e| Furn.trampoline(k, e) }
      define('recreation.basketball_hoop') { |k, e| Furn.hoop(k, e) }
      define('recreation.football_goal') { |k, e| Furn.goal(k, e) }
    end
  end
end
