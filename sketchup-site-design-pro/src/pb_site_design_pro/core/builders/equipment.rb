# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Builders
      # Generic rectangular body centred on the insertion point.
      class BoxEquipment < PointBuilder
        def build(e, c)
          z0 = e.num('baseOffset')
          c.mesh(Mesh.box(0, 0, z0, e.num('width', 1.0), e.num('depth', 0.6), e.num('height', 1.2)), material: mat(e, 'material', 'steel_dark'), name: 'Tijelo')
        end
      end
      register('box_equipment', BoxEquipment)

      class CylinderEquipment < PointBuilder
        def build(e, c)
          z0 = e.num('baseOffset')
          n = @ctx.segments(16)
          c.mesh(Mesh.cylinder([0, 0, z0], e.num('diameter', 0.6) / 2, e.num('height', 1.0), n), material: mat(e, 'material', 'steel_dark'), name: 'Tijelo', smooth: true)
        end
      end
      register('cylinder_equipment', CylinderEquipment)

      # Pole with arm (along +X) and head (luminaire, camera, speaker).
      class PoleMounted < PointBuilder
        def build(e, c)
          h = e.num('poleHeight', 6.0)
          r = e.num('poleDiameter', 0.12) / 2
          arm = e.num('armLength', 0.6)
          hl = e.num('headLength', 0.6)
          hw = e.num('headWidth', 0.25)
          hh = e.num('headHeight', 0.12)
          pole_mat = mat(e, 'poleMaterial', 'steel_dark')
          head_mat = mat(e, 'headMaterial', 'luminaire')
          c.mesh(Mesh.cylinder([0, 0, 0], r, h, @ctx.segments(12)), material: pole_mat, name: 'Stub', smooth: true)
          if arm > 1e-6
            c.mesh(Mesh.box(arm / 2, 0, h - 0.06, arm, 0.05, 0.05), material: pole_mat, name: 'Krak')
            c.mesh(Mesh.box(arm + (hl / 2) - 0.05, 0, h - hh - 0.02, hl, hw, hh), material: head_mat, name: 'Glava')
          else
            c.mesh(Mesh.box(0, 0, h, hl, hw, hh), material: head_mat, name: 'Glava')
          end
          return unless e.bool('solarPanel')

          tilt = Units.deg_to_rad(30)
          corner = [-0.4, -0.25 * Math.cos(tilt), h + 0.15]
          c.mesh(Mesh.block(corner, [0.8, 0, 0], [0, 0.5 * Math.cos(tilt), 0.5 * Math.sin(tilt)], [0, -0.03 * Math.sin(tilt), 0.03 * Math.cos(tilt)]),
                 material: 'solar_pv', name: 'Solarni panel')
        end
      end
      register('pole_mounted', PoleMounted)

      class Bollard < PointBuilder
        def build(e, c)
          h = e.num('height', 0.9)
          r = e.num('diameter', 0.2) / 2
          n = @ctx.segments(16)
          material = mat(e, 'material', 'steel_dark')
          if e.bool('lit') && h > 0.2
            c.mesh(Mesh.cylinder([0, 0, 0], r, h - 0.12, n), material: material, name: 'Tijelo', smooth: true)
            c.mesh(Mesh.cylinder([0, 0, h - 0.12], r, 0.12, n), material: 'luminaire', name: 'Svjetiljka', smooth: true)
          else
            c.mesh(Mesh.cylinder([0, 0, 0], r, h, n), material: material, name: 'Tijelo', smooth: true)
          end
        end
      end
      register('bollard', Bollard)

      # Sign or board on 0-2 posts; the face is readable from local -Y.
      class SignPost < PointBuilder
        def build(e, c)
          posts = e.num('postCount', 1).round
          ph = e.num('postHeight', 2.6)
          pr = e.num('postDiameter', 0.076) / 2
          pw = e.num('plateWidth', 0.6)
          plh = e.num('plateHeight', 0.6)
          pb = e.num('plateBottom', 2.0)
          pt = e.num('plateThickness', 0.02)
          post_mat = mat(e, 'postMaterial', 'steel_galv')
          plate_mat = mat(e, 'plateMaterial', 'sign_plate')
          n = @ctx.segments(10)
          xs = case posts
               when 0 then []
               when 1 then [0.0]
               else [-pw * 0.4, pw * 0.4]
               end
          xs.each { |x| c.mesh(Mesh.cylinder([x, 0, 0], pr, ph, n), material: post_mat, name: 'Stub', smooth: true) }
          y = posts.zero? ? 0.0 : -(pr + (pt / 2))
          case e.text('plateShape', 'rectangle')
          when 'circle'
            d = [pw, plh].min
            zc = pb + (d / 2)
            c.mesh(Mesh.tube([0, y - (pt / 2), zc], [0, y + (pt / 2), zc], d / 2, @ctx.segments(16)), material: plate_mat, name: 'Panel', smooth: true)
          when 'triangle'
            c.mesh(triangle_plate(pw, plh, pb, y, pt), material: plate_mat, name: 'Panel')
          else
            c.mesh(Mesh.box(0, y, pb, pw, pt, plh), material: plate_mat, name: 'Panel')
          end
        end

        private

        def triangle_plate(w, h, z0, y, t)
          f = [[-w / 2, y - (t / 2), z0], [w / 2, y - (t / 2), z0], [0, y - (t / 2), z0 + h]]
          b = f.map { |p| [p[0], p[1] + t, p[2]] }
          polys = [f, b.reverse]
          3.times do |i|
            j = (i + 1) % 3
            polys << [f[j], f[i], b[i], b[j]]
          end
          Mesh.fix_orientation(polys)
        end
      end
      register('sign_post', SignPost)

      # Gate or barrier in the closed position; insertion = centre of the opening along local X.
      class Gate < PointBuilder
        def build(e, c)
          w = e.num('width', 1.0)
          h = e.num('height', 1.8)
          post = e.num('postSize', 0.1)
          lt = e.num('leafThickness', 0.05)
          material = mat(e, 'material', 'steel_dark')
          px = (w / 2) + (post / 2)
          kind = e.text('gateType', 'swing_single')
          [-px, px].each { |x| c.mesh(Mesh.box(x, 0, 0, post, post, (kind == 'barrier' ? 1.0 : h + 0.05)), material: material, name: 'Stub kapije') }
          case kind
          when 'barrier'
            c.mesh(Mesh.box(-px, 0.35, 0, 0.3, 0.3, 1.0), material: material, name: 'Kućište rampe')
            c.mesh(Mesh.tube([-w / 2, 0, h * 0.9], [w / 2 - 0.05, 0, h * 0.9], 0.04, 8), material: 'line_paint_white', name: 'Krak rampe', smooth: true)
          when 'swing_double'
            half = (w / 2) - 0.015
            c.mesh(Mesh.box(-w / 4, 0, 0.05, half, lt, h - 0.05), material: material, name: 'Krilo')
            c.mesh(Mesh.box(w / 4, 0, 0.05, half, lt, h - 0.05), material: material, name: 'Krilo')
          when 'sliding'
            c.mesh(Mesh.box(-0.1, -(post / 2) - (lt / 2) - 0.02, 0.05, w + 0.2, lt, h - 0.05), material: material, name: 'Klizno krilo')
          else
            c.mesh(Mesh.box(0, 0, 0.05, w - 0.02, lt, h - 0.05), material: material, name: 'Krilo')
          end
        end
      end
      register('gate', Gate)

      # Ground-mounted PV array: modules as shared components, tilted toward +Y.
      class SolarArray < PointBuilder
        def build(e, c)
          rows = [e.num('rows', 2).round, 1].max
          per_row = [e.num('modulesPerRow', 10).round, 1].max
          mw = e.num('moduleWidth', 1.13)
          ml = e.num('moduleLength', 1.72)
          tilt = Units.deg_to_rad(e.num('tilt', 25.0))
          pitch = e.num('rowSpacing', 3.5)
          low = e.num('lowEdgeHeight', 0.5)
          material = mat(e, 'material', 'solar_pv')
          frame = mat(e, 'frameMaterial', 'aluminium')
          key = "pv-module|#{fmt(mw)}|#{fmt(ml)}|#{fmt(tilt)}|#{material}"
          rows.times do |r|
            y0 = r * pitch
            per_row.times do |m|
              c.instance(key, [m * mw, y0, low], 0.0, name: 'FN modul') do |dc|
                dc.mesh(Mesh.block([0.01, 0, 0], [mw - 0.02, 0, 0], [0, ml * Math.cos(tilt), ml * Math.sin(tilt)],
                                   [0, -0.04 * Math.sin(tilt), 0.04 * Math.cos(tilt)]), material: material, name: 'Modul')
              end
            end
            next if @ctx.conceptual?

            supports = [(per_row / 2.0).ceil, 1].max
            supports.times do |s|
              x = (s + 0.5) * per_row * mw / supports
              c.mesh(Mesh.box(x, y0 + 0.1, 0, 0.06, 0.06, low), material: frame, name: 'Prednja noga')
              back_h = low + (ml * Math.sin(tilt)) - 0.1
              c.mesh(Mesh.box(x, y0 + (ml * Math.cos(tilt)) - 0.15, 0, 0.06, 0.06, back_h), material: frame, name: 'Stražnja noga')
            end
          end
        end
      end
      register('solar_array', SolarArray)

      # Vertical-axis wind turbine: mast, hub and three blades.
      class Vawt < PointBuilder
        def build(e, c)
          mh = e.num('mastHeight', 6.0)
          mr = e.num('mastDiameter', 0.15) / 2
          rr = e.num('rotorDiameter', 1.5) / 2
          rh = e.num('rotorHeight', 2.5)
          material = mat(e, 'material', 'steel_galv')
          rotor = mat(e, 'rotorMaterial', 'aluminium')
          c.mesh(Mesh.cylinder([0, 0, 0], mr, mh + rh, @ctx.segments(12)), material: material, name: 'Stub', smooth: true)
          3.times do |i|
            a = i * 2 * Math::PI / 3
            x = Math.cos(a) * rr
            y = Math.sin(a) * rr
            c.mesh(Mesh.box(x, y, mh, 0.04, 0.25, rh, a), material: rotor, name: 'Lopatica')
            [mh + 0.1, mh + rh - 0.1].each do |z|
              c.mesh(Mesh.tube([Math.cos(a) * mr, Math.sin(a) * mr, z], [x * 0.97, y * 0.97, z], 0.015, 6), material: material, name: 'Krak')
            end
          end
        end
      end
      register('vawt', Vawt)
    end
  end
end
