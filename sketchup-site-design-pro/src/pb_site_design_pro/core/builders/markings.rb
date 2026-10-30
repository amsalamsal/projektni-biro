# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Builders
      MARKING = 0.003

      # Parking bay markings; insertion = centre of the aisle edge, bay along local +Y.
      class ParkingBay < PointBuilder
        def build(e, c)
          w = e.num('width', 2.5)
          l = e.num('length', 5.0)
          lw = e.num('lineWidth', 0.12)
          material = mat(e, 'markingMaterial', 'line_paint_white')
          c.mesh(Mesh.box(-w / 2, l / 2, 0, lw, l, MARKING), material: material, name: 'Bočna linija')
          c.mesh(Mesh.box(w / 2, l / 2, 0, lw, l, MARKING), material: material, name: 'Bočna linija')
          c.mesh(Mesh.box(0, l - (lw / 2), 0, w - lw - 0.002, lw, MARKING), material: material, name: 'Krajnja linija')
          acc = e.bool('accessible')
          c.mesh(Mesh.box(0, l * (e.bool('ev') ? 0.32 : 0.5), 0, [w * 0.45, 1.2].min, [w * 0.45, 1.2].min, MARKING), material: 'line_paint_blue', name: 'Površina simbola pristupačnosti') if acc
          # an accessible bay can also have a charger: both symbols, one behind the other
          c.mesh(Mesh.box(0, l * (acc ? 0.7 : 0.5), 0, [w * 0.4, 1.0].min, [w * 0.6, acc ? 1.2 : 1.5].min, MARKING), material: 'marking_green', name: 'Površina simbola punjača') if e.bool('ev')
        end
      end
      register('parking_bay', ParkingBay)

      # Stripes parallel to the first boundary edge, repeated across the oriented rectangle.
      class Stripes < PolygonBuilder
        def build(e, c)
          outer, = polygon_rings(e)
          r = Geometry.oriented_rect(outer)
          sw = e.num('stripeWidth', 0.5)
          gap = e.num('gapWidth', 0.5)
          t = e.num('thickness', MARKING)
          polys = []
          v = 0.0
          while v + 1e-6 < r[:length_v]
            width = [sw, r[:length_v] - v].min
            corner = Geometry.rect_point(r, 0, v)
            polys.concat(Mesh.block(corner, [r[:u][0] * r[:length_u], r[:u][1] * r[:length_u], 0], [r[:v][0] * width, r[:v][1] * width, 0], [0, 0, t]))
            v += sw + gap
          end
          c.mesh(polys, material: mat(e, 'material', 'line_paint_white'), name: 'Pruge') unless polys.empty?
        end
      end
      register('stripes', Stripes)

      # Painted arrow pointing along local +X from the insertion point.
      class ArrowMarking < PointBuilder
        def build(e, c)
          l = e.num('length', 5.0)
          hw = e.num('width', 1.0)
          sw = e.num('shaftWidth', 0.15)
          head = [l * 0.4, hw * 1.6].min
          ring = [[0, -sw / 2], [l - head, -sw / 2], [l - head, -hw / 2], [l, 0], [l - head, hw / 2], [l - head, sw / 2], [0, sw / 2]]
          c.mesh(Mesh.prism(ring, 0, e.num('thickness', MARKING)), material: mat(e, 'material', 'line_paint_white'), name: 'Strelica')
        end
      end
      register('arrow_marking', ArrowMarking)

      # Flat triangular reference marker pointing along local +X.
      class Marker < PointBuilder
        def build(e, c)
          s = e.num('size', 1.0)
          ring = [[s * 0.5, 0], [-s * 0.5, s * 0.35], [-s * 0.5, -s * 0.35]]
          c.mesh(Mesh.prism(ring, 0.0, 0.01), material: mat(e, 'material', 'line_paint_blue'), name: 'Oznaka')
        end
      end
      register('marker', Marker)

      # Circular zone volume (root protection, safety envelope, illustrative coverage).
      class CircleZone < PointBuilder
        def build(e, c)
          z0 = e.num('baseOffset')
          c.mesh(Mesh.cylinder([0, 0, z0], e.num('radius', 2.0), e.num('height', 0.05), @ctx.segments(32)),
                 material: mat(e, 'material', 'zone_generic'), name: 'Zona', smooth: true)
        end
      end
      register('circle_zone', CircleZone)
    end
  end
end
