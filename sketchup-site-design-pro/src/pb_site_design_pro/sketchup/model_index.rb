# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Finds managed Site Design Pro objects anywhere in the model (all group and component instances) and
    # computes their fingerprints for change detection.
    module ModelIndex
      DICT = 'SiteDesignPro'

      module_function

      # All entities carrying an element id. Returns an array of [entity, attributes Hash].
      def managed_entities(model)
        out = []
        model.definitions.each do |d|
          d.instances.each do |inst|
            next unless inst.valid?

            id = inst.get_attribute(DICT, 'id')
            out << inst if id
          end
        end
        out
      end

      def entries(model)
        managed_entities(model).map { |e| entry_for(e) }
      end

      def entry_for(entity)
        local = entity.get_attribute(DICT, 'localParams')
        ModelEntry.new(
          id: entity.get_attribute(DICT, 'id'),
          project_id: entity.get_attribute(DICT, 'projectId'),
          type_id: entity.get_attribute(DICT, 'type'),
          content_hash: entity.get_attribute(DICT, 'contentHash'),
          stored_fingerprint: entity.get_attribute(DICT, 'fingerprint'),
          current_fingerprint: fingerprint(entity),
          deleted_in_source: entity.get_attribute(DICT, 'deletedInSource') == true,
          local_params: local ? JSON.parse(local) : nil,
          ref: entity
        )
      end

      # Fingerprint of the entity's placement and content. Moving, rotating, scaling or editing the
      # contents changes it; renaming or re-tagging does not.
      def fingerprint(entity)
        d = entity.definition
        b = d.bounds
        Fingerprint.compute([entity.is_a?(Sketchup::Group) ? 'g' : 'i', d.name, entity.transformation.to_a,
                             d.entities.length, b.min.to_a, b.max.to_a])
      end

      # Placement of a point element relative to the project root (metres, radians).
      def placement(entity, root)
        t = entity.transformation
        parent = entity.parent
        while parent.is_a?(Sketchup::ComponentDefinition)
          owner = parent.instances.first
          break if owner.nil? || owner == root

          t = owner.transformation * t
          parent = owner.parent
        end
        o = t.origin
        x = t.xaxis
        z = t.zaxis
        sx = Math.sqrt((t.to_a[0]**2) + (t.to_a[1]**2) + (t.to_a[2]**2))
        {
          position: [Units.in_to_m(o.x), Units.in_to_m(o.y), Units.in_to_m(o.z)],
          rotation: Math.atan2(x.y, x.x),
          tilted: x.z.abs > 1e-6 || (z.z - 1.0).abs > 1e-6,
          scaled: (sx - 1.0).abs > 1e-6
        }
      end
    end

    # Project root group, one container group per model group (tagged), and special tags.
    class Organizer
      DICT = 'SiteDesignPro'
      DELETED_TAG = 'SDP Deleted in source'
      TAG_FOLDER = 'Site Design Pro'

      attr_reader :root

      def initialize(model, package, catalog)
        @model = model
        @package = package
        @catalog = catalog
        @containers = {}
        @root = find_root || create_root
      end

      def container(model_group_id)
        @containers[model_group_id] ||= find_container(model_group_id) || create_container(model_group_id)
      end

      def tag(name)
        @model.layers[name] || begin
          layer = @model.layers.add(name)
          folder = tag_folder
          layer.folder = folder if folder && layer.respond_to?(:folder=)
          layer
        end
      end

      def deleted_tag
        t = tag(DELETED_TAG)
        t.visible = false
        t
      end

      def review_tag
        tag(@catalog.model_group('review_zones').tag)
      end

      private

      def tag_folder
        return nil unless @model.layers.respond_to?(:add_folder)

        @model.layers.folders.find { |f| f.name == TAG_FOLDER } || @model.layers.add_folder(TAG_FOLDER)
      rescue StandardError => e
        Log.warn("Tag folder unavailable: #{e.message}")
        nil
      end

      def find_root
        @model.entities.grep(Sketchup::Group).find do |g|
          g.get_attribute(DICT, 'role') == 'projectRoot' && g.get_attribute(DICT, 'projectId') == @package.project_id
        end
      end

      def create_root
        g = @model.entities.add_group
        g.name = "#{@package.project_name} (Site Design Pro)"
        g.set_attribute(DICT, 'role', 'projectRoot')
        g.set_attribute(DICT, 'projectId', @package.project_id)
        g
      end

      def find_container(id)
        @root.entities.grep(Sketchup::Group).find { |g| g.get_attribute(DICT, 'role') == 'modelGroup' && g.get_attribute(DICT, 'modelGroup') == id }
      end

      def create_container(id)
        mg = @catalog.model_group(id)
        g = @root.entities.add_group
        g.name = mg.name
        g.set_attribute(DICT, 'role', 'modelGroup')
        g.set_attribute(DICT, 'modelGroup', id)
        g.set_attribute(DICT, 'order', mg.order)
        g.layer = tag(mg.tag)
        g
      end
    end
  end
end
