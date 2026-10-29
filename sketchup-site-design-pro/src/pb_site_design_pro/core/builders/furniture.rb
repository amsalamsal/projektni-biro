# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Builders
      # Bench / chair: length along local X, seat front facing local -Y.
      class Bench < PointBuilder
        def build(e, c)
          l = e.num('length', 1.8)
          d = e.num('depth', 0.6)
          sh = e.num('seatHeight', 0.45)
          seat = mat(e, 'material', 'timber')
          frame = mat(e, 'frameMaterial', 'steel_dark')
          slab = 0.05
          c.mesh(Mesh.box(0, 0, sh - slab, l, d, slab), material: seat, name: 'Sjedište')
          leg_x = [(l / 2) - 0.15, l * 0.35].min
          [-leg_x, leg_x].each do |x|
            c.mesh(Mesh.box(x, 0, 0, 0.06, d * 0.8, sh - slab), material: frame, name: 'Nosač')
          end
          if e.bool('backrest', true) && e.num('backHeight', 0.8) > sh + 0.1
            back_h = e.num('backHeight', 0.8) - (sh + 0.1)
            c.mesh(Mesh.box(0, (d / 2) - 0.035, sh + 0.1, l, 0.04, back_h), material: seat, name: 'Naslon')
            [-leg_x, leg_x].each do |x|
              c.mesh(Mesh.box(x, (d / 2) - 0.085, sh, 0.05, 0.05, back_h + 0.1), material: frame, name: 'Nosač naslona')
            end
          end
          return unless e.bool('armrests')

          [(-l / 2) + 0.03, (l / 2) - 0.03].each do |x|
            c.mesh(Mesh.box(x, 0, sh, 0.05, d * 0.9, 0.22), material: frame, name: 'Rukohvat')
          end
        end
      end
      register('bench', Bench)

      # Table with optional attached benches on both long sides.
      class Table < PointBuilder
        def build(e, c)
          l = e.num('length', 2.0)
          w = e.num('width', 0.8)
          h = e.num('height', 0.75)
          top = mat(e, 'material', 'timber')
          frame = mat(e, 'frameMaterial', 'steel_dark')
          c.mesh(Mesh.box(0, 0, h - 0.05, l, w, 0.05), material: top, name: 'Ploča stola')
          leg_x = [(l / 2) - 0.2, l * 0.3].min
          [-leg_x, leg_x].each { |x| c.mesh(Mesh.box(x, 0, 0, 0.08, w * 0.7, h - 0.05), material: frame, name: 'Okvir nogu') }
          return unless e.bool('benches', true)

          bw = e.num('benchWidth', 0.3)
          [-1, 1].each do |s|
            y = s * ((w / 2) + (bw / 2) + 0.02)
            c.mesh(Mesh.box(0, y, 0.40, l, bw, 0.05), material: top, name: 'Sjedište klupe')
            [-leg_x, leg_x].each { |x| c.mesh(Mesh.box(x, y, 0, 0.06, bw * 0.6, 0.40), material: frame, name: 'Nosač klupe') }
          end
        end
      end
      register('table', Table)

      # Row of Sheffield-style hoops along local +Y.
      class BikeStand < PointBuilder
        def build(e, c)
          count = [e.num('count', 1).round, 1].max
          spacing = e.num('spacing', 1.0)
          w = e.num('width', 0.8)
          h = e.num('height', 0.8)
          r = e.num('tubeDiameter', 0.05) / 2
          material = mat(e, 'material', 'steel_galv')
          n = @ctx.conceptual? ? 6 : 10
          count.times do |i|
            y = i * spacing
            if @ctx.conceptual?
              c.mesh(Mesh.box(0, y, 0, w, 2 * r, h), material: material, name: 'Stalak')
              next
            end
            x = (w / 2) - r
            c.mesh(Mesh.tube([-x, y, 0], [-x, y, h - r], r, n), material: material, name: 'Noga', smooth: true)
            c.mesh(Mesh.tube([x, y, 0], [x, y, h - r], r, n), material: material, name: 'Noga', smooth: true)
            c.mesh(Mesh.tube([-x - r, y, h - r], [x + r, y, h - r], r, n), material: material, name: 'Gornja cijev', smooth: true)
          end
        end
      end
      register('bike_stand', BikeStand)

      # Swing frame: A-frame legs, beam, seats on chains.
      class Swing < PointBuilder
        def build(e, c)
          w = e.num('width', 3.4)
          h = e.num('height', 2.4)
          spread = e.num('frameSpread', 1.8)
          r = e.num('frameDiameter', 0.1) / 2
          seats = [e.num('seats', 2).round, 1].max
          sh = e.num('seatHeight', 0.45)
          material = mat(e, 'material', 'steel_dark')
          n = @ctx.conceptual? ? 6 : 10
          c.mesh(Mesh.tube([-w / 2, 0, h], [w / 2, 0, h], r, n), material: material, name: 'Greda', smooth: true)
          [-w / 2 + r, w / 2 - r].each do |x|
            [-spread / 2, spread / 2].each do |y|
              c.mesh(Mesh.tube([x, y, 0], [x, 0, h - r], r, n), material: material, name: 'Noga', smooth: true)
            end
          end
          bay = (w - (4 * r)) / seats
          seats.times do |i|
            x = (-w / 2) + (2 * r) + ((i + 0.5) * bay)
            c.mesh(Mesh.box(x, 0, sh, [0.45, bay * 0.6].min, 0.18, 0.03), material: 'rubber', name: 'Sjedište')
            next if @ctx.conceptual?

            [-0.2, 0.2].each do |dx|
              c.mesh(Mesh.tube([x + dx, 0, sh + 0.03], [x + dx, 0, h - r], 0.006, 4), material: 'steel_galv', name: 'Lanac')
            end
          end
        end
      end
      register('swing', Swing)
    end
  end
end
