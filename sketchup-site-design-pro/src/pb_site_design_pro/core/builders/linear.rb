# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Builders
      # Strip of constant width along a centreline (roads, paths, markings, trenches); follows path elevations.
      class LinearSlab < PathBuilder
        def build(e, c)
          pts = path_points(e)
          w = e.num('width', 3.0)
          top = e.num('topOffset')
          bottom = top - e.num('thickness', 0.1)
          material = mat(e, 'material', 'asphalt')
          if e.geometry[:closed]
            build_closed_ring(e, c, pts, w, top, bottom, material)
          else
            c.mesh(Mesh.sweep(pts, rect_profile(w / 2, w / 2, bottom, top)), material: material, name: 'Traka')
          end
        end

        private

        def build_closed_ring(e, c, pts, w, top, bottom, material)
          z = base_z(pts)
          outer = Geometry.offset_ring(pts, w / 2)
          inner = Geometry.offset_ring(pts, -w / 2)
          if outer && inner
            c.mesh(Mesh.prism(outer, z + bottom, z + top, [inner]), material: material, name: 'Traka')
          else
            warn(e, 'SDP-B002', 'Zatvorena osovina je preuska za zadanu širinu; izrađena je kao otvorena traka.', 'Provjerite širinu i trasu.')
            c.mesh(Mesh.sweep(pts, rect_profile(w / 2, w / 2, bottom, top), true), material: material, name: 'Traka')
          end
        end
      end
      register('linear_slab', LinearSlab)

      # Wall, kerb, hedge or edging with optional strip footing.
      class Wall < PathBuilder
        def build(e, c)
          pts = path_points(e)
          t = e.num('thickness', 0.2)
          left, right = case e.text('alignment', 'center')
                        when 'left' then [t, 0.0]
                        when 'right' then [0.0, t]
                        else [t / 2, t / 2]
                        end
          z0 = e.num('baseOffset')
          closed = e.geometry[:closed]
          c.mesh(Mesh.sweep(pts, rect_profile(left, right, z0, z0 + e.num('height', 1.0)), closed),
                 material: mat(e, 'material', 'concrete'), name: 'Zid')
          fw = e.num('footingWidth')
          fd = e.num('footingDepth')
          return unless fw.positive? && fd.positive?

          centre = (left - right) / 2.0
          c.mesh(Mesh.sweep(pts, rect_profile(centre + (fw / 2), (fw / 2) - centre, z0 - fd, z0), closed),
                 material: 'concrete', name: 'Temelj')
        end
      end
      register('wall', Wall)

      # Fence: posts at stations (corners and evenly within each run, spacing <= postSpacing) and panels.
      class Fence < PathBuilder
        def build(e, c)
          g = e.geometry
          h = e.num('height', 1.8)
          if @ctx.conceptual?
            pts = path_points(e)
            c.mesh(Mesh.sweep(pts, rect_profile(0.02, 0.02, 0.0, h), g[:closed]), material: mat(e, 'panelMaterial', 'steel_galv'), name: 'Ograda (šematski)')
            return
          end

          stations = Geometry.stations(g[:vertices], g[:bulges], g[:closed], e.num('postSpacing', 2.5))
          post = e.num('postSize', 0.06)
          post_mat = mat(e, 'postMaterial', 'steel_galv')
          foundation = e.num('foundationDepth')
          post_key = "fence-post|#{fmt(post)}|#{fmt(h)}|#{fmt(foundation)}|#{post_mat}|#{@ctx.lod}"
          stations.each do |s|
            c.instance(post_key, s, 0.0, name: 'Stub') do |dc|
              dc.mesh(Mesh.box(0, 0, 0, post, post, h + 0.02), material: post_mat, name: 'Stub')
              if foundation.positive?
                dc.mesh(Mesh.cylinder([0, 0, -foundation], post * 2.5, foundation, 8), material: 'concrete', name: 'Temelj stuba')
              end
            end
          end
          pairs = stations.each_cons(2).to_a
          pairs << [stations.last, stations.first] if g[:closed] && stations.length > 2
          pairs.each { |a, b| build_panel(e, c, a, b, post, h) }
        end

        private

        def build_panel(e, c, a, b, post, h)
          d = Geometry.dist(a, b)
          len = d - post
          return if len <= 0.01

          dir = Geometry.norm(Geometry.sub(b, a))
          clearance = e.num('groundClearance', 0.05)
          thickness = e.num('panelThickness', 0.04)
          style = e.text('panelStyle', 'mesh')
          material = mat(e, 'panelMaterial', 'steel_galv')
          panel_h = h - clearance - 0.03
          return if panel_h <= 0.01

          dz = (b[2] || 0) - (a[2] || 0)
          start = [a[0] + (dir[0] * post / 2), a[1] + (dir[1] * post / 2), (a[2] || 0) + (dz * (post / 2) / d)]
          if dz.abs < 0.001
            key = "fence-panel|#{format('%.3f', len)}|#{fmt(panel_h)}|#{fmt(thickness)}|#{style}|#{material}|#{@ctx.lod}"
            c.instance(key, [start[0], start[1], start[2] + clearance], Math.atan2(dir[1], dir[0]), name: 'Panel') do |dc|
              panel_parts(dc, [0.0, -thickness / 2, 0.0], [len, 0.0, 0.0], [0.0, thickness, 0.0], panel_h, style, material)
            end
          else
            normal = Geometry.perp(dir)
            corner = [start[0] - (normal[0] * thickness / 2), start[1] - (normal[1] * thickness / 2), start[2] + clearance]
            xv = [dir[0] * len, dir[1] * len, dz * len / d]
            panel_parts(c, corner, xv, [normal[0] * thickness, normal[1] * thickness, 0.0], panel_h, style, material)
          end
        end

        # Panel infill between two posts. Solid/glass: one plate; mesh/bars/boards/slats: rails plus infill.
        def panel_parts(c, corner, xv, yv, h, style, material)
          if %w[solid glass].include?(style) || @ctx.conceptual?
            c.mesh(Mesh.block(corner, xv, yv, [0.0, 0.0, h]), material: material, name: 'Panel')
            return
          end

          rail = [0.04, h * 0.1].min
          c.mesh(Mesh.block(corner, xv, yv, [0.0, 0.0, rail]), material: material, name: 'Donja prečka')
          c.mesh(Mesh.block(Mesh.add3(corner, [0.0, 0.0, h - rail]), xv, yv, [0.0, 0.0, rail]), material: material, name: 'Gornja prečka')
          inner = [corner[0], corner[1], corner[2] + rail]
          ih = h - (2 * rail)
          return if ih <= 0.01

          if style == 'bars'
            length = Mesh.len3(xv)
            count = [(length / 0.12).floor, 1].max
            bar = 0.02
            unit = Mesh.scale3(xv, 1.0 / length)
            ym = Mesh.scale3(yv, 0.25)
            count.times do |i|
              at = ((i + 0.5) * length / count) - (bar / 2)
              base = Mesh.add3(Mesh.add3(inner, Mesh.scale3(unit, at)), ym)
              c.mesh(Mesh.block(base, Mesh.scale3(unit, bar), Mesh.scale3(yv, 0.5), [0.0, 0.0, ih]), material: material, name: 'Šipka')
            end
          elsif style == 'boards'
            # vertical boards 12 cm wide with 1 cm gaps on two horizontal rails
            length = Mesh.len3(xv)
            unit = Mesh.scale3(xv, 1.0 / length)
            count = [(length / 0.13).floor, 1].max
            board = (length / count) - 0.01
            count.times do |i|
              # boards in front of the rails (no coplanar faces)
              base = Mesh.add3(Mesh.add3(corner, Mesh.scale3(unit, (i * length / count) + 0.005)), yv)
              c.mesh(Mesh.block(base, Mesh.scale3(unit, board), Mesh.scale3(yv, 0.8), [0.0, 0.0, h]), material: material, name: 'Daska')
            end
          elsif style == 'slats'
            # horizontal slats 9 cm high with 3 cm gaps
            count = [(ih / 0.12).floor, 1].max
            pitch = ih / count
            count.times do |i|
              base = Mesh.add3(inner, [0.0, 0.0, (i * pitch) + 0.015])
              c.mesh(Mesh.block(Mesh.add3(base, Mesh.scale3(yv, 0.2)), xv, Mesh.scale3(yv, 0.6), [0.0, 0.0, pitch - 0.03]), material: material, name: 'Lamela')
            end
          elsif @ctx.presentation? || @ctx.lod == 'standard'
            # welded mesh 50 x 200 mm: vertical wires on one plane, horizontal wires on the next
            length = Mesh.len3(xv)
            unit = Mesh.scale3(xv, 1.0 / length)
            wire = 0.005
            pitch = @ctx.presentation? ? 0.05 : 0.1
            polys = []
            nv = [(length / pitch).floor, 1].max
            nv.times do |i|
              base = Mesh.add3(Mesh.add3(inner, Mesh.scale3(unit, (i + 0.5) * length / nv)), Mesh.scale3(yv, 0.5))
              polys.concat(Mesh.block(base, Mesh.scale3(unit, wire), Mesh.scale3(yv, 0.12), [0.0, 0.0, ih]))
            end
            nh = [(ih / 0.2).round, 1].max
            (1...nh).each do |j|
              base = Mesh.add3(Mesh.add3(inner, [0.0, 0.0, j * ih / nh]), Mesh.scale3(yv, 0.38))
              polys.concat(Mesh.block(base, xv, Mesh.scale3(yv, 0.12), [0.0, 0.0, wire]))
            end
            c.mesh(polys, material: material, name: 'Mrežna ispuna')
          else
            ym = Mesh.scale3(yv, 0.4)
            c.mesh(Mesh.block(Mesh.add3(inner, ym), xv, Mesh.scale3(yv, 0.2), [0.0, 0.0, ih]), material: material, name: 'Mrežna ispuna')
          end
        end
      end
      register('fence', Fence)

      # Linear drainage channel: body below the grate level defined by the path.
      class Channel < PathBuilder
        GRATE = 0.02

        def build(e, c)
          pts = path_points(e)
          w = e.num('width', 0.15)
          depth = e.num('depth', 0.25)
          closed = e.geometry[:closed]
          c.mesh(Mesh.sweep(pts, rect_profile(w / 2, w / 2, -depth, -GRATE), closed), material: mat(e, 'bodyMaterial', 'concrete_precast'), name: 'Tijelo kanala')
          c.mesh(Mesh.sweep(pts, rect_profile(w / 2, w / 2, -GRATE, 0.0), closed), material: mat(e, 'grateMaterial', 'grate_steel'), name: 'Rešetka')
        end
      end
      register('channel', Channel)

      # Pipe, duct or cable: circular section swept along the path at the given axis offset.
      class Pipe < PathBuilder
        def build(e, c)
          pts = path_points(e)
          r = e.num('diameter', 0.2) / 2
          n = @ctx.conceptual? ? 6 : @ctx.segments(12)
          c.mesh(Mesh.sweep(pts, circle_profile(r, n, e.num('axisOffset', -1.0)), e.geometry[:closed]),
                 material: mat(e, 'material', 'hdpe'), name: 'Cijev', smooth: true)
        end
      end
      register('pipe', Pipe)

      # Cable between posts at the first and last vertex (zip line).
      class CableLine < PathBuilder
        def build(e, c)
          v = e.geometry[:vertices]
          a = v.first
          b = v.last
          ha = e.num('startHeight', 3.0)
          hb = e.num('endHeight', 2.0)
          r = e.num('postDiameter', 0.2) / 2
          material = mat(e, 'material', 'steel_galv')
          n = @ctx.segments(12)
          c.mesh(Mesh.cylinder(a, r, ha, n), material: material, name: 'Početni stub', smooth: true)
          c.mesh(Mesh.cylinder(b, r, hb, n), material: material, name: 'Krajnji stub', smooth: true)
          top_a = [a[0], a[1], a[2] + ha - 0.15]
          top_b = [b[0], b[1], b[2] + hb - 0.15]
          c.mesh(Mesh.tube(top_a, top_b, e.num('cableDiameter', 0.02) / 2, 6), material: material, name: 'Sajla')
        end
      end
      register('cable_line', CableLine)
    end
  end
end
