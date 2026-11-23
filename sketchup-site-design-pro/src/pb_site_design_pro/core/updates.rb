# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # The description of the newest published version (latest.json of the newest GitHub release), read the same way as
    # in AutoCAD (Core/Updates/UpdateInfo.cs), so both sides update to the same version. Pure: no SketchUp, no network.
    module Updates
      LATEST_URL = 'https://github.com/amsalamsal/projektni-biro/releases/latest/download/latest.json'
      MODES = %w[ask auto off].freeze

      module_function

      # Hash with 'version', 'date', 'notes', 'url' and 'sha256' of the SketchUp extension; raises ArgumentError when
      # the file does not describe a usable version.
      def parse(json)
        data = JSON.parse(json.to_s)
        raise ArgumentError, 'opis verzije nije objekat' unless data.is_a?(Hash)

        su = data['sketchup'].is_a?(Hash) ? data['sketchup'] : {}
        info = { 'version' => data['version'].to_s, 'date' => data['date'].to_s,
                 'notes' => Array(data['notes']).map(&:to_s).reject(&:empty?),
                 'url' => su['url'].to_s, 'sha256' => su['sha256'].to_s.downcase }
        unless parts(info['version']) && info['url'].start_with?('https://') && info['sha256'] =~ /\A[0-9a-f]{64}\z/
          raise ArgumentError, 'opis verzije nije potpun (verzija, adresa ili SHA-256 paketa za SketchUp)'
        end

        info
      rescue JSON::ParserError => e
        raise ArgumentError, "opis verzije nije ispravan JSON: #{e.message}"
      end

      # Whether +candidate+ is newer than +current+ (major.minor.patch, numerically).
      def newer?(candidate, current)
        a = parts(candidate)
        b = parts(current)
        return false unless a && b

        (a <=> b) == 1
      end

      def parts(version)
        p = version.to_s.strip.sub(/\A[vV]/, '').split('.')
        return nil if p.length < 2 || p.length > 4 || p.any? { |x| x !~ /\A\d+\z/ }

        (p.map(&:to_i) + [0, 0, 0])[0, 3]
      end
    end
  end
end
