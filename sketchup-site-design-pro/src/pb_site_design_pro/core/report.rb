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
        "[#{severity}] #{code} #{label || element_id || 'package'}: #{message}"
      end
    end

    # Collects issues and import statistics; renders text and JSON reports.
    class Report
      SEVERITIES = %i[error warning review info].freeze
      DISCLAIMER = 'Automated checks support design coordination only. They use values entered by the user ' \
                   'and are not certified regulatory compliance checks.'

      attr_reader :issues, :stats
      attr_accessor :title, :package_path, :started_at, :finished_at

      def initialize(title = 'Site Design Pro report')
        @title = title
        @issues = []
        @stats = Hash.new(0)
        @started_at = Time.now
      end

      def add(severity, code, element_id, message, resolution = '', label = nil)
        raise ArgumentError, "unknown severity #{severity}" unless SEVERITIES.include?(severity)

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
          'stats' => @stats.dup,
          'errors' => count(:error), 'warnings' => count(:warning), 'reviews' => count(:review),
          'issues' => @issues.map(&:to_h),
          'disclaimer' => DISCLAIMER
        }
      end

      def to_text
        lines = [@title]
        lines << "Package: #{@package_path}" if @package_path
        lines << format('Duration: %.2f s', duration) if duration
        unless @stats.empty?
          lines << @stats.sort.map { |k, v| "#{k}: #{v}" }.join(', ')
        end
        lines << "Errors: #{count(:error)}  Warnings: #{count(:warning)}  Reviews: #{count(:review)}"
        SEVERITIES.each do |sev|
          list = @issues.select { |i| i.severity == sev }
          next if list.empty?

          lines << '' << sev.to_s.upcase
          list.each do |i|
            lines << "  #{i.code}  #{i.label || i.element_id || 'package'}: #{i.message}"
            lines << "      -> #{i.resolution}" unless i.resolution.to_s.empty?
          end
        end
        lines << '' << DISCLAIMER
        lines.join("\n")
      end
    end
  end
end
