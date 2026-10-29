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
        'lastFolder' => ''
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

    # Diagnostic log in SketchUp's temp folder (also echoed to the Ruby console).
    module Log
      module_function

      def path
        dir = Sketchup.respond_to?(:temp_dir) ? Sketchup.temp_dir : Dir.tmpdir
        File.join(dir, 'site_design_pro.log')
      end

      def write(level, message)
        line = "#{Time.now.strftime('%Y-%m-%d %H:%M:%S')} [#{level.upcase}] #{message}"
        puts "Site Design Pro: #{line}" if level != :debug
        File.open(path, 'a') { |f| f.puts(line) }
      rescue SystemCallError, IOError => e
        puts "Site Design Pro: could not write log (#{e.message}): #{line}"
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
