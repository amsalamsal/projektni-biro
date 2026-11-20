# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Roads
      # Builds the roads of the model from the source elements: request -> road engine -> package -> import
      # (only what changed is rebuilt: the generated elements keep their ids), then the 2D overlays (texts of the
      # garage ramps, longitudinal sections).
      module Rebuild
        OVERLAY_ROLE = 'roadsOverlay'
        OVERLAY_NAME = 'Saobraćajnice – tekstovi i presjeci (Vanjsko uređenje PRO)'
        OVERLAY_TAG = 'VU Saobraćajnice – tekstovi i presjeci'
        POLICY = { modified: 'overwrite', deleted: 'delete' }.freeze

        Outcome = Compat.kw_struct(:result, :report, :notes, :warnings, :summary, :seconds)

        @busy = false

        class << self
          attr_accessor :busy
        end

        module_function

        def plugin_dir
          File.expand_path('../..', __dir__)
        end

        def work_dir(project_id)
          base = Sketchup.respond_to?(:temp_dir) ? Sketchup.temp_dir : Dir.tmpdir
          File.join(base, 'sdp-ceste', project_id.to_s)
        end

        def engine(project_id, command = Engine.default_command(plugin_dir))
          Engine.new(command, work_dir(project_id))
        end

        # Rebuilds the roads. operation: :own (one undoable step), :none (the caller has started one; the roads
        # are part of it) or :transparent (merged with the previous step: after a move with the SketchUp tools).
        def run(model, operation: :own, engine: nil)
          return nil if Rebuild.busy

          run!(model, operation, engine)
        end

        def run!(model, operation, engine)
          Rebuild.busy = true
          started = Time.now
          store = SourceStore.new(model)
          settings = store.settings
          records = store.records
          request = Request.build(records, project_id: settings['projectId'], project_name: settings['projectName'],
                                           base: settings['base'], flat: settings['flat'])
          eng = engine || self.engine(settings['projectId'])
          result, folder = eng.build(request)
          Sketchup.status_text = 'Vanjsko uređenje PRO: uvoz saobraćajnica...'
          begin_operation(model, operation)
          begin
            importer = Importer.new(model, folder)
            raise Engine::Error, "paket saobraćajnica nije čitljiv: #{importer.read_report.to_text[0, 400]}" unless importer.readable?

            importer.plan!(policy: POLICY)
            report = importer.apply!(create_scenes: false, backup: false, operation: false, set_north: false)
            name_root(model, settings)
            draw_overlays(model, result)
            store.built_signature = store.signature
            model.set_attribute(SourceStore::SETTINGS, 'violations', JSON.generate(violation_keys(result)))
            model.commit_operation unless operation == :none
          rescue StandardError
            model.abort_operation unless operation == :none
            raise
          end
          Outcome.new(result: result, report: report, notes: result['notes'] || [], warnings: result['warnings'] || [],
                      summary: result['summary'].to_s, seconds: (Time.now - started).round(1))
        ensure
          Rebuild.busy = false
          Sketchup.status_text = '' if defined?(Sketchup) && Sketchup.respond_to?(:status_text=)
        end

        def begin_operation(model, operation)
          case operation
          when :own then model.start_operation('Saobraćajnice: izgradnja', true)
          when :transparent then model.start_operation('Saobraćajnice: osvježavanje', true, false, true)
          end
        end

        # ---- technical minimums (the engine reports a ramp that breaks one: it cannot be designed)

        # "id|kind" of every broken minimum of a build result (kind: the text up to the first number).
        def violation_keys(result)
          (result['violations'] || []).flat_map do |v|
            (v['rules'] || []).map { |r| "#{v['id']}|#{r.to_s.split(/\d/, 2).first}" }
          end
        end

        # The broken minimums the last build added (texts "GR-001: …"): the change that caused them is refused.
        def new_violations(before, outcome)
          return [] unless outcome

          (outcome.result['violations'] || []).flat_map do |v|
            (v['rules'] || []).reject { |r| before.include?("#{v['id']}|#{r.to_s.split(/\d/, 2).first}") }.map { |r| "#{v['name']}: #{r}" }
          end
        end

        def violations_before(model)
          JSON.parse(model.get_attribute(SourceStore::SETTINGS, 'violations', '[]') || '[]')
        rescue JSON::ParserError
          []
        end

        def name_root(model, settings)
          root = model.entities.grep(Sketchup::Group).find do |g|
            g.get_attribute('SiteDesignPro', 'role') == 'projectRoot' && g.get_attribute('SiteDesignPro', 'projectId') == settings['projectId']
          end
          return unless root

          root.name = "#{settings['projectName']} – 3D (Vanjsko uređenje PRO)"
          root.set_attribute('SiteDesignPro', 'roadsProject', true)
        end

        # ---- 2D overlays: ramp texts L / h / slope and the longitudinal sections, as in the AutoCAD drawing

        def overlay(model, create = true)
          g = model.entities.grep(Sketchup::Group).find { |x| x.get_attribute(SourceStore::DICT, 'role') == OVERLAY_ROLE }
          return g if g || !create

          g = model.entities.add_group
          g.name = OVERLAY_NAME
          g.set_attribute(SourceStore::DICT, 'role', OVERLAY_ROLE)
          layer = model.layers[OVERLAY_TAG] || model.layers.add(OVERLAY_TAG)
          g.layer = layer if layer
          g
        end

        def draw_overlays(model, result)
          ramps = result['ramps'] || []
          sections = result['sections'] || []
          existing = overlay(model, false)
          if ramps.empty? && sections.empty?
            existing.erase! if existing && existing.valid?
            return
          end

          g = existing || overlay(model)
          g.entities.clear!
          ramps.each do |r|
            (r['texts'] || []).each { |t| text(g.entities, t) }
          end
          sections.each do |sec|
            sg = g.entities.add_group
            sg.name = sec['name'].to_s
            (sec['shapes'] || []).each { |shape| shape_2d(sg.entities, shape) }
          end
        end

        def pt(x, y, z = 0.0)
          Geom::Point3d.new(Units.m_to_in(x.to_f), Units.m_to_in(y.to_f), Units.m_to_in(z.to_f))
        end

        def shape_2d(entities, shape)
          return text(entities, shape) if shape['text']

          pts = (shape['points'] || []).map { |p| [p[0], p[1], 0.0] }
          return if pts.length < 2

          line = Geometry.tessellate(pts, shape['bulges'], shape['closed'] == true, 0.02)
          line += [line.first] if shape['closed'] == true
          entities.add_edges(line.map { |p| pt(p[0], p[1]) })
        end

        # Text lying in the plan (3D text, flat), fitted to its length like the AutoCAD text.
        def text(entities, shape)
          return unless shape['text'] && shape['position']

          h = [shape['height'].to_f, 0.1].max
          tg = entities.add_group
          if tg.entities.respond_to?(:add_3d_text)
            tg.entities.add_3d_text(shape['text'].to_s, TextAlignCenter, 'Arial', false, false, Units.m_to_in(h), 0.0, 0.0, true, 0.0)
          end
          pos = shape['position']
          rot = shape['rotation'].to_f
          tr = Geom::Transformation.translation(Geom::Vector3d.new(Units.m_to_in(pos[0].to_f), Units.m_to_in(pos[1].to_f), Units.m_to_in(0.01))) *
               Geom::Transformation.rotation(Geom::Point3d.new(0, 0, 0), Geom::Vector3d.new(0, 0, 1), rot) *
               Geom::Transformation.translation(Geom::Vector3d.new(0, -Units.m_to_in(h / 2), 0))
          tg.transformation = tr
          tg.name = shape['text'].to_s
          tg
        end

        # Text of the outcome for the user.
        def message(outcome)
          lines = []
          lines << outcome.summary unless outcome.summary.to_s.empty?
          stats = outcome.report ? outcome.report.stats : {}
          lines << "3D: novo #{stats['created'].to_i}, ažurirano #{stats['updated'].to_i}, uklonjeno #{stats['deleted'].to_i} (#{Roads.num(outcome.seconds, 1)} s)."
          unless outcome.warnings.empty?
            lines << ''
            lines << 'UPOZORENJA:'
            lines.concat(outcome.warnings.first(12).map { |w| "• #{w}" })
          end
          unless outcome.notes.empty?
            lines << ''
            lines << 'Napomene:'
            lines.concat(outcome.notes.first(12).map { |n| "• #{n}" })
          end
          errors = outcome.report ? outcome.report.issues.select { |i| i.severity == :error } : []
          unless errors.empty?
            lines << ''
            lines.concat(errors.first(5).map { |i| "GREŠKA: #{i.message}" })
          end
          lines.join("\n")
        end
      end
    end
  end
end
