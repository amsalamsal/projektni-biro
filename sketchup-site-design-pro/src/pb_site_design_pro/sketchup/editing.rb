# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # SketchUp-side element editing and the reverse exchange (changes back to AutoCAD).
    module Editing
      DICT = 'SiteDesignPro'
      TYPES_DICT = 'SiteDesignPro.types'

      module_function

      def managed?(entity)
        entity.respond_to?(:get_attribute) && !entity.get_attribute(DICT, 'id').nil?
      end

      # Data shown in the element properties dialog.
      def describe(model, entity, catalog: CatalogFiles.new)
        td = type_definition(model, entity.get_attribute(DICT, 'type'))
        params = JSON.parse(entity.get_attribute(DICT, 'params') || '{}')
        local = JSON.parse(entity.get_attribute(DICT, 'localParams') || '{}')
        {
          'id' => entity.get_attribute(DICT, 'id'),
          'label' => entity.get_attribute(DICT, 'label'),
          'name' => entity.get_attribute(DICT, 'name'),
          'type' => entity.get_attribute(DICT, 'type'),
          'typeName' => td ? td['name'] : entity.get_attribute(DICT, 'type'),
          'category' => entity.get_attribute(DICT, 'category'),
          'projectId' => entity.get_attribute(DICT, 'projectId'),
          'revision' => entity.get_attribute(DICT, 'revision'),
          'importedAt' => entity.get_attribute(DICT, 'importedAt'),
          'deletedInSource' => entity.get_attribute(DICT, 'deletedInSource') == true,
          'locallyModified' => ModelIndex.fingerprint(entity) != entity.get_attribute(DICT, 'fingerprint') || !local.empty?,
          'parameters' => (td ? td['parameters'] : []).map do |p|
            p.merge('value' => local.key?(p['key']) ? local[p['key']] : params[p['key']], 'edited' => local.key?(p['key']))
          end,
          'properties' => JSON.parse(entity.get_attribute(DICT, 'properties') || '{}'),
          'materials' => catalog.materials.values.map { |m| { 'key' => m.key, 'name' => m.name } },
          'optionLabels' => catalog.option_labels,
          'propertyLabels' => catalog.property_labels,
          'categoryName' => catalog.category_name(entity.get_attribute(DICT, 'category'))
        }
      end

      def type_definition(model, type_id)
        raw = model.get_attribute(TYPES_DICT, type_id)
        raw ? JSON.parse(raw) : nil
      end

      # Applies parameter edits made in SketchUp: validates them, stores them as local edits (sent back with
      # "Export changes") and rebuilds the element's geometry in place. Returns [entity, report].
      def apply_parameters(model, entity, edits, catalog: CatalogFiles.new)
        report = Report.new('Izmjena elementa')
        td_hash = type_definition(model, entity.get_attribute(DICT, 'type'))
        raise PackageError, 'definicija tipa elementa nije pronađena u modelu; ponovo uvezite paket' unless td_hash

        td = TypeDef.new(td_hash.merge('category' => entity.get_attribute(DICT, 'category'), 'modelGroup' => entity.get_attribute(DICT, 'modelGroup')))
        clean = validate_edits(td, edits, report)
        return [entity, report] if report.errors?

        base = JSON.parse(entity.get_attribute(DICT, 'params') || '{}')
        local = JSON.parse(entity.get_attribute(DICT, 'localParams') || '{}').merge(clean)
        local.reject! { |k, v| base[k] == v }
        element = element_from(entity, td, base.merge(local))
        model.start_operation('Vanjsko uređenje PRO: izmjena elementa', true)
        begin
          new_entity = rebuild(model, entity, element, catalog, report)
          if local.empty?
            new_entity.attribute_dictionary(DICT)&.delete_key('localParams')
          else
            new_entity.set_attribute(DICT, 'localParams', JSON.generate(local))
          end
          model.commit_operation
          [new_entity, report]
        rescue StandardError => e
          model.abort_operation
          Log.error('Izmjena elementa nije uspjela', e)
          report.add(:error, 'SDP-E999', element.id, "Izmjena nije uspjela i poništena je: #{e.message}")
          [entity, report]
        end
      end

      def validate_edits(td, edits, report)
        edits.each_with_object({}) do |(key, value), acc|
          pd = td.param_def(key)
          unless pd
            report.add(:error, 'SDP-E001', nil, "Nepoznat parametar '#{key}'.")
            next
          end
          v = coerce(pd, value)
          ok = case pd['type']
               when 'length', 'number', 'angle', 'integer'
                 v.is_a?(Numeric) && (pd['min'].nil? || v >= pd['min']) && (pd['max'].nil? || v <= pd['max']) &&
                 (pd['type'] != 'integer' || (v - v.round).abs < 1e-9)
               when 'bool' then [true, false].include?(v)
               when 'enum' then (pd['options'] || []).include?(v)
               else true
               end
          if ok
            acc[key] = v
          else
            report.add(:error, 'SDP-E002', nil, "Neispravna vrijednost #{value.inspect} za '#{pd['label'] || key}'" \
                                                "#{pd['min'] ? " (#{pd['min']} do #{pd['max']})" : ''}.")
          end
        end
      end

      def coerce(pd, value)
        case pd['type']
        when 'length', 'number', 'angle' then value.is_a?(String) ? Float(value.tr(',', '.')) : value
        when 'integer' then value.is_a?(String) ? Integer(value, 10) : value
        when 'bool' then value.is_a?(String) ? value == 'true' : value
        else value
        end
      rescue ArgumentError, TypeError
        value
      end

      # Reconstructs a package Element from stored attributes (for rebuilding after edits).
      def element_from(entity, td, params)
        g = if td.geometry == 'point'
              { kind: 'point', position: entity.get_attribute(DICT, 'position'), rotation: entity.get_attribute(DICT, 'rotation') }
            else
              raw = entity.get_attribute(DICT, 'geometry')
              raise PackageError, 'geometrija elementa nije sačuvana u modelu; ponovo uvezite paket' unless raw

              PackageReader.parse_geometry(JSON.parse(raw))
            end
        h = { 'id' => entity.get_attribute(DICT, 'id'), 'type' => td.id, 'category' => td.category, 'builder' => td.builder,
              'name' => entity.get_attribute(DICT, 'name'), 'label' => entity.get_attribute(DICT, 'label'),
              'modelGroup' => td.model_group, 'contentHash' => entity.get_attribute(DICT, 'contentHash'), 'parameters' => params,
              'revision' => entity.get_attribute(DICT, 'revision') }
        Element.new(h, g, td)
      end

      # Point elements swap their component definition (keeping position, rotation and tag); path and
      # polygon elements are rebuilt from the package geometry stored on the element at import.
      def rebuild(model, entity, element, catalog, report)
        materials = MaterialLibrary.new(model, {}, catalog.materials)
        ctx = Builders::Context.new(lod: entity.get_attribute(DICT, 'lod') || 'standard', report: report)
        defs = SuCanvas.index_definitions(model)
        canvas = SuCanvas.new(model, entity.parent.entities, materials, defs, report)
        builder = Builders.for(element.builder).new(ctx)
        if element.kind == 'point'
          key = builder.definition_key(element)
          defn = defs[key]
          unless defn&.valid?
            temp = canvas.instance(key, [0, 0, 0], 0.0) { |dc| builder.build(element, dc) }
            defn = temp.definition
            temp.erase!
          end
          entity.definition = defn
          entity.set_attribute(DICT, 'fingerprint', ModelIndex.fingerprint(entity))
          return entity
        end

        attrs = entity.attribute_dictionary(DICT).to_a.to_h
        layer = entity.layer
        entity.erase!
        new_entity = ElementBuilder.build(element, canvas, ctx, attrs)
        raise PackageError, 'element nije moguće ponovo izraditi' unless new_entity

        new_entity.layer = layer
        new_entity.set_attribute(DICT, 'fingerprint', ModelIndex.fingerprint(new_entity))
        new_entity
      end

      # Collects SketchUp-side changes for one project into a ChangeSet.
      def collect_changes(model, project_id)
        root = model.entities.grep(Sketchup::Group).find { |g| g.get_attribute(DICT, 'role') == 'projectRoot' && g.get_attribute(DICT, 'projectId') == project_id }
        raise PackageError, 'projekat nije pronađen u ovom modelu' unless root

        cs = ChangeSet.new(project_id: project_id, package_id: root.get_attribute(DICT, 'packageId'), origin: root.get_attribute(DICT, 'origin'))
        ModelIndex.managed_entities(model).each do |e|
          next unless e.get_attribute(DICT, 'projectId') == project_id
          next if e.get_attribute(DICT, 'deletedInSource') == true

          kind = type_definition(model, e.get_attribute(DICT, 'type'))&.fetch('geometry', nil) || (e.is_a?(Sketchup::Group) ? 'polygon' : 'point')
          imported = kind == 'point' ? { position: e.get_attribute(DICT, 'position'), rotation: e.get_attribute(DICT, 'rotation') } : nil
          current = kind == 'point' ? ModelIndex.placement(e, root) : nil
          cs.add(id: e.get_attribute(DICT, 'id'), type_id: e.get_attribute(DICT, 'type'), label: e.get_attribute(DICT, 'label'),
                 base_content_hash: e.get_attribute(DICT, 'contentHash'),
                 params: JSON.parse(e.get_attribute(DICT, 'localParams') || '{}'),
                 imported: imported, current: current, kind: kind,
                 geometry_modified: ModelIndex.fingerprint(e) != e.get_attribute(DICT, 'fingerprint'))
        end
        cs
      end

      def projects(model)
        model.entities.grep(Sketchup::Group).select { |g| g.get_attribute(DICT, 'role') == 'projectRoot' }.map do |g|
          { 'id' => g.get_attribute(DICT, 'projectId'), 'name' => g.get_attribute(DICT, 'projectName') || g.name }
        end
      end
    end
  end
end
