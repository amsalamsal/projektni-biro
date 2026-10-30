# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Inserting and replacing library models in SketchUp. Library elements carry the same attributes as
    # imported ones (type, parameters, geometry), so the properties dialog can edit them; they are marked
    # source = "library" and belong to no AutoCAD project (they are not sent back with "Export changes").
    module AssetInsert
      DICT = 'SiteDesignPro'
      ROTATE_STEP = 15.0

      module_function

      def library
        @library ||= AssetLibrary.new
      end

      def lod
        Settings.get('lod') || 'standard'
      end

      def catalog
        @catalog ||= CatalogFiles.new
      end

      def materials(model)
        MaterialLibrary.new(model, {}, catalog.materials)
      end

      def canvas(model, entities, report)
        SuCanvas.new(model, entities, materials(model), SuCanvas.index_definitions(model), report)
      end

      def context(report)
        Builders::Context.new(lod: lod, materials: catalog.materials, report: report)
      end

      def attributes(element)
        attrs = {
          'id' => element.id, 'type' => element.type_id, 'builder' => element.builder, 'category' => element.category,
          'modelGroup' => element.model_group, 'name' => element.name, 'label' => '', 'lod' => lod, 'revision' => 1,
          'params' => JSON.generate(element.params), 'properties' => '{}', 'custom' => '{}', 'contentHash' => '',
          'source' => 'library', 'assetId' => element.type_id, 'libraryVersion' => library.version.to_s,
          'importedAt' => Time.now.utc.strftime('%Y-%m-%dT%H:%M:%SZ'), 'deletedInSource' => false
        }
        if element.kind == 'point'
          attrs['position'] = element.geometry[:position]
          attrs['rotation'] = element.geometry[:rotation]
        else
          attrs['geometry'] = JSON.generate(PackageReader.geometry_json(element.geometry))
        end
        attrs
      end

      # Type definition stored in the model, so the properties dialog can edit library elements.
      def store_type(model, id)
        model.set_attribute(Editing::TYPES_DICT, id, JSON.generate(library.type_definition(id)))
      end

      def tag(model, element)
        name = catalog.model_group(element.model_group).tag
        model.layers[name] || model.layers.add(name)
      end

      # Builds one element into `entities`; returns the new entity (raises with the build report on failure).
      def build(model, element, entities)
        report = Report.new('Biblioteka modela')
        entity = ElementBuilder.build(element, canvas(model, entities, report), context(report), attributes(element))
        unless entity
          msg = report.issues.map(&:message).first || 'model nije izrađen'
          raise PackageError, msg
        end
        entity.layer = tag(model, element)
        entity.set_attribute(DICT, 'fingerprint', ModelIndex.fingerprint(entity))
        store_type(model, element.type_id)
        Log.warn("Biblioteka: #{report.issues.map(&:message).uniq.first(3).join('; ')}") unless report.issues.empty?
        entity
      end

      # ---- geometry from the selection --------------------------------------------------------------------

      # Vertex positions of the selection are already in the coordinates of the active (edited) context, which is
      # where the model is built, so they are converted to metres without any transformation.
      def to_m(point)
        [Units.in_to_m(point.x), Units.in_to_m(point.y), Units.in_to_m(point.z)]
      end

      # Polygon from the first selected face (outer loop and holes), in the active context (metres).
      def polygon_from(model)
        face = model.selection.grep(Sketchup::Face).first
        return nil unless face

        ring = ->(loop) { { vertices: loop.vertices.map { |v| to_m(v.position) }, bulges: [0.0] * loop.vertices.length } }
        holes = face.loops.reject(&:outer?).map { |l| ring.call(l) }
        { kind: 'polygon', outer: ring.call(face.outer_loop), holes: holes }
      end

      # Path from the selected edges when they form one chain (open or closed).
      def path_from(model)
        edges = model.selection.grep(Sketchup::Edge)
        return nil if edges.empty?

        left = edges.dup
        count = Hash.new(0)
        edges.each { |e| e.vertices.each { |v| count[v] += 1 } }
        start = count.find { |_, n| n == 1 }&.first || edges.first.start
        pts = [start]
        v = start
        until left.empty?
          e = left.find { |x| x.used_by?(v) }
          break unless e

          left.delete(e)
          v = e.other_vertex(v)
          pts << v
        end
        return nil unless left.empty?

        closed = pts.length > 2 && pts.first == pts.last
        pts.pop if closed
        { kind: 'path', vertices: pts.map { |x| to_m(x.position) }, bulges: [0.0] * pts.length, closed: closed }
      end

      # ---- dialog actions ---------------------------------------------------------------------------------

      # Starts insertion: path/polygon models use the selected edges/face when present, otherwise the insert
      # tool places the model (point) or a default-size outline (path/polygon) at the clicked point.
      def start(id, params, shape = {})
        model = Sketchup.active_model
        kind = library.type_definition(id)['geometry']
        from_selection = kind == 'polygon' ? polygon_from(model) : (kind == 'path' ? path_from(model) : nil)
        if from_selection
          model.start_operation('Vanjsko uređenje PRO: model iz biblioteke', true)
          begin
            element = library.element(id, params: params, geometry: from_selection)
            build(model, element, model.active_entities)
            model.commit_operation
            Sketchup.status_text = "Vanjsko uređenje PRO: #{element.name} izrađen na odabranoj #{kind == 'polygon' ? 'površini' : 'liniji'}."
          rescue StandardError => e
            model.abort_operation
            raise e
          end
          return
        end
        model.select_tool(InsertTool.new(id, params, shape))
      end

      # Replaces the selected element(s) with the chosen model, keeping position, rotation and tag.
      def replace(id, params)
        model = Sketchup.active_model
        sel = model.selection.to_a.select { |e| e.is_a?(Sketchup::ComponentInstance) || e.is_a?(Sketchup::Group) }
        raise PackageError, 'odaberite element ili komponentu koju želite zamijeniti' if sel.empty?

        kind = library.type_definition(id)['geometry']
        skipped = []
        done = []
        model.start_operation('Vanjsko uređenje PRO: zamjena modela', true)
        begin
          sel.each do |ent|
            if Editing.managed?(ent) && ent.get_attribute(DICT, 'source') != 'library'
              skipped << "#{ent.get_attribute(DICT, 'label') || ent.name}: element iz AutoCAD paketa – tip se mijenja u AutoCAD-u (ili dodijelite komponentu u „Biblioteka i postavke“)"
              next
            end
            done << replace_one(model, ent, id, params, kind)
          end
          model.commit_operation
        rescue StandardError => e
          model.abort_operation
          raise e
        end
        model.selection.clear
        model.selection.add(done.compact)
        [done.compact.length, skipped]
      end

      def replace_one(model, ent, id, params, kind)
        layer = ent.layer
        parent = ent.parent.entities
        if kind == 'point'
          tr = ent.transformation
          origin = [Units.in_to_m(tr.origin.x), Units.in_to_m(tr.origin.y), Units.in_to_m(tr.origin.z)]
          rotation = Math.atan2(tr.xaxis.y, tr.xaxis.x)
          element = library.element(id, params: params, geometry: library.default_geometry(id, origin, rotation))
        else
          raw = ent.get_attribute(DICT, 'geometry')
          g = raw ? PackageReader.parse_geometry(JSON.parse(raw)) : nil
          raise PackageError, 'linijski i površinski modeli zamjenjuju se samo na elementima iste vrste geometrije' unless g && g[:kind] == kind

          element = library.element(id, params: params, geometry: g)
        end
        ent.erase!
        new_ent = build(model, element, parent)
        new_ent.layer = layer unless layer.nil? || layer == model.layers[0]
        new_ent
      end

      # Imports the asset's COLLADA file with SketchUp's own importer (placed with the mouse).
      def import_dae(id)
        path = library.file(id, 'model')
        raise PackageError, "datoteka modela za #{id} nedostaje u biblioteci" unless path

        Sketchup.active_model.import(path, false)
      end
    end

    # Interactive placement: click to insert (repeats until Esc), Left/Right arrows rotate by 15 degrees,
    # a typed value sets the rotation in degrees. Path/polygon models are placed as a default-size outline.
    class InsertTool
      def initialize(id, params, shape)
        @id = id
        @params = params
        @shape = shape || {}
        @rotation = 0.0
        @kind = AssetInsert.library.type_definition(id)['geometry']
        @name = AssetInsert.library.asset(id)&.fetch('name', id) || id
      end

      def activate
        @ip = Sketchup::InputPoint.new
        @extent = preview_extent
        Sketchup.vcb_label = 'Rotacija (°)'
        Sketchup.vcb_value = format('%.0f', 0)
        status
      end

      def deactivate(view)
        view.invalidate
      end

      def resume(view)
        status
        view.invalidate
      end

      def status
        Sketchup.status_text = "#{@name}: klik = umetni, strelice lijevo/desno = rotacija ±#{AssetInsert::ROTATE_STEP.to_i}°, " \
                               'upišite ugao u stepenima, Esc = kraj.'
      end

      # Local outline (metres) drawn under the cursor.
      def preview_extent
        case @kind
        when 'point'
          d = AssetInsert.library.asset(@id)&.fetch('dimensions', nil) || { 'x' => 1.0, 'y' => 1.0 }
          [d['x'].to_f, d['y'].to_f]
        when 'path' then [@shape.fetch('length', 6.0).to_f, 0.0]
        else [@shape.fetch('width', 4.0).to_f, @shape.fetch('depth', 3.0).to_f]
        end
      end

      def outline(origin)
        w, d = @extent
        local = case @kind
                when 'point' then [[-w / 2, -d / 2], [w / 2, -d / 2], [w / 2, d / 2], [-w / 2, d / 2]]
                when 'path' then [[0, 0], [w, 0]]
                else [[0, 0], [w, 0], [w, d], [0, d]]
                end
        c = Math.cos(@rotation)
        s = Math.sin(@rotation)
        local.map { |(x, y)| Geom::Point3d.new(origin.x + Units.m_to_in((c * x) - (s * y)), origin.y + Units.m_to_in((s * x) + (c * y)), origin.z) }
      end

      def onMouseMove(_flags, x, y, view)
        @ip.pick(view, x, y)
        view.tooltip = @ip.tooltip if @ip.valid?
        view.invalidate
      end

      def draw(view)
        return unless @ip&.valid?

        @ip.draw(view)
        pts = outline(@ip.position)
        view.drawing_color = Sketchup::Color.new(45, 127, 211)
        view.line_width = 2
        view.draw(pts.length > 2 ? GL_LINE_LOOP : GL_LINES, pts)
        dir = Geom::Point3d.new(@ip.position.x + Units.m_to_in(Math.cos(@rotation)), @ip.position.y + Units.m_to_in(Math.sin(@rotation)), @ip.position.z)
        view.drawing_color = Sketchup::Color.new(214, 69, 65)
        view.draw(GL_LINES, [@ip.position, dir])
      end

      def getExtents
        bb = Geom::BoundingBox.new
        outline(@ip.position).each { |p| bb.add(p) } if @ip&.valid?
        bb
      end

      def onLButtonDown(_flags, _x, _y, view)
        return unless @ip.valid?

        model = Sketchup.active_model
        # the input point is in model coordinates; the model is built in the active (possibly edited) context
        p = @ip.position.transform(model.edit_transform.inverse)
        origin = [Units.in_to_m(p.x), Units.in_to_m(p.y), Units.in_to_m(p.z)]
        w, d = @extent
        geometry = AssetInsert.library.default_geometry(@id, origin, @rotation, width: w, depth: d, length: w)
        model.start_operation('Vanjsko uređenje PRO: umetanje modela', true)
        begin
          element = AssetInsert.library.element(@id, params: @params, geometry: geometry)
          AssetInsert.build(model, element, model.active_entities)
          model.commit_operation
        rescue StandardError => e
          model.abort_operation
          Log.error("Umetanje modela #{@id} nije uspjelo", e)
          UI.messagebox("Model nije umetnut: #{e.message}")
        end
        view.invalidate
      end

      def onKeyDown(key, _repeat, _flags, view)
        step = Units.deg_to_rad(AssetInsert::ROTATE_STEP)
        case key
        when VK_LEFT then @rotation += step
        when VK_RIGHT then @rotation -= step
        else return false
        end
        Sketchup.vcb_value = format('%.0f', Units.rad_to_deg(@rotation) % 360)
        view.invalidate
        true
      end

      def onUserText(text, view)
        deg = Float(text.tr(',', '.'))
        @rotation = Units.deg_to_rad(deg)
        view.invalidate
      rescue ArgumentError
        UI.beep
      end

      def onCancel(_reason, _view)
        Sketchup.active_model.select_tool(nil)
      end
    end
  end
end
