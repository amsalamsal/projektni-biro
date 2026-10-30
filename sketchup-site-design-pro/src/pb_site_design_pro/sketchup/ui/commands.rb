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
        path = UI.openpanel('Uvoz paketa Vanjsko uređenje PRO', dir, 'Paket Vanjsko uređenje PRO|*.sdpkg;manifest.json||')
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
          UI.messagebox(roots.empty? ? 'U ovaj model još nije uvezen nijedan projekat Vanjsko uređenje PRO.' : 'Posljednji paket nije pronađen. Koristite „Uvoz paketa" i odaberite ažuriranu datoteku.')
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
          UI.messagebox('Odaberite jedan element Vanjsko uređenje PRO (grupu ili komponentu nastalu uvozom).')
        end
      end

      def select_type(type_id)
        ents = ModelIndex.managed_entities(model).select { |e| e.get_attribute(DICT, 'type') == type_id }
        model.selection.clear
        model.selection.add(ents) unless ents.empty?
        Sketchup.status_text = "Vanjsko uređenje PRO: odabrano #{ents.length} elemenata tipa #{type_id}"
      end

      def export_changes
        projects = Editing.projects(model)
        if projects.empty?
          UI.messagebox('U ovom modelu nema projekta Vanjsko uređenje PRO.')
          return
        end
        project = projects.first
        if projects.length > 1
          names = projects.map { |p| p['name'] }
          input = UI.inputbox(['Projekat'], [names.first], [names.join('|')], 'Izvoz izmjena')
          return unless input

          project = projects[names.index(input.first)]
        end
        cs = Editing.collect_changes(model, project['id'])
        if cs.empty?
          UI.messagebox("Nisu pronađene izmjene koje se mogu prenijeti.\n\n#{cs.report.issues.map(&:message).uniq.first(5).join("\n")}")
          return
        end
        path = UI.savepanel('Izvoz izmjena za AutoCAD', Settings.get('lastFolder').to_s, 'sdp-izmjene.json')
        return unless path

        File.write(path, cs.to_json)
        notes = cs.report.issues.empty? ? '' : "\n\nNapomene:\n#{cs.report.issues.map(&:message).uniq.first(8).join("\n")}"
        UI.messagebox("Zapisano izmjena: #{cs.changes.length}\n#{path}\n\nPrimijenite ih u AutoCAD-u naredbom SDPAPPLYCHANGES.#{notes}")
      rescue PackageError, SystemCallError, IOError => e
        Log.error('Izvoz izmjena nije uspio', e)
        UI.messagebox("Izmjene nije moguće izvesti: #{e.message}")
      end

      def create_scenes
        catalog = CatalogFiles.new
        roots = model.entities.grep(Sketchup::Group).select { |g| g.get_attribute(DICT, 'role') == 'projectRoot' }
        if roots.empty?
          UI.messagebox('Najprije uvezite paket Vanjsko uređenje PRO.')
          return
        end
        organizer = Struct.new(:root).new(roots.first)
        model.start_operation('Vanjsko uređenje PRO: scene', true)
        Scenes.create(model, organizer, catalog)
        model.commit_operation
      rescue StandardError => e
        model.abort_operation
        Log.error('Izrada scena nije uspjela', e)
        UI.messagebox("Scene nije moguće izraditi: #{e.message}")
      end

      # Toggles an underground review state: hides surface tags and enables X-ray.
      def toggle_underground
        catalog = CatalogFiles.new
        tags = Scenes::SURFACE_GROUPS.map { |id| model.layers[catalog.model_group(id).tag] }.compact
        return UI.messagebox('U modelu nema oznaka (tagova) Vanjsko uređenje PRO.') if tags.empty?

        underground_on = tags.any?(&:visible?)
        model.start_operation('Vanjsko uređenje PRO: prikaz podzemnog', true)
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
        UI.messagebox("Vanjsko uređenje PRO za SketchUp #{VERSION}\nShema razmjene #{SCHEMA_VERSION}\nPodržano: SketchUp 2022 i novije verzije\n\n" \
                      "Uvozi pakete iz AutoCAD-a u organizovane, izmjenjive 3D modele.\nDnevnik: #{Log.path}")
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
          Log.error("Naredba '#{text}' nije uspjela", e)
          UI.messagebox("#{text}: greška – #{e.message}\nDetalji su u dnevniku (log).")
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
          import: command('Uvoz paketa...', 'Uvoz paketa iz AutoCAD-a (.sdpkg ili manifest.json) s pregledom prije primjene.', 'import') { import_package },
          update: command('Ažuriranje iz paketa', 'Ponovni uvoz posljednjeg paketa: prikazuje nove, izmijenjene, obrisane i konfliktne elemente prije primjene.', 'update') { update_from_last },
          properties: command('Svojstva elementa', 'Prikaz i izmjena parametara odabranog elementa.', 'properties') { element_properties },
          export: command('Izvoz izmjena za AutoCAD...', 'Zapisuje izmijenjene parametre i pomjerene/zarotirane tačkaste elemente za naredbu SDPAPPLYCHANGES.', 'export') { export_changes },
          scenes: command('Izrada scena za pregled', 'Izrada ili osvježavanje scena (situacija, 3D pregled, zelenilo, rasvjeta, podzemno ...).', 'scenes') { create_scenes },
          underground: command('Prikaz podzemnog', 'Sakriva površine i prikazuje ukopane instalacije u X-ray prikazu.', 'underground') { toggle_underground },
          models: command('Biblioteka 3D modela...', 'Pregled, umetanje i zamjena 3D modela iz lokalne biblioteke (14 kategorija, bez interneta).', 'models') { Dialogs.models },
          library: command('Biblioteka i postavke...', 'Tipovi elemenata, dodjela komponenti, materijali, nivoi detalja i pravila ažuriranja.', 'library') { library_dialog },
          report: command('Izvještaj posljednjeg uvoza', 'Prikaz izvještaja posljednjeg uvoza.', 'report') { Dialogs.show_last_report },
          recovery: command('Sigurnosne kopije i oporavak...', 'Prekinute operacije i sigurnosne kopije trenutnog modela.', nil) { recovery }
        }
        cmds[:properties].set_validation_proc { selected_element ? MF_ENABLED : MF_GRAYED }

        menu = UI.menu('Extensions').add_submenu('Vanjsko uređenje PRO')
        %i[import update].each { |k| menu.add_item(cmds[k]) }
        menu.add_separator
        %i[properties export].each { |k| menu.add_item(cmds[k]) }
        menu.add_separator
        %i[scenes underground].each { |k| menu.add_item(cmds[k]) }
        menu.add_separator
        %i[models library report recovery].each { |k| menu.add_item(cmds[k]) }
        menu.add_item('Otvori dnevnik (log)') { open_log }
        menu.add_item('O programu Vanjsko uređenje PRO') { about }

        tb = UI::Toolbar.new('Vanjsko uređenje PRO')
        %i[import update properties export scenes underground models library report].each { |k| tb.add_item(cmds[k]) }
        tb.restore

        UI.add_context_menu_handler do |context_menu|
          entity = selected_element
          next unless entity

          sub = context_menu.add_submenu('Vanjsko uređenje PRO')
          sub.add_item(cmds[:properties])
          sub.add_item("Odaberi sve tipa '#{entity.get_attribute(DICT, 'type')}'") { select_type(entity.get_attribute(DICT, 'type')) }
        end
        UI.start_timer(2, false) { check_interrupted }
        cmds
      end

      # Reports operations that started but never finished (e.g. SketchUp closed during an import).
      def check_interrupted
        journal = Safety.journal
        open = journal ? journal.unfinished : []
        return if open.empty?

        text = open.last(5).map { |e| RecoveryJournal.describe(e) }.join("\n")
        msg = "Pronađene su prekinute operacije Vanjsko uređenje PRO:\n\n#{text}\n\n" \
              'SketchUp poništava nedovršene operacije; ako model nije ispravan, otvorite sigurnosnu kopiju navedenu iznad. ' \
              'Označiti ove operacije kao pregledane?'
        return unless UI.messagebox(msg, MB_YESNO) == IDYES

        open.each { |e| journal.acknowledge(e['OperationId'], 'Korisnik je pregledao nakon ponovnog pokretanja.') }
      rescue StandardError => e
        Log.warn("Provjera prekinutih operacija nije uspjela: #{e.message}")
      end

      # Lists unfinished operations and the backups of the current model; opens the backup folder.
      def recovery
        journal = Safety.journal
        open = journal ? journal.unfinished : []
        path = model.path.to_s
        lines = []
        lines << (open.empty? ? 'Nema prekinutih operacija.' : "Prekinute operacije:\n#{open.map { |e| RecoveryJournal.describe(e) }.join("\n")}")
        if path.empty?
          lines << "\nModel još nije sačuvan, pa nema sigurnosnih kopija."
        else
          backups = Safety.backup_manager.list(path)
          lines << (backups.empty? ? "\nNema sigurnosnih kopija ovog modela." : "\nSigurnosne kopije (najnovija prva):\n#{backups.first(10).map { |b| "#{b[:time].strftime('%d.%m.%Y. %H:%M:%S')}  #{File.basename(b[:path])}" }.join("\n")}")
        end
        lines << "\nKopija se otvara kao zaseban model (Datoteka > Otvori); trenutni model se nikad ne prepisuje."
        answer = UI.messagebox("#{lines.join("\n")}\n\nOtvoriti folder sa sigurnosnim kopijama?", MB_YESNO)
        UI.openURL("file:///#{Safety.backup_manager.folder_for(path).tr('\\', '/')}") if answer == IDYES && !path.empty?
      end

      def library_dialog
        Dialogs.library
      end
    end
  end
end
