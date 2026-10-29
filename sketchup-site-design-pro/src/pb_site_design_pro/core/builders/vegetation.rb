# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Builders
      # Tree: trunk plus crown. Overall height = height; crown diameter = canopyDiameter.
      # Conceptual LOD uses octagonal prisms; standard/presentation use smooth faceted solids.
      class Tree < PointBuilder
        def build(e, c)
          h = e.num('height', 8.0)
          canopy = e.num('canopyDiameter', 5.0)
          clear = [e.num('clearTrunkHeight', 2.2), h * 0.9].min
          crown_h = h - clear
          trunk_r = e.num('trunkDiameter', 0.25) / 2
          foliage = mat(e, 'foliageMaterial', 'foliage')
          trunk_top = clear + (crown_h * 0.3)
          if @ctx.conceptual?
            c.mesh(Mesh.cylinder([0, 0, 0], trunk_r, trunk_top, 6), material: 'bark', name: 'Trunk')
            c.mesh(Mesh.cylinder([0, 0, clear], canopy / 2, crown_h, 8), material: foliage, name: 'Crown')
            return
          end

          n = @ctx.segments(12)
          c.mesh(Mesh.cylinder([0, 0, 0], trunk_r, trunk_top, [n / 2, 6].max), material: 'bark', name: 'Trunk', smooth: true)
          crown = case e.text('form', 'round')
                  when 'conical' then Mesh.cone([0, 0, clear], canopy / 2, crown_h, n)
                  when 'oval' then Mesh.ellipsoid([0, 0, clear + (crown_h / 2)], canopy / 2, canopy / 2, crown_h / 2, n, n / 2)
                  else Mesh.ellipsoid([0, 0, clear + (crown_h / 2)], canopy / 2, canopy / 2, crown_h / 2, n, [n / 2, 4].max)
                  end
          c.mesh(crown, material: foliage, name: 'Crown', smooth: true)
        end
      end
      register('tree', Tree)

      class Shrub < PointBuilder
        def build(e, c)
          h = e.num('height', 1.2)
          d = e.num('diameter', 1.2)
          foliage = mat(e, 'foliageMaterial', 'foliage')
          if @ctx.conceptual?
            c.mesh(Mesh.cylinder([0, 0, 0], d / 2, h, 8), material: foliage, name: 'Shrub')
          else
            n = @ctx.segments(10)
            c.mesh(Mesh.ellipsoid([0, 0, h / 2], d / 2, d / 2, h / 2, n, [n / 2, 4].max), material: foliage, name: 'Shrub', smooth: true)
          end
        end
      end
      register('shrub', Shrub)

      # Faceted boulder, partly embedded.
      class Boulder < PointBuilder
        def build(e, c)
          h = e.num('height', 0.7)
          embed = [e.num('embedment', 0.15), h * 0.9].min
          n = @ctx.conceptual? ? 6 : 8
          c.mesh(Mesh.ellipsoid([0, 0, (h / 2) - embed], e.num('length', 1.2) / 2, e.num('width', 0.9) / 2, h / 2, n, 4),
                 material: mat(e, 'material', 'stone_wall'), name: 'Boulder')
        end
      end
      register('boulder', Boulder)
    end
  end
end
