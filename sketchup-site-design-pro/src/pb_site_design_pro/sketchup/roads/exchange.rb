# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Roads
      # The roads both ways between SketchUp and AutoCAD.
      #
      # SketchUp -> AutoCAD: "Izvoz saobraćajnica za AutoCAD" writes the source elements of the road tools (streets,
      # roads, roundabout parts, crossings, elevation marks, kerb ramp marks, garage ramps, ramp sections) as a
      # package; AutoCAD's Uvoz paketa adds or replaces them and draws its own road network from them.
      #
      # AutoCAD -> SketchUp: "Preuzmi saobraćajnice iz AutoCAD-a" makes the roads of an imported AutoCAD project
      # source elements of the road tools (editable here; the 3D is built by the road engine). From then on the project
      # is linked: every later import of an AutoCAD package of it brings the changes of its roads into the source
      # elements (new, changed and – for roads that were in AutoCAD – erased), and the roads are rebuilt.
      #
      # Coordinates: the source elements are kept in the model frame; a package is in the project frame of the AutoCAD
      # project (metres from its origin). The root group of the imported AutoCAD project gives the transformation.
      module Exchange
        DICT = 'SiteDesignPro'
        IN_AUTOCAD = 'inAutoCAD'
        Item = Compat.kw_struct(:id, :type, :name, :label, :params, :geometry)

        module_function

        # ---- what belongs to the roads

        def engine_package?(package)
          (package.generator || {})['host'] == ENGINE_HOST
        end

        def road_element?(type_id, custom)
          SOURCE_TYPES.include?(type_id.to_s) || NETWORK_ONLY_TYPES.include?(type_id.to_s) ||
            (custom.is_a?(Hash) && custom[DERIVED_FLAG] == true)
        end

        def source_element?(type_id, custom)
          SOURCE_TYPES.include?(type_id.to_s) && !(custom.is_a?(Hash) && custom[DERIVED_FLAG] == true)
        end

        def entity_custom(entity)
          JSON.parse(entity.get_attribute(DICT, 'custom', '{}') || '{}')
        rescue JSON::ParserError
          {}
        end

        def road_entity?(entity)
          road_element?(entity.get_attribute(DICT, 'type'), entity_custom(entity))
        end

        # Projects whose roads are source elements of the road tools here: the linked AutoCAD project and the
        # project of the road tools themselves (when AutoCAD took it over from an exported package).
        def linked_ids(model)
          [model.get_attribute(SourceStore::SETTINGS, 'linkedProjectId', nil), model.get_attribute(SourceStore::SETTINGS, 'projectId', nil)].compact.uniq
        end

        def linked?(model, package)
          !engine_package?(package) && linked_ids(model).include?(package.project_id)
        end

        # ---- import of an AutoCAD package of a linked project (called by the importer)

        # Takes the road elements out of the package (the importer neither plans nor builds them); returns the
        # source elements among them, or nil when the package is not of a linked project.
        def take_roads!(model, package)
          return nil unless linked?(model, package)

          removed = package.drop_elements! { |e| road_element?(e.type_id, e.custom) }
          removed.select { |e| source_element?(e.type_id, e.custom) }.map do |e|
            Item.new(id: e.id, type: e.type_id, name: e.name.to_s, label: e.label.to_s, params: e.raw_params || {},
                     geometry: PackageReader.geometry_json(e.geometry))
          end
        end

        # The plan leaves the 3D of the road tools alone (it is not in an AutoCAD package).
        def keep_roads(plan)
          kept = plan.actions.reject do |a|
            a.element.nil? && a.entry && a.entry.ref && %i[delete mark_deleted keep_orphan].include?(a.kind) && road_entity?(a.entry.ref)
          end
          kept.length == plan.actions.length ? plan : plan.class.new(kept, plan.policy)
        end

        # After the import: the roads of the package become the source elements, then the roads are rebuilt.
        def after_import(model, package, items)
          model.start_operation('Saobraćajnice iz AutoCAD-a', true)
          begin
            stats = sync(model, items, root_transform(model, package.project_id))
            link(model, package_info(package))
            model.commit_operation
          rescue StandardError
            model.abort_operation
            raise
          end
          outcome = rebuild(model)
          [stats, outcome]
        end

        # ---- taking over the roads of an imported AutoCAD project

        # AutoCAD projects imported into the model whose roads can be taken over: [[root group, number of roads]].
        def candidates(model)
          own = model.get_attribute(SourceStore::SETTINGS, 'projectId', nil)
          roots = model.entities.grep(Sketchup::Group).select do |g|
            g.get_attribute(DICT, 'role') == 'projectRoot' && g.get_attribute(DICT, 'roadsProject') != true && g.get_attribute(DICT, 'projectId') != own
          end
          roots.map do |r|
            pid = r.get_attribute(DICT, 'projectId')
            n = ModelIndex.managed_entities(model).count do |e|
              e.get_attribute(DICT, 'projectId') == pid && source_element?(e.get_attribute(DICT, 'type'), entity_custom(e))
            end
            [r, n]
          end.select { |_r, n| n.positive? }
        end

        # Makes the roads of the AutoCAD project (root group) source elements; their AutoCAD 3D is removed (the road
        # engine builds it). Returns [stats, outcome of the rebuild].
        def adopt(model, root)
          pid = root.get_attribute(DICT, 'projectId')
          entities = ModelIndex.managed_entities(model).select { |e| e.get_attribute(DICT, 'projectId') == pid && road_entity?(e) }
          items = entities.select { |e| source_element?(e.get_attribute(DICT, 'type'), entity_custom(e)) }.map { |e| item_of(e) }.compact
          model.start_operation('Preuzimanje saobraćajnica iz AutoCAD-a', true)
          begin
            entities.each { |e| e.erase! if e.valid? }
            stats = sync(model, items, root.transformation, delete: false)
            link(model, root_package_info(root))
            model.commit_operation
          rescue StandardError
            model.abort_operation
            raise
          end
          outcome = rebuild(model)
          [stats, outcome]
        end

        # The source element kept on an imported entity (its attributes).
        def item_of(entity)
          type = entity.get_attribute(DICT, 'type')
          params = JSON.parse(entity.get_attribute(DICT, 'params', '{}') || '{}')
          geometry = if (g = entity.get_attribute(DICT, 'geometry'))
                       JSON.parse(g)
                     elsif (pos = entity.get_attribute(DICT, 'position'))
                       { 'kind' => 'point', 'position' => pos.to_a.map(&:to_f), 'rotation' => entity.get_attribute(DICT, 'rotation', 0.0).to_f }
                     end
          return nil unless geometry

          Item.new(id: entity.get_attribute(DICT, 'id'), type: type, name: entity.get_attribute(DICT, 'name', '').to_s,
                   label: entity.get_attribute(DICT, 'label', '').to_s, params: params, geometry: geometry)
        rescue JSON::ParserError
          nil
        end

        # ---- export to AutoCAD

        # Writes the roads as a package for AutoCAD. Returns the result of the engine (package path, counts, notes).
        def export(model, path)
          store = SourceStore.new(model)
          settings = store.settings
          records = store.records
          raise Engine::Error, 'u modelu nema saobraćajnica alata Saobraćajnice (ulica, cesta, kružni tok, …)' if records.empty?

          linked = model.get_attribute(SourceStore::SETTINGS, 'linkedProjectId', nil)
          to_project = root_transform(model, linked).inverse
          in_project = records.map do |r|
            Record.new(id: r.id, type: r.type, name: r.name, label: r.label, params: r.params,
                       geometry: Geo.map(r.geometry) { |v| SourceStore.xf(to_project, v) })
          end
          origin = linked ? linked_origin(model) : [0.0, 0.0, 0.0]
          name = linked ? model.get_attribute(SourceStore::SETTINGS, 'linkedName', settings['projectName']) : settings['projectName']
          request = Request.build(in_project, project_id: linked || settings['projectId'], project_name: name,
                                              base: settings['base'], flat: settings['flat'], origin: origin)
          result = Rebuild.engine(settings['projectId']).export(request, path)
          model.start_operation('Izvoz saobraćajnica za AutoCAD', true, false, true)
          records.each { |r| r.ref.set_attribute(SourceStore::DICT, IN_AUTOCAD, true) if r.ref && r.ref.valid? }
          model.commit_operation
          result
        end

        # ---- shared

        # The source elements become the items (same ids): changed ones are updated, new ones added; with delete,
        # those that were in AutoCAD and are no more are erased. Heights of elevation marks on a vertex of a road
        # axis go back into the axis (as drawn here). tr: package (project) frame -> model.
        def sync(model, items, tr, delete: true)
          store = SourceStore.new(model)
          existing = store.records.each_with_object({}) { |r, h| h[r.id] = r }
          stats = { 'added' => 0, 'updated' => 0, 'erased' => 0, 'heights' => 0 }
          marks = []
          items.each do |it|
            geometry = Geo.map(it.geometry) { |v| SourceStore.xf(tr, v) }
            if it.type == LEVEL_MARK
              marks << [it, geometry]
              next
            end

            put(store, existing[it.id], it, geometry, stats)
          end

          absorbed = absorb_marks(store, marks, existing, stats)
          if delete
            keep = items.map(&:id) - absorbed
            store.records.each do |r|
              next if keep.include?(r.id) || !r.ref || r.ref.get_attribute(SourceStore::DICT, IN_AUTOCAD) != true

              store.erase(r.ref)
              stats['erased'] += 1
            end
          end
          stats
        end

        def put(store, rec, it, geometry, stats)
          if rec && rec.ref && rec.ref.valid?
            store.update(rec.ref, params: it.params, name: it.name, geometry: geometry, type: it.type)
            rec.ref.set_attribute(SourceStore::DICT, 'label', it.label) unless it.label.to_s.empty?
            rec.ref.set_attribute(SourceStore::DICT, IN_AUTOCAD, true)
            stats['updated'] += 1
          else
            g = store.add(type: it.type, params: it.params, geometry: geometry, name: it.name,
                          label: it.label.to_s.empty? ? nil : it.label, id: it.id)
            g.set_attribute(SourceStore::DICT, IN_AUTOCAD, true)
            stats['added'] += 1
          end
        end

        # Elevation marks lying on a vertex of a road axis set the height of that vertex; the others stay marks.
        # Returns the ids of the marks that went into an axis.
        def absorb_marks(store, marks, existing, stats)
          absorbed = []
          return absorbed if marks.empty?

          roads = store.records.select { |r| NETWORK_ROADS.include?(r.type) && r.kind == 'path' && r.geometry['bulges'].nil? }
          marks.each do |it, geometry|
            pos = geometry['position']
            level = it.params['level']
            hit = level.is_a?(Numeric) && roads.find { |r| r.geometry['vertices'].any? { |v| Math.hypot(v[0] - pos[0], v[1] - pos[1]) < 0.05 } }
            if hit
              g = JSON.parse(JSON.generate(hit.geometry))
              g['vertices'] = g['vertices'].map { |v| Math.hypot(v[0] - pos[0], v[1] - pos[1]) < 0.05 ? [v[0], v[1], level.to_f] : v }
              store.update(hit.ref, geometry: g)
              hit.geometry = g
              old = existing[it.id]
              store.erase(old.ref) if old && old.ref && old.ref.valid?
              absorbed << it.id
              stats['heights'] += 1
            else
              put(store, existing[it.id], it, geometry, stats)
            end
          end
          absorbed
        end

        def link(model, info)
          own = model.get_attribute(SourceStore::SETTINGS, 'projectId', nil)
          return if info[:id].nil? || info[:id] == own

          s = SourceStore::SETTINGS
          model.set_attribute(s, 'linkedProjectId', info[:id])
          model.set_attribute(s, 'linkedOrigin', JSON.generate((info[:origin] || [0, 0, 0]).map(&:to_f)))
          model.set_attribute(s, 'linkedName', info[:name].to_s) unless info[:name].to_s.empty?
          model.set_attribute(s, 'base', info[:base].to_f) if info[:base] && model.get_attribute(s, 'base', nil).nil?
        end

        def root_package_info(root)
          { id: root.get_attribute(DICT, 'projectId'), origin: root.get_attribute(DICT, 'origin'),
            name: root.get_attribute(DICT, 'projectName'), base: root.get_attribute(DICT, 'buildingZeroAbsolute') }
        end

        def package_info(package)
          { id: package.project_id, origin: package.origin, name: package.project_name, base: package.building_zero_absolute }
        end

        def linked_origin(model)
          JSON.parse(model.get_attribute(SourceStore::SETTINGS, 'linkedOrigin', '[0,0,0]') || '[0,0,0]').map(&:to_f)
        rescue JSON::ParserError
          [0.0, 0.0, 0.0]
        end

        # Transformation of the project frame of the AutoCAD project (its root group) into the model.
        def root_transform(model, project_id)
          root = project_id && model.entities.grep(Sketchup::Group).find do |g|
            g.get_attribute(DICT, 'role') == 'projectRoot' && g.get_attribute(DICT, 'projectId') == project_id
          end
          root ? root.transformation : Geom::Transformation.new
        end

        def rebuild(model)
          Rebuild.run(model, operation: :own)
        rescue Engine::Error => e
          Log.warn("Saobraćajnice nisu izgrađene: #{e.message}")
          nil
        end

        def stats_text(stats)
          "novih #{stats['added']}, izmijenjenih #{stats['updated']}, obrisanih #{stats['erased']}" +
            (stats['heights'].positive? ? ", visina osovina iz kota #{stats['heights']}" : '')
        end
      end
    end
  end
end
