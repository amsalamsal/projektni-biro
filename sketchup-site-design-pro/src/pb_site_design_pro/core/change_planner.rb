# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # State of one managed element found in the model (supplied by the SketchUp adapter).
    #   content_hash         - package content hash stored at the last import
    #   stored_fingerprint   - fingerprint stored at the last import
    #   current_fingerprint  - fingerprint computed now
    #   deleted_in_source    - already marked as deleted by an earlier import
    #   local_params         - parameters edited in SketchUp (Site Design Pro edit dialog), or nil
    #   ref                  - opaque reference to the entity (adapter use only)
    ModelEntry = Struct.new(:id, :project_id, :type_id, :content_hash, :stored_fingerprint, :current_fingerprint,
                            :deleted_in_source, :local_params, :ref, keyword_init: true) do
      def locally_modified?
        (stored_fingerprint && current_fingerprint && stored_fingerprint != current_fingerprint) ||
          (local_params.is_a?(Hash) && !local_params.empty?)
      end
    end

    # One planned action. kind is one of:
    #   :create, :update, :unchanged, :keep_local, :conflict, :mark_deleted, :delete, :keep_orphan,
    #   :restore (element reappeared after being marked deleted), :detach_duplicate, :skip
    PlanAction = Struct.new(:kind, :element, :entry, :reason)

    # Compares a package with the model and plans the import. Pure: never touches the model.
    class ChangePlanner
      POLICIES = {
        modified: %w[keep overwrite],   # elements edited in SketchUp whose source also changed
        deleted: %w[mark delete keep]   # managed elements missing from the package
      }.freeze

      DEFAULT_POLICY = { modified: 'keep', deleted: 'mark' }.freeze

      def initialize(policy = {})
        @policy = DEFAULT_POLICY.merge(policy.transform_keys(&:to_sym))
        POLICIES.each do |k, allowed|
          raise ArgumentError, "invalid #{k} policy #{@policy[k].inspect}" unless allowed.include?(@policy[k].to_s)
        end
      end

      attr_reader :policy

      # entries: array of ModelEntry (several entries may share an id when objects were copied in SketchUp).
      # supported: proc(element) -> true when a 3D builder exists.
      def plan(package, entries, supported = ->(_e) { true })
        actions = []
        by_id = entries.group_by(&:id)
        primary = {}
        by_id.each do |id, list|
          keep, *copies = list.sort_by { |en| en.stored_fingerprint == en.current_fingerprint ? 0 : 1 }
          primary[id] = keep
          copies.each do |copy|
            actions << PlanAction.new(:detach_duplicate, package.element(id), copy,
                                      'Copy of a managed object made in SketchUp; it will be kept as unmanaged geometry.')
          end
        end

        package.elements.each do |el|
          entry = primary[el.id]
          unless supported.call(el)
            actions << PlanAction.new(:skip, el, entry, "No 3D builder for '#{el.builder}'.")
            next
          end
          actions << plan_element(el, entry)
        end

        primary.each_value do |entry|
          next if package.element(entry.id)
          next unless entry.project_id == package.project_id

          actions << plan_missing(entry)
        end
        Plan.new(actions, @policy)
      end

      private

      def plan_element(el, entry)
        return PlanAction.new(:create, el, nil, 'New element.') unless entry
        return PlanAction.new(:restore, el, entry, 'Element is present in the package again.') if entry.deleted_in_source

        source_changed = entry.content_hash != el.content_hash
        local = entry.locally_modified?
        if !source_changed && !local
          PlanAction.new(:unchanged, el, entry, nil)
        elsif !source_changed
          PlanAction.new(:keep_local, el, entry, 'Modified in SketchUp; source unchanged - SketchUp edits preserved.')
        elsif !local
          PlanAction.new(:update, el, entry, 'Changed in AutoCAD.')
        elsif @policy[:modified] == 'overwrite'
          PlanAction.new(:update, el, entry, 'Changed in AutoCAD; SketchUp edits overwritten (policy: overwrite).')
        else
          PlanAction.new(:conflict, el, entry, 'Changed in AutoCAD and modified in SketchUp; not updated (policy: keep).')
        end
      end

      def plan_missing(entry)
        return PlanAction.new(:unchanged, nil, entry, 'Already marked as deleted in source.') if entry.deleted_in_source && @policy[:deleted] == 'mark'

        case @policy[:deleted]
        when 'delete' then PlanAction.new(:delete, nil, entry, 'Removed from the AutoCAD project.')
        when 'keep' then PlanAction.new(:keep_orphan, nil, entry, 'Removed from the AutoCAD project; kept (policy: keep).')
        else PlanAction.new(:mark_deleted, nil, entry, 'Removed from the AutoCAD project; moved to the "SDP Deleted in source" tag.')
        end
      end
    end

    # Result of planning: actions plus summary helpers for previews and reports.
    class Plan
      ORDER = %i[create update restore conflict mark_deleted delete keep_orphan keep_local detach_duplicate skip unchanged].freeze
      LABELS = {
        create: 'New', update: 'Changed', restore: 'Restored', conflict: 'Conflicts', mark_deleted: 'Deleted (marked)',
        delete: 'Deleted', keep_orphan: 'Deleted (kept)', keep_local: 'SketchUp edits kept', detach_duplicate: 'Copies detached',
        skip: 'Not reconstructable', unchanged: 'Unchanged'
      }.freeze

      attr_reader :actions, :policy

      def initialize(actions, policy)
        @actions = actions
        @policy = policy
      end

      def of(kind)
        @actions.select { |a| a.kind == kind }
      end

      def counts
        ORDER.each_with_object({}) { |k, acc| acc[k] = of(k).length }
      end

      # True when applying the plan would change the model.
      def changes?
        @actions.any? { |a| %i[create update restore mark_deleted delete detach_duplicate].include?(a.kind) }
      end

      def summary
        counts.reject { |_k, v| v.zero? }.map { |k, v| "#{LABELS[k]}: #{v}" }.join(', ')
      end

      def to_h
        {
          'policy' => @policy.transform_keys(&:to_s),
          'counts' => counts.transform_keys(&:to_s),
          'actions' => @actions.reject { |a| a.kind == :unchanged }.map do |a|
            el = a.element
            { 'kind' => a.kind.to_s, 'id' => el ? el.id : a.entry&.id, 'label' => el&.label,
              'name' => el ? el.display_name : a.entry&.type_id, 'type' => el ? el.type_id : a.entry&.type_id, 'reason' => a.reason }
          end
        }
      end
    end
  end
end
