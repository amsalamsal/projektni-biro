# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Models
      # Municipal and water infrastructure. Buried parts keep their real depth below the insertion level
      # (ground = z 0), so they show in the underground scene and in section cuts.
      module Util
        module_function

        FRACTION_COLOURS = { 'glass' => 'marking_green', 'paper' => 'plastic_blue', 'plastic' => 'plastic_yellow', 'organic' => 'bark',
                             'metal' => 'steel_galv', 'textile' => 'flower_violet', 'mixed' => 'steel_dark' }.freeze

        # Cover with frame at ground level; round or square, with a cast pattern on presentation detail.
        def cover(k, shape, size, z, material)
          if shape == 'square'
            k.prism(Mesh.rect_ring(0, 0, size + 0.12, size + 0.12), z - 0.08, z, 'cast_iron', 'Okvir poklopca', holes: [Mesh.rect_ring(0, 0, size, size)])
            k.box(0, 0, z - 0.04, size - 0.01, size - 0.01, 0.04, material, 'Poklopac')
            if k.detail >= 2
              5.times { |i| k.box(0, (-size / 2) + ((i + 1) * size / 6), z, size * 0.8, 0.02, 0.004, material, 'Rebro poklopca') }
            end
          else
            r = size / 2
            k.lathe([[r, z - 0.08], [r + 0.08, z - 0.08], [r + 0.08, z], [r, z]], 'cast_iron', 'Okvir poklopca', closed_loop: true, n: k.seg(20))
            k.cyl(0, 0, z - 0.04, r - 0.005, 0.04, material, 'Poklopac', n: k.seg(20))
            if k.detail >= 2
              [r * 0.35, r * 0.7].each { |rr| k.torus([0, 0, z], rr, 0.008, material, 'Reljef poklopca', n: k.seg(16), m: 4) }
              k.box(0, r * 0.82, z, 0.08, 0.025, 0.004, 'steel_dark', 'Otvor za podizanje')
            end
          end
        end

        def chamber(k, e)
          shape = e.text('shape', 'round')
          inner = e.num('innerSize', 1.0)
          t = e.num('wallThickness', 0.12)
          depth = e.num('depth', 1.5)
          top = e.num('coverLevelOffset', 0.0)
          m = k.mat('material', 'concrete_precast')
          cs = e.text('coverShape', shape)
          csize = [e.num('coverSize', 0.6), inner].min
          bottom = top - depth
          if shape == 'square'
            k.box(0, 0, bottom - 0.15, inner + (2 * t), inner + (2 * t), 0.15, m, 'Dno okna')
            k.prism(Mesh.rect_ring(0, 0, inner + (2 * t), inner + (2 * t)), bottom, top - 0.23, m, 'Zid okna', holes: [Mesh.rect_ring(0, 0, inner, inner)])
            open = cs == 'square' ? Mesh.rect_ring(0, 0, csize, csize) : Mesh.circle_ring(0, 0, csize / 2, k.seg(16))
            k.prism(Mesh.rect_ring(0, 0, inner + (2 * t), inner + (2 * t)), top - 0.23, top - 0.08, m, 'Ploča okna', holes: [open])
          else
            ro = (inner / 2) + t
            k.cyl(0, 0, bottom - 0.15, ro, 0.15, m, 'Dno okna', n: k.seg(20))
            k.lathe([[inner / 2, bottom], [ro, bottom], [ro, top - 0.6], [inner / 2, top - 0.6]], m, 'Prsten okna', closed_loop: true, n: k.seg(20))
            k.lathe([[csize / 2, top - 0.6], [ro, top - 0.6], [csize / 2 + t, top - 0.08], [csize / 2, top - 0.08]], m, 'Konusni završetak', closed_loop: true, n: k.seg(20))
          end
          cover(k, cs, csize, top, k.mat('coverMaterial', 'cast_iron'))
          return if k.detail.zero?

          steps = [((depth - 0.6) / 0.3).floor, 1].max
          steps.times do |i|
            z = top - 0.5 - (i * 0.3)
            k.rail([[-0.15, (inner / 2) - 0.01, z], [-0.15, (inner / 2) - 0.14, z], [0.15, (inner / 2) - 0.14, z], [0.15, (inner / 2) - 0.01, z]], 0.012, 'steel_galv', 'Penjalica')
          end
        end

        def tank(k, e, label: 'Spremnik')
          shape = e.text('shape', 'box')
          l = e.num('length', 3.0)
          w = e.num('width', 1.5)
          h = e.num('height', 1.5)
          under = e.text('installation', 'underground') == 'underground'
          m = k.mat('material', 'hdpe')
          z0 = under ? -e.num('coverDepth', 0.8) - h : 0.0
          case shape
          when 'horizontal_cylinder'
            r = [w, h].min / 2
            k.bar([-l / 2 + r * 0.3, 0, z0 + r], [l / 2 - r * 0.3, 0, z0 + r], r, m, label, n: k.seg(18))
            [-1, 1].each { |s| k.ellipsoid([s * (l / 2 - r * 0.3), 0, z0 + r], r * 0.3, r, r, m, 'Dno spremnika', n: k.seg(18)) }
            if k.detail.positive?
              ribs = (l / 0.4).floor
              ribs.times { |i| x = (-l / 2) + (r * 0.3) + ((i + 0.5) * (l - (r * 0.6)) / ribs); k.bar([x - 0.03, 0, z0 + r], [x + 0.03, 0, z0 + r], r * 1.03, m, 'Rebro', n: k.seg(18)) }
            end
          when 'vertical_cylinder'
            r = [l, w].min / 2
            k.lathe([[0, z0], [r, z0], [r, z0 + h - (r * 0.15)], [r * 0.3, z0 + h], [0, z0 + h]], m, label, n: k.seg(20))
            k.cyl(0, 0, z0 + h - 0.02, r * 0.28, 0.06, m, 'Poklopac', n: k.seg(14))
            unless under
              k.bar([0, -r, z0 + 0.15], [0, -r - 0.1, z0 + 0.15], 0.025, 'steel_galv', 'Slavina', n: 8)
              k.box(0, 0, z0 - 0.15, r * 2.2, r * 2.2, 0.15, 'concrete', 'Temeljna ploča')
            end
          else
            k.box(0, 0, z0, l, w, h, m, label)
            if k.detail.positive? && m == 'hdpe'
              ribs = (l / 0.5).floor
              ribs.times { |i| k.box((-l / 2) + ((i + 0.5) * l / ribs), 0, z0 + 0.05, 0.06, w + 0.04, h - 0.1, m, 'Rebro') }
            end
          end
          return unless under && e.bool('accessShaft', true)

          sr = e.num('shaftDiameter', 0.6) / 2
          k.cyl(0, 0, z0 + h, sr, -z0 - h - 0.08, m, 'Pristupno okno', n: k.seg(16))
          cover(k, 'round', sr * 2, 0.0, 'cast_iron')
        end

        def underground_container(k, e)
          l = e.num('bodyLength', 2.0)
          w = e.num('bodyWidth', 2.0)
          d = e.num('bodyDepth', 2.4)
          ah = e.num('aboveGroundHeight', 1.0)
          cr = e.num('columnDiameter', 0.8) / 2
          col = k.mat('columnMaterial', 'steel_dark')
          frac = FRACTION_COLOURS[e.text('wasteFraction', 'mixed')] || 'steel_dark'
          if e.bool('showExcavation', false)
            mg = e.num('excavationMargin', 0.5)
            k.mesh(Mesh.box(0, 0, -d - 0.2, l + (2 * mg), w + (2 * mg), d + 0.2), 'zone_excavation', 'Gabarit iskopa')
          end
          if e.text('installation', 'underground') == 'semi_underground'
            r = [l, w].min / 2
            k.lathe([[0, -d], [r, -d], [r, ah - 0.1], [r * 0.95, ah - 0.06], [0, ah - 0.06]], k.mat('material', 'concrete_precast'), 'Tijelo kontejnera', n: k.seg(20))
            if k.detail.positive?
              n = [12, 16, 24][k.detail]
              n.times { |i| a = 2 * Math::PI * i / n; k.box(Math.cos(a) * (r + 0.012), Math.sin(a) * (r + 0.012), 0, 0.12, 0.025, ah - 0.12, 'timber', 'Obloga', angle: a + (Math::PI / 2)) }
            end
            k.lathe([[0, ah - 0.06], [r * 1.02, ah - 0.06], [r * 0.9, ah + 0.08], [0, ah + 0.14]], frac, 'Poklopac', n: k.seg(20))
            k.box(0, -r * 0.6, ah + 0.02, 0.5, 0.3, 0.06, 'steel_dark', 'Otvor za ubacivanje')
            return
          end
          k.box(0, 0, -d, l + 0.1, w + 0.1, 0.15, 'concrete_precast', 'Dno jame')
          # pit walls (four precast panels)
          [-1, 1].each do |s|
            k.box(0, s * ((w / 2) + 0.1), -d, l + 0.3, 0.1, d - 0.06, 'concrete_precast', 'Tijelo kontejnera (betonska jama)')
            k.box(s * ((l / 2) + 0.1), 0, -d, 0.1, w + 0.1, d - 0.06, 'concrete_precast', 'Tijelo kontejnera (betonska jama)')
          end
          k.box(0, 0, -d + 0.15, l, w, d - 0.21, 'steel_galv', 'Unutrašnji spremnik')
          k.box(0, 0, -0.06, l + 0.3, w + 0.3, 0.06, 'steel_dark', 'Sigurnosna platforma')
          if k.detail >= 2
            6.times { |i| k.box(0, (-w / 2) + ((i + 0.5) * (w + 0.2) / 6), 0, l + 0.2, 0.02, 0.003, 'steel_galv', 'Protuklizna rebra') }
          end
          k.lathe([[0, 0], [cr, 0], [cr, ah - 0.45], [cr * 0.9, ah - 0.3], [0, ah - 0.3]], col, 'Ulazni stub', n: k.seg(20))
          k.lathe([[0, ah - 0.32], [cr * 1.04, ah - 0.32], [cr * 0.6, ah - 0.18], [0, ah - 0.16]], frac, 'Kapa stuba', n: k.seg(20))
          k.beam_rect([0, -cr - 0.01, ah * 0.35], [0, -cr - 0.01, ah * 0.6], 0.45, 0.03, frac, 'Bubanj za ubacivanje')
          k.cyl(0, 0, ah - 0.16, 0.05, 0.08, 'steel_galv', 'Nosač za podizanje', n: 10)
          k.lathe([[0, ah - 0.08], [0.12, ah - 0.08], [0.06, ah], [0, ah]], 'steel_galv', 'Gljiva za podizanje', n: 12)
        end

        def eurocontainer(k, e)
          w = e.num('width', 1.4)
          d = e.num('depth', 1.1)
          h = e.num('height', 1.4)
          m = k.mat('material', 'plastic_dark')
          prof = [[-d / 2 + 0.12, 0.18], [d / 2 - 0.12, 0.18], [d / 2, h - 0.1], [-d / 2, h - 0.1]]
          k.plate([w / 2, 0, 0], [0.0, 1.0], prof, w, m, 'Tijelo kontejnera')
          k.beam_rect([0, d / 2 + 0.02, h - 0.08], [0, -d / 2 - 0.04, h - 0.05], w + 0.04, 0.05, m, 'Poklopac')
          [[-1, -1], [1, -1], [1, 1], [-1, 1]].each do |sx, sy|
            x = sx * (w / 2 - 0.15)
            y = sy * (d / 2 - 0.22)
            k.box(x, y, 0.12, 0.08, 0.08, 0.06, 'steel_galv', 'Nosač kotača')
            k.bar([x - 0.03, y, 0.06], [x + 0.03, y, 0.06], 0.06, 'rubber_black', 'Kotač', n: 10)
          end
          [-1, 1].each { |s| k.bar([s * (w / 2 + 0.03), -0.25, h - 0.35], [s * (w / 2 + 0.03), 0.25, h - 0.35], 0.02, 'steel_galv', 'Ručka') }
        end

        def fountain(k, e)
          r = e.num('basinDiameter', 3.0) / 2
          bh = e.num('basinHeight', 0.45)
          rim = e.num('rimWidth', 0.25)
          jet = e.num('jetHeight', 2.0)
          stone = k.mat('basinMaterial', 'stone_wall')
          water = k.mat('waterMaterial', 'water')
          k.lathe([[r - rim, 0.0], [r, 0.0], [r, bh - 0.03], [r - 0.02, bh], [r - rim + 0.02, bh], [r - rim, bh - 0.03]], stone, 'Rub bazena', closed_loop: true, n: k.seg(28))
          k.cyl(0, 0, -0.1, r - rim, 0.1, 'pool_finish', 'Dno bazena', n: k.seg(28))
          k.cyl(0, 0, 0.0, r - rim - 0.005, bh - 0.12, water, 'Voda', n: k.seg(28))
          k.lathe([[0, 0], [0.22, 0], [0.18, 0.15], [0.12, 0.7], [0.18, 0.8], [0, 0.8]], stone, 'Postament', n: k.seg(16))
          k.lathe([[0, 0.78], [0.12, 0.78], [r * 0.38, 0.92], [r * 0.4, 0.98], [r * 0.36, 0.98], [0.1, 0.86], [0, 0.86]], stone, 'Tanjir fontane', n: k.seg(24))
          k.cyl(0, 0, 0.9, r * 0.35, 0.06, water, 'Voda u tanjiru', n: k.seg(24))
          jr = e.num('jetDiameter', 0.08) / 2
          k.lathe([[0, 0.96], [jr * 1.5, 0.96], [jr, 0.96 + (jet * 0.8)], [jr * 2.2, 0.96 + jet], [0, 0.96 + jet + 0.05]], water, 'Mlaz', n: 10)
        end

        def water_jet(k, e)
          r = e.num('diameter', 0.2) / 2
          k.cyl(0, 0, -0.25, r * 2, 0.25, 'plastic_dark', 'Ugradna kutija mlaznice', n: k.seg(14))
          k.box(0, 0, -0.01, r * 4.4, r * 4.4, 0.012, 'grate_steel', 'Rešetka')
          k.lathe([[0, 0.0], [0.02, 0.0], [0.012, 1.2], [0.04, 1.4], [0, 1.45]], 'water', 'Vodeni mlaz', n: 8)
        end

        def inlet(k, e)
          size = e.num('innerSize', 0.45)
          depth = e.num('depth', 0.9)
          t = e.num('wallThickness', 0.08)
          m = k.mat('material', 'concrete_precast')
          k.box(0, 0, -depth - 0.1, size + (2 * t), size + (2 * t), 0.1, m, 'Dno slivnika')
          k.prism(Mesh.rect_ring(0, 0, size + (2 * t), size + (2 * t)), -depth, -0.06, m, 'Tijelo slivnika', holes: [Mesh.rect_ring(0, 0, size, size)])
          k.prism(Mesh.rect_ring(0, 0, size + (2 * t), size + (2 * t)), -0.06, 0.0, 'cast_iron', 'Okvir rešetke', holes: [Mesh.rect_ring(0, 0, size, size)])
          bars = [4, 8, 12][k.detail]
          bars.times { |i| k.box((-size / 2) + ((i + 0.5) * size / bars), 0, -0.05, size / bars * 0.55, size - 0.01, 0.05, 'cast_iron', 'Šipka rešetke') }
          k.bar([size / 2, 0, -depth + 0.25], [size / 2 + t + 0.2, 0, -depth + 0.25], 0.08, 'hdpe', 'Izlazna cijev', n: k.seg(12))
        end

        def collector(k, e)
          r = e.num('width', 0.8) / 2
          h = e.num('height', 1.2)
          m = k.mat('material', 'plastic_green')
          k.box(0, 0, 0, r * 2.2, r * 2.2, 0.2, 'concrete_precast', 'Postolje')
          k.lathe([[0, 0.2], [r * 0.9, 0.2], [r, 0.35], [r, h - 0.1], [r * 0.95, h], [0, h]], m, 'Bačva za kišnicu', n: k.seg(20))
          [0.45, h - 0.25].each { |z| k.torus([0, 0, z], r * 1.005, 0.015, m, 'Obruč', n: k.seg(20)) } if k.detail.positive?
          k.cyl(0, 0, h, r * 0.85, 0.04, m, 'Poklopac', n: k.seg(20))
          k.bar([0, -r, 0.42], [0, -r - 0.1, 0.42], 0.02, 'steel_galv', 'Slavina', n: 8)
          k.rail([[r * 0.3, r * 0.3, h + 0.04], [r * 0.3, r * 0.3, h + 0.5], [r * 0.3, r + 0.15, h + 0.7], [r * 0.3, r + 0.15, h + 1.6]], 0.04, 'steel_galv', 'Oluk')
        end

        def crates(k, e)
          l = e.num('length', 6.0)
          w = e.num('width', 2.4)
          h = e.num('height', 0.66)
          z0 = -e.num('coverDepth', 0.6) - h
          nx = [(l / 1.2).round, 1].max
          ny = [(w / 0.6).round, 1].max
          nz = [(h / 0.66).round, 1].max
          if k.detail.zero?
            k.box(0, 0, z0, l, w, h, 'hdpe', 'Infiltracijski blokovi')
          else
            nx.times do |i|
              ny.times do |j|
                nz.times do |kk|
                  x = (-l / 2) + ((i + 0.5) * l / nx)
                  y = (-w / 2) + ((j + 0.5) * w / ny)
                  z = z0 + (kk * h / nz)
                  k.place("blok-#{format('%.3g', l / nx)}-#{format('%.3g', w / ny)}-#{format('%.3g', h / nz)}", [x, y, z]) do |pk|
                    crate(pk, (l / nx) - 0.01, (w / ny) - 0.01, (h / nz) - 0.01)
                  end
                end
              end
            end
          end
          k.box(0, 0, z0 - 0.1, l + 0.2, w + 0.2, 0.1, 'gravel', 'Šljunčana posteljica')
        end

        # One plastic infiltration crate: frame plates and open lattice.
        def crate(k, l, w, h)
          k.box(0, 0, 0, l, w, 0.03, 'hdpe', 'Ploča bloka')
          k.box(0, 0, h - 0.03, l, w, 0.03, 'hdpe', 'Ploča bloka')
          [[-1, -1], [1, -1], [1, 1], [-1, 1]].each { |sx, sy| k.cyl(sx * (l / 2 - 0.08), sy * (w / 2 - 0.08), 0.03, 0.04, h - 0.06, 'hdpe', 'Stup', n: 8) }
          k.cyl(0, 0, 0.03, 0.05, h - 0.06, 'hdpe', 'Stup', n: 8)
        end

        def standpipe(k, e)
          h = e.num('height', 0.8)
          k.box(0, 0, 0, 0.6, 0.6, 0.05, 'grate_steel', 'Odvodna rešetka')
          k.box(0, 0.1, 0.05, 0.16, 0.16, h, k.mat('material', 'steel_galv'), 'Hidrantski stub')
          k.cyl(0, 0.1, h + 0.05, 0.1, 0.02, k.mat('material', 'steel_galv'), 'Kapa', n: 12)
          k.rail([[0, 0.02, h - 0.1], [0, -0.06, h - 0.1], [0, -0.08, h - 0.2]], 0.015, 'stainless', 'Slavina')
          k.cyl(0, -0.08, h - 0.04, 0.035, 0.015, 'plastic_blue', 'Ručica', n: 10)
        end
      end

      %w[water.inspection_chamber water.surface_inlet utility.electrical_chamber utility.telecom_chamber utility.water_meter_chamber
         utility.valve_chamber utility.manhole utility.pump_chamber].each do |id|
        define(id) { |k, e| id == 'water.surface_inlet' ? Util.inlet(k, e) : Util.chamber(k, e) }
      end
      define('utility.waste_container_ug_3m3') { |k, e| Util.underground_container(k, e) }
      define('utility.waste_container_ug_5m3') { |k, e| Util.underground_container(k, e) }
      define('utility.waste_container_semi') { |k, e| Util.underground_container(k, e) }
      define('utility.waste_container_above') { |k, e| Util.eurocontainer(k, e) }
      define('utility.cistern') { |k, e| Util.tank(k, e, label: 'Cisterna') }
      define('utility.water_tank') { |k, e| Util.tank(k, e, label: 'Spremnik vode') }
      define('utility.septic_tank') do |k, e|
        Util.tank(k, e, label: 'Septička jama')
        z = -e.num('coverDepth', 0.8)
        [-1, 1].each { |s| k.cyl(s * e.num('length', 3.0) * 0.3, 0, z, 0.3, -z - 0.08, 'concrete_precast', 'Okno', n: 16) }
        [-1, 1].each { |s| k.cyl(s * e.num('length', 3.0) * 0.3, 0, -0.08, 0.32, 0.08, 'cast_iron', 'Poklopac', n: 16) }
      end
      define('utility.equipment_enclosure') { |k, e| Light.cabinet(k, e, label: 'Kućište opreme') }
      define('water.rainwater_tank') { |k, e| Util.tank(k, e, label: 'Spremnik kišnice') }
      define('water.rainwater_collector') { |k, e| Util.collector(k, e) }
      define('water.infiltration_crates') { |k, e| Util.crates(k, e) }
      define('sustainability.suds_crate') { |k, e| Util.crates(k, e) }
      define('sustainability.rainwater_harvesting') { |k, e| Util.tank(k, e, label: 'Spremnik za korištenje kišnice') }
      define('water.fountain') { |k, e| Util.fountain(k, e) }
      define('water.water_jet') { |k, e| Util.water_jet(k, e) }
      define('water.supply_point') { |k, e| Util.standpipe(k, e) }
    end
  end
end
