# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Reads, validates, plans and applies a package import as one undoable SketchUp operation.
    # Before anything changes, a verified backup of the saved model is made (when the model has a file) and the
    # operation is written to the recovery journal; an interrupted import is reported at the next start.
    class Importer
      DICT = 'SiteDesignPro'
      PROGRESS_EVERY = 50
      OPERATION = 'Uvoz paketa Vanjsko uređenje PRO'

      attr_reader :package, :read_report, :plan, :terrain_plan, :report, :backup_path

      def initialize(model, path, catalog: CatalogFiles.new, lod: Settings.get('lod'), policy: Settings.policy)
        @model = model
        @path = path
        @catalog = catalog
        @lod = lod
        @policy = policy
        @package, @read_report = PackageReader.read(path)
      end

      def readable?
        !@package.nil?
      end

      def plan!(lod: @lod, policy: @policy)
        @lod = lod
        @policy = policy
        raise PackageError, 'paket nije moguće pročitati' unless @package

        entries = ModelIndex.entries(@model)
        @plan = ChangePlanner.new(@policy).plan(@package, entries, ->(e) { ElementBuilder.supported?(e) || custom_component(e) })
        @terrain_plan = TerrainPlanner.plan(@package.terrains, ModelIndex.terrain_entries(@model), @package.project_id, @plan.policy)
        @plan
      end

      # Applies the current plan. Returns the import report. Any exception aborts the whole operation.
      def apply!(create_scenes: Settings.get('createScenes'), backup: Settings.get('backupEnabled'))
        plan! unless @plan
        @report = Report.new('Vanjsko uređenje PRO – izvještaj uvoza')
        @report.package_path = @path
        @report.merge!(@read_report)
        started = Time.now
        @backup_path = backup ? make_backup : nil
        journal = Safety.journal
        op_id = journal&.begin_operation(OPERATION, @model.path.to_s.empty? ? '(nesačuvan model)' : @model.path, @backup_path)
        @model.start_operation(OPERATION, true)
        begin
          run_plan
          run_terrain_plan
          store_project_metadata
          Scenes.create(@model, @organizer, @catalog) if create_scenes
          @model.commit_operation
          journal&.end_operation(op_id, 'Completed', @plan.summary)
        rescue StandardError => e
          @model.abort_operation
          journal&.end_operation(op_id, 'RolledBack', "#{e.class}: #{e.message}")
          Log.error('Uvoz prekinut', e)
          @report.add(:error, 'SDP-I999', nil, "Uvoz je prekinut i poništen: #{e.class}: #{e.message}",
                      'Model nije izmijenjen. Pogledajte dnevnik (log) i prijavite grešku ako se ponavlja.')
        ensure
          Sketchup.status_text = ''
        end
        @report.finished_at = Time.now
        @report.stats['seconds'] = (@report.finished_at - started).round(2)
        save_report
        @report
      end

      private

      # Backup of the model before the import. A saved model is copied (Model#save_copy keeps unsaved changes
      # too); an unsaved model cannot be backed up and the user relies on Undo.
      def make_backup
        path = @model.path.to_s
        if path.empty?
          @report.add(:info, 'SDP-B001', nil, 'Model još nije sačuvan, pa sigurnosna kopija nije napravljena.',
                      'Uvoz se može poništiti naredbom Uredi > Poništi (Undo). Sačuvajte model prije većih izmjena.')
          return nil
        end

        manager = Safety.backup_manager
        target = manager.next_path(path, 'uvoz paketa')
        FileUtils.mkdir_p(File.dirname(target))
        saved = @model.respond_to?(:save_copy) && @model.save_copy(target)
        saved ? manager.adopt(path, target) : (target = manager.backup_file(path, 'uvoz paketa'))
        @report.add(:info, 'SDP-B002', nil, "Sigurnosna kopija modela: #{target}", '')
        target
      rescue StandardError => e
        Log.warn("Sigurnosna kopija nije napravljena: #{e.message}")
        @report.add(:warning, 'SDP-B003', nil, "Sigurnosna kopija nije napravljena: #{e.message}",
                    'Uvoz se i dalje može poništiti naredbom Uredi > Poništi (Undo).')
        nil
      end

      def run_plan
        @organizer = Organizer.new(@model, @package, @catalog)
        @materials = MaterialLibrary.new(@model, @package.materials, @catalog.materials, @package)
        @ctx = Builders::Context.new(lod: @lod, materials: @package.materials, report: @report)
        @definitions = SuCanvas.index_definitions(@model)
        @tags = { review: @organizer.review_tag }
        total = @plan.actions.length
        @plan.actions.each_with_index do |action, i|
          Sketchup.status_text = "Vanjsko uređenje PRO: #{i + 1} / #{total} – #{action.element&.display_name}" if (i % PROGRESS_EVERY).zero?
          apply_action(action)
        end
      end

      def run_terrain_plan
        @terrain_plan.each do |a|
          case a.kind
          when :create then build_terrain(a.element) && count(:terrains_created)
          when :update, :restore
            a.entry.ref.erase!
            build_terrain(a.element) && count(:terrains_updated)
          when :conflict
            count(:conflicts)
            @report.add(:warning, 'SDP-I014', nil, "Teren \"#{a.element.name}\": #{a.reason}", 'Uključite „Prepiši izmjene iz SketchUp-a" za ažuriranje terena.')
          when :mark_deleted then mark_deleted(a.entry.ref) && count(:terrains_marked_deleted)
          when :delete
            a.entry.ref.erase!
            count(:terrains_deleted)
          end
        end
      end

      def build_terrain(surface)
        container = @organizer.container('terrain')
        canvas = SuCanvas.new(@model, container.entities, @materials, @definitions, @report, @tags, @report.stats)
        attrs = {
          'terrainId' => surface.id, 'projectId' => @package.project_id, 'packageId' => @package.package_id,
          'name' => surface.name, 'contentHash' => surface.content_hash, 'triangles' => surface.triangle_count,
          'importedAt' => Time.now.utc.strftime('%Y-%m-%dT%H:%M:%SZ'), 'deletedInSource' => false
        }
        group = TerrainBuilder.build(surface, canvas, attrs)
        group.set_attribute(DICT, 'fingerprint', ModelIndex.fingerprint(group))
        true
      end

      def apply_action(a)
        case a.kind
        when :create then create(a.element) && count(:created)
        when :update, :restore then update(a.element, a.entry.ref) && count(a.kind == :update ? :updated : :restored)
        when :conflict
          count(:conflicts)
          issue(:warning, 'SDP-I010', a, 'Izmijenjeno u AutoCAD-u, ali i u SketchUp-u: nije ažurirano. ' \
                                         'Koristite „Prepiši izmjene iz SketchUp-a" ili ažurirajte objekat ručno.')
        when :keep_local then count(:kept_local)
        when :mark_deleted then mark_deleted(a.entry.ref) && count(:marked_deleted)
        when :delete
          a.entry.ref.erase!
          count(:deleted)
        when :keep_orphan
          count(:orphans)
          issue(:info, 'SDP-I011', a, 'Element više ne postoji u AutoCAD-u; zadržan u SketchUp-u (pravilo: zadrži).')
        when :detach_duplicate
          a.entry.ref.attribute_dictionaries.delete(DICT)
          count(:detached)
          issue(:info, 'SDP-I012', a, 'Kopija upravljanog objekta je odvojena; sada je obična SketchUp geometrija.')
        when :skip
          count(:skipped)
          issue(:warning, 'SDP-I013', a, a.reason)
        else count(:unchanged)
        end
      end

      def count(key)
        @report.stats[key.to_s] += 1
        true
      end

      def issue(sev, code, action, message)
        el = action.element
        @report.add(sev, code, el ? el.id : action.entry&.id, message, '', el&.label)
      end

      def canvas_for(element)
        container = @organizer.container(element.model_group)
        SuCanvas.new(@model, container.entities, @materials, @definitions, @report, @tags, @report.stats)
      end

      def create(element)
        canvas = canvas_for(element)
        entity = build(element, canvas)
        return false unless entity

        stamp(entity, element)
        true
      end

      def build(element, canvas)
        attrs = element_attributes(element)
        path = custom_component(element)
        if path && element.kind == 'point'
          g = element.geometry
          return canvas.external_instance(path, g[:position], g[:rotation], name: element.display_name, attributes: attrs)
        end
        ElementBuilder.build(element, canvas, @ctx, attrs)
      end

      def update(element, entity)
        parent_entities = if entity.get_attribute(DICT, 'modelGroup') == element.model_group
                            entity.parent.entities
                          else
                            @organizer.container(element.model_group).entities # type moved to another model group
                          end
        layer = entity.layer
        entity.erase!
        canvas = SuCanvas.new(@model, parent_entities, @materials, @definitions, @report, @tags, @report.stats)
        new_entity = build(element, canvas)
        return false unless new_entity

        new_entity.layer = layer unless layer == @organizer.deleted_tag
        stamp(new_entity, element)
        true
      end

      def mark_deleted(entity)
        entity.set_attribute(DICT, 'deletedInSource', true)
        entity.layer = @organizer.deleted_tag
        true
      end

      # Custom SketchUp component for an element: package asset, then user library mapping.
      def custom_component(element)
        if element.asset_id
          asset = @package.asset(element.asset_id)
          if asset && asset.kind == 'component'
            bytes = @package.asset_bytes(asset)
            if bytes
              file = File.join(Dir.tmpdir, "sdp_asset_#{asset.id.gsub(/[^\w.-]/, '_')}.skp")
              File.binwrite(file, bytes) unless File.file?(file)
              return file
            end
          end
        end
        Settings.component_for(element.type_id)
      end

      def element_attributes(element)
        attrs = {
          'id' => element.id, 'projectId' => @package.project_id, 'packageId' => @package.package_id,
          'type' => element.type_id, 'builder' => element.builder, 'category' => element.category,
          'modelGroup' => element.model_group, 'label' => element.label.to_s, 'name' => element.name,
          'contentHash' => element.content_hash, 'revision' => element.revision, 'lod' => @lod,
          'params' => JSON.generate(element.params), 'properties' => JSON.generate(element.properties),
          'custom' => JSON.generate(element.custom), 'importedAt' => Time.now.utc.strftime('%Y-%m-%dT%H:%M:%SZ'),
          'deletedInSource' => false
        }
        if element.kind == 'point'
          attrs['position'] = element.geometry[:position]
          attrs['rotation'] = element.geometry[:rotation]
        else
          attrs['geometry'] = JSON.generate(PackageReader.geometry_json(element.geometry))
        end
        attrs
      end

      # Final attributes after the entity exists: fingerprint and cleared local edits.
      def stamp(entity, element)
        entity.set_attribute(DICT, 'fingerprint', ModelIndex.fingerprint(entity))
        dicts = entity.attribute_dictionary(DICT)
        dicts&.delete_key('localParams')
        element
      end

      def store_project_metadata
        root = @organizer.root
        root.set_attribute(DICT, 'projectName', @package.project_name)
        root.set_attribute(DICT, 'packageId', @package.package_id)
        root.set_attribute(DICT, 'origin', @package.origin)
        root.set_attribute(DICT, 'trueNorthDegrees', @package.true_north_degrees)
        root.set_attribute(DICT, 'crs', @package.crs.to_s)
        root.set_attribute(DICT, 'sourceLengthUnit', @package.units['sourceLengthUnit'])
        root.set_attribute(DICT, 'packageZeroAbsolute', @package.package_zero_absolute) if @package.package_zero_absolute
        root.set_attribute(DICT, 'buildingZeroAbsolute', @package.building_zero_absolute) if @package.building_zero_absolute
        root.set_attribute(DICT, 'verticalDatum', @package.vertical_datum)
        root.set_attribute(DICT, 'lastImport', Time.now.utc.strftime('%Y-%m-%dT%H:%M:%SZ'))
        root.set_attribute(DICT, 'packagePath', @path.to_s)
        @package.type_defs.each_value do |td|
          @model.set_attribute('SiteDesignPro.types', td.id, JSON.generate('id' => td.id, 'name' => td.name, 'builder' => td.builder,
                                                                         'geometry' => td.geometry, 'parameters' => td.parameters))
        end
        unless @package.package_zero_absolute
          @report.add(:info, 'SDP-E001', nil, 'Projekat nema definisanu apsolutnu nadmorsku visinu; model je u relativnim kotama.',
                      'Definišite nultu kotu objekta u AutoCAD-u (SDPDATUM) za prikaz apsolutnih kota.')
        end
        return unless Settings.get('setNorth')

        @model.shadow_info['NorthAngle'] = @package.true_north_degrees
      end

      def save_report
        dir = Sketchup.respond_to?(:temp_dir) ? Sketchup.temp_dir : Dir.tmpdir
        File.write(File.join(dir, 'site_design_pro_last_import.json'), JSON.pretty_generate(@report.to_h))
        File.write(File.join(dir, 'site_design_pro_last_import.txt'), @report.to_text)
        Log.info("Uvoz završen: #{@report.stats.sort_by { |k, _| k.to_s }.map { |k, v| "#{k}=#{v}" }.join(', ')}")
      rescue SystemCallError, IOError => e
        Log.warn("Izvještaj nije sačuvan: #{e.message}")
      end
    end
  end
end
