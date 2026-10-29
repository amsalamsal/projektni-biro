# frozen_string_literal: true

require 'digest'

module ProjektniBiro
  module SiteDesignPro
    # A triangulated terrain surface from terrain.json (metres, package frame). Built in AutoCAD from survey
    # points, contours and breaklines; SketchUp only displays it - it never invents elevations.
    TerrainSurface = Struct.new(:id, :name, :vertices, :triangles, :breaklines, :issues, :statistics, :content_hash) do
      def triangle_count
        triangles.length
      end

      def min_z
        vertices.map { |v| v[2] }.min
      end

      def max_z
        vertices.map { |v| v[2] }.max
      end
    end

    module TerrainReader
      module_function

      # Parses terrain.json text; problems go to the report. Returns an array of TerrainSurface.
      def parse(text, report)
        doc = JSON.parse(text)
        raise PackageError, 'terrain.json: dužine moraju biti u metrima' unless doc['lengthUnit'] == 'm'

        (doc['surfaces'] || []).filter_map do |s|
          verts = (s['vertices'] || []).map { |v| PackageReader.vec3(v) }
          tris = (s['triangles'] || []).map do |t|
            unless t.is_a?(Array) && t.length == 3 && t.all? { |i| i.is_a?(Integer) && i >= 0 && i < verts.length }
              raise PackageError, "teren #{s['id']}: neispravan trougao #{t.inspect}"
            end

            t
          end
          if tris.empty?
            report.add(:warning, 'SDP-T100', nil, "Teren \"#{s['name']}\" nema trouglova i neće se prikazati.",
                       'Provjerite izvorne podatke terena u AutoCAD-u (SDPTEREN).')
            next
          end
          (s['issues'] || []).each do |i|
            sev = i['severity'] == 'error' ? :error : :warning
            report.add(sev, i['code'].to_s, nil, "Teren \"#{s['name']}\": #{i['message']}", 'Provjera terena iz AutoCAD-a.')
          end
          TerrainSurface.new(s['id'].to_s, s['name'].to_s, verts, tris, s['breaklines'] || [], s['issues'] || [],
                             s['statistics'] || {}, Digest::SHA256.hexdigest(JSON.generate(s)))
        end
      end
    end

    # Emits a terrain surface to a canvas as one smooth, open mesh (the surface is not a solid).
    module TerrainBuilder
      MATERIAL = 'terrain'

      module_function

      def build(surface, canvas, attributes = {})
        polygons = surface.triangles.map { |a, b, c| [surface.vertices[a], surface.vertices[b], surface.vertices[c]] }
        canvas.group(surface.name.empty? ? 'Teren' : surface.name, attributes) do |g|
          g.mesh(polygons, material: MATERIAL, name: 'Površina terena', smooth: true, solid: false)
        end
      end
    end
  end
end
