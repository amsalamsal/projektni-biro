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
          raise ArgumentError, "neispravno pravilo #{k}: #{@policy[k].inspect}" unless allowed.include?(@policy[k].to_s)
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
                                      'Kopija upravljanog objekta napravljena u SketchUp-u; zadržava se kao obična geometrija.')
          end
        end

        package.elements.each do |el|
          entry = primary[el.id]
          unless supported.call(el)
            actions << PlanAction.new(:skip, el, entry, "Nema 3D generatora za '#{el.builder}'.")
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
        return PlanAction.new(:create, el, nil, 'Novi element.') unless entry
        return PlanAction.new(:restore, el, entry, 'Element je ponovo u paketu.') if entry.deleted_in_source

        source_changed = entry.content_hash != el.content_hash
        local = entry.locally_modified?
        if !source_changed && !local
          PlanAction.new(:unchanged, el, entry, nil)
        elsif !source_changed
          PlanAction.new(:keep_local, el, entry, 'Izmijenjeno u SketchUp-u; izvor nepromijenjen – izmjene iz SketchUp-a se zadržavaju.')
        elsif !local
          PlanAction.new(:update, el, entry, 'Izmijenjeno u AutoCAD-u.')
        elsif @policy[:modified] == 'overwrite'
          PlanAction.new(:update, el, entry, 'Izmijenjeno u AutoCAD-u; izmjene iz SketchUp-a se prepisuju (pravilo: prepiši).')
        else
          PlanAction.new(:conflict, el, entry, 'Izmijenjeno u AutoCAD-u i u SketchUp-u; ne ažurira se (pravilo: zadrži).')
        end
      end

      def plan_missing(entry)
        return PlanAction.new(:unchanged, nil, entry, 'Već označeno kao obrisano u izvoru.') if entry.deleted_in_source && @policy[:deleted] == 'mark'

        case @policy[:deleted]
        when 'delete' then PlanAction.new(:delete, nil, entry, 'Uklonjeno iz AutoCAD projekta.')
        when 'keep' then PlanAction.new(:keep_orphan, nil, entry, 'Uklonjeno iz AutoCAD projekta; zadržava se (pravilo: zadrži).')
        else PlanAction.new(:mark_deleted, nil, entry, 'Uklonjeno iz AutoCAD projekta; premješteno na oznaku "VU Obrisano u izvoru".')
        end
      end
    end

    # State of a terrain surface found in the model (group with attribute terrainId).
    TerrainEntry = Struct.new(:id, :project_id, :content_hash, :stored_fingerprint, :current_fingerprint, :deleted_in_source, :ref,
                              keyword_init: true) do
      def locally_modified?
        stored_fingerprint && current_fingerprint && stored_fingerprint != current_fingerprint
      end
    end

    # Plans terrain surfaces with the same policies as elements. Actions carry the TerrainSurface in .element.
    module TerrainPlanner
      module_function

      def plan(surfaces, entries, project_id, policy)
        by_id = entries.select { |e| e.project_id == project_id }.group_by(&:id).transform_values(&:first)
        actions = surfaces.map do |s|
          e = by_id[s.id]
          if e.nil?
            PlanAction.new(:create, s, nil, 'Novi teren.')
          elsif e.deleted_in_source
            PlanAction.new(:restore, s, e, 'Teren je ponovo u paketu.')
          elsif e.content_hash == s.content_hash
            PlanAction.new(e.locally_modified? ? :keep_local : :unchanged, s, e, e.locally_modified? ? 'Teren izmijenjen u SketchUp-u; izvor nepromijenjen.' : nil)
          elsif !e.locally_modified? || policy[:modified].to_s == 'overwrite'
            PlanAction.new(:update, s, e, 'Teren izmijenjen u AutoCAD-u.')
          else
            PlanAction.new(:conflict, s, e, 'Teren izmijenjen u AutoCAD-u i u SketchUp-u; ne ažurira se (pravilo: zadrži).')
          end
        end
        ids = surfaces.map(&:id)
        by_id.each_value do |e|
          next if ids.include?(e.id)

          actions << case policy[:deleted].to_s
                     when 'delete' then PlanAction.new(:delete, nil, e, 'Teren uklonjen iz AutoCAD projekta.')
                     when 'keep' then PlanAction.new(:keep_orphan, nil, e, 'Teren uklonjen iz AutoCAD projekta; zadržava se.')
                     else e.deleted_in_source ? PlanAction.new(:unchanged, nil, e, nil) : PlanAction.new(:mark_deleted, nil, e, 'Teren uklonjen iz AutoCAD projekta; označen.')
                     end
        end
        actions
      end
    end

    # Result of planning: actions plus summary helpers for previews and reports.
    class Plan
      ORDER = %i[create update restore conflict mark_deleted delete keep_orphan keep_local detach_duplicate skip unchanged].freeze
      LABELS = {
        create: 'Novo', update: 'Izmijenjeno', restore: 'Vraćeno', conflict: 'Konflikti', mark_deleted: 'Obrisano (označeno)',
        delete: 'Obrisano', keep_orphan: 'Obrisano (zadržano)', keep_local: 'Zadržane izmjene iz SketchUp-a', detach_duplicate: 'Odvojene kopije',
        skip: 'Ne može se rekonstruisati', unchanged: 'Nepromijenjeno'
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
              'name' => el ? el.base_name : a.entry&.type_id, 'type' => el ? el.type_id : a.entry&.type_id, 'reason' => a.reason }
          end
        }
      end
    end
  end
end
