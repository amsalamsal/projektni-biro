# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Models
      # Vegetation: branched trees with clustered foliage, shrubs, planters and planted areas.
      # Plant geometry is generic (no species claims): form, size and colours follow the element parameters.
      module Veg
        module_function

        # Tree with trunk, root flare, scaffold branches and foliage clusters inside the crown envelope
        # (overall height = height, crown width <= canopyDiameter).
        def tree(k, e, foliage:, fruit: false)
          k.fit(width: e.num('canopyDiameter', 5.0), height: e.num('height', 8.0)) { tree_body(k, e, foliage: foliage, fruit: fruit) }
        end

        def tree_body(k, e, foliage:, fruit: false)
          h = e.num('height', 8.0)
          canopy = e.num('canopyDiameter', 5.0)
          clear = [e.num('clearTrunkHeight', 2.2), h * 0.85].min
          tr = e.num('trunkDiameter', 0.25) / 2.0
          form = e.text('form', 'round')
          crown_h = h - clear
          return conifer(k, h, canopy, clear, tr, foliage) if form == 'conical'

          r = canopy / 2.0
          rz = crown_h / 2.0
          zc = clear + rz
          if k.detail.zero?
            k.bar([0, 0, 0], [0, 0, clear + (crown_h * 0.3)], tr, 'bark', 'Deblo', r2: tr * 0.7, n: 6)
            k.blob([0, 0, zc], r * 0.92, r * 0.92, rz * 0.92, foliage, 'Krošnja', rough: 0.07, level: 1)
            return
          end

          k.frustum(0, 0, 0, tr * 1.7, tr, [tr * 1.6, 0.3].min, 'bark', 'Korijenski vrat', n: k.seg(10))
          k.bar([0, 0, 0], [0, 0, clear + (crown_h * 0.45)], tr, 'bark', 'Deblo', r2: tr * 0.55, n: k.seg(10))
          return columnar(k, r, rz, zc, clear, h, tr, foliage) if form == 'columnar'

          count = k.detail >= 2 ? 22 : 11
          size = k.detail >= 2 ? 0.36 : 0.46
          clusters = Array.new(count) do |i|
            rc = r * size * k.rand(0.8, 1.15)
            rcz = [rc * (rz / r), rz * 0.7].min
            # golden-angle spiral directions, biased to the upper crown
            t = i * 2.39996
            dz = 1.0 - (1.6 * (i + 0.5) / count)
            dz = [dz, -0.55].max
            ring = Math.sqrt([1.0 - (dz * dz), 0.0].max)
            c = [Math.cos(t) * ring * (r - (rc * 1.12)), Math.sin(t) * ring * (r - (rc * 1.12)), zc + (dz * (rz - (rcz * 1.12)))]
            c[2] = [c[2], clear + (rcz * 0.9)].max
            [c, rc, rc, rcz]
          end
          clusters << [[0, 0, zc], r * 0.55, r * 0.55, rz * 0.6]
          # balance the crown on the trunk axis (organic, but centred on the insertion point)
          mx = (clusters.map { |(c, rc, _, _)| c[0] - rc }.min + clusters.map { |(c, rc, _, _)| c[0] + rc }.max) / 2.0
          my = (clusters.map { |(c, rc, _, _)| c[1] - rc }.min + clusters.map { |(c, rc, _, _)| c[1] + rc }.max) / 2.0
          clusters = clusters.map { |(c, rx, ry, rcz)| [[c[0] - mx, c[1] - my, c[2]], rx, ry, rcz] }
          scaffold = clusters.first(k.detail >= 2 ? 9 : 6).map do |(c, rc, _, _)|
            start = [0.0, 0.0, clear + k.rand(0.0, crown_h * 0.3)]
            tip = [c[0] * 0.85, c[1] * 0.85, c[2] - (rc * 0.2)]
            [start, tip, tr * 0.45, tr * 0.12]
          end
          if k.detail >= 2
            scaffold += scaffold.each_with_index.map do |(s, t, _, _), i|
              mid = Mesh.scale3(Mesh.add3(s, t), 0.5)
              other = clusters[(i + 9) % (clusters.length - 1)][0]
              [mid, Mesh.add3(mid, Mesh.scale3(Mesh.sub3(other, mid), 0.6)), tr * 0.14, tr * 0.06]
            end
          end
          k.bars(scaffold, 'bark', 'Grane', n: k.seg(5))
          # foliage measured after the irregular surface is made, then centred on the trunk axis
          crown = clusters.flat_map { |(c, rx, ry, rcz)| Kit.blob_polys(c, rx, ry, rcz, 0.15, k.detail >= 2 ? 2 : 1, k.rng) }
          xs = crown.flatten(1).map { |q| q[0] }.minmax
          ys = crown.flatten(1).map { |q| q[1] }.minmax
          dx = -(xs[0] + xs[1]) / 2.0
          dy = -(ys[0] + ys[1]) / 2.0
          k.mesh(crown.map { |t| t.map { |q| [q[0] + dx, q[1] + dy, q[2]] } }, foliage, 'Krošnja', smooth: true)
          fruits(k, clusters.map { |(c, *r)| [[c[0] + dx, c[1] + dy, c[2]], *r] }) if fruit && k.detail >= 2
        end

        def columnar(k, r, rz, zc, clear, h, tr, foliage)
          n = k.detail >= 2 ? 9 : 5
          specs = Array.new(n) do |i|
            f = (i + 0.5) / n
            z = clear + ((h - clear) * f)
            w = r * Math.sin(Math::PI * (0.15 + (0.8 * f))) * k.rand(0.85, 1.0)
            [[k.rand(-0.05, 0.05) * r, k.rand(-0.05, 0.05) * r, z], w * 0.92, w * 0.92, (h - clear) / n * 0.95]
          end
          specs.first[0][2] = [specs.first[0][2], clear + specs.first[3]].max
          specs.last[0][2] = h - specs.last[3]
          k.bar([0, 0, clear], [0, 0, h * 0.9], tr * 0.5, 'bark', 'Provodnica', r2: tr * 0.15, n: 5)
          k.blobs(specs, foliage, 'Krošnja', rough: 0.12)
        end

        # Conifer: stacked whorls with drooping skirts, slightly irregular per tier.
        def conifer(k, h, canopy, clear, tr, foliage)
          r = canopy / 2.0
          k.bar([0, 0, 0], [0, 0, h * 0.92], tr, 'bark', 'Deblo', r2: tr * 0.25, n: k.detail.zero? ? 6 : k.seg(8))
          tiers = [3, 6, 10][k.detail]
          th = (h - clear) / tiers
          polys = []
          tiers.times do |i|
            z0 = clear + (i * th * 0.92)
            f = 1.0 - (i.to_f / tiers)
            rr = r * (f**0.85) * (i.zero? ? 1.0 : k.rand(0.88, 1.0))
            top = i == tiers - 1 ? h : z0 + (th * 1.7)
            n = [9, 11, 13][k.detail] + (i % 3)
            polys.concat(Kit.lathe_polys([[0.0, z0 + (th * 0.35)], [rr, z0], [0.0, top]], 0.0, 0.0, n, false))
          end
          k.mesh(polys, foliage, 'Krošnja')
        end

        def fruits(k, clusters)
          polys = []
          clusters[0...-1].each do |(c, rc, _, rcz)|
            3.times do
              a = k.rand(0, 2 * Math::PI)
              b = k.rand(-0.6, 0.4)
              p = [c[0] + (Math.cos(a) * Math.cos(b) * rc * 0.98), c[1] + (Math.sin(a) * Math.cos(b) * rc * 0.98), c[2] + (Math.sin(b) * rcz * 0.98)]
              polys.concat(Mesh.ellipsoid(p, 0.04, 0.04, 0.045, 6, 3))
            end
          end
          k.mesh(polys, 'fruit_red', 'Plodovi', smooth: true)
        end

        # Shrub: dome of foliage lumps standing on the ground.
        def shrub(k, h, d, foliage, x: 0.0, y: 0.0, z: 0.0)
          r = d / 2.0
          return k.blob([x, y, z + (h / 2.0)], r * 0.95, r * 0.95, h / 2.0 * 0.95, foliage, 'Grm', rough: 0.1, level: 1) if k.detail.zero?

          n = k.detail >= 2 ? 11 : 6
          specs = Array.new(n) do |i|
            t = i * 2.39996
            ring = i.zero? ? 0.0 : Math.sqrt(i.to_f / n)
            rc = r * k.rand(0.38, 0.5)
            rcz = [h * k.rand(0.32, 0.45), h / 2.0].min
            cx = x + (Math.cos(t) * ring * (r - rc))
            cy = y + (Math.sin(t) * ring * (r - rc))
            cz = [h - rcz - (ring * h * 0.35), rcz * 0.8].max
            [[cx, cy, z + cz], rc, rc, rcz]
          end
          k.blobs(specs, foliage, 'Grm', rough: 0.18)
        end

        # Tapered bowl planter (r at the rim), filled with soil to 85 %.
        def bowl(k, r, h, material)
          k.lathe([[0.0, 0.0], [r * 0.72, 0.0], [r * 0.74, 0.04], [r * 0.97, h * 0.92], [r, h], [r * 0.9, h], [r * 0.86, h * 0.86], [0.0, h * 0.86]],
                  material, 'Posuda', n: k.seg(20))
          k.cyl(0, 0, h * 0.86, r * 0.87, 0.02, 'soil', 'Zemlja', n: k.seg(20))
        end

        # Low flowering clump (leaves + blossom dots) used by beds and planters.
        def flower_clump(k, r, h, leaf, colours)
          k.blobs([[[0, 0, h * 0.45], r, r, h * 0.45], [[r * 0.35, -r * 0.2, h * 0.4], r * 0.7, r * 0.7, h * 0.4]], leaf, 'Listovi', rough: 0.2, level: 1)
          return if colours.empty? || k.detail.zero?

          dots = Array.new(k.detail >= 2 ? 9 : 5) do |i|
            a = i * 2.39996
            rr = r * 0.75 * Math.sqrt((i + 0.5) / 9.0)
            [[Math.cos(a) * rr, Math.sin(a) * rr, h * 0.82 + (k.rand * h * 0.12)], r * 0.18, r * 0.18, r * 0.12]
          end
          colours.each_with_index do |c, ci|
            sel = dots.each_with_index.select { |_, i| i % colours.length == ci }.map(&:first)
            k.blobs(sel, c, 'Cvjetovi', rough: 0.1, level: 1) unless sel.empty?
          end
        end

        # Grass tuft: fan of tapered blades (bars) from one base.
        def grass_tuft(k, h, r, colour)
          n = [5, 9, 15][k.detail]
          blades = Array.new(n) do |i|
            a = i * 2.39996
            lean = k.rand(0.25, 0.6)
            top = [Math.cos(a) * r * lean, Math.sin(a) * r * lean, h * k.rand(0.75, 1.0)]
            [[Math.cos(a) * r * 0.08, Math.sin(a) * r * 0.08, 0.0], top, 0.012, 0.003]
          end
          k.bars(blades, colour, 'Vlati', n: 4)
          k.blob([0, 0, h * 0.12], r * 0.3, r * 0.3, h * 0.14, colour, 'Busen', rough: 0.15, level: 1)
        end

        # Planted bed: soil body plus repeated plant components scattered inside the outline.
        PLANTING = {
          'landscape.flower_bed' => { spacing: 0.45, kinds: [[:flower, %w[flower_red flower_yellow]], [:flower, %w[flower_white flower_violet]], [:flower, %w[flower_pink]]] },
          'landscape.rose_garden' => { spacing: 0.7, kinds: [[:rose, %w[flower_red]], [:rose, %w[flower_pink]], [:rose, %w[flower_white]]] },
          'landscape.ornamental_grasses' => { spacing: 0.6, kinds: [[:grass, 'grass_blade'], [:grass, 'meadow'], [:grass, 'grass_blade']] },
          'landscape.perennials' => { spacing: 0.5, kinds: [[:flower, %w[flower_violet]], [:flower, %w[flower_white]], [:mound, 'foliage_light']] },
          'landscape.ground_cover' => { spacing: 0.35, kinds: [[:mat, 'foliage'], [:mat, 'foliage_dark'], [:mat, 'foliage_light']] },
          'landscape.meadow' => { spacing: 0.4, kinds: [[:grass, 'meadow'], [:flower, %w[flower_yellow flower_white]], [:grass, 'grass']] },
          'landscape.green_roof' => { spacing: 0.3, height: 0.12, kinds: [[:mat, 'sedum'], [:mat, 'foliage_light'], [:flower, %w[flower_pink]]] }
        }.freeze

        def plant(k, kind, arg, h)
          case kind
          when :flower then flower_clump(k, h * 0.5, h, 'foliage', arg)
          when :rose
            shrub(k, h, h * 0.9, 'foliage_dark')
            flower_clump(k, h * 0.25, h * 1.02, 'foliage_dark', arg) if k.detail.positive?
          when :grass then grass_tuft(k, h, h * 0.55, arg)
          when :mound then k.blob([0, 0, h * 0.4], h * 0.45, h * 0.45, h * 0.4, arg, 'Biljka', rough: 0.2, level: 1)
          else k.blob([0, 0, h * 0.25], h * 0.5, h * 0.5, h * 0.25, arg, 'Pokrivač', rough: 0.25, level: 1)
          end
        end

        def planted_area(k, e, spec)
          outer, holes = k.outline
          z = (outer.first[2] || 0.0) + e.num('topOffset', 0.0)
          soil = e.param('soilThickness').nil? ? e.num('thickness', 0.15) : e.num('soilThickness', 0.3)
          k.prism(outer, z - soil, z, k.mat('soilMaterial', 'soil'), 'Supstrat', holes: holes)
          return if k.detail.zero?

          h = spec[:height] || e.num('plantHeight', 0.6)
          spacing = e.num('plantSpacing', 0.0).positive? ? e.num('plantSpacing', 0.0) : spec[:spacing]
          spacing *= 1.4 if k.detail == 1
          k.scatter(outer, holes, spacing, inset: spacing * 0.3).each_with_index do |(x, y), i|
            v = i % spec[:kinds].length
            kind, arg = spec[:kinds][v]
            k.place("biljka-#{v}-#{format('%.3g', h)}", [x, y, z], k.rand(0, 2 * Math::PI)) { |pk| plant(pk, kind, arg, h) }
          end
        end
      end

      %w[landscape.tree_deciduous landscape.tree_existing].each do |id|
        define(id) { |k, e| Veg.tree(k, e, foliage: k.mat('foliageMaterial', 'foliage')) }
      end
      define('landscape.tree_evergreen') { |k, e| Veg.tree(k, e, foliage: k.mat('foliageMaterial', 'foliage_evergreen')) }
      define('landscape.tree_columnar') { |k, e| Veg.tree(k, e, foliage: k.mat('foliageMaterial', 'foliage')) }
      %w[landscape.tree_fruit landscape.orchard_tree].each do |id|
        define(id) { |k, e| Veg.tree(k, e, foliage: k.mat('foliageMaterial', 'foliage_fruit'), fruit: true) }
      end

      define('landscape.shrub') { |k, e| Veg.shrub(k, e.num('height', 1.2), e.num('diameter', 1.2), k.mat('foliageMaterial', 'foliage')) }

      define('landscape.planting_group') do |k, e|
        d = e.num('diameter', 3.0)
        h = e.num('height', 1.4)
        colours = %w[foliage foliage_dark foliage_light foliage]
        5.times do |i|
          a = i * 2.39996
          ring = i.zero? ? 0.0 : d * 0.3
          s = i.zero? ? 1.0 : k.rand(0.55, 0.8)
          Veg.shrub(k, h * s, d * 0.42 * s, colours[i % 4], x: Math.cos(a) * ring, y: Math.sin(a) * ring)
        end
      end

      define('landscape.seasonal_planter') do |k, e|
        r = e.num('diameter', 1.2) / 2.0
        h = e.num('height', 0.5)
        Veg.bowl(k, r, h, k.mat('material', 'concrete'))
        cols = [%w[flower_red flower_yellow], %w[flower_violet flower_white], %w[flower_pink]]
        n = [3, 5, 7][k.detail]
        n.times do |i|
          a = i * 2.39996
          rr = i.zero? ? 0.0 : r * 0.55
          k.place("sezonsko-#{i % 3}", [Math.cos(a) * rr, Math.sin(a) * rr, h * 0.88], a) { |pk| Veg.flower_clump(pk, r * 0.28, 0.28, 'foliage', cols[i % 3]) }
        end
      end

      define('landscape.planting_container') do |k, e|
        r = e.num('diameter', 1.0) / 2.0
        h = e.num('height', 0.8)
        Veg.bowl(k, r, h, k.mat('material', 'concrete'))
        Veg.shrub(k, [r * 1.2, 0.9].max, r * 1.5, 'foliage', z: h * 0.86) if k.detail.positive?
      end

      Veg::PLANTING.each do |id, spec|
        define(id, geometry: 'polygon') { |k, e| Veg.planted_area(k, e, spec) }
      end
    end
  end
end
