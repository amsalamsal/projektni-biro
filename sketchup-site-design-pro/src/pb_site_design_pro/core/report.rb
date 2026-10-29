# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Raised for unreadable or incompatible packages.
    class PackageError < StandardError; end

    # One validation or import message. Severity: :error, :warning, :review, :info.
    Issue = Struct.new(:severity, :code, :element_id, :message, :resolution, :label) do
      def to_h
        { 'severity' => severity.to_s, 'code' => code, 'elementId' => element_id, 'label' => label,
          'message' => message, 'resolution' => resolution }
      end

      def to_s
        "[#{severity}] #{code} #{label || element_id || 'paket'}: #{message}"
      end
    end

    # Collects issues and import statistics; renders text and JSON reports.
    class Report
      SEVERITIES = %i[error warning review info].freeze
      DISCLAIMER = 'Automatske provjere služe samo za koordinaciju projekta. Koriste vrijednosti koje je unio korisnik ' \
                   'i nisu ovjerena provjera usklađenosti s propisima.'
      SEVERITY_NAMES = { error: 'Greške', warning: 'Upozorenja', review: 'Za stručni pregled', info: 'Informacije' }.freeze
      STAT_NAMES = {
        'created' => 'novo', 'updated' => 'ažurirano', 'restored' => 'vraćeno', 'conflicts' => 'konflikti', 'kept_local' => 'zadržane izmjene',
        'marked_deleted' => 'označeno obrisano', 'deleted' => 'obrisano', 'orphans' => 'zadržano bez izvora', 'detached' => 'odvojene kopije',
        'skipped' => 'preskočeno', 'unchanged' => 'nepromijenjeno', 'elements' => 'elemenata', 'terrains' => 'terena',
        'terrains_created' => 'novi tereni', 'terrains_updated' => 'ažurirani tereni', 'seconds' => 'sekundi',
        'groups' => 'grupa', 'meshes' => 'mreža', 'definitions' => 'definicija komponenti', 'instances' => 'instanci komponenti'
      }.freeze

      attr_reader :issues, :stats
      attr_accessor :title, :package_path, :started_at, :finished_at

      def initialize(title = 'Vanjsko uređenje PRO – izvještaj')
        @title = title
        @issues = []
        @stats = Hash.new(0)
        @started_at = Time.now
      end

      def add(severity, code, element_id, message, resolution = '', label = nil)
        raise ArgumentError, "nepoznata ozbiljnost #{severity}" unless SEVERITIES.include?(severity)

        @issues << Issue.new(severity, code, element_id, message, resolution, label)
        @issues.last
      end

      def merge!(other)
        @issues.concat(other.issues)
        other.stats.each { |k, v| @stats[k] += v }
        self
      end

      def count(severity)
        @issues.count { |i| i.severity == severity }
      end

      def errors?
        count(:error).positive?
      end

      def element_issues(id)
        @issues.select { |i| i.element_id == id }
      end

      def duration
        return nil unless @finished_at

        @finished_at - @started_at
      end

      def to_h
        {
          'title' => @title,
          'package' => @package_path,
          'startedAt' => @started_at.utc.strftime('%Y-%m-%dT%H:%M:%SZ'),
          'durationSeconds' => duration ? duration.round(3) : nil,
          'stats' => @stats.to_h { |k, v| [k.to_s, v] },
          'errors' => count(:error), 'warnings' => count(:warning), 'reviews' => count(:review),
          'issues' => @issues.map(&:to_h),
          'disclaimer' => DISCLAIMER
        }
      end

      def to_text
        lines = [@title]
        lines << "Paket: #{@package_path}" if @package_path
        lines << format('Trajanje: %.2f s', duration).tr('.', ',') if duration
        unless @stats.empty?
          lines << @stats.sort_by { |k, _| k.to_s }.map { |k, v| "#{STAT_NAMES[k.to_s] || k}: #{v}" }.join(', ')
        end
        lines << "Greške: #{count(:error)}  Upozorenja: #{count(:warning)}  Za pregled: #{count(:review)}"
        SEVERITIES.each do |sev|
          list = @issues.select { |i| i.severity == sev }
          next if list.empty?

          lines << '' << SEVERITY_NAMES[sev].upcase
          list.each do |i|
            lines << "  #{i.code}  #{i.label || i.element_id || 'paket'}: #{i.message}"
            lines << "      -> #{i.resolution}" unless i.resolution.to_s.empty?
          end
        end
        lines << '' << DISCLAIMER
        lines.join("\n")
      end
    end
  end
end
