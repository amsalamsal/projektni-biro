# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Builders
      MARKING = 0.005
      # paint reaches 1 mm into the surface below, so its bottom never lies in the plane of that surface
      SINK = 0.001

      # Parking bay markings; insertion = centre of the aisle edge, bay along local +Y.
      class ParkingBay < PointBuilder
        ROW_LAYOUTS = %w[perpendicular parallel angled].freeze
        YELLOW = 'line_paint_yellow'

        def build(e, c)
          return build_row(e, c) if ROW_LAYOUTS.include?(e.text('layout', 'single'))

          w = e.num('width', 2.5)
          l = e.num('length', 5.0)
          lw = e.num('lineWidth', 0.12)
          material = mat(e, 'markingMaterial', 'line_paint_white')
          c.mesh(Mesh.box(-w / 2, l / 2, -SINK, lw, l, MARKING + SINK), material: material, name: 'Bočna linija')
          c.mesh(Mesh.box(w / 2, l / 2, -SINK, lw, l, MARKING + SINK), material: material, name: 'Bočna linija')
          c.mesh(Mesh.box(0, l - (lw / 2), -SINK, w - lw - 0.002, lw, MARKING + SINK), material: material, name: 'Krajnja linija')
          acc = e.bool('accessible')
          c.mesh(Mesh.box(0, l * (e.bool('ev') ? 0.32 : 0.5), -SINK, [w * 0.45, 1.2].min, [w * 0.45, 1.2].min, MARKING + SINK), material: 'line_paint_blue', name: 'Površina simbola pristupačnosti') if acc
          # an accessible bay can also have a charger: both symbols, one behind the other
          c.mesh(Mesh.box(0, l * (acc ? 0.7 : 0.5), -SINK, [w * 0.4, 1.0].min, [w * 0.6, acc ? 1.2 : 1.5].min, MARKING + SINK), material: 'marking_green', name: 'Površina simbola punjača') if e.bool('ev')
        end
      end
      # A bay of a row (AutoCAD SDPPARKINGOKOMITO / PARALELNO / UGAO): the same markings as the plan – end line with
      # a T head (hook at the end of the row), start line with a hook on the first bay, the yellow bay for persons
      # with disabilities with its hatched area and kerb ramp, the hatched triangle of a 45° row. Local frame: the
      # middle of the aisle edge, +Y into the bay, the row along rowDir × X.
      class ParkingBay
        def build_row(e, c)
          layout = e.text('layout')
          w = e.num('width', 2.5)
          d = e.num('length', 5.0)
          f = e.num('rowDir', 1.0).negative? ? -1.0 : 1.0
          lw = e.num('lineWidth', 0.10)
          acc = e.bool('accessible')
          hatch = acc ? e.num('hatchWidth', 0.0) : 0.0
          paint = acc ? YELLOW : mat(e, 'markingMaterial', 'line_paint_white')
          half = w / 2.0
          pt = ->(s, y) { [f * s, y] }
          bars = []
          bar = ->(a, b, width = lw) { bars << [pt.call(*a), pt.call(*b), width] }
          t = layout == 'angled' ? d : 0.0
          head = 1.0
          hook = layout == 'angled' ? 0.5 : 0.55
          y0 = lw / 2.0
          if e.bool('rowStart')
            bar.call([-half, 0], [-half + t, d])
            bar.call([-half - y0, y0], [-half + hook, y0])
          end
          if hatch.positive?
            s0 = half - hatch
            # border and stripes of the hatched area
            [[[s0, y0], [half, y0]], [[half - y0, 0], [half - y0, d]], [[half, d - y0], [s0, d - y0]], [[s0 + y0, d], [s0 + y0, 0]]].each { |a, b| bar.call(a, b) }
            k = 0.7
            while k < d + hatch
              pts = []
              [lw, d - lw].each { |y| x = s0 + k - (d - y); pts << [x, y] if x >= s0 + lw - 1e-9 && x <= half - lw + 1e-9 }
              [s0 + lw, half - lw].each { |x| y = d - (k - (x - s0)); pts << [x, y] if y > lw && y < d - lw }
              pts.sort!
              bar.call(pts.first, pts.last) if pts.length >= 2 && Math.hypot(pts.last[0] - pts.first[0], pts.last[1] - pts.first[1]) > 0.05
              k += 0.7
            end
          else
            bar.call([half, 0], [half + t, d])
          end
          if e.bool('rowEnd')
            bar.call([half - hook, y0], [half + y0, y0])
          elsif hatch.zero?
            bar.call([half - (head / 2), y0], [half + (head / 2), y0])
          end
          bars.each_with_index do |(a, b, width), i|
            ring = Geometry.offset_line([[a[0], a[1], 0.0], [b[0], b[1], 0.0]], width / 2.0) + Geometry.offset_line([[a[0], a[1], 0.0], [b[0], b[1], 0.0]], -width / 2.0).reverse
            c.mesh(Mesh.prism(ring, -SINK, MARKING), material: paint, name: i.zero? ? 'Oznaka parking mjesta' : 'Linija')
          end
          return unless acc

          # the wheelchair symbol: a yellow square in the bay
          size = [(w - hatch) * 0.45, 1.2].min
          c.mesh(Mesh.box(f * ((-half + (half - hatch)) / 2.0), d * 0.55, -SINK, size, size, MARKING + SINK), material: YELLOW, name: 'Površina simbola pristupačnosti')
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
            corner = [corner[0], corner[1], (corner[2] || 0.0) - SINK]
            polys.concat(Mesh.block(corner, [r[:u][0] * r[:length_u], r[:u][1] * r[:length_u], 0], [r[:v][0] * width, r[:v][1] * width, 0], [0, 0, t + SINK]))
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
          c.mesh(Mesh.prism(ring, -SINK, e.num('thickness', MARKING)), material: mat(e, 'material', 'line_paint_white'), name: 'Strelica')
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
