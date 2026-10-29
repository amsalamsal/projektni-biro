# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # HtmlDialog windows. Pages are local files (no network); data is pushed with sdp.render(json) and the
    # page calls back through window.sketchup.<callback>(json).
    module Dialogs
      HTML_DIR = File.join(__dir__, 'html')

      module_function

      def create(title, file, key, width, height)
        dlg = UI::HtmlDialog.new(dialog_title: title, preferences_key: "PB_SiteDesignPro_#{key}", scrollable: true,
                                 resizable: true, width: width, height: height, min_width: 420, min_height: 300,
                                 style: UI::HtmlDialog::STYLE_DIALOG)
        dlg.set_file(File.join(HTML_DIR, file))
        dlg
      end

      def push(dlg, data)
        dlg.execute_script("sdp.render(#{JSON.generate(data)})")
      end

      def parse(arg)
        arg.is_a?(String) ? JSON.parse(arg) : (arg || {})
      end

      # ---- Import preview and report ----

      def import(path)
        importer = Importer.new(Sketchup.active_model, path)
        unless importer.readable?
          UI.messagebox("Paket nije moguće pročitati.\n\n#{importer.read_report.to_text[0, 1500]}")
          return
        end
        importer.plan!
        dlg = create('Vanjsko uređenje PRO – uvoz', 'import.html', 'Import', 920, 700)
        dlg.add_action_callback('ready') { |_ctx| push(dlg, preview_data(importer, path)) }
        dlg.add_action_callback('replan') do |_ctx, arg|
          o = parse(arg)
          importer.plan!(lod: o['lod'], policy: { modified: o['modifiedPolicy'], deleted: o['deletedPolicy'] })
          push(dlg, preview_data(importer, path))
        end
        dlg.add_action_callback('apply') do |_ctx, arg|
          o = parse(arg)
          Settings.update('lod' => o['lod'], 'modifiedPolicy' => o['modifiedPolicy'], 'deletedPolicy' => o['deletedPolicy'],
                          'createScenes' => o['createScenes'] == true, 'backupEnabled' => o['backup'] != false, 'lastPackage' => path)
          Sketchup.write_default(Settings::SECTION, 'lastPackage', path)
          report = importer.apply!(create_scenes: o['createScenes'] == true, backup: o['backup'] != false)
          push(dlg, 'mode' => 'report', 'report' => report.to_h)
        end
        dlg.add_action_callback('saveReport') { |_ctx, arg| save_text(parse(arg)) }
        dlg.add_action_callback('close') { |_ctx| dlg.close }
        dlg.show
        dlg
      rescue PackageError, SystemCallError, IOError => e
        Log.error('Dijalog uvoza nije uspio', e)
        UI.messagebox("Uvoz nije moguće pripremiti:\n#{e.message}")
      end

      def preview_data(importer, path)
        pkg = importer.package
        rep = importer.read_report
        catalog = CatalogFiles.new
        by_group = pkg.elements.group_by(&:model_group).map { |k, v| { 'group' => catalog.model_group(k).name, 'count' => v.length } }
        terrain = (importer.terrain_plan || []).map do |a|
          s = a.element
          { 'kind' => a.kind.to_s, 'id' => s ? s.id : a.entry&.id, 'name' => s ? s.name : 'Teren', 'reason' => a.reason,
            'triangles' => s ? s.triangle_count : 0, 'minZ' => s ? s.min_z : nil, 'maxZ' => s ? s.max_z : nil }
        end
        {
          'mode' => 'preview',
          'package' => {
            'path' => path, 'project' => pkg.project, 'packageId' => pkg.package_id, 'schemaVersion' => pkg.schema_version,
            'created' => pkg.created_utc, 'generator' => pkg.generator, 'units' => pkg.units, 'origin' => pkg.origin,
            'trueNorth' => pkg.true_north_degrees, 'crs' => pkg.crs, 'elementCount' => pkg.elements.length, 'groups' => by_group,
            'elevation' => pkg.elevation
          },
          'terrain' => terrain,
          'changes' => importer.plan.changes? || terrain.any? { |t| %w[create update restore mark_deleted delete].include?(t['kind']) },
          'validation' => { 'errors' => rep.count(:error), 'warnings' => rep.count(:warning), 'reviews' => rep.count(:review),
                            'issues' => rep.issues.map(&:to_h) },
          'plan' => importer.plan.to_h.merge('summary' => importer.plan.summary, 'changes' => importer.plan.changes?),
          'options' => { 'lod' => importer.instance_variable_get(:@lod), 'modifiedPolicy' => importer.plan.policy[:modified],
                         'deletedPolicy' => importer.plan.policy[:deleted], 'createScenes' => Settings.get('createScenes'),
                         'backup' => Settings.get('backupEnabled') != false }
        }
      end

      def show_last_report
        dir = Sketchup.respond_to?(:temp_dir) ? Sketchup.temp_dir : Dir.tmpdir
        file = File.join(dir, 'site_design_pro_last_import.json')
        unless File.file?(file)
          UI.messagebox('Izvještaj uvoza još ne postoji.')
          return
        end
        data = JSON.parse(File.read(file, encoding: 'UTF-8'))
        dlg = create('Vanjsko uređenje PRO – izvještaj posljednjeg uvoza', 'import.html', 'Report', 820, 600)
        dlg.add_action_callback('ready') { |_ctx| push(dlg, 'mode' => 'report', 'report' => data) }
        dlg.add_action_callback('saveReport') { |_ctx, arg| save_text(parse(arg)) }
        dlg.add_action_callback('close') { |_ctx| dlg.close }
        dlg.show
      end

      def save_text(o)
        path = UI.savepanel('Sačuvaj izvještaj', Settings.get('lastFolder').to_s, o['filename'] || 'vanjsko-uredjenje-izvjestaj.txt')
        File.write(path, o['text'].to_s) if path
      rescue SystemCallError, IOError => e
        UI.messagebox("Izvještaj nije sačuvan: #{e.message}")
      end

      # ---- Element properties ----

      def properties(entity)
        model = Sketchup.active_model
        dlg = create('Vanjsko uređenje PRO – svojstva elementa', 'properties.html', 'Properties', 540, 660)
        current = entity
        dlg.add_action_callback('ready') { |_ctx| push(dlg, Editing.describe(model, current)) }
        dlg.add_action_callback('apply') do |_ctx, arg|
          current, report = Editing.apply_parameters(model, current, parse(arg)['edits'] || {})
          data = Editing.describe(model, current).merge('messages' => report.issues.map(&:to_h))
          push(dlg, data)
        end
        dlg.add_action_callback('revert') do |_ctx|
          stored = JSON.parse(current.get_attribute(Editing::DICT, 'params') || '{}')
          local = JSON.parse(current.get_attribute(Editing::DICT, 'localParams') || '{}')
          current, report = Editing.apply_parameters(model, current, local.keys.to_h { |k| [k, stored[k]] })
          push(dlg, Editing.describe(model, current).merge('messages' => report.issues.map(&:to_h)))
        end
        dlg.add_action_callback('selectType') { |_ctx| Commands.select_type(current.get_attribute(Editing::DICT, 'type')) }
        dlg.add_action_callback('close') { |_ctx| dlg.close }
        dlg.show
      end

      # ---- Library and settings ----

      def library
        catalog_dir = CatalogFiles.default_dir
        types = JSON.parse(File.read(File.join(catalog_dir, 'element-types.json'), encoding: 'UTF-8'))['types']
        cats = JSON.parse(File.read(File.join(catalog_dir, 'categories.json'), encoding: 'UTF-8'))['categories']
        mats = JSON.parse(File.read(File.join(catalog_dir, 'materials.json'), encoding: 'UTF-8'))['materials']
        dlg = create('Vanjsko uređenje PRO – biblioteka i postavke', 'library.html', 'Library', 940, 700)
        data = lambda do
          { 'types' => types.map { |t| t.slice('id', 'name', 'nameEn', 'category', 'subcategory', 'builder') },
            'categories' => cats, 'materials' => mats, 'settings' => Settings.all,
            'inModel' => ModelIndex.managed_entities(Sketchup.active_model).group_by { |e| e.get_attribute(Editing::DICT, 'type') }.transform_values(&:length) }
        end
        dlg.add_action_callback('ready') { |_ctx| push(dlg, data.call) }
        dlg.add_action_callback('saveSettings') do |_ctx, arg|
          Settings.update(parse(arg))
          push(dlg, data.call)
        end
        dlg.add_action_callback('mapComponent') do |_ctx, arg|
          type_id = parse(arg)['type']
          path = UI.openpanel("Komponenta za #{type_id}", Settings.get('lastFolder').to_s, 'SketchUp komponente|*.skp||')
          if path
            maps = (Settings.get('componentMappings') || {}).merge(type_id => path)
            Settings.set('componentMappings', maps)
            Settings.set('lastFolder', File.dirname(path))
          end
          push(dlg, data.call)
        end
        dlg.add_action_callback('clearComponent') do |_ctx, arg|
          maps = (Settings.get('componentMappings') || {}).reject { |k, _| k == parse(arg)['type'] }
          Settings.set('componentMappings', maps)
          push(dlg, data.call)
        end
        dlg.add_action_callback('selectType') { |_ctx, arg| Commands.select_type(parse(arg)['type']) }
        dlg.add_action_callback('close') { |_ctx| dlg.close }
        dlg.show
      end
    end
  end
end
