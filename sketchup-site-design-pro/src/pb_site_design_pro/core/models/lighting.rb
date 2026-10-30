# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Models
      # Exterior lighting. Poles stand on the insertion point; arms reach along +X (the element direction, towards
      # the lit area); the overall height equals poleHeight;
      # wall-mounted fixtures have the wall on +Y at y = 0 and project towards -Y at height baseOffset.
      module Light
        module_function

        # Tapered round pole with base flange and anchor cover.
        def pole(k, h, r, material)
          k.box(0, 0, 0, r * 4.2, r * 4.2, 0.02, material, 'Prirubnica')
          k.frustum(0, 0, 0.02, r * 1.8, r * 1.35, 0.35, material, 'Kapa ankera', n: k.seg(12))
          k.frustum(0, 0, 0.37, r * 1.15, r * 0.7, h - 0.37, material, 'Stub', n: k.seg(14))
        end

        # Streamlined luminaire head along +X from the arm tip (x, y, z): housing plus diffuser underneath.
        def head(k, tip, len, wid, hh, housing)
          x, y, z = tip
          outline = [[0.0, 0.0], [len * 0.15, -hh * 0.5], [len, -hh * 0.35], [len, hh * 0.15], [len * 0.2, hh * 0.5], [0.0, hh * 0.3]]
          k.plate([x, y - (wid / 2), z], [1.0, 0.0], outline, wid, housing, 'Kućište svjetiljke')
          k.box(x + (len * 0.58), y, z - (hh * 0.47), len * 0.7, wid * 0.82, 0.012, 'luminaire', 'Difuzor')
        end

        def street(k, e, smart: false, solar: false)
          h = e.num('poleHeight', 8.0)
          r = e.num('poleDiameter', 0.12) / 2
          arm = e.num('armLength', 1.0)
          hh = e.num('headHeight', 0.12)
          solar ||= e.bool('solarPanel', false)
          pm = k.mat('poleMaterial', 'steel_dark')
          hm = k.mat('headMaterial', 'luminaire') == 'luminaire' ? pm : k.mat('headMaterial', 'steel_dark')
          top = solar ? h - 0.45 : h
          pole(k, top - 0.15, r, pm)
          tip = [arm, 0.0, top - (hh * 0.5)]
          if arm > 0.05
            k.rail([[0, 0, top - 0.4], [arm * 0.35, 0, top - (hh * 0.5) - 0.02], [arm, 0, top - (hh * 0.5)]], r * 0.55, pm, 'Krak')
          else
            tip = [0.0, 0.0, top - (hh * 0.5)]
          end
          head(k, tip, e.num('headLength', 0.6), e.num('headWidth', 0.25), hh, hm)
          k.ellipsoid([arm + 0.12, 0, top - hh - 0.05], 0.05, 0.05, 0.035, 'plastic_dark', 'Senzor pokreta') if smart
          k.box(0.05, 0, h - 1.6, 0.06, 0.1, 0.35, 'steel_dark', 'Pametni kontroler') if smart
          return unless solar

          k.box(-r - 0.1, 0, h * 0.55, 0.12, 0.22, 0.45, pm, 'Baterija')
          k.box(0, 0, top - 0.15, 0.05, 0.05, 0.25, pm, 'Nosač panela')
          k.beam_rect([-0.32, 0, h - 0.33], [0.32, 0, h - 0.035], 0.9, 0.035, 'solar_pv', 'Solarni panel')
        end

        def lantern(k, e)
          h = e.num('poleHeight', 4.0)
          r = e.num('poleDiameter', 0.1) / 2
          lw = e.num('headWidth', 0.4)
          lh = e.num('headHeight', 0.3) + 0.25
          pm = k.mat('poleMaterial', 'steel_dark')
          pole(k, h, r, pm)
          k.lathe([[0, h], [lw * 0.35, h], [lw * 0.35, h + 0.05], [0, h + 0.05]], pm, 'Prihvat')
          k.cyl(0, 0, h + 0.05, lw * 0.32, lh * 0.7, 'luminaire', 'Staklo')
          k.lathe([[0, h + 0.05 + (lh * 0.7)], [lw * 0.5, h + 0.05 + (lh * 0.7)], [lw * 0.48, h + 0.08 + (lh * 0.7)], [0.03, h + 0.05 + lh], [0, h + 0.05 + lh]], pm, 'Kapa')
        end

        def floodlight(k, e)
          h = e.num('poleHeight', 10.0)
          r = e.num('poleDiameter', 0.12) / 2 * 1.4
          pm = k.mat('poleMaterial', 'steel_galv')
          pole(k, h - 0.04, r, pm)
          k.beam([0, -0.9, h - 0.08], [0, 0.9, h - 0.08], 0.08, pm, 'Traverza')
          [-0.65, 0.0, 0.65].each do |y|
            k.box(0.12, y, h - 0.17, 0.05, 0.05, 0.12, pm, 'Nosač')
            k.beam_rect([0.05, y, h - 0.2], [0.42, y, h - 0.44], e.num('headWidth', 0.5), 0.12, 'steel_dark', 'Reflektor')
            k.beam_rect([0.06, y, h - 0.28], [0.4, y, h - 0.5], e.num('headWidth', 0.5) * 0.85, 0.02, 'luminaire', 'Staklo reflektora')
          end
        end

        def bollard(k, e)
          h = e.num('height', 1.0)
          r = e.num('diameter', 0.2) / 2
          m = k.mat('material', 'steel_dark')
          k.cyl(0, 0, 0, r * 1.2, 0.02, m, 'Podnožje')
          k.cyl(0, 0, 0.02, r, h - 0.2, m, 'Tijelo')
          k.cyl(0, 0, h - 0.18, r * 0.92, 0.12, 'luminaire', 'Svjetlosni pojas')
          if k.detail.positive?
            [0.03, 0.07].each { |dz| k.torus([0, 0, h - 0.18 + dz], r * 0.95, 0.006, m, 'Rebro') }
          end
          k.lathe([[0, h - 0.06], [r * 1.08, h - 0.06], [r * 1.08, h - 0.03], [r * 0.6, h], [0, h]], m, 'Kapa')
        end

        def recessed(k, e)
          r = e.num('diameter', 0.15) / 2
          h = e.num('height', 0.12)
          k.cyl(0, 0, -h, r * 0.9, h - 0.004, 'plastic_dark', 'Ugradbeno kućište')
          k.lathe([[r * 0.7, -0.004], [r, -0.004], [r, 0.0], [r * 0.7, 0.0]], 'stainless', 'Vijenac', closed_loop: true)
          k.cyl(0, 0, -0.004, r * 0.7, 0.003, 'luminaire', 'Staklo')
        end

        def underwater(k, e)
          r = e.num('diameter', 0.2) / 2
          z = e.num('baseOffset', -0.6)
          k.bar([0, 0.12, z], [0, 0.0, z], r * 0.9, 'plastic_dark', 'Niša', n: k.seg(16))
          k.bar([0, 0.0, z], [0, -0.012, z], r * 1.15, 'stainless', 'Prednji prsten', n: k.seg(16))
          k.bar([0, -0.012, z], [0, -0.016, z], r * 0.8, 'luminaire', 'Leća', n: k.seg(16))
        end

        def step_light(k, e)
          w = e.num('width', 0.15)
          h = e.num('height', 0.08)
          z = e.num('baseOffset', 0.2)
          k.box(0, 0.02, z, w, 0.05, h, 'plastic_dark', 'Ugradbena kutija')
          k.box(0, -0.004, z - 0.005, w + 0.02, 0.008, h + 0.01, 'stainless', 'Okvir')
          3.times { |i| k.box(0, -0.01, z + (h * (i + 0.5) / 3) - 0.005, w * 0.8, 0.008, 0.008, 'stainless', 'Lamela') } if k.detail.positive?
          k.box(0, -0.002, z + 0.005, w * 0.85, 0.004, 0.012, 'luminaire', 'Svjetlo')
        end

        def wall_light(k, e)
          z = e.num('baseOffset', 2.2)
          h = e.num('height', 0.25)
          w = e.num('width', 0.2)
          m = k.mat('material', 'luminaire') == 'luminaire' ? 'steel_dark' : k.mat('material', 'steel_dark')
          k.box(0, -0.01, z, w * 0.6, 0.02, h, m, 'Zidna ploča')
          k.cyl(0, -0.08, z + 0.02, w * 0.33, h - 0.04, m, 'Kućište', n: k.seg(14))
          k.cyl(0, -0.08, z, w * 0.28, 0.02, 'luminaire', 'Svjetlo dolje', n: k.seg(14))
          k.cyl(0, -0.08, z + h - 0.02, w * 0.28, 0.02, 'luminaire', 'Svjetlo gore', n: k.seg(14))
          k.box(0, -0.04, z + (h / 2) - 0.02, 0.04, 0.06, 0.04, m, 'Nosač')
        end

        def spot(k, e, spike: false)
          r = e.num('diameter', 0.2) / 2
          z = e.num('baseOffset', 0.0)
          if spike
            k.bar([0, 0, z - 0.25], [0, 0, z + 0.02], 0.012, 'steel_dark', 'Šiljak', n: 6)
          else
            k.box(0, 0, z, 0.2, 0.2, 0.02, 'steel_dark', 'Podloga')
          end
          k.box(0, 0, z + 0.02, 0.03, 0.03, 0.08, 'steel_dark', 'Nosač')
          [-1, 1].each { |s| k.box(s * (r + 0.01), 0, z + 0.06, 0.012, 0.04, r + 0.08, 'steel_dark', 'Viljuška') }
          c = [0, 0, z + 0.08 + r]
          k.bar(Mesh.add3(c, [0, 0.08, -0.06]), Mesh.add3(c, [0, -0.1, 0.12]), r, 'steel_dark', 'Reflektor', n: k.seg(14))
          k.bar(Mesh.add3(c, [0, -0.1, 0.12]), Mesh.add3(c, [0, -0.106, 0.126]), r * 0.85, 'luminaire', 'Staklo', n: k.seg(14))
        end

        def garden(k, e)
          h = e.num('height', 0.4)
          r = e.num('diameter', 0.1) / 2
          k.bar([0, 0, -0.2], [0, 0, 0.02], 0.01, 'steel_dark', 'Šiljak', n: 6)
          k.cyl(0, 0, 0.0, r * 0.4, h - 0.1, 'steel_dark', 'Stub', n: 10)
          k.cyl(0, 0, h - 0.1, r * 0.8, 0.06, 'luminaire', 'Svjetlo', n: k.seg(12))
          k.lathe([[0, h - 0.04], [r * 1.6, h - 0.06], [r * 1.6, h - 0.04], [0, h]], 'steel_dark', 'Kapa', n: k.seg(14))
        end

        def pendant(k, e)
          w = e.num('width', 0.6)
          z = e.num('baseOffset', 2.5)
          k.box(0, 0, z, w, e.num('depth', 0.06), 0.05, 'aluminium', 'Linijska svjetiljka')
          k.box(0, 0, z - 0.004, w * 0.95, e.num('depth', 0.06) * 0.7, 0.004, 'luminaire', 'Difuzor')
          [-w * 0.4, w * 0.4].each { |x| k.bar([x, 0, z + 0.05], [x, 0, z + 0.6], 0.003, 'steel_dark', 'Sajla', n: 5) }
        end

        def cabinet(k, e, label: 'Ormar')
          w = e.num('width', 0.8)
          d = e.num('depth', 0.3)
          h = e.num('height', 1.2)
          m = k.mat('material', 'steel_galv')
          k.box(0, 0, 0, w + 0.06, d + 0.06, 0.25, 'concrete', 'Postolje')
          k.box(0, 0, 0.25, w, d, h - 0.25, m, label)
          k.box(0, 0, h, w + 0.04, d + 0.04, 0.025, m, 'Krovna ploča')
          doors = w > 0.7 ? 2 : 1
          doors.times do |i|
            x = (-w / 2) + ((i + 0.5) * w / doors)
            k.box(x, -d / 2 - 0.004, 0.3, (w / doors) - 0.03, 0.008, h - 0.35, m, 'Vrata')
            k.box(x + ((w / doors) * 0.3 * (i.zero? ? 1 : -1)), -d / 2 - 0.02, 0.3 + ((h - 0.35) / 2), 0.02, 0.02, 0.12, 'steel_dark', 'Ručka')
          end
          4.times { |i| k.box(0, d / 2 + 0.002, h - 0.12 - (i * 0.03), w * 0.5, 0.006, 0.012, 'steel_dark', 'Ventilacija') } if k.detail.positive?
        end
      end

      define('lighting.street_light') { |k, e| Light.street(k, e) }
      define('lighting.smart_light') { |k, e| Light.street(k, e, smart: true) }
      define('lighting.solar_light') { |k, e| Light.street(k, e, solar: true) }
      define('lighting.pedestrian_light') { |k, e| Light.lantern(k, e) }
      define('lighting.area_floodlight') { |k, e| Light.floodlight(k, e) }
      define('lighting.bollard_light') { |k, e| Light.bollard(k, e) }
      define('lighting.path_light') { |k, e| Light.bollard(k, e) }
      define('lighting.recessed_ground_light') { |k, e| Light.recessed(k, e) }
      define('lighting.underwater_light') { |k, e| Light.underwater(k, e) }
      define('lighting.step_light') { |k, e| Light.step_light(k, e) }
      define('lighting.wall_light') { |k, e| Light.wall_light(k, e) }
      define('lighting.facade_light') { |k, e| Light.spot(k, e) }
      define('lighting.feature_light') { |k, e| Light.spot(k, e, spike: true) }
      define('lighting.garden_light') { |k, e| Light.garden(k, e) }
      define('lighting.pergola_light') { |k, e| Light.pendant(k, e) }
      define('lighting.control_cabinet') { |k, e| Light.cabinet(k, e, label: 'Ormar upravljanja rasvjetom') }
    end
  end
end
