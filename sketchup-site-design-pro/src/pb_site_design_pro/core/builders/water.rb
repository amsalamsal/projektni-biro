# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Builders
      # Pool / pond: walls around the water-side boundary, floor and a water volume.
      class Pool < PolygonBuilder
        def build(e, c)
          inner, = polygon_rings(e)
          z = base_z(inner)
          depth = e.num('depth', 1.5)
          floor_t = e.num('floorThickness', 0.25)
          finish = mat(e, 'finishMaterial', 'pool_finish')
          outer = Geometry.offset_ring(inner, e.num('wallThickness', 0.25))
          if outer
            c.mesh(Mesh.prism(outer, z - depth - floor_t, z, [inner]), material: finish, name: 'Zidovi bazena')
          else
            warn(e, 'SDP-B030', 'Zidovi bazena se ne mogu odmaknuti od ove granice; izrađeni su samo dno i voda.', 'Pojednostavite konturu bazena.')
          end
          c.mesh(Mesh.prism(inner, z - depth - floor_t, z - depth), material: finish, name: 'Dno bazena')
          water_top = z - e.num('waterLevelOffset', 0.1)
          return unless water_top > z - depth + 1e-4

          c.mesh(Mesh.prism(inner, z - depth, water_top), material: mat(e, 'waterMaterial', 'water'), name: 'Voda')
        end
      end
      register('pool', Pool)

      # Basin with sloped sides (retention basin, rain garden, swale): an open dish surface plus optional water.
      class Basin < PolygonBuilder
        def build(e, c)
          top, = polygon_rings(e)
          top = Geometry.ccw(Geometry.simplify_collinear(top))
          z = base_z(top)
          depth = e.num('depth', 0.5)
          slope = e.num('sideSlope', 3.0)
          bottom, inset = inset_ring(top, slope * depth)
          if inset < (slope * depth) - 1e-6
            warn(e, 'SDP-B031', format('Bazen je preuzak za nagib kosine 1:%<s>.1f; kosine su strmije (uvlačenje %<i>.2f m).', s: slope, i: inset).tr('.', ','),
                 'Smanjite nagib kosine ili dubinu, ili povećajte bazen.')
          end
          polys = [bottom.map { |p| [p[0], p[1], z - depth] }]
          top.each_index do |i|
            j = (i + 1) % top.length
            a = top[i]
            b = top[j]
            # sides face up into the basin (toward the air)
            polys << [[b[0], b[1], z], [bottom[j][0], bottom[j][1], z - depth], [bottom[i][0], bottom[i][1], z - depth], [a[0], a[1], z]]
          end
          c.mesh(polys, material: mat(e, 'floorMaterial', 'grass'), name: 'Korito', solid: false)
          wd = e.num('waterDepth')
          return unless wd.positive?

          level = z - depth + wd
          ratio = depth.positive? ? wd / depth : 0
          water = top.each_index.map do |i|
            [bottom[i][0] + ((top[i][0] - bottom[i][0]) * ratio), bottom[i][1] + ((top[i][1] - bottom[i][1]) * ratio), level]
          end
          c.mesh([water], material: mat(e, 'waterMaterial', 'water'), name: 'Vodena površina', solid: false)
        end

        private

        # Inset ring with the same vertex count as the input (falls back to smaller insets, then vertical sides).
        def inset_ring(top, inset)
          [inset, inset * 0.5, inset * 0.25, inset * 0.1].each do |d|
            next if d <= 0

            r = Geometry.offset_ring(top, -d)
            return [r, d] if r && r.length == top.length
          end
          [top.map(&:dup), 0.0]
        end
      end
      register('basin', Basin)

      # Round fountain: rim wall, water and jet.
      class Fountain < PointBuilder
        def build(e, c)
          r = e.num('basinDiameter', 3.0) / 2
          rim = [e.num('rimWidth', 0.25), r * 0.5].min
          bh = e.num('basinHeight', 0.45)
          n = @ctx.segments(24)
          stone = mat(e, 'basinMaterial', 'stone_wall')
          water = mat(e, 'waterMaterial', 'water')
          if bh.positive?
            c.mesh(Mesh.ring_wall([0, 0, -0.3], r, r - rim, bh + 0.3, n), material: stone, name: 'Rub korita', smooth: true)
          end
          c.mesh(Mesh.cylinder([0, 0, -0.45], r - (bh.positive? ? rim : 0), 0.15, n), material: stone, name: 'Dno korita', smooth: true)
          level = bh.positive? ? bh - 0.05 : 0.0
          c.mesh(Mesh.cylinder([0, 0, -0.3], r - (bh.positive? ? rim : 0), level + 0.3, n), material: water, name: 'Voda', smooth: true)
          jh = e.num('jetHeight', 2.0)
          return unless jh.positive?

          c.mesh(Mesh.cylinder([0, 0, level], e.num('jetDiameter', 0.08) / 2, jh, 8), material: water, name: 'Mlaz', smooth: true)
        end
      end
      register('fountain', Fountain)
    end
  end
end
