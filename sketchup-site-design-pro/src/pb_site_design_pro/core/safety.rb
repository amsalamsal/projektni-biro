# frozen_string_literal: true

require 'json'
require 'securerandom'
require 'fileutils'
require 'digest'
require 'time'

module ProjektniBiro
  module SiteDesignPro
    # Timestamped backups "name_YYYYMMDD-HHMMSS_operacija.ext" with retention. The newest backup of a file is never
    # deleted and the source file is never touched. Same naming as the AutoCAD extension.
    class BackupManager
      FOLDER = '_SDP_backup'
      STAMP = /\A(\d{8}-\d{6})_([a-z0-9-]+)(?:_\d+)?\z/.freeze

      attr_reader :keep, :max_age_days

      def initialize(keep: 10, max_age_days: 30, folder: nil, clock: -> { Time.now })
        @keep = keep
        @max_age_days = max_age_days
        @folder = folder
        @clock = clock
      end

      def folder_for(source)
        @folder.to_s.empty? ? File.join(File.dirname(File.expand_path(source)), FOLDER) : @folder
      end

      def self.slug(operation)
        s = operation.to_s.downcase.tr('čćžšđ', 'cczsd').gsub(/[^a-z0-9]+/, '-').gsub(/\A-+|-+\z/, '')
        s.empty? ? 'operacija' : s[0, 40]
      end

      def next_path(source, operation)
        stem = File.basename(source, '.*')
        ext = File.extname(source)
        base = "#{stem}_#{@clock.call.strftime('%Y%m%d-%H%M%S')}_#{BackupManager.slug(operation)}"
        path = File.join(folder_for(source), base + ext)
        n = 2
        while File.exist?(path)
          path = File.join(folder_for(source), "#{base}_#{n}#{ext}")
          n += 1
        end
        path
      end

      # Copies an existing file to a verified backup; returns the backup path.
      def backup_file(source, operation)
        raise IOError, "Datoteka za sigurnosnu kopiju ne postoji: #{source}" unless File.file?(source)

        target = next_path(source, operation)
        FileUtils.mkdir_p(File.dirname(target))
        FileUtils.cp(source, target)
        unless Digest::SHA256.file(source).hexdigest == Digest::SHA256.file(target).hexdigest
          File.delete(target)
          raise IOError, "Provjera sigurnosne kopije nije uspjela: #{target}"
        end
        prune(source)
        target
      end

      # Applies retention to a backup the host wrote itself (e.g. Model#save_copy).
      def adopt(source, backup)
        raise IOError, "Sigurnosna kopija nije zapisana: #{backup}" unless File.file?(backup) && File.size(backup).positive?

        prune(source)
        backup
      end

      # Backups of a file, newest first: [{ path:, time:, operation: }].
      def list(source)
        dir = folder_for(source)
        stem = "#{File.basename(source, '.*')}_"
        ext = File.extname(source)
        return [] unless File.directory?(dir)

        Dir.glob(File.join(dir, "#{glob_escape(stem)}*#{ext}")).filter_map do |f|
          rest = File.basename(f, ext)[stem.length..]
          m = STAMP.match(rest.to_s)
          next unless m

          t = Time.strptime(m[1], '%Y%m%d-%H%M%S') rescue nil # rubocop:disable Style/RescueModifier
          t && { path: f, time: t, operation: m[2] }
        end.sort_by { |b| [b[:time].to_f, b[:path]] }.reverse
      end

      def prune(source)
        deleted = []
        now = @clock.call
        list(source).each_with_index do |b, i|
          next if i.zero?

          too_many = @keep.positive? && i >= @keep
          too_old = @max_age_days.positive? && (now - b[:time]) > @max_age_days * 86_400
          next unless too_many || too_old

          begin
            File.delete(b[:path])
            deleted << b[:path]
          rescue SystemCallError
            nil # locked: left for the next run
          end
        end
        deleted
      end

      private

      def glob_escape(s)
        s.gsub(/[\\*?\[\]{}]/) { |c| "\\#{c}" }
      end
    end

    # Append-only JSON-lines journal. "Started" is written before an operation changes anything and
    # "Completed"/"Failed"/"RolledBack" afterwards; an operation without an end record was interrupted.
    class RecoveryJournal
      attr_reader :path

      def initialize(path, clock: -> { Time.now.utc })
        @path = path
        @clock = clock
      end

      def begin_operation(operation, document, backup)
        id = SecureRandom.hex(16)
        append('OperationId' => id, 'Operation' => operation, 'Document' => document.to_s, 'BackupPath' => backup, 'State' => 'Started')
        id
      end

      def end_operation(id, state, message = '')
        start = read.find { |e| e['OperationId'] == id && e['State'] == 'Started' } || {}
        append('OperationId' => id, 'Operation' => start['Operation'].to_s, 'Document' => start['Document'].to_s,
               'BackupPath' => start['BackupPath'], 'State' => state, 'Message' => message.to_s)
      end

      def read
        return [] unless File.file?(@path)

        File.read(@path, encoding: 'UTF-8').split("\n").filter_map do |line|
          next if line.strip.empty?

          JSON.parse(line)
        rescue JSON::ParserError
          nil # torn line from an interrupted write
        end
      end

      def unfinished(document = nil)
        all = read
        ended = all.reject { |e| e['State'] == 'Started' }.map { |e| e['OperationId'] }
        all.select do |e|
          e['State'] == 'Started' && !ended.include?(e['OperationId']) &&
            (document.nil? || e['Document'].to_s.casecmp(document.to_s).zero?)
        end
      end

      def acknowledge(id, message)
        end_operation(id, 'RolledBack', message)
      end

      def self.describe(e)
        backup = e['BackupPath'].to_s.empty? ? '' : " | kopija: #{e['BackupPath']}"
        "#{e['Utc']} | #{e['Operation']} | #{e['Document']}#{backup}"
      end

      private

      def append(entry)
        entry['Utc'] = @clock.call.strftime('%Y-%m-%dT%H:%M:%SZ')
        FileUtils.mkdir_p(File.dirname(File.expand_path(@path)))
        line = JSON.generate(entry) + "\n"
        File.open(@path, 'a+b') do |f|
          if f.size.positive?
            f.seek(-1, IO::SEEK_END)
            line = "\n#{line}" unless f.read(1) == "\n"
          end
          f.write(line)
          f.flush
          f.fsync rescue nil # rubocop:disable Style/RescueModifier
        end
      end
    end
  end
end
