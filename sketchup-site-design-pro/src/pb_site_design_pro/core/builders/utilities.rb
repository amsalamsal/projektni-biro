# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Builders
      # Underground / semi-underground waste container. Preserves burial depth: the body extends
      # bodyDepth below the cover level (insertion elevation + coverLevelOffset).
      class UndergroundContainer < PointBuilder
        PLATFORM = 0.12

        def build(e, c)
          zc = e.num('coverLevelOffset')
          l = e.num('bodyLength', 2.0)
          w = e.num('bodyWidth', 2.0)
          depth = e.num('bodyDepth', 2.4)
          above = e.num('aboveGroundHeight', 1.0)
          body = mat(e, 'material', 'concrete_precast')
          column = mat(e, 'columnMaterial', 'steel_dark')
          if e.text('installation', 'underground') == 'semi_underground'
            c.mesh(Mesh.box(0, 0, zc - depth, l, w, depth + above), material: body, name: 'Tijelo kontejnera')
            c.mesh(Mesh.box(0, 0, zc + above, l + 0.04, w + 0.04, 0.04), material: column, name: 'Poklopac')
          else
            c.mesh(Mesh.box(0, 0, zc - depth, l, w, depth - PLATFORM), material: body, name: 'Tijelo kontejnera (ukopano)')
            c.mesh(Mesh.box(0, 0, zc - PLATFORM, l, w, PLATFORM), material: 'concrete_paver', name: 'Pješačka platforma')
            if above.positive?
              c.mesh(Mesh.cylinder([0, 0, zc], e.num('columnDiameter', 0.8) / 2, above, @ctx.segments(16)), material: column, name: 'Stub za ubacivanje', smooth: true)
            end
          end
          return unless e.bool('showExcavation')

          m = e.num('excavationMargin', 0.5)
          c.mesh(Mesh.box(0, 0, zc - depth - 0.2, l + (2 * m), w + (2 * m), depth + 0.2), material: 'zone_excavation', name: 'Gabarit iskopa')
        end
      end
      register('underground_container', UndergroundContainer)

      # Inspection chamber / manhole: base, walls, cover slab with cover.
      class Chamber < PointBuilder
        COVER = 0.08
        BASE = 0.15

        def build(e, c)
          zc = e.num('coverLevelOffset')
          inner = e.num('innerSize', 1.0)
          wall = e.num('wallThickness', 0.12)
          depth = e.num('depth', 1.5)
          cover = [e.num('coverSize', 0.6), inner].min
          outer = inner + (2 * wall)
          body = mat(e, 'material', 'concrete_precast')
          lid = mat(e, 'coverMaterial', 'cast_iron')
          n = @ctx.segments(16)
          round = e.text('shape', 'round') == 'round'
          outer_ring = round ? Mesh.circle_ring(0, 0, outer / 2, n) : Mesh.rect_ring(0, 0, outer, outer)
          inner_ring = round ? Mesh.circle_ring(0, 0, inner / 2, n) : Mesh.rect_ring(0, 0, inner, inner)
          cover_ring = e.text('coverShape', 'round') == 'round' ? Mesh.circle_ring(0, 0, cover / 2, n) : Mesh.rect_ring(0, 0, cover, cover)
          c.mesh(Mesh.prism(outer_ring, zc - depth - BASE, zc - depth), material: body, name: 'Dno okna')
          c.mesh(Mesh.prism(outer_ring, zc - depth, zc - COVER, [inner_ring]), material: body, name: 'Zidovi okna')
          c.mesh(Mesh.prism(outer_ring, zc - COVER, zc, [cover_ring]), material: body, name: 'Pokrovna ploča')
          c.mesh(Mesh.prism(cover_ring, zc - COVER, zc), material: lid, name: 'Poklopac')
        end
      end
      register('chamber', Chamber)

      # Tank or cistern (box, horizontal or vertical cylinder) with optional access shaft.
      class Tank < PointBuilder
        def build(e, c)
          l = e.num('length', 3.0)
          w = e.num('width', 1.5)
          h = e.num('height', 1.5)
          shape = e.text('shape', 'box')
          underground = e.text('installation', 'underground') == 'underground'
          height = shape == 'horizontal_cylinder' ? w : h
          top = underground ? -e.num('coverDepth', 0.8) : height
          bottom = top - height
          material = mat(e, 'material', 'hdpe')
          n = @ctx.segments(20)
          case shape
          when 'horizontal_cylinder'
            zc = bottom + (w / 2)
            c.mesh(Mesh.tube([-l / 2, 0, zc], [l / 2, 0, zc], w / 2, n), material: material, name: 'Rezervoar', smooth: true)
          when 'vertical_cylinder'
            c.mesh(Mesh.cylinder([0, 0, bottom], w / 2, h, n), material: material, name: 'Rezervoar', smooth: true)
          else
            c.mesh(Mesh.box(0, 0, bottom, l, w, h), material: material, name: 'Rezervoar')
          end
          return unless underground && e.bool('accessShaft', true) && top.negative?

          sr = e.num('shaftDiameter', 0.6) / 2
          shaft_bottom = shape == 'horizontal_cylinder' ? top - 0.05 : top
          c.mesh(Mesh.cylinder([0, 0, shaft_bottom], sr, -shaft_bottom - 0.04, n), material: material, name: 'Pristupno okno', smooth: true)
          c.mesh(Mesh.cylinder([0, 0, -0.04], sr + 0.05, 0.04, n), material: 'cast_iron', name: 'Poklopac okna', smooth: true)
        end
      end
      register('tank', Tank)
    end
  end
end
