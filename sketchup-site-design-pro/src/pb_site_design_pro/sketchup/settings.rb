# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Persistent user preferences (SketchUp defaults storage).
    module Settings
      SECTION = 'PB_SiteDesignPro'
      DEFAULTS = {
        'lod' => 'standard',
        'modifiedPolicy' => 'keep',
        'deletedPolicy' => 'mark',
        'createScenes' => true,
        'setNorth' => true,
        'componentMappings' => {},
        'favorites' => [],
        'lastFolder' => '',
        'backupEnabled' => true,
        'backupKeep' => 10,
        'backupMaxAgeDays' => 30
      }.freeze

      module_function

      def get(key)
        raw = Sketchup.read_default(SECTION, key, nil)
        return DEFAULTS[key] if raw.nil?

        JSON.parse(raw)['v']
      rescue JSON::ParserError
        DEFAULTS[key]
      end

      def set(key, value)
        Sketchup.write_default(SECTION, key, JSON.generate('v' => value))
      end

      def all
        DEFAULTS.keys.each_with_object({}) { |k, acc| acc[k] = get(k) }
      end

      def update(hash)
        hash.each do |k, v|
          next unless DEFAULTS.key?(k)

          set(k, v)
        end
      end

      def policy
        { modified: get('modifiedPolicy'), deleted: get('deletedPolicy') }
      end

      # Path of a custom SketchUp component mapped to an element type, or nil.
      def component_for(type_id)
        path = (get('componentMappings') || {})[type_id]
        path && File.file?(path) ? path : nil
      end
    end

    # Backups and recovery journal for the SketchUp side (see core/safety.rb).
    module Safety
      module_function

      # Persistent per-user folder: %APPDATA%\SiteDesignPro (Windows), ~/Library/Application Support/SiteDesignPro (macOS).
      def user_dir
        base = ENV['APPDATA'].to_s.empty? ? File.join(Dir.home, 'Library', 'Application Support') : ENV['APPDATA']
        File.join(base, 'SiteDesignPro')
      rescue ArgumentError
        File.join(Dir.tmpdir, 'SiteDesignPro')
      end

      def journal
        RecoveryJournal.new(File.join(user_dir, 'sketchup-journal.jsonl'))
      rescue StandardError => e
        Log.warn("Dnevnik operacija nije dostupan: #{e.message}")
        nil
      end

      def backup_manager
        BackupManager.new(keep: Settings.get('backupKeep').to_i, max_age_days: Settings.get('backupMaxAgeDays').to_i)
      end
    end

    # Diagnostic log in SketchUp's temp folder (also echoed to the Ruby console).
    module Log
      module_function

      def path
        dir = Sketchup.respond_to?(:temp_dir) ? Sketchup.temp_dir : Dir.tmpdir
        File.join(dir, 'site_design_pro.log')
      end

      def write(level, message)
        line = "#{Time.now.strftime('%Y-%m-%d %H:%M:%S')} [#{level.upcase}] #{message}"
        puts "Vanjsko uređenje PRO: #{line}" if level != :debug && !ENV['SDP_QUIET']
        File.open(path, 'a') { |f| f.puts(line) }
      rescue SystemCallError, IOError => e
        puts "Vanjsko uređenje PRO: dnevnik nije zapisan (#{e.message}): #{line}"
      end

      def info(msg)
        write(:info, msg)
      end

      def warn(msg)
        write(:warn, msg)
      end

      def error(msg, exception = nil)
        msg = "#{msg}: #{exception.class}: #{exception.message}\n  #{exception.backtrace&.first(8)&.join("\n  ")}" if exception
        write(:error, msg)
      end
    end
  end
end
