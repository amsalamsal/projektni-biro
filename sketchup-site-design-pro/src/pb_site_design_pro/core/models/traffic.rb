# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Models
      # Signs (shared by traffic, cycling and information), traffic equipment and bicycle infrastructure.
      module Sign
        module_function

        # Post(s) with a framed plate. Plate faces -Y (towards the viewer); posts behind it on +Y.
        def post_sign(k, e, plate_material: nil, face: 'sign_plate', border: 'steel_galv')
          n = [e.num('postCount', 1).to_i, 1].max
          ph = e.num('postHeight', 2.6)
          pr = e.num('postDiameter', 0.076) / 2
          pw = e.num('plateWidth', 0.6)
          plh = e.num('plateHeight', 0.6)
          pb = e.num('plateBottom', 2.0)
          pt = [e.num('plateThickness', 0.02), 0.01].max
          pm = k.mat('postMaterial', 'steel_galv')
          plm = plate_material ? (k.chosen('plateMaterial') || plate_material) : k.mat('plateMaterial', face)
          xs = n == 1 ? [0.0] : Array.new(n) { |i| (-pw / 2) + (pw * 0.15) + (i * (pw * 0.7) / (n - 1)) }
          xs.each do |x|
            k.cyl(x, pr + pt, -0.05, pr, ph + 0.05, pm, 'Stub', n: k.seg(10))
            k.cyl(x, pr + pt, ph, pr * 1.1, 0.02, 'plastic_dark', 'Kapa stuba', n: k.seg(10))
            [pb + (plh * 0.2), pb + (plh * 0.8)].each { |z| k.box(x, pt + (pr * 0.5), z, pr * 2.6, pr * 1.2, 0.04, 'steel_dark', 'Obujmica') } if k.detail.positive?
          end
          zc = pb + (plh / 2)
          case e.text('plateShape', 'rectangle')
          when 'circle'
            k.bar([0, pt, zc], [0, 0, zc], pw / 2, border, 'Rub table', n: k.seg(24))
            k.bar([0, 0, zc], [0, -0.002, zc], pw / 2 * 0.82, plm, 'Tabla', n: k.seg(24))
          when 'triangle'
            tri = [[-pw / 2, 0.0], [pw / 2, 0.0], [0.0, plh]]
            k.plate([0, pt, pb], [1.0, 0.0], tri, pt, border, 'Tabla')
          else
            k.box(0, pt / 2, pb, pw, pt, plh, border, 'Okvir table')
            k.box(0, -0.001, pb + 0.025, pw - 0.05, 0.002, plh - 0.05, plm, 'Lice table')
          end
        end
      end

      module Traffic
        module_function

        def speed_hump(k, e)
          w = e.num('width', 3.5)
          d = e.num('depth', 3.7)
          h = e.num('height', 0.07)
          m = k.mat('material', 'asphalt')
          n = [6, 10, 16][k.detail]
          profile = Array.new(n + 1) { |i| t = i.to_f / n; [(-d / 2) + (d * t), h * Math.sin(Math::PI * t)] }
          profile = [[-d / 2, 0.0]] + profile[1...-1] + [[d / 2, 0.0]]
          k.plate([w / 2, 0, 0], [0.0, 1.0], profile, w, m, 'Usporivač')
          return if k.detail.zero?

          4.times do |i|
            x = (-w / 2) + ((i + 0.5) * w / 4)
            k.box(x, 0, h + 0.001, 0.3, 0.6, 0.003, 'line_paint_white', 'Oznaka')
          end
        end

        def wheel_stop(k, e)
          w = e.num('width', 1.8)
          d = e.num('depth', 0.15)
          h = e.num('height', 0.1)
          m = k.mat('material', 'concrete_precast')
          prof = [[-d / 2, 0.0], [d / 2, 0.0], [d * 0.3, h], [-d * 0.3, h]]
          k.plate([w / 2, 0, 0], [0.0, 1.0], prof, w, m, 'Graničnik')
          [-w * 0.3, 0.0, w * 0.3].each { |x| k.box(x, 0, h, 0.2, d * 0.55, 0.003, 'line_paint_yellow', 'Reflektivna traka') }
          [-w * 0.38, w * 0.38].each { |x| k.cyl(x, 0, h, 0.018, 0.006, 'steel_galv', 'Vijak', n: 8) }
        end

        def bollard(k, e)
          h = e.num('height', 0.9)
          r = e.num('diameter', 0.2) / 2
          m = k.mat('material', 'steel_dark')
          if e.bool('removable', false)
            k.box(0, 0, -0.01, r * 3, r * 3, 0.012, 'steel_galv', 'Ugradna čahura')
            k.cyl(r * 1.1, 0, 0.002, 0.02, 0.01, 'steel_dark', 'Brava', n: 8)
          else
            k.cyl(0, 0, -0.005, r * 1.25, 0.01, m, 'Podnožje', n: k.seg(14))
          end
          k.cyl(0, 0, 0, r, h - (r * 0.6), m, 'Stubić', n: k.seg(14))
          k.lathe([[0, h - (r * 0.6)], [r, h - (r * 0.6)], [r * 0.6, h - (r * 0.1)], [0, h]], m, 'Kapa', n: k.seg(14))
          [h * 0.72, h * 0.8].each { |z| k.cyl(0, 0, z, r * 1.01, 0.03, 'line_paint_white', 'Reflektivna traka', n: k.seg(14)) }
          k.cyl(0, 0, h - (r * 0.6) - 0.07, r * 1.005, 0.05, 'luminaire', 'Svjetlosni pojas', n: k.seg(14)) if e.bool('lit', false)
        end

        def barrier(k, e)
          w = e.num('width', 4.0)
          bh = e.num('height', 1.0)
          post = e.num('postSize', 0.1)
          m = k.mat('material', 'steel_dark')
          k.box(-w / 2, 0, 0, 0.35, 0.35, 1.05, m == 'steel_dark' ? 'steel_galv' : m, 'Kućište rampe')
          k.box(-w / 2, 0, 1.05, 0.4, 0.4, 0.03, 'steel_galv', 'Poklopac')
          k.box(-w / 2, -0.18, 0.6, 0.12, 0.004, 0.12, 'luminaire', 'Signalna lampa') if e.bool('automatic', true)
          seg = [3, 7, 9][k.detail]
          sl = (w - 0.2) / seg
          seg.times do |i|
            x0 = (-w / 2) + 0.18 + (i * sl)
            k.beam_rect([x0, 0, bh - 0.1], [x0 + sl, 0, bh - 0.1], 0.06, 0.09, i.even? ? 'line_paint_white' : 'plastic_red', 'Krak rampe')
          end
          k.box(w / 2 - 0.05, 0, 0, post, post, bh - 0.15, 'steel_galv', 'Oslonac kraka')
          k.box(w / 2 - 0.05, 0, bh - 0.17, 0.18, 0.12, 0.04, 'rubber_black', 'Ležište kraka')
        end

        def pay_station(k, e)
          w = e.num('width', 0.45)
          d = e.num('depth', 0.35)
          h = e.num('height', 1.6)
          m = k.mat('material', 'steel_dark')
          k.box(0, 0, 0, w + 0.08, d + 0.08, 0.1, 'concrete', 'Postolje')
          k.plate([w / 2, 0, 0.1], [0.0, 1.0], [[-d / 2, 0.0], [d / 2, 0.0], [d / 2, h - 0.1], [-d / 2 + 0.06, h - 0.18], [-d / 2, h - 0.28]], w, m, 'Kućište automata')
          k.beam_rect([0, -d / 2 + 0.02, h - 0.4], [0, -d / 2 + 0.06, h - 0.28], w * 0.7, 0.01, 'screen_display', 'Ekran')
          3.times { |r| 3.times { |c| k.box((c - 1) * 0.045, -d / 2 - 0.005, h - 0.62 - (r * 0.045), 0.035, 0.01, 0.035, 'stainless', 'Tipka') } } if k.detail.positive?
          k.box(0, -d / 2 - 0.005, h - 0.85, 0.12, 0.012, 0.03, 'steel_galv', 'Otvor za karticu')
          k.box(0, -d / 2 - 0.01, 0.55, w * 0.6, 0.02, 0.18, 'steel_galv', 'Izlaz za karte')
          k.beam_rect([0, d / 2, h + 0.05], [0, -d / 2 + 0.05, h + 0.2], w * 1.1, 0.02, 'solar_pv', 'Solarni panel')
          k.box(0, d / 2 - 0.03, h - 0.12, 0.04, 0.04, 0.24, m, 'Nosač panela')
        end

        # Markings are paint on the surface: the parametric marking geometry is the real object.
        def marking(k, e)
          k.builder_draw(e)
        end
      end

      module Bike
        module_function

        # Bicycle (side along X, wheels touching the ground), used in stands and docking stations.
        def bicycle(k, x, y, frame = 'plastic_blue')
          wr = 0.34
          n = [8, 12, 16][k.detail]
          [-0.52, 0.52].each { |dx| k.torus([x + dx, y, wr], wr, 0.018, 'rubber_black', 'Kotač', n: n, m: 5) unless k.detail.zero? }
          [-0.52, 0.52].each { |dx| k.bar([x + dx, y - 0.02, wr], [x + dx, y + 0.02, wr], 0.04, 'steel_galv', 'Glavčina', n: 8) }
          bb = [x, y, 0.3]
          seat = [x - 0.14, y, 0.85]
          head = [x + 0.4, y, 0.88]
          k.bars([[bb, seat, 0.02, 0.018], [bb, head, 0.024, 0.02], [seat, head, 0.018, 0.018], [bb, [x - 0.52, y, wr], 0.014, 0.012],
                  [seat, [x - 0.52, y, wr], 0.012, 0.012], [head, [x + 0.52, y, wr], 0.016, 0.014]], frame, 'Okvir bicikla')
          k.bar([x - 0.14, y, 0.85], [x - 0.16, y, 0.95], 0.012, 'steel_dark', 'Sjedalna cijev')
          k.box(x - 0.18, y, 0.95, 0.24, 0.1, 0.05, 'rubber_black', 'Sjedalo')
          k.bar([x + 0.4, y, 0.88], [x + 0.38, y, 1.0], 0.014, 'steel_dark', 'Volan')
          k.bar([x + 0.38, y - 0.25, 1.0], [x + 0.38, y + 0.25, 1.0], 0.012, 'steel_dark', 'Upravljač')
          k.box(x + 0.48, y, 0.75, 0.25, 0.3, 0.02, 'steel_dark', 'Korpa') if k.detail >= 2
        end

        # Sheffield (inverted-U) stands repeated along X at `spacing`.
        def stands(k, e)
          count = [e.num('count', 1).to_i, 1].max
          spacing = e.num('spacing', 1.0)
          w = e.num('width', 0.8)
          h = e.num('height', 0.8)
          r = e.num('tubeDiameter', 0.05) / 2
          m = k.mat('material', 'steel_galv')
          rr = w / 2 * 0.45
          count.times do |i|
            y = (i - ((count - 1) / 2.0)) * spacing
            pts = [[-w / 2, y, -0.2], [-w / 2, y, h - rr]] + Array.new(7) { |j| a = Math::PI * (j + 1) / 8; [-(Math.cos(a) * (w / 2)), y, h - rr + (Math.sin(a) * rr)] } + [[w / 2, y, h - rr], [w / 2, y, -0.2]]
            k.rail(pts, r, m, 'Stalak')
            k.bar([-w / 2 + 0.05, y, 0.25], [w / 2 - 0.05, y, 0.25], r * 0.6, m, 'Prečka') if k.detail.positive?
          end
        end

        def locker(k, e)
          w = e.num('width', 2.0)
          d = e.num('depth', 0.8)
          h = e.num('height', 1.3)
          m = k.mat('material', 'steel_dark')
          prof = [[-w / 2, 0.0], [w / 2, 0.0], [w / 2, h * 0.75], [w * 0.15, h], [-w * 0.15, h], [-w / 2, h * 0.75]]
          k.plate([0, -d / 2, 0], [1.0, 0.0], prof, d, m, 'Kućište ormarića')
          [-1, 1].each do |s|
            k.box(s * w / 4, -d / 2 - 0.006, 0.08, (w / 2) - 0.06, 0.012, h * 0.62, 'steel_galv', 'Vrata')
            k.box(s * 0.08, -d / 2 - 0.02, h * 0.4, 0.03, 0.02, 0.12, 'steel_dark', 'Brava')
          end
          k.box(0, -d / 2 - 0.004, h * 0.72, w * 0.3, 0.008, 0.12, 'sign_plate', 'Oznaka')
        end

        def share_station(k, e)
          w = e.num('width', 8.0)
          d = e.num('depth', 1.8)
          docks = [(w / 0.85).floor - 1, 2].max
          k.box(0.4, 0, 0, w - 0.8, 0.25, 0.08, 'steel_dark', 'Temeljna šina')
          docks.times do |i|
            x = (-w / 2) + 1.4 + (i * (w - 1.6) / docks)
            k.plate([x + 0.06, 0, 0.08], [0.0, 1.0], [[-0.1, 0.0], [0.1, 0.0], [0.08, 0.75], [-0.05, 0.82], [-0.1, 0.7]], 0.12, 'steel_galv', 'Priključno mjesto')
            k.box(x, -0.12, 0.62, 0.08, 0.04, 0.08, 'luminaire', 'Indikator')
            bicycle(k, x, 0.1 - (d / 2), 'plastic_blue') if k.detail.positive? && i.even? && i < 6 # docked bikes fill the depth
          end
          kx = -w / 2 + 0.4
          k.box(kx, 0, 0, 0.5, 0.4, 2.0, 'steel_dark', 'Terminal')
          k.beam_rect([kx, -0.21, 1.1], [kx, -0.21, 1.55], 0.36, 0.01, 'screen_display', 'Ekran')
          k.box(kx, 0, 2.0, 0.6, 0.5, 0.05, 'steel_dark', 'Krović terminala')
          k.box(kx, -0.22, 1.7, 0.4, 0.01, 0.2, 'plastic_blue', 'Logo panel')
        end

        def repair_station(k, e)
          h = e.num('height', 1.6)
          k.box(0, 0, 0, 0.3, 0.3, 0.02, 'steel_dark', 'Podloga')
          k.box(0, 0, 0, 0.18, 0.12, h - 0.1, 'plastic_blue', 'Stub alata')
          k.box(0, 0, h - 0.1, 0.24, 0.16, 0.06, 'steel_dark', 'Kapa')
          [-1, 1].each { |s| k.rail([[0, 0, h - 0.3], [s * 0.35, 0, h - 0.25], [s * 0.38, 0, h - 0.15]], 0.015, 'rubber_black', 'Nosač bicikla') }
          4.times do |i|
            x = -0.06 + (i * 0.04)
            k.bar([x, -0.07, h - 0.45], [x, -0.1, h - 0.85], 0.004, 'steel_dark', 'Sajla alata', n: 5)
            k.box(x, -0.1, h - 0.95, 0.015, 0.01, 0.1, 'stainless', 'Alat')
          end
          k.box(0, -0.065, h - 0.35, 0.16, 0.006, 0.2, 'sign_plate', 'QR uputstvo')
        end

        def service_stand(k, e)
          h = e.num('height', 1.5)
          m = 'steel_galv'
          k.box(0, 0, 0, 0.55, 0.45, 0.02, 'steel_dark', 'Podloga')
          k.cyl(0, 0, 0, 0.035, h - 0.15, m, 'Stub')
          k.rail([[0, 0, h - 0.15], [0.15, 0, h - 0.05], [0.32, 0, h - 0.05]], 0.025, m, 'Krak stezaljke')
          k.box(0.38, 0, h - 0.12, 0.1, 0.08, 0.14, 'plastic_red', 'Stezaljka')
          k.bar([0.33, 0, h - 0.05], [0.33, -0.15, h - 0.05], 0.01, 'steel_dark', 'Ručica')
        end

        def pump(k, e)
          h = e.num('height', 1.0)
          r = [e.num('diameter', 0.15) / 2, 0.07].max
          k.box(0, 0, 0, 0.25, 0.25, 0.02, 'steel_dark', 'Podloga')
          k.lathe([[0, 0.02], [r, 0.02], [r, h - 0.1], [r * 0.7, h], [0, h]], 'plastic_red', 'Tijelo pumpe')
          k.bar([0, -r, h - 0.25], [0, -r - 0.012, h - 0.25], 0.045, 'stainless', 'Manometar', n: 14)
          k.rail([[r * 0.7, 0, h - 0.4], [r + 0.08, 0, h - 0.6], [r + 0.06, -0.05, h - 0.75]], 0.01, 'rubber_black', 'Crijevo')
          k.box(r + 0.06, -0.06, h - 0.82, 0.03, 0.03, 0.07, 'stainless', 'Nastavak')
        end

        def charger(k, e)
          w = e.num('width', 0.3)
          h = e.num('height', 1.2)
          k.box(0, 0, 0, w + 0.1, w + 0.1, 0.03, 'steel_dark', 'Podloga')
          k.lathe([[0, 0.03], [w / 2, 0.03], [w / 2, h - 0.05], [w / 2 - 0.03, h], [0, h]], 'steel_dark', 'Stub punjača', n: k.seg(12))
          [-1, 1].each { |s| k.bar([s * w * 0.18, -w / 2 + 0.01, h * 0.62], [s * w * 0.18, -w / 2 - 0.01, h * 0.62], 0.025, 'plastic_dark', 'Utičnica', n: 10) }
          k.cyl(0, 0, h - 0.06, w / 2 * 1.01, 0.03, 'marking_green', 'Svjetlosni prsten', n: k.seg(12))
        end
      end

      define('road.speed_hump') { |k, e| Traffic.speed_hump(k, e) }
      define('road.wheel_stop') { |k, e| Traffic.wheel_stop(k, e) }
      define('road.bollard') { |k, e| Traffic.bollard(k, e) }
      define('road.removable_bollard') { |k, e| Traffic.bollard(k, e) }
      define('road.vehicle_barrier') { |k, e| Traffic.barrier(k, e) }
      define('road.parking_meter') { |k, e| Traffic.pay_station(k, e) }
      define('road.traffic_sign') { |k, e| Sign.post_sign(k, e, plate_material: 'line_paint_white', border: 'plastic_red') }
      %w[road.parking_bay road.parking_bay_accessible road.parking_bay_ev road.loading_bay road.arrow cycling.route_marking].each do |id|
        define(id) { |k, e| Traffic.marking(k, e) }
      end
      define('cycling.bike_stand') { |k, e| Bike.stands(k, e) }
      define('cycling.bike_rack') { |k, e| Bike.stands(k, e) }
      define('cycling.bike_locker') { |k, e| Bike.locker(k, e) }
      define('cycling.share_station') { |k, e| Bike.share_station(k, e) }
      define('cycling.repair_station') { |k, e| Bike.repair_station(k, e) }
      define('cycling.service_stand') { |k, e| Bike.service_stand(k, e) }
      define('cycling.pump') { |k, e| Bike.pump(k, e) }
      define('cycling.charging_point') { |k, e| Bike.charger(k, e) }
      define('cycling.direction_sign') { |k, e| Sign.post_sign(k, e) }
    end
  end
end
