# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Builders
      # Pergola / carport / canopy on the oriented rectangle of the footprint.
      class Pergola < PolygonBuilder
        COVER_MATERIALS = { 'fabric' => 'fabric', 'solid' => 'steel_dark', 'glass' => 'glass', 'solar' => 'solar_pv' }.freeze

        def build(e, c)
          outer, = polygon_rings(e)
          r = Geometry.oriented_rect(outer)
          lu = r[:length_u]
          lv = r[:length_v]
          z = r[:z]
          h = e.num('height', 2.6)
          cs = e.num('columnSize', 0.15)
          bd = e.num('beamDepth', 0.25)
          bw = e.num('beamWidth', 0.1)
          material = mat(e, 'material', 'timber')
          # rotation that maps local +Y onto the rectangle's V direction (local X then lies along +/-U)
          angle = Math.atan2(r[:v][1], r[:v][0]) - (Math::PI / 2)

          # columns along both long edges (and intermediate on short edges when needed)
          col_key = "pergola-column|#{fmt(cs)}|#{fmt(h)}|#{material}"
          column_positions(lu, lv, cs, e.num('columnSpacing', 3.5)).each do |(a, b)|
            p = Geometry.rect_point(r, a, b)
            c.instance(col_key, p, angle, name: 'Stub') do |dc|
              dc.mesh(Mesh.box(0, 0, 0, cs, cs, h), material: material, name: 'Stub')
            end
          end

          # perimeter beams on top of the columns
          beam_z = z + h
          [cs / 2, lv - (cs / 2)].each do |b|
            corner = Geometry.rect_point(r, 0, b - (bw / 2), beam_z)
            c.mesh(Mesh.block(corner, vec(r[:u], lu), vec(r[:v], bw), [0, 0, bd]), material: material, name: 'Greda')
          end
          [cs / 2, lu - (cs / 2)].each do |a|
            span = lv - cs - bw
            next if span <= 0.01

            corner = Geometry.rect_point(r, a - (bw / 2), (cs / 2) + (bw / 2), beam_z)
            c.mesh(Mesh.block(corner, vec(r[:u], bw), vec(r[:v], span), [0, 0, bd]), material: material, name: 'Poprečna greda')
          end

          rafters(e, c, r, beam_z + bd, material, angle) unless @ctx.conceptual? && e.text('cover', 'none') != 'none'
          cover(e, c, r, beam_z + bd + e.num('rafterDepth', 0.15))
        end

        private

        def vec(v2, length)
          [v2[0] * length, v2[1] * length, 0.0]
        end

        def column_positions(lu, lv, cs, spacing)
          us = stations_1d(lu, cs, spacing)
          vs = stations_1d(lv, cs, spacing)
          pts = []
          us.each { |a| pts << [a, cs / 2] << [a, lv - (cs / 2)] }
          vs[1...-1].each { |b| pts << [cs / 2, b] << [lu - (cs / 2), b] } if vs.length > 2
          pts.uniq
        end

        def stations_1d(length, cs, spacing)
          span = length - cs
          n = [(span / spacing - 1e-9).ceil, 1].max
          Array.new(n + 1) { |i| (cs / 2) + (span * i / n) }
        end

        def rafters(e, c, r, z, material, angle)
          spacing = e.num('rafterSpacing', 0.6)
          rd = e.num('rafterDepth', 0.15)
          rw = e.num('rafterWidth', 0.05)
          louver = Units.deg_to_rad(e.num('louverAngle', 0.0))
          lu = r[:length_u]
          lv = r[:length_v]
          n = [(lu / spacing).floor, 1].max
          step = lu / n
          key = "pergola-rafter|#{fmt(lv)}|#{fmt(rd)}|#{fmt(rw)}|#{fmt(louver)}|#{material}"
          (0..n).each do |i|
            a = [[i * step, rw / 2].max, lu - (rw / 2)].min
            p = Geometry.rect_point(r, a, 0, z)
            c.instance(key, p, angle, name: louver.zero? ? 'Rožnjača' : 'Lamela') do |dc|
              # local: x across the rafter, y along v, z up; louvers rotate about the local Y axis
              xv = [Math.cos(louver) * rw, 0.0, -Math.sin(louver) * rw]
              zv = [Math.sin(louver) * rd, 0.0, Math.cos(louver) * rd]
              corner = [-(xv[0] + zv[0]) / 2, 0.0, 0.0]
              dc.mesh(Mesh.block(corner, xv, [0.0, lv, 0.0], zv), material: material, name: 'Rožnjača')
            end
          end
        end

        def cover(e, c, r, z)
          kind = e.text('cover', 'none')
          return if kind == 'none'

          material = e.param('coverMaterial') ? mat(e, 'coverMaterial', COVER_MATERIALS[kind]) : COVER_MATERIALS[kind]
          lu = r[:length_u]
          lv = r[:length_v]
          slope = Units.deg_to_rad(e.num('roofSlope', 0.0))
          corner = Geometry.rect_point(r, -0.05, -0.05, z)
          rise = (lv + 0.1) * Math.tan(slope)
          c.mesh(Mesh.block(corner, vec(r[:u], lu + 0.1), [r[:v][0] * (lv + 0.1), r[:v][1] * (lv + 0.1), rise], [0, 0, kind == 'fabric' ? 0.01 : 0.03]),
                 material: material, name: "Poklopac (#{kind})")
        end
      end
      register('pergola', Pergola)

      # Shelter / gazebo / pavilion centred on the insertion point; open side toward local -Y.
      class Shelter < PointBuilder
        def build(e, c)
          w = e.num('width', 3.0)
          d = e.num('depth', 1.5)
          h = e.num('height', 2.5)
          ps = e.num('postSize', 0.1)
          rt = e.num('roofThickness', 0.12)
          slope = Units.deg_to_rad(e.num('roofSlope', 5.0))
          frame = mat(e, 'frameMaterial', 'steel_dark')
          wall = mat(e, 'wallMaterial', 'glass')
          roof = mat(e, 'roofMaterial', 'steel_dark')
          type = e.text('roofType', 'mono')
          back_extra = type == 'mono' ? (d - ps) * Math.tan(slope) : 0.0
          [[-1, -1], [1, -1], [1, 1], [-1, 1]].each do |sx, sy|
            ph = h + (sy.positive? ? back_extra : 0.0)
            c.mesh(Mesh.box(sx * ((w - ps) / 2), sy * ((d - ps) / 2), 0, ps, ps, ph), material: frame, name: 'Stub')
          end
          o = 0.15
          case type
          when 'flat'
            c.mesh(Mesh.box(0, 0, h, w + (2 * o), d + (2 * o), rt), material: roof, name: 'Krov')
          when 'pyramid'
            c.mesh(pyramid(w + (2 * o), d + (2 * o), h, slope, rt), material: roof, name: 'Krov')
          else
            corner = [-(w / 2) - o, -(d / 2) - o, h - (o * Math.tan(slope))]
            run = d + (2 * o)
            c.mesh(Mesh.block(corner, [w + (2 * o), 0, 0], [0, run, run * Math.tan(slope)], [0, 0, rt]), material: roof, name: 'Krov')
          end
          if e.bool('backWall', true)
            c.mesh(Mesh.box(0, (d / 2) - ps - 0.01, 0.1, w - (2 * ps) - 0.02, 0.02, h - 0.2), material: wall, name: 'Stražnji zid')
          end
          return unless e.bool('sideWalls')

          [-1, 1].each do |sx|
            c.mesh(Mesh.box(sx * ((w / 2) - ps - 0.01), ps / 2, 0.1, 0.02, d - (2 * ps) - 0.02, h - 0.2), material: wall, name: 'Bočni zid')
          end
        end

        private

        # Closed pyramid roof over a rectangle at eaves height z.
        def pyramid(w, d, z, slope, thickness)
          apex_h = [[w, d].min / 2 * Math.tan(slope), thickness].max
          a = [-w / 2, -d / 2, z]
          b = [w / 2, -d / 2, z]
          cc = [w / 2, d / 2, z]
          dd = [-w / 2, d / 2, z]
          apex = [0.0, 0.0, z + apex_h]
          [[a, dd, cc, b], [a, b, apex], [b, cc, apex], [cc, dd, apex], [dd, a, apex]]
        end
      end
      register('shelter', Shelter)

      # Shade sail through 3 or 4 corners with alternating high/low fixing heights, plus corner masts.
      class Sail < PolygonBuilder
        def build(e, c)
          pts = Geometry.dedupe(e.geometry[:outer][:vertices], true)
          if pts.length < 3 || pts.length > 4
            warn(e, 'SDP-B020', "Jedro za sjenu treba 3 ili 4 ugla (pronađeno #{pts.length}); nije izrađeno.", 'Nacrtajte konturu s 3 ili 4 tjemena.')
            return
          end
          high = e.num('highHeight', 3.5)
          low = e.num('lowHeight', 2.6)
          corners = pts.each_with_index.map { |p, i| [p[0], p[1], (p[2] || 0) + (i.even? ? high : low)] }
          corners = corners.reverse if Geometry.signed_area(corners).negative?
          tris = corners.length == 3 ? [corners] : [[corners[0], corners[1], corners[2]], [corners[0], corners[2], corners[3]]]
          c.mesh(tris, material: mat(e, 'material', 'fabric'), name: 'Jedro', solid: false)
          md = e.num('mastDiameter', 0.12)
          return unless md.positive?

          pts.each_with_index do |p, i|
            c.mesh(Mesh.cylinder([p[0], p[1], p[2] || 0], md / 2, (i.even? ? high : low) + 0.2, @ctx.segments(10)),
                   material: mat(e, 'mastMaterial', 'steel_galv'), name: 'Stub', smooth: true)
          end
        end
      end
      register('sail', Sail)
    end
  end
end
