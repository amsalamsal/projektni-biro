# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Builders
      # Surface slab: top at boundary elevation + topOffset, extruded down by thickness.
      class Slab < PolygonBuilder
        def build(e, c)
          outer, holes = polygon_rings(e)
          top = base_z(outer) + e.num('topOffset')
          c.mesh(Mesh.prism(outer, top - e.num('thickness', 0.08), top, holes), material: mat(e, 'material'), name: 'Surface')
        end
      end
      register('slab', Slab)

      # Building / reference massing.
      class Massing < PolygonBuilder
        def build(e, c)
          outer, holes = polygon_rings(e)
          z0 = base_z(outer) + e.num('baseOffset')
          c.mesh(Mesh.prism(outer, z0, z0 + e.num('height', 12.0), holes), material: mat(e, 'material', 'building_massing'), name: 'Massing')
        end
      end
      register('massing', Massing)

      # Translucent zone volume.
      class Zone < PolygonBuilder
        def build(e, c)
          outer, holes = polygon_rings(e)
          z0 = base_z(outer) + e.num('baseOffset')
          c.mesh(Mesh.prism(outer, z0, z0 + e.num('height', 2.0), holes), material: mat(e, 'material', 'zone_generic'), name: 'Zone')
        end
      end
      register('zone', Zone)

      # Reference outline: edges only.
      class Outline < PolygonBuilder
        def build(e, c)
          outer, holes = polygon_rings(e)
          dz = e.num('lineHeight', 0.02)
          lift = ->(r) { r.map { |p| [p[0], p[1], (p[2] || 0.0) + dz] } }
          c.edges(lift.call(outer), closed: true, name: 'Outline')
          holes.each { |h| c.edges(lift.call(h), closed: true, name: 'Outline opening') }
        end
      end
      register('outline', Outline)

      # Planting bed: soil layer plus a simplified planting mass (or individual clumps at presentation LOD).
      class PlantingArea < PolygonBuilder
        MAX_CLUMPS = 2000

        def build(e, c)
          outer, holes = polygon_rings(e)
          top = base_z(outer) + e.num('topOffset')
          c.mesh(Mesh.prism(outer, top - e.num('soilThickness', 0.3), top, holes), material: mat(e, 'soilMaterial', 'soil'), name: 'Soil')
          h = e.num('plantHeight')
          return if h <= 0

          spacing = e.param('plantSpacing')
          if @ctx.presentation? && spacing.is_a?(Numeric) && spacing.positive?
            build_clumps(e, c, outer, holes, top, h, spacing.to_f)
          else
            inset = Geometry.offset_ring(outer, -0.05) || outer
            c.mesh(Mesh.prism(inset, top, top + h, holes), material: mat(e, 'plantMaterial', 'planting_mixed'), name: 'Planting')
          end
        end

        private

        def build_clumps(e, c, outer, holes, top, h, spacing)
          xs = outer.map { |p| p[0] }
          ys = outer.map { |p| p[1] }
          count = 0
          material = mat(e, 'plantMaterial', 'planting_mixed')
          key = "clump|#{fmt(spacing)}|#{fmt(h)}|#{material}|#{@ctx.lod}"
          y = ys.min + (spacing / 2)
          row = 0
          while y < ys.max
            x = xs.min + (spacing / 2) + (row.odd? ? spacing / 2 : 0)
            while x < xs.max
              pt = [x, y]
              if Geometry.point_in_polygon?(pt, outer) && holes.none? { |hole| Geometry.point_in_polygon?(pt, hole) }
                count += 1
                if count > MAX_CLUMPS
                  warn(e, 'SDP-B010', "Planting clumps limited to #{MAX_CLUMPS}; the rest of the bed is shown as a mass.")
                  return
                end
                c.instance(key, [x, y, top], (x * 7.3) % (2 * Math::PI), name: 'Plant') do |dc|
                  dc.mesh(Mesh.ellipsoid([0, 0, h / 2], spacing * 0.45, spacing * 0.45, h / 2, 8, 4), material: material, name: 'Plant', smooth: true)
                end
              end
              x += spacing
            end
            y += spacing * 0.866
            row += 1
          end
        end
      end
      register('planting_area', PlantingArea)

      # Raised planter: perimeter wall, soil and optional planting.
      class Planter < PolygonBuilder
        def build(e, c)
          outer, = polygon_rings(e)
          z0 = base_z(outer)
          h = e.num('height', 0.6)
          inner = Geometry.offset_ring(outer, -e.num('wallThickness', 0.1))
          unless inner
            warn(e, 'SDP-B001', 'Planter is too narrow for its wall thickness; built as a solid block.', 'Reduce the wall thickness or enlarge the planter.')
            c.mesh(Mesh.prism(outer, z0, z0 + h), material: mat(e, 'material', 'concrete'), name: 'Planter')
            return
          end
          c.mesh(Mesh.prism(outer, z0, z0 + h, [inner]), material: mat(e, 'material', 'concrete'), name: 'Planter wall')
          soil_top = z0 + h - e.num('soilDepthBelowTop', 0.05)
          c.mesh(Mesh.prism(inner, z0, soil_top), material: mat(e, 'soilMaterial', 'soil'), name: 'Soil')
          ph = e.num('plantHeight')
          return if ph <= 0

          plants = Geometry.offset_ring(inner, -0.05) || inner
          c.mesh(Mesh.prism(plants, soil_top, soil_top + ph), material: mat(e, 'plantMaterial', 'planting_mixed'), name: 'Planting')
        end
      end
      register('planter', Planter)

      # Flight of steps on the oriented rectangle of the boundary, ascending away from the first edge.
      class Steps < PolygonBuilder
        def build(e, c)
          outer, = polygon_rings(e)
          r = Geometry.oriented_rect(outer)
          rise = e.num('totalRise', 0.6)
          n = [(rise / e.num('riserHeight', 0.15) - 1e-9).ceil, 1].max
          riser = rise / n
          going = r[:length_v] / n
          profile = [[0.0, 0.0]]
          n.times do |k|
            profile << [k * going, (k + 1) * riser]
            profile << [(k + 1) * going, (k + 1) * riser]
          end
          profile << [r[:length_v], 0.0]
          origin = [r[:origin][0], r[:origin][1], r[:z]]
          c.mesh(Mesh.extrude_profile(origin, r[:u], r[:v], profile, r[:length_u]), material: mat(e, 'material', 'concrete'), name: "Steps (#{n} risers)")
        end
      end
      register('steps', Steps)

      # Ramp slab rising from the first boundary edge to the opposite side.
      class Ramp < PolygonBuilder
        def build(e, c)
          outer, = polygon_rings(e)
          r = Geometry.oriented_rect(outer)
          rise = e.num('rise', 0.3)
          t = e.num('thickness', 0.2)
          profile = [[0.0, -t], [r[:length_v], rise - t], [r[:length_v], rise], [0.0, 0.0]]
          origin = [r[:origin][0], r[:origin][1], r[:z]]
          c.mesh(Mesh.extrude_profile(origin, r[:u], r[:v], profile, r[:length_u]), material: mat(e, 'material', 'concrete'), name: 'Ramp')
        end
      end
      register('ramp', Ramp)
    end
  end
end
