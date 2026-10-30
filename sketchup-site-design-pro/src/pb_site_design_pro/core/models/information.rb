# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Models
      # Decorative elements, information and security systems, and the remaining point greenery.
      # Faces and screens look towards -Y.
      module Info
        module_function

        def totem(k, e, label, screen: false, map: false)
          w = e.num('width', 0.8)
          d = e.num('depth', 0.15)
          h = e.num('height', 2.2)
          m = k.mat('material', 'sign_plate')
          k.box(0, 0, 0, w + 0.1, d + 0.1, 0.08, 'concrete', 'Temelj')
          k.box(0, 0, 0.08, w, d, h - 0.08, m == 'sign_plate' ? 'steel_dark' : m, label)
          k.box(0, -d / 2 - 0.003, h * 0.45, w - 0.08, 0.006, h * 0.45, map ? 'flower_white' : 'sign_plate', map ? 'Karta' : 'Tabla')
          k.box(0, -d / 2 - 0.004, h - 0.25, w - 0.08, 0.008, 0.15, 'plastic_blue', 'Zaglavlje')
          k.beam_rect([0, -d / 2 - 0.006, h * 0.35], [0, -d / 2 - 0.006, h * 0.7], w * 0.8, 0.008, 'screen_display', 'Ekran') if screen
          return unless map && k.detail.positive?

          4.times { |i| k.box(0, -d / 2 - 0.006, (h * 0.5) + (i * 0.08), (w - 0.2) * (0.4 + (0.15 * i)), 0.004, 0.03, 'line_paint_white', 'Strelica smjera') }
        end

        def kiosk(k, e)
          w = e.num('width', 0.6)
          d = e.num('depth', 0.4)
          h = e.num('height', 1.9)
          m = k.mat('material', 'steel_dark')
          k.box(0, 0, 0, w + 0.2, d + 0.2, 0.05, 'concrete', 'Podloga')
          k.plate([w / 2, 0, 0.05], [0.0, 1.0], [[-d / 2, 0.0], [d / 2, 0.0], [d / 2, h - 0.05], [-d / 2 + 0.05, h - 0.05], [-d / 2 + 0.05, h * 0.55], [-d / 2, h * 0.5]], w, m, 'Kiosk')
          k.beam_rect([0, -d / 2 + 0.02, h * 0.55], [0, -d / 2 + 0.06, h * 0.92], w * 0.8, 0.01, 'screen_display', 'Ekran na dodir')
          k.box(0, -d / 2 - 0.005, h * 0.42, w * 0.3, 0.01, 0.04, 'stainless', 'Čitač')
          k.box(0, 0, h, w + 0.12, d + 0.12, 0.04, m, 'Krović')
        end

        def cctv(k, e)
          h = e.num('poleHeight', 4.0)
          r = e.num('poleDiameter', 0.1) / 2
          Light.pole(k, h, r, k.mat('poleMaterial', 'steel_dark'))
          k.rail([[0, 0, h - 0.1], [0.15, 0, h - 0.05], [0.3, 0, h - 0.05]], 0.02, 'steel_galv', 'Nosač kamere')
          k.bar([0.25, 0, h - 0.12], [0.55, 0, h - 0.18], 0.06, 'flower_white', 'Kućište kamere', n: k.seg(12))
          k.box(0.4, 0, h - 0.07, 0.36, 0.13, 0.015, 'flower_white', 'Štitnik od sunca')
          k.bar([0.55, 0, h - 0.18], [0.56, 0, h - 0.182], 0.045, 'screen_display', 'Objektiv', n: k.seg(12))
          k.box(-r - 0.06, 0, h * 0.4, 0.1, 0.18, 0.3, 'steel_dark', 'Razvodna kutija')
        end

        def post_panel(k, e, label, colour: 'steel_dark')
          w = e.num('width', 0.2)
          d = e.num('depth', 0.12)
          h = e.num('height', 1.4)
          k.box(0, 0, 0, 0.25, 0.25, 0.02, 'steel_dark', 'Podloga')
          k.box(0, 0, 0.02, w * 0.6, d * 0.6, h - 0.35, 'steel_dark', 'Stub')
          k.box(0, 0, h - 0.4, w, d, 0.4, colour, label)
          k.box(0, -d / 2 - 0.003, h - 0.18, w * 0.6, 0.006, 0.08, 'screen_display', 'Ekran / čitač')
          3.times { |i| k.box((i - 1) * w * 0.25, -d / 2 - 0.004, h - 0.32, 0.03, 0.008, 0.03, 'stainless', 'Tipka') } if k.detail.positive?
          k.bar([0, -d / 2, h - 0.06], [0, -d / 2 - 0.01, h - 0.06], 0.012, 'screen_display', 'Kamera', n: 8)
        end

        def emergency(k, e)
          w = e.num('width', 0.3)
          h = e.num('height', 1.6)
          k.box(0, 0, 0, w + 0.1, w + 0.1, 0.03, 'steel_dark', 'Podloga')
          k.lathe([[0, 0.03], [w / 2, 0.03], [w / 2, h - 0.15], [w * 0.4, h - 0.1], [0, h - 0.1]], k.mat('material', 'brick') == 'brick' ? 'plastic_red' : k.mat('material', 'plastic_red'), 'Stub za hitne pozive', n: k.seg(14))
          k.cyl(0, 0, h - 0.1, w * 0.3, 0.1, 'flower_yellow', 'Rotaciono svjetlo', n: k.seg(12))
          k.bar([0, -w / 2, h * 0.65], [0, -w / 2 - 0.02, h * 0.65], 0.04, 'flower_yellow', 'Taster za poziv', n: 12)
          k.box(0, -w / 2 - 0.004, h * 0.75, 0.12, 0.008, 0.08, 'screen_display', 'Zvučnik')
        end

        def speaker(k, e)
          h = e.num('poleHeight', 3.5)
          Light.pole(k, h - 0.1, e.num('poleDiameter', 0.12) / 2, k.mat('poleMaterial', 'steel_dark'))
          k.box(0.08, 0, h - 0.4, 0.04, 0.06, 0.3, 'steel_dark', 'Nosač')
          k.mesh(Mesh.fix_orientation(Kit.lathe_polys([[0, 0], [0.06, 0], [0.16, 0.26], [0, 0.22]], 0, 0, k.seg(14), false).map { |p| p.map { |q| [0.12 + q[2], q[1], h - 0.25 + q[0]] } }), 'flower_white', 'Truba zvučnika')
        end

        def wifi(k, e)
          h = e.num('poleHeight', 4.0)
          Light.pole(k, h - 0.3, e.num('poleDiameter', 0.12) / 2, k.mat('poleMaterial', 'steel_dark'))
          k.box(0, 0, h - 0.3, 0.2, 0.08, 0.3, 'flower_white', 'Pristupna tačka')
          [-0.07, 0.07].each { |x| k.bar([x, 0, h - 0.02], [x, 0, h + 0.0], 0.008, 'steel_dark', 'Antena', n: 6) }
        end

        def fingerpost(k, e)
          ph = e.num('postHeight', 2.6)
          pw = e.num('plateWidth', 0.9)
          plh = e.num('plateHeight', 0.2)
          pb = e.num('plateBottom', 2.1)
          k.cyl(0, 0, -0.05, 0.04, ph + 0.05, k.mat('postMaterial', 'steel_galv'), 'Stub', n: k.seg(10))
          k.cyl(0, 0, ph, 0.05, 0.03, 'steel_dark', 'Kapa', n: 10)
          [[0.0, 0], [2.2, 1], [-0.9, 2]].each do |(a, i)|
            z = pb - (i * (plh + 0.06))
            dir = [Math.cos(a), Math.sin(a)]
            tip = [[0.0, 0.0], [pw - 0.12, 0.0], [pw, plh / 2], [pw - 0.12, plh], [0.0, plh]]
            k.plate([dir[1] * 0.012, -dir[0] * 0.012, z], dir, tip, 0.024, k.mat('plateMaterial', 'sign_plate'), 'Putokazna tabla')
          end
        end

        def site_map(k, e)
          pw = e.num('plateWidth', 1.0)
          plh = e.num('plateHeight', 0.7)
          pb = e.num('plateBottom', 0.9)
          [-pw / 2 + 0.06, pw / 2 - 0.06].each { |x| k.box(x, 0.1, 0, 0.06, 0.06, pb + 0.2, 'steel_dark', 'Noga') }
          k.beam_rect([0, 0.15, pb + 0.35], [0, -0.3, pb], pw + 0.04, 0.04, 'steel_dark', 'Okvir karte')
          k.beam_rect([0, 0.14, pb + 0.39], [0, -0.29, pb + 0.055], pw - 0.04, 0.006, 'flower_white', 'Karta lokacije')
          return if k.detail.zero?

          [[-0.2, 0.0, 0.25], [0.15, -0.1, 0.2], [0.1, 0.12, 0.1]].each do |(x, f, s)|
            k.beam_rect([x, 0.05 - (f * 0.4), pb + 0.27 - (f * 0.3)], [x, -0.05 - (f * 0.4), pb + 0.2 - (f * 0.3)], s, 0.004, 'marking_green', 'Zona na karti')
          end
        end

        # --- decorative -------------------------------------------------------------------------

        def sculpture(k, e)
          w = e.num('width', 1.0)
          h = e.num('height', 2.0)
          m = k.mat('material', 'stone_wall')
          k.box(0, 0, 0, w * 0.8, w * 0.8, 0.3, 'concrete', 'Postament')
          n = [3, 5, 7][k.detail]
          n.times do |i|
            t = i.to_f / n
            a = t * 2.4
            r = w * 0.32 * (1 - (t * 0.4))
            k.blob([Math.cos(a) * w * 0.08, Math.sin(a) * w * 0.08, 0.3 + ((h - 0.3) * (t + (0.5 / n)))], r, r * 0.8, (h - 0.3) / n * 0.62, m, 'Skulptura', rough: 0.1, level: 2)
          end
        end

        def public_art(k, e)
          w = e.num('width', 2.0)
          h = e.num('height', 3.0)
          m = k.mat('material', 'steel_galv')
          k.box(0, 0, 0, w, w * 0.4, 0.1, 'concrete', 'Podloga')
          3.times do |i|
            r = (h / 2) * (1 - (i * 0.18))
            a = i * 0.7
            pts = Array.new(k.seg(18) + 1) { |j| t = Math::PI * j / k.seg(18); [Math.cos(t) * r * Math.cos(a), Math.cos(t) * r * Math.sin(a), 0.1 + (Math.sin(t) * (h - 0.1))] }
            pts.each_cons(2) { |p, q| k.beam_rect(p, q, 0.08, 0.2, i.even? ? m : 'plastic_red', 'Luk skulpture') }
          end
        end

        def boulder(k, e)
          l = e.num('length', 1.2)
          wd = e.num('width', 0.9)
          h = e.num('height', 0.7)
          emb = [e.num('embedment', 0.15), h * 0.9].min
          k.blob([0, 0, (h / 2) - emb], l / 2, wd / 2, h / 2, k.mat('material', 'stone_wall'), 'Kamen', rough: 0.18, level: 2)
        end

        def urn(k, e)
          r = e.num('diameter', 1.2) / 2
          h = e.num('height', 1.5)
          m = k.mat('material', 'stone_wall')
          k.box(0, 0, 0, r * 1.5, r * 1.5, h * 0.35, m, 'Postament')
          k.box(0, 0, h * 0.35, r * 1.6, r * 1.6, 0.05, m, 'Vijenac postamenta')
          k.lathe([[0, h * 0.4], [r * 0.3, h * 0.4], [r * 0.2, h * 0.5], [r * 0.7, h * 0.7], [r, h * 0.95], [r * 1.02, h], [r * 0.9, h], [r * 0.85, h * 0.92], [0, h * 0.92]], m, 'Posuda', n: k.seg(20))
          Veg.shrub(k, 0.5, r * 1.5, 'foliage', z: h * 0.9) if k.detail.positive?
        end

        def light_installation(k, e)
          w = e.num('width', 2.0)
          h = e.num('height', 2.5)
          k.box(0, 0, 0, w, 0.3, 0.05, 'steel_dark', 'Podloga')
          n = [5, 9, 13][k.detail]
          n.times do |i|
            x = (-w / 2) + ((i + 0.5) * w / n)
            hh = h * (0.55 + (0.45 * Math.sin(Math::PI * (i + 0.5) / n)))
            k.cyl(x, 0, 0.05, 0.025, hh - 0.05, 'luminaire', 'Svjetlosni štap', n: 8)
            k.cyl(x, 0, 0.05, 0.035, 0.1, 'steel_dark', 'Podnožje štapa', n: 8)
          end
        end

        def monument(k, e)
          w = e.num('width', 3.0)
          d = e.num('depth', 0.6)
          h = e.num('height', 2.2)
          m = k.mat('material', 'stone_wall')
          k.box(0, 0, 0, w, d, h * 0.15, 'concrete', 'Sokl')
          k.box(0, 0, h * 0.15, w * 0.96, d * 0.9, h * 0.8, m, 'Ulazni zid')
          k.box(0, 0, h * 0.95, w, d, h * 0.05, 'concrete', 'Kapa zida')
          k.box(0, -d * 0.45 - 0.01, h * 0.5, w * 0.6, 0.02, h * 0.18, 'stainless', 'Natpis')
          [-w * 0.4, w * 0.4].each { |x| k.cyl(x, -d * 0.45 - 0.05, 0, 0.06, 0.08, 'luminaire', 'Podna svjetiljka', n: 10) }
        end

        # --- greenery points ------------------------------------------------------------------------

        def trellis(k, e)
          w = e.num('width', 1.0)
          h = e.num('height', 2.5)
          k.box(0, 0.05, 0, w + 0.04, 0.04, 0.3, 'concrete', 'Žardinjera')
          [-w / 2, w / 2].each { |x| k.box(x, 0.05, 0.3, 0.04, 0.04, h - 0.3, 'timber', 'Stub rešetke') }
          n = (h / 0.3).floor
          n.times { |i| k.box(0, 0.05, 0.45 + (i * 0.3), w, 0.02, 0.02, 'timber', 'Letva') }
          4.times { |i| k.box((-w / 2) + ((i + 0.5) * w / 4), 0.05, 0.3, 0.02, 0.02, h - 0.3, 'timber', 'Letva') }
          return if k.detail.zero?

          specs = Array.new([6, 14, 22][k.detail]) do |i|
            z = 0.4 + ((h - 0.5) * (i + 0.5) / [6, 14, 22][k.detail])
            [[k.rand(-w * 0.35, w * 0.35), 0.0, z], k.rand(0.15, 0.28), 0.1, k.rand(0.15, 0.25)]
          end
          k.blobs(specs, 'foliage', 'Penjačica', rough: 0.25, level: 1)
        end

        def vertical_garden(k, e)
          w = e.num('width', 2.0)
          d = e.num('depth', 0.3)
          h = e.num('height', 2.4)
          k.box(0, d / 2, 0, w, 0.05, h, 'steel_galv', 'Noseći panel')
          rows = [3, 6, 8][k.detail]
          rows.times do |r|
            z = 0.15 + (r * (h - 0.2) / rows)
            k.box(0, d / 2 - 0.08, z, w - 0.05, 0.12, 0.1, 'plastic_dark', 'Džep za sadnju')
            next if k.detail.zero?

            n = (w / 0.3).floor
            n.times do |i|
              x = (-w / 2) + ((i + 0.5) * w / n)
              colour = (i + r).%(5).zero? ? 'flower_violet' : ((i + r) % 3).zero? ? 'foliage_light' : 'foliage'
              k.blob([x, d / 2 - 0.12, z + 0.16], 0.15, 0.1, 0.13, colour, 'Biljka', rough: 0.25, level: 1)
            end
          end
        end

        def tree_grate(k, e)
          w = e.num('width', 1.2)
          m = k.mat('material', 'cast_iron')
          k.prism(Mesh.rect_ring(0, 0, w + 0.08, w + 0.08), -0.05, 0.0, m, 'Okvir rešetke', holes: [Mesh.rect_ring(0, 0, w, w)])
          ring = Mesh.circle_ring(0, 0, w * 0.2, k.seg(16))
          k.prism(Mesh.rect_ring(0, 0, w, w), -0.03, 0.0, m, 'Rešetka', holes: [ring])
          if k.detail.positive?
            n = [8, 12, 16][k.detail]
            n.times { |i| a = 2 * Math::PI * i / n; k.box(Math.cos(a) * w * 0.33, Math.sin(a) * w * 0.33, 0.0, w * 0.2, 0.025, 0.004, 'steel_dark', 'Prorez', angle: a) }
          end
          k.cyl(0, 0, -0.12, w * 0.19, 0.11, 'mulch', 'Malč', n: k.seg(16))
        end

        def tree_pit(k, e)
          w = e.num('width', 2.0)
          d = e.num('depth', 2.0)
          h = e.num('height', 1.0)
          z0 = e.num('baseOffset', -1.0)
          k.box(0, 0, z0, w, d, h - 0.05, 'soil', 'Supstrat sadne jame')
          k.box(0, 0, z0 + h - 0.05, w, d, 0.05, 'mulch', 'Malč')
          [-1, 1].each { |s| k.box(0, s * (d / 2 + 0.01), z0, w + 0.04, 0.01, h, 'hdpe', 'Barijera za korijen') }
          [-1, 1].each { |s| k.box(s * (w / 2 + 0.01), 0, z0, 0.01, d, h, 'hdpe', 'Barijera za korijen') }
          k.torus([0, 0, z0 + h - 0.25], [w, d].min * 0.32, 0.04, 'plastic_dark', 'Cijev za prozračivanje', n: k.seg(16), m: 6)
          k.cyl([w, d].min * 0.32, 0, z0 + h - 0.25, 0.045, 0.27, 'plastic_dark', 'Otvor za zalijevanje', n: 10)
        end

        def irrigation(k, e)
          r = e.num('diameter', 0.25) / 2
          h = e.num('height', 0.3)
          k.prism(Mesh.rect_ring(0, 0, r * 3, r * 2.2), -h, -0.03, 'plastic_green', 'Ventilska kutija', holes: [Mesh.rect_ring(0, 0, r * 2.6, r * 1.8)])
          k.box(0, 0, -0.03, r * 3.1, r * 2.3, 0.03, 'plastic_green', 'Poklopac')
          k.bar([-r * 1.1, 0, -h + 0.1], [r * 1.1, 0, -h + 0.1], 0.02, 'hdpe', 'Cijev', n: 8)
          k.cyl(0, 0, -h + 0.1, 0.035, 0.08, 'steel_galv', 'Ventil', n: 8)
        end
      end

      define('info.site_board') { |k, e| Sign.post_sign(k, e) }
      define('info.qr_panel') { |k, e| Sign.post_sign(k, e) }
      define('info.accessibility_sign') { |k, e| Sign.post_sign(k, e, plate_material: 'plastic_blue') }
      define('info.fire_info_point') { |k, e| Sign.post_sign(k, e, plate_material: 'plastic_red') }
      define('info.directional_sign') { |k, e| Info.fingerpost(k, e) }
      define('info.site_map') { |k, e| Info.site_map(k, e) }
      define('info.wayfinding_sign') { |k, e| Info.totem(k, e, 'Totem za orijentaciju', map: true) }
      define('info.building_sign') { |k, e| Info.totem(k, e, 'Totem objekta') }
      define('info.digital_kiosk') { |k, e| Info.kiosk(k, e) }
      define('info.cctv_camera') { |k, e| Info.cctv(k, e) }
      define('info.security_cabinet') { |k, e| Light.cabinet(k, e, label: 'Ormar sigurnosnih sistema') }
      define('info.access_reader') { |k, e| Info.post_panel(k, e, 'Čitač pristupa') }
      define('info.intercom') { |k, e| Info.post_panel(k, e, 'Interfon') }
      define('info.speaker') { |k, e| Info.speaker(k, e) }
      define('info.wifi_ap') { |k, e| Info.wifi(k, e) }
      define('info.emergency_call_point') { |k, e| Info.emergency(k, e) }
      define('decorative.sculpture') { |k, e| Info.sculpture(k, e) }
      define('decorative.public_art') { |k, e| Info.public_art(k, e) }
      define('decorative.boulder') { |k, e| Info.boulder(k, e) }
      define('decorative.focal_point') { |k, e| Info.urn(k, e) }
      define('decorative.light_installation') { |k, e| Info.light_installation(k, e) }
      define('decorative.entrance_monument') { |k, e| Info.monument(k, e) }
      define('decorative.signage_structure') { |k, e| Sign.post_sign(k, e) }
      define('landscape.climber') { |k, e| Info.trellis(k, e) }
      define('landscape.vertical_garden') { |k, e| Info.vertical_garden(k, e) }
      define('landscape.tree_grate') { |k, e| Info.tree_grate(k, e) }
      define('landscape.tree_pit') { |k, e| Info.tree_pit(k, e) }
      define('landscape.irrigation_point') { |k, e| Info.irrigation(k, e) }
    end
  end
end
