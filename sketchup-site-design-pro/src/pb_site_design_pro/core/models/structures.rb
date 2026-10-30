# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Models
      # Shelters, canopies and gates. Shelter: width along X, depth along Y, back wall on +Y.
      # Gates: opening along X centred on the insertion point, leaves in the X-Z plane.
      module Shelter
        module_function

        def shelter(k, e, bench: false, bikes: false)
          w = e.num('width', 3.0)
          d = e.num('depth', 1.5)
          h = e.num('height', 2.5)
          roof = e.text('roofType', 'mono')
          slope = e.num('roofSlope', 5.0) * Math::PI / 180
          rt = e.num('roofThickness', 0.12)
          post = e.num('postSize', 0.1)
          fm = k.mat('frameMaterial', 'steel_dark')
          wm = k.mat('wallMaterial', 'glass')
          rm = k.mat('roofMaterial', 'steel_dark')
          nx = [(w / 2.5).ceil, 1].max
          xs = Array.new(nx + 1) { |i| (-w / 2) + (post / 2) + (i * (w - post) / nx) }
          rise = roof == 'mono' ? d * Math.tan(slope) : 0.0
          xs.each do |x|
            k.box(x, d / 2 - (post / 2), 0, post, post, h, fm, 'Stub')
            k.box(x, -d / 2 + (post / 2), 0, post, post, h - rise, fm, 'Stub')
          end
          # roof
          case roof
          when 'pyramid'
            apex = h + ([w, d].max / 2 * Math.tan(slope))
            k.mesh(Mesh.fix_orientation(Kit.lathe_polys([[0, h], [[w, d].max * 0.74, h], [0, apex]], 0, 0, 4, false)).map { |p| p.map { |q| Furn.rot45(q, 0.0) } }, rm, 'Krov')
            k.box(0, 0, h - 0.12, w, d, 0.12, fm, 'Vijenac')
          when 'mono'
            k.beam_rect([0, d / 2 + 0.1, h + 0.02], [0, -d / 2 - 0.15, h - rise - 0.02], w + 0.2, rt, rm, 'Krov')
            k.beam_rect([-w / 2, d / 2, h - 0.05], [-w / 2, -d / 2, h - rise - 0.05], 0.06, 0.1, fm, 'Rožnjača') if k.detail.positive?
            k.beam_rect([w / 2, d / 2, h - 0.05], [w / 2, -d / 2, h - rise - 0.05], 0.06, 0.1, fm, 'Rožnjača') if k.detail.positive?
          else
            k.box(0, 0, h, w + 0.2, d + 0.2, rt, rm, 'Krov')
            k.box(0, 0, h - 0.08, w, d, 0.08, fm, 'Okvir krova')
          end
          # glazed walls with frames
          if e.bool('backWall', true)
            k.box(0, d / 2 - (post / 2), 0.1, w - post, 0.012, h - 0.35, wm, 'Zadnji zid')
            k.box(0, d / 2 - (post / 2), 0.08, w - post, 0.04, 0.04, fm, 'Okvir')
            k.box(0, d / 2 - (post / 2), h - 0.27, w - post, 0.04, 0.04, fm, 'Okvir')
          end
          if e.bool('sideWalls', false)
            [-w / 2 + (post / 2), w / 2 - (post / 2)].each { |x| k.box(x, 0, 0.1, 0.012, d - post, h - rise - 0.35, wm, 'Bočni zid') }
          end
          if bench
            k.box(0, d / 2 - 0.25, 0.42, w * 0.7, 0.35, 0.04, 'timber', 'Klupa')
            [-w * 0.3, w * 0.3].each { |x| k.box(x, d / 2 - 0.15, 0, 0.05, 0.05, 0.42, fm, 'Nosač klupe') }
          end
          if bikes
            n = [(w / 0.9).floor, 1].max
            n.times do |i|
              x = (-w / 2) + ((i + 0.5) * w / n)
              k.rail([[x, -0.35, 0], [x, -0.35, 0.65], [x, 0.35, 0.65], [x, 0.35, 0]], 0.025, 'steel_galv', 'Stalak')
            end
          end
        end

        def entrance_canopy(k, e)
          w = e.num('width', 3.0)
          d = e.num('depth', 1.5)
          z = e.num('baseOffset', 2.7)
          k.box(0, -d / 2, z, w, d, 0.025, 'glass', 'Staklena nadstrešnica')
          k.beam_rect([-w / 2, 0, z - 0.04], [-w / 2, -d, z - 0.04], 0.06, 0.08, 'steel_galv', 'Konzola')
          k.beam_rect([w / 2, 0, z - 0.04], [w / 2, -d, z - 0.04], 0.06, 0.08, 'steel_galv', 'Konzola')
          [-w / 2, w / 2].each do |x|
            k.bar([x, 0, z + 0.9], [x, -d * 0.9, z + 0.03], 0.01, 'stainless', 'Zatega', n: 6)
            k.box(x, 0.01, z + 0.85, 0.1, 0.02, 0.12, 'stainless', 'Zidna pločica')
          end
          k.box(0, 0.01, z - 0.04, w, 0.02, 0.1, 'steel_galv', 'Zidni profil')
        end

        # Infill of a gate leaf: frame plus vertical bars.
        def leaf(k, x0, x1, z0, z1, y, m, name: 'Krilo', t: 0.05)
          k.beam([x0, y, z0], [x1, y, z0], t, m, name)
          k.beam([x0, y, z1], [x1, y, z1], t, m, name)
          k.beam([x0, y, z0], [x0, y, z1], t, m, name)
          k.beam([x1, y, z0], [x1, y, z1], t, m, name)
          n = [((x1 - x0) / 0.12).floor, 1].max
          n.times { |i| x = x0 + ((i + 1) * (x1 - x0) / (n + 1)); k.box(x, y, z0, 0.02, 0.02, z1 - z0, m, "#{name} – šipka") } if k.detail.positive?
        end

        def gate(k, e)
          w = e.num('width', 1.0)
          h = e.num('height', 1.8)
          post = e.num('postSize', 0.1)
          m = k.mat('material', 'steel_dark')
          kind = e.text('gateType', 'swing_single')
          return Traffic.barrier(k, e) if kind == 'barrier'

          lt = [e.num('leafThickness', 0.05), 0.02].max
          side = e.text('openingSide', 'back') == 'front' ? -1.0 : 1.0 # leaves open towards +Y (back) or -Y (front)
          auto = e.bool('automatic', false)

          px = (w / 2) + (post / 2)
          [-px, px].each do |x|
            k.box(x, 0, -0.4, post, post, h + 0.45, m, 'Stub kapije')
            k.box(x, 0, h + 0.05, post + 0.02, post + 0.02, 0.02, m, 'Kapa stuba')
          end
          case kind
          when 'swing_double'
            leaf(k, -w / 2 + 0.02, -0.01, 0.08, h - 0.02, 0, m, t: lt)
            leaf(k, 0.01, w / 2 - 0.02, 0.08, h - 0.02, 0, m, t: lt)
            k.box(0.04, -0.04, h * 0.5, 0.04, 0.05, 0.12, 'steel_galv', 'Brava')
          when 'sliding'
            # track and leaf on the opening side (+Y back, -Y front)
            tl = (w * 2) + 0.4
            k.box(-w / 2, side * 0.15, -0.02, tl, 0.08, 0.02, 'steel_galv', 'Vodilica')
            leaf(k, -w / 2 + 0.02, w / 2 + 0.3, 0.12, h - 0.02, side * 0.15, m, name: 'Klizno krilo', t: lt)
            [-w / 2 + 0.3, w / 2].each { |x| k.bar([x, side * 0.13, 0.06], [x, side * 0.17, 0.06], 0.06, 'steel_galv', 'Kotač', n: 10) }
            k.box(-px - 0.4, side * 0.35, 0, 0.3, 0.25, 0.4, 'steel_galv', 'Pogonski motor') if auto
            k.box(-px - 0.4, side * 0.2, 0.42, 0.06, 0.06, 0.04, 'flower_yellow', 'Signalna lampa') if auto
          else
            leaf(k, -w / 2 + 0.02, w / 2 - 0.02, 0.08, h - 0.02, 0, m, t: lt)
            k.box(w / 2 - 0.08, -0.04, h * 0.5, 0.04, 0.05, 0.14, 'steel_galv', 'Brava')
            k.bar([w / 2 - 0.08, -0.05, h * 0.5 + 0.04], [w / 2 - 0.2, -0.05, h * 0.5 + 0.04], 0.012, 'steel_galv', 'Kvaka', n: 6)
          end
          return if kind == 'sliding'

          # hinges on the opening side of the hinge posts; automatic swing gates get arm operators there
          hinges = kind == 'swing_double' ? [-w / 2, w / 2] : [-w / 2]
          hinges.each do |x|
            [h * 0.2, h * 0.75].each { |z| k.cyl(x, side * (lt / 2 + 0.01), z, 0.02, 0.08, 'steel_galv', 'Šarka', n: 8) }
            next unless auto

            px_side = x.negative? ? -px : px
            k.box(px_side, side * (post / 2 + 0.06), h * 0.45, 0.12, 0.12, 0.16, 'steel_galv', 'Pogon krila')
            k.bar([px_side, side * (post / 2 + 0.06), h * 0.5], [x * 0.5, side * (lt / 2 + 0.03), h * 0.5], 0.025, 'steel_galv', 'Krak pogona', n: 8)
          end
          k.box(-px, side * (post / 2 + 0.05), h + 0.08, 0.08, 0.08, 0.06, 'flower_yellow', 'Signalna lampa') if auto
        end
      end

      define('structure.bus_shelter') { |k, e| Shelter.shelter(k, e, bench: true) }
      define('structure.seating_shelter') { |k, e| Shelter.shelter(k, e, bench: true) }
      define('structure.gazebo') { |k, e| Shelter.shelter(k, e, bench: true) }
      define('structure.pavilion') { |k, e| Shelter.shelter(k, e) }
      define('cycling.bike_shelter') { |k, e| Shelter.shelter(k, e, bikes: true) }
      define('structure.entrance_canopy') { |k, e| Shelter.entrance_canopy(k, e) }
      %w[wall.pedestrian_gate wall.vehicle_gate wall.sliding_gate wall.swing_gate wall.access_barrier].each do |id|
        define(id) { |k, e| Shelter.gate(k, e) }
      end
    end
  end
end
