# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Canvas implementation on SketchUp entities (see RecordingCanvas for the contract). Converts metres to
    # SketchUp's internal inches, creates one group per solid (so solids never merge), orients faces outward,
    # cuts openings, softens curved surfaces and shares component definitions by key across imports.
    class SuCanvas
      DICT = 'SiteDesignPro'
      SMOOTH_ANGLE = 50.0 * Math::PI / 180.0
      COPLANAR_ANGLE = 0.05 * Math::PI / 180.0
      PLANAR_TOL = 1e-4 # metres

      attr_reader :entities, :stats

      # definitions: shared Hash key => ComponentDefinition (seeded from the model by #index_definitions)
      def initialize(model, entities, materials, definitions, report, tags = {}, stats = nil)
        @model = model
        @entities = entities
        @materials = materials
        @definitions = definitions
        @report = report
        @tags = tags
        @stats = stats || Hash.new(0)
      end

      def self.index_definitions(model)
        model.definitions.each_with_object({}) do |d, acc|
          key = d.get_attribute(DICT, 'definitionKey')
          acc[key] = d if key && d.valid?
        end
      end

      def child(entities)
        SuCanvas.new(@model, entities, @materials, @definitions, @report, @tags, @stats)
      end

      def group(name, attributes = {})
        grp = @entities.add_group
        grp.name = name.to_s if name
        write_attributes(grp, attributes)
        yield child(grp.entities) if block_given?
        @stats['groups'] += 1
        grp
      end

      def mesh(polygons, material:, name: nil, smooth: false, solid: true, tag: nil)
        grp = @entities.add_group
        grp.name = name.to_s if name
        if smooth && !solid && polygons.length > 20 && polygons.all? { |p| p.is_a?(Array) } && defined?(Geom::PolygonMesh)
          add_polygon_mesh(grp.entities, polygons)
        else
          polygons.each { |poly| add_polygon(grp.entities, poly, solid) }
          soften(grp.entities) if smooth
        end
        grp.material = @materials.get(material)
        grp.layer = @tags[tag] if tag && @tags[tag]
        @stats['meshes'] += 1
        grp
      end

      def edges(points, closed: false, name: nil)
        grp = @entities.add_group
        grp.name = name.to_s if name
        pts = points.map { |p| pt(p) }
        pts << pts.first if closed
        grp.entities.add_edges(pts)
        grp
      end

      def instance(key, origin, rotation, name: nil, attributes: {})
        defn = @definitions[key]
        unless defn&.valid?
          defn = @model.definitions.add(definition_name(key))
          defn.set_attribute(DICT, 'definitionKey', key)
          yield child(defn.entities) if block_given?
          @definitions[key] = defn
          @stats['definitions'] += 1
        end
        inst = @entities.add_instance(defn, SuCanvas.transformation(origin, rotation))
        inst.name = name.to_s if name
        write_attributes(inst, attributes)
        @stats['instances'] += 1
        inst
      end

      # Places an external SketchUp component (custom asset or library mapping) instead of a builder.
      def external_instance(path, origin, rotation, name: nil, attributes: {})
        defn = @model.definitions.load(path)
        inst = @entities.add_instance(defn, SuCanvas.transformation(origin, rotation))
        inst.name = name.to_s if name
        write_attributes(inst, attributes)
        inst
      end

      def self.transformation(origin, rotation)
        t = Geom::Transformation.translation(Geom::Vector3d.new(Units.m_to_in(origin[0]), Units.m_to_in(origin[1]), Units.m_to_in(origin[2] || 0.0)))
        t * Geom::Transformation.rotation(Geom::Point3d.new(0, 0, 0), Geom::Vector3d.new(0, 0, 1), rotation.to_f)
      end

      def write_attributes(entity, attributes)
        attributes.each { |k, v| entity.set_attribute(DICT, k.to_s, v) }
      end

      private

      def definition_name(key)
        builder = key.split('|').first
        "VU #{builder} #{Zlib.crc32(key).to_s(16).rjust(8, '0')}"
      end

      def pt(p)
        Geom::Point3d.new(Units.m_to_in(p[0]), Units.m_to_in(p[1]), Units.m_to_in(p[2] || 0.0))
      end

      def add_polygon(ents, poly, solid)
        if poly.is_a?(Hash)
          face = add_face(ents, poly[:outer])
          return unless face

          poly[:holes].each do |hole|
            inner = add_face(ents, hole, orient: false)
            inner.erase! if inner&.valid?
          end
        elsif Mesh.planar?(poly, PLANAR_TOL)
          add_face(ents, poly)
        else
          (1...(poly.length - 1)).each { |i| add_face(ents, [poly[0], poly[i], poly[i + 1]]) }
        end
      rescue ArgumentError => e
        @stats['failed_faces'] += 1
        Log.write(:debug, "face skipped (#{e.message})") if solid
      end

      # A large surface of triangles (a road surface following its heights) is added in one step, which is many
      # times faster than face by face: each polygon keeps its point order (its normal), and edges between faces
      # that are flat or bend gently are softened and smoothed, so the surface shows no triangle lines.
      def add_polygon_mesh(ents, polygons)
        pm = Geom::PolygonMesh.new(polygons.length * 3, polygons.length)
        added = 0
        polygons.each do |poly|
          next if poly.length < 3

          pm.add_polygon(poly.map { |p| pt(p) })
          added += 1
        rescue ArgumentError
          @stats['failed_faces'] += 1
        end
        ents.add_faces_from_mesh(pm, Geom::PolygonMesh::AUTO_SOFTEN | Geom::PolygonMesh::SMOOTH_SOFT_EDGES)
        @stats['faces'] += added
      rescue StandardError => e # fall back to face by face
        Log.write(:debug, "mreža površine nije dodana u jednom koraku (#{e.message})")
        polygons.each { |poly| add_polygon(ents, poly, false) }
        soften(ents)
      end

      # Adds a face and orients it to the polygon's right-hand-rule normal (SketchUp may flip faces on z = 0).
      def add_face(ents, points, orient: true)
        face = ents.add_face(points.map { |p| pt(p) })
        return nil unless face

        if orient
          n = Mesh.newell(points)
          fn = face.normal
          face.reverse! if (fn.x * n[0]) + (fn.y * n[1]) + (fn.z * n[2]) < 0
        end
        @stats['faces'] += 1
        face
      end

      # Edges between two faces of one mesh: between faces in one plane the edge is erased, so the triangles of a
      # flat or evenly sloped part become one face (no triangle lines, even with hidden geometry shown); between
      # faces that bend less than SMOOTH_ANGLE it is softened and smoothed; sharper edges stay.
      def soften(ents)
        flat = []
        ents.grep(Sketchup::Edge).each do |edge|
          faces = edge.faces
          next unless faces.length == 2

          angle = faces[0].normal.angle_between(faces[1].normal)
          next if angle > SMOOTH_ANGLE

          if angle < COPLANAR_ANGLE && faces[0].material == faces[1].material
            flat << edge
          else
            edge.soft = true
            edge.smooth = true
          end
        end
        merge_coplanar(ents, flat)
      end

      # Erasing an edge between two faces in one plane joins them into one face (as the Eraser does). An edge that
      # would leave a face open (an end that only it uses) is softened instead.
      def merge_coplanar(ents, edges)
        return if edges.empty?

        keep, drop = edges.partition { |e| [e.start, e.end].any? { |v| v.edges.length < 3 } }
        keep.each do |e|
          e.soft = true
          e.smooth = true
        end
        ents.erase_entities(drop) unless drop.empty?
      rescue StandardError => e # merging is cosmetic: the faces stay triangles if it fails
        Log.write(:debug, "spajanje ravnih trouglova nije uspjelo (#{e.message})")
        edges.each { |x| x.soft = true if x.valid? }
      end
    end
  end
end
