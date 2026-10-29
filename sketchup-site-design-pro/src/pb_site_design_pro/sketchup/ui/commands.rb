# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Menu, toolbar and context-menu commands.
    module Commands
      DICT = 'SiteDesignPro'
      ICONS = File.expand_path('../../icons', __dir__)

      module_function

      def model
        Sketchup.active_model
      end

      def import_package
        dir = Settings.get('lastFolder').to_s
        path = UI.openpanel('Import Site Design Pro package', dir, 'Site Design Pro package|*.sdpkg;manifest.json||')
        return unless path

        Settings.set('lastFolder', File.dirname(path))
        Dialogs.import(path)
      end

      def update_from_last
        path = Sketchup.read_default(Settings::SECTION, 'lastPackage', nil)
        roots = Editing.projects(model)
        if path.nil? || !File.exist?(path)
          root = model.entities.grep(Sketchup::Group).find { |g| g.get_attribute(DICT, 'role') == 'projectRoot' }
          path = root&.get_attribute(DICT, 'packagePath')
        end
        if path && File.exist?(path)
          Dialogs.import(path)
        else
          UI.messagebox(roots.empty? ? 'No Site Design Pro project has been imported into this model yet.' : 'The last package file was not found. Use "Import package" and choose the updated file.')
        end
      end

      def selected_element
        sel = model.selection.to_a.select { |e| Editing.managed?(e) }
        sel.length == 1 ? sel.first : nil
      end

      def element_properties
        entity = selected_element
        if entity
          Dialogs.properties(entity)
        else
          UI.messagebox('Select one Site Design Pro element (a group or component created by the import).')
        end
      end

      def select_type(type_id)
        ents = ModelIndex.managed_entities(model).select { |e| e.get_attribute(DICT, 'type') == type_id }
        model.selection.clear
        model.selection.add(ents) unless ents.empty?
        Sketchup.status_text = "Site Design Pro: #{ents.length} elements of type #{type_id} selected"
      end

      def export_changes
        projects = Editing.projects(model)
        if projects.empty?
          UI.messagebox('No Site Design Pro project in this model.')
          return
        end
        project = projects.first
        if projects.length > 1
          names = projects.map { |p| p['name'] }
          input = UI.inputbox(['Project'], [names.first], [names.join('|')], 'Export changes')
          return unless input

          project = projects[names.index(input.first)]
        end
        cs = Editing.collect_changes(model, project['id'])
        if cs.empty?
          UI.messagebox("No transferable changes were found.\n\n#{cs.report.issues.map(&:message).uniq.first(5).join("\n")}")
          return
        end
        path = UI.savepanel('Export changes for AutoCAD', Settings.get('lastFolder').to_s, 'sdp-changes.json')
        return unless path

        File.write(path, cs.to_json)
        notes = cs.report.issues.empty? ? '' : "\n\nNotes:\n#{cs.report.issues.map(&:message).uniq.first(8).join("\n")}"
        UI.messagebox("#{cs.changes.length} changes written to\n#{path}\n\nApply them in AutoCAD with SDPAPPLYCHANGES.#{notes}")
      rescue PackageError, SystemCallError, IOError => e
        Log.error('Export changes failed', e)
        UI.messagebox("Changes could not be exported: #{e.message}")
      end

      def create_scenes
        catalog = CatalogFiles.new
        roots = model.entities.grep(Sketchup::Group).select { |g| g.get_attribute(DICT, 'role') == 'projectRoot' }
        if roots.empty?
          UI.messagebox('Import a Site Design Pro package first.')
          return
        end
        organizer = Struct.new(:root).new(roots.first)
        model.start_operation('Site Design Pro scenes', true)
        Scenes.create(model, organizer, catalog)
        model.commit_operation
      rescue StandardError => e
        model.abort_operation
        Log.error('Scene creation failed', e)
        UI.messagebox("Scenes could not be created: #{e.message}")
      end

      # Toggles an underground review state: hides surface tags and enables X-ray.
      def toggle_underground
        catalog = CatalogFiles.new
        tags = Scenes::SURFACE_GROUPS.map { |id| model.layers[catalog.model_group(id).tag] }.compact
        return UI.messagebox('No Site Design Pro tags found in this model.') if tags.empty?

        underground_on = tags.any?(&:visible?)
        model.start_operation('Site Design Pro underground view', true)
        tags.each { |t| t.visible = !underground_on }
        model.rendering_options['ModelTransparency'] = underground_on
        ug = model.layers[catalog.model_group('underground').tag]
        ug.visible = true if ug
        model.commit_operation
      end

      def open_log
        UI.openURL("file:///#{Log.path.tr('\\', '/')}")
      end

      def about
        UI.messagebox("Site Design Pro for SketchUp #{VERSION}\nExchange schema #{SCHEMA_VERSION}\n\n" \
                      "Imports AutoCAD Site Design Pro packages into organised, editable 3D models.\nLog: #{Log.path}")
      end

      def icon(name, size)
        svg = File.join(ICONS, "#{name}.svg")
        png = File.join(ICONS, "#{name}_#{size}.png")
        Sketchup.platform == :platform_win && File.file?(svg) ? svg : png
      end

      def command(text, tip, icon_name, &block)
        cmd = UI::Command.new(text) do
          block.call
        rescue StandardError => e
          Log.error("Command '#{text}' failed", e)
          UI.messagebox("#{text} failed: #{e.message}\nSee the log for details.")
        end
        cmd.tooltip = text
        cmd.status_bar_text = tip
        if icon_name
          cmd.small_icon = icon(icon_name, 16)
          cmd.large_icon = icon(icon_name, 24)
        end
        cmd
      end

      def install
        cmds = {
          import: command('Import package...', 'Import an AutoCAD Site Design Pro package (.sdpkg or manifest.json) with preview.', 'import') { import_package },
          update: command('Update from package', 'Re-import the last package: shows new, changed, deleted and conflicting elements before applying.', 'update') { update_from_last },
          properties: command('Element properties', 'Show and edit the parameters of the selected Site Design Pro element.', 'properties') { element_properties },
          export: command('Export changes to AutoCAD...', 'Write parameter edits and moved/rotated point elements to a change file for SDPAPPLYCHANGES.', 'export') { export_changes },
          scenes: command('Create review scenes', 'Create or refresh the Site Design Pro scenes (plan, overview, planting, lighting, underground ...).', 'scenes') { create_scenes },
          underground: command('Toggle underground view', 'Hide surfaces and show buried utilities in X-ray.', 'underground') { toggle_underground },
          library: command('Library and settings...', 'Element types, component mappings, materials, levels of detail and update policies.', 'library') { library_dialog },
          report: command('Last import report', 'Show the report of the last import.', 'report') { Dialogs.show_last_report }
        }
        cmds[:properties].set_validation_proc { selected_element ? MF_ENABLED : MF_GRAYED }

        menu = UI.menu('Extensions').add_submenu('Site Design Pro')
        %i[import update].each { |k| menu.add_item(cmds[k]) }
        menu.add_separator
        %i[properties export].each { |k| menu.add_item(cmds[k]) }
        menu.add_separator
        %i[scenes underground].each { |k| menu.add_item(cmds[k]) }
        menu.add_separator
        %i[library report].each { |k| menu.add_item(cmds[k]) }
        menu.add_item('Open log') { open_log }
        menu.add_item('About Site Design Pro') { about }

        tb = UI::Toolbar.new('Site Design Pro')
        %i[import update properties export scenes underground library report].each { |k| tb.add_item(cmds[k]) }
        tb.restore

        UI.add_context_menu_handler do |context_menu|
          entity = selected_element
          next unless entity

          sub = context_menu.add_submenu('Site Design Pro')
          sub.add_item(cmds[:properties])
          sub.add_item("Select all '#{entity.get_attribute(DICT, 'type')}'") { select_type(entity.get_attribute(DICT, 'type')) }
        end
        cmds
      end

      def library_dialog
        Dialogs.library
      end
    end
  end
end
