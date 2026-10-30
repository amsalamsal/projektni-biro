# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Models
      # Eco technologies and outdoor climate equipment. Wall-mounted items: wall on +Y at y = 0.
      module Equip
        module_function

        # Rows of PV modules on a tilted aluminium frame; modules face -Y (south when the element points north).
        def solar_array(k, e)
          rows = [e.num('rows', 2).to_i, 1].max
          mods = [e.num('modulesPerRow', 10).to_i, 1].max
          mw = e.num('moduleWidth', 1.13)
          ml = e.num('moduleLength', 1.72)
          tilt = e.num('tilt', 25.0) * Math::PI / 180
          spacing = e.num('rowSpacing', 3.5)
          low = e.num('lowEdgeHeight', 0.5)
          pv = k.mat('material', 'solar_pv')
          fr = k.mat('frameMaterial', 'aluminium')
          span = mods * (mw + 0.02)
          rows.times do |r|
            y0 = (r - ((rows - 1) / 2.0)) * spacing
            yl = y0 - (ml * Math.cos(tilt) / 2)
            yh = y0 + (ml * Math.cos(tilt) / 2)
            zh = low + (ml * Math.sin(tilt))
            legs = [(span / 2.5).ceil + 1, 2].max
            legs.times do |i|
              x = (-span / 2) + (i * span / (legs - 1))
              k.box(x, yl, 0, 0.06, 0.06, low, fr, 'Prednja noga')
              k.box(x, yh, 0, 0.06, 0.06, zh, fr, 'Zadnja noga')
              k.beam([x, yl, low], [x, yh, zh], 0.05, fr, 'Kosi nosač')
              k.box(x, yl, -0.3, 0.25, 0.25, 0.3, 'concrete', 'Temelj') if k.detail.positive?
            end
            [0.25, 0.75].each do |f|
              k.beam([-span / 2, yl + ((yh - yl) * f), low + ((zh - low) * f) + 0.03], [span / 2, yl + ((yh - yl) * f), low + ((zh - low) * f) + 0.03], 0.04, fr, 'Poprečni profil')
            end
            mods.times do |i|
              x = (-span / 2) + ((i + 0.5) * (mw + 0.02))
              k.beam_rect([x, yl + 0.01, low + 0.07], [x, yh - 0.01, zh + 0.07], mw, 0.035, pv, 'PV modul')
            end
          end
        end

        def inverter(k, e)
          w = e.num('width', 0.6)
          d = e.num('depth', 0.25)
          h = e.num('height', 0.8)
          z = e.num('baseOffset', 0.6)
          k.box(0, -d / 2, z, w, d * 0.8, h, k.mat('material', 'steel_galv'), 'Invertor')
          fins = [4, 8, 12][k.detail]
          fins.times { |i| k.box((-w / 2) + ((i + 0.5) * w / fins), -d * 0.1, z + 0.05, 0.01, d * 0.2, h - 0.1, 'aluminium', 'Hladnjak') }
          k.beam_rect([0, -d * 0.9 - 0.004, z + (h * 0.65)], [0, -d * 0.9 - 0.004, z + (h * 0.8)], w * 0.3, 0.004, 'screen_display', 'Displej')
          k.box(0, -d / 2, z - 0.15, 0.25, 0.08, 0.15, 'steel_dark', 'Kablovska uvodnica')
        end

        def wallbox(k, e)
          w = e.num('width', 0.3)
          h = e.num('height', 0.45)
          z = e.num('baseOffset', 1.0)
          k.box(0, -0.075, z, w, 0.13, h, k.mat('material', 'steel_dark'), 'Zidni punjač')
          k.box(0, -0.143, z + h * 0.6, w * 0.6, 0.006, 0.04, 'marking_green', 'LED indikator')
          k.rail([[0, -0.14, z + 0.05], [0.05, -0.2, z - 0.3], [0.12, -0.2, z - 0.55], [0.18, -0.14, z - 0.3], [0.18, -0.14, z + 0.1]], 0.012, 'rubber_black', 'Kabl punjača')
          k.box(0.18, -0.14, z + 0.1, 0.06, 0.06, 0.14, 'plastic_dark', 'Utikač')
        end

        def ev_pedestal(k, e)
          w = e.num('width', 0.4)
          d = e.num('depth', 0.3)
          h = e.num('height', 1.5)
          m = k.mat('material', 'steel_dark')
          k.box(0, 0, 0, w + 0.1, d + 0.1, 0.05, 'concrete', 'Postolje')
          k.plate([w / 2, 0, 0.05], [0.0, 1.0], [[-d / 2, 0.0], [d / 2, 0.0], [d / 2, h - 0.1], [d * 0.2, h], [-d / 2, h - 0.05]], w, m, 'Stanica za punjenje')
          k.beam_rect([0, -d / 2 - 0.004, h * 0.62], [0, -d / 2 - 0.004, h * 0.78], w * 0.55, 0.006, 'screen_display', 'Ekran')
          k.box(0, -d / 2 - 0.003, h * 0.85, w * 0.8, 0.006, 0.03, 'marking_green', 'LED traka')
          [-1, 1].each do |s|
            k.box(s * (w / 2 + 0.02), 0, h * 0.45, 0.04, 0.08, 0.16, 'plastic_dark', 'Držač konektora')
            k.rail([[s * (w / 2 + 0.03), 0, h * 0.45], [s * (w / 2 + 0.12), -0.05, h * 0.2], [s * (w / 2 + 0.08), 0, 0.15], [s * (w / 2 + 0.03), 0, h * 0.4]], 0.014, 'rubber_black', 'Kabl')
          end
        end

        # Outdoor unit with a round fan grille on the front (-Y) and fins on the back.
        def fan_unit(k, e, fans: 1)
          w = e.num('width', 1.1)
          d = e.num('depth', 0.45)
          h = e.num('height', 0.9)
          m = k.mat('material', 'steel_galv')
          [-1, 1].each { |s| k.box(s * (w / 2 - 0.1), 0, 0, 0.08, d + 0.1, 0.12, 'rubber_black', 'Antivibracijski nosač') }
          k.box(0, 0, 0.12, w, d, h - 0.12, m, 'Kućište')
          r = [h - 0.25, (w / fans) - 0.1].min / 2
          fans.times do |i|
            x = (-w / 2) + ((i + 0.5) * w / fans) - (fans == 1 ? w * 0.12 : 0)
            z = 0.12 + ((h - 0.12) / 2)
            k.bar([x, -d / 2, z], [x, -d / 2 - 0.02, z], r, 'steel_dark', 'Rešetka ventilatora', n: k.seg(20))
            next if k.detail.zero?

            [r * 0.35, r * 0.65, r * 0.95].each do |rr|
              k.mesh(Mesh.fix_orientation(Kit.lathe_polys(Array.new(5) { |j| t = 2 * Math::PI * j / 5; [rr + (0.006 * Math.cos(t)), 0.006 * Math.sin(t)] }, 0, 0, k.seg(16), true).map { |p| p.map { |q| [x + q[0], -d / 2 - 0.024 + q[2], z + q[1]] } }), 'steel_galv', 'Prsten rešetke')
            end
          end
          if fans == 1
            k.box(w / 2 - (w * 0.15), -d / 2 - 0.005, h * 0.3, w * 0.2, 0.01, h * 0.45, 'steel_dark', 'Servisni poklopac')
          end
          k.box(0, 0, h, w + 0.02, d + 0.02, 0.02, m, 'Krovna ploča')
        end

        def wind_turbine(k, e)
          mh = e.num('mastHeight', 6.0)
          mr = e.num('mastDiameter', 0.15) / 2
          rd = e.num('rotorDiameter', 1.5) / 2
          rh = e.num('rotorHeight', 2.5)
          m = k.mat('material', 'steel_galv')
          blade = k.mat('rotorMaterial', 'aluminium')
          k.box(0, 0, -0.6, 1.0, 1.0, 0.6, 'concrete', 'Temelj')
          k.box(0, 0, 0, 0.4, 0.4, 0.02, m, 'Prirubnica')
          k.frustum(0, 0, 0.02, mr * 1.4, mr, mh, m, 'Jarbol', n: k.seg(12))
          k.cyl(0, 0, mh, mr * 1.6, 0.25, 'steel_dark', 'Generator', n: k.seg(14))
          z0 = mh + 0.25
          k.cyl(0, 0, z0, mr * 0.5, rh, m, 'Osovina rotora', n: 10)
          blades = 3
          steps = [4, 8, 12][k.detail]
          blades.times do |b|
            pts = Array.new(steps + 1) do |i|
              t = i.to_f / steps
              a = (2 * Math::PI * b / blades) + (t * Math::PI * 2 / 3)
              [Math.cos(a) * rd, Math.sin(a) * rd, z0 + (t * rh)]
            end
            pts.each_cons(2) { |p, q| k.beam_rect(p, q, 0.22, 0.03, blade, 'Lopatica') }
            [0.15, 0.85].each do |f|
              a = (2 * Math::PI * b / blades) + (f * Math::PI * 2 / 3)
              k.bar([0, 0, z0 + (f * rh)], [Math.cos(a) * rd * 0.95, Math.sin(a) * rd * 0.95, z0 + (f * rh)], 0.02, m, 'Krak rotora', n: 6)
            end
          end
        end

        def compost(k, e)
          w = e.num('width', 2.4)
          d = e.num('depth', 1.0)
          h = e.num('height', 1.0)
          wood = k.mat('material', 'timber')
          bays = 3
          (bays + 1).times do |i|
            x = (-w / 2) + (i * w / bays)
            [-d / 2, d / 2].each { |y| k.box(x, y, 0, 0.07, 0.07, h, wood, 'Stub') }
          end
          slats = [3, 6, 8][k.detail]
          slats.times do |s|
            z = 0.05 + (s * (h - 0.1) / slats)
            k.box(0, d / 2, z, w, 0.02, (h - 0.1) / slats * 0.75, wood, 'Letva')
            (bays + 1).times { |i| k.box((-w / 2) + (i * w / bays), 0, z, 0.02, d, (h - 0.1) / slats * 0.75, wood, 'Letva') }
          end
          bays.times { |i| k.blob([(-w / 2) + ((i + 0.5) * w / bays), 0, h * 0.25], w / bays * 0.38, d * 0.38, h * 0.28, 'mulch', 'Kompost', rough: 0.2, level: 1) }
        end

        def battery(k, e)
          Light.cabinet(k, e, label: 'Ormar baterija')
          k.box(0, -e.num('depth', 0.8) / 2 - 0.005, e.num('height', 2.0) * 0.75, 0.25, 0.01, 0.12, 'plastic_yellow', 'Upozorenje')
        end

        def infrared(k, e)
          w = e.num('width', 1.5)
          z = e.num('baseOffset', 2.5)
          k.box(0, -0.12, z, w, 0.12, 0.1, 'steel_dark', 'Grijač')
          k.box(0, -0.12, z - 0.004, w - 0.1, 0.09, 0.004, 'flower_red', 'Grijni element')
          [-w * 0.35, w * 0.35].each { |x| k.beam_rect([x, 0, z + 0.05], [x, -0.12, z + 0.05], 0.04, 0.04, 'steel_dark', 'Zidni nosač') }
        end

        def patio_heater(k, e)
          r = e.num('diameter', 0.5) / 2
          h = e.num('height', 2.2)
          m = k.mat('material', 'steel_galv')
          k.lathe([[0, 0], [r, 0], [r, 0.05], [r * 0.4, 0.1], [r * 0.3, 0.75], [0, 0.75]], m, 'Kućište plinske boce', n: k.seg(16))
          k.cyl(0, 0, 0.75, 0.035, h - 1.0, m, 'Stub', n: 10)
          k.cyl(0, 0, h - 0.4, 0.08, 0.2, 'flower_red', 'Plamenik', n: 12)
          k.lathe([[0, h - 0.2], [0.05, h - 0.2], [r * 1.6, h - 0.12], [r * 1.6, h - 0.1], [0.05, h - 0.15], [0, h - 0.15]], m, 'Reflektor', n: k.seg(20))
          k.cyl(0, 0, h - 0.1, 0.03, 0.1, m, 'Kapa', n: 8)
        end

        def tower_unit(k, e, label)
          w = e.num('width', 0.8)
          d = e.num('depth', 0.8)
          h = e.num('height', 1.8)
          m = k.mat('material', 'steel_galv')
          k.box(0, 0, 0, w, d, 0.1, 'steel_dark', 'Postolje')
          k.box(0, 0, 0.1, w * 0.95, d * 0.95, h - 0.3, m, label)
          louvres = [3, 8, 12][k.detail]
          louvres.times { |i| k.beam_rect([-w * 0.4, -d * 0.48, 0.3 + (i * (h - 0.6) / louvres)], [w * 0.4, -d * 0.48, 0.3 + (i * (h - 0.6) / louvres)], 0.04, 0.012, 'steel_dark', 'Žaluzina') }
          k.bar([0, -d * 0.475, h - 0.32], [0, -d * 0.5, h - 0.32], [w, d].min * 0.32, 'steel_dark', 'Ventilator', n: k.seg(16))
          k.box(0, 0, h - 0.2, w, d, 0.2, m, 'Vrh')
        end

        def awning(k, e)
          w = e.num('width', 4.0)
          proj = e.num('depth', 2.5)
          z = e.num('baseOffset', 2.4)
          drop = proj * 0.25
          k.box(0, -0.08, z + 0.05, w + 0.1, 0.16, 0.2, 'aluminium', 'Kaseta tende')
          k.beam_rect([0, -0.12, z + 0.05], [0, -proj, z - drop + 0.05], w, 0.01, k.mat('material', 'fabric'), 'Platno tende')
          k.box(0, -proj, z - drop, w, 0.08, 0.06, 'aluminium', 'Prednji profil')
          k.box(0, -proj - 0.04, z - drop - 0.2, w, 0.005, 0.2, k.mat('material', 'fabric'), 'Volan')
          [-w / 2 + 0.3, w / 2 - 0.3].each do |x|
            mid = [x + (w > 2 ? 0.3 : 0.1), -proj * 0.55, z - drop * 0.3]
            k.bar([x, -0.12, z], mid, 0.02, 'aluminium', 'Zglobni krak', n: 8)
            k.bar(mid, [x, -proj + 0.05, z - drop + 0.02], 0.02, 'aluminium', 'Zglobni krak', n: 8)
          end
        end

        def misting_post(k, e)
          h = e.num('height', 2.6)
          r = e.num('diameter', 0.12) / 2
          k.box(0, 0, 0, 0.35, 0.35, 0.02, 'steel_dark', 'Podloga')
          k.cyl(0, 0, 0.02, r, h - 0.02, k.mat('material', 'steel_galv'), 'Stub', n: k.seg(12))
          k.torus([0, 0, h - 0.1], 0.35, 0.015, 'stainless', 'Prsten mlaznica', n: k.seg(16), m: 5)
          [0, 1, 2, 3].each { |i| a = i * Math::PI / 2; k.bar([0, 0, h - 0.1], [Math.cos(a) * 0.35, Math.sin(a) * 0.35, h - 0.1], 0.012, 'stainless', 'Krak', n: 6) }
          n = [4, 8, 12][k.detail]
          n.times { |i| a = 2 * Math::PI * i / n; k.cyl(Math.cos(a) * 0.35, Math.sin(a) * 0.35, h - 0.14, 0.012, 0.03, 'steel_dark', 'Mlaznica', n: 6) }
          k.box(0, r + 0.06, 0.6, 0.18, 0.1, 0.25, 'steel_dark', 'Pumpa i ventil')
        end
      end

      define('sustainability.solar_array') { |k, e| Equip.solar_array(k, e) }
      define('sustainability.inverter') { |k, e| Equip.inverter(k, e) }
      define('sustainability.battery_cabinet') { |k, e| Equip.battery(k, e) }
      define('sustainability.energy_cabinet') { |k, e| Light.cabinet(k, e, label: 'Energetski ormar') }
      define('sustainability.ev_charger_pedestal') { |k, e| Equip.ev_pedestal(k, e) }
      define('sustainability.ev_charger_wallbox') { |k, e| Equip.wallbox(k, e) }
      define('sustainability.heat_pump') { |k, e| Equip.fan_unit(k, e, fans: 1) }
      define('sustainability.outdoor_unit') { |k, e| Equip.fan_unit(k, e, fans: 1) }
      define('sustainability.wind_turbine') { |k, e| Equip.wind_turbine(k, e) }
      define('sustainability.greywater_unit') { |k, e| Equip.tower_unit(k, e, 'Uređaj za sivu vodu') }
      define('sustainability.compost_station') { |k, e| Equip.compost(k, e) }
      define('climate.infrared_heater') { |k, e| Equip.infrared(k, e) }
      define('climate.patio_heater') { |k, e| Equip.patio_heater(k, e) }
      define('climate.cooling_unit') { |k, e| Equip.tower_unit(k, e, 'Rashladni uređaj') }
      define('climate.evaporative_cooler') { |k, e| Equip.tower_unit(k, e, 'Evaporativni hladnjak') }
      define('climate.ventilation_unit') { |k, e| Equip.fan_unit(k, e, fans: 2) }
      define('climate.awning') { |k, e| Equip.awning(k, e) }
      define('climate.misting_post') { |k, e| Equip.misting_post(k, e) }
    end
  end
end
