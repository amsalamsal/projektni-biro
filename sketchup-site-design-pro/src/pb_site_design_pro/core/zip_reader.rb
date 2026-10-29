# frozen_string_literal: true

require 'zlib'

module ProjektniBiro
  module SiteDesignPro
    # Minimal, dependency-free reader for .sdpkg archives (standard zip, stored or deflated entries).
    # SketchUp's embedded Ruby provides zlib but no zip library. Zip64 and encryption are not supported.
    class ZipReader
      Entry = Struct.new(:name, :method, :compressed_size, :size, :local_offset, :crc)

      EOCD_SIG = 0x06054b50
      CDIR_SIG = 0x02014b50
      LOCAL_SIG = 0x04034b50
      MAX_ENTRY_BYTES = 512 * 1024 * 1024

      attr_reader :path

      def initialize(path)
        @path = path
        @entries = nil
      end

      def entries
        @entries ||= read_directory
      end

      def names
        entries.keys
      end

      def include?(name)
        entries.key?(name)
      end

      # Returns the entry contents as a binary String.
      def read(name)
        entry = entries[name] or raise PackageError, "#{name} nedostaje u arhivi paketa"
        raise PackageError, "#{name} prelazi najveću podržanu veličinu" if entry.size > MAX_ENTRY_BYTES

        File.open(@path, 'rb') do |f|
          f.seek(entry.local_offset)
          header = f.read(30)
          raise PackageError, 'oštećena arhiva (lokalno zaglavlje)' unless header && header.unpack1('V') == LOCAL_SIG

          name_len, extra_len = header[26, 4].unpack('vv')
          f.seek(entry.local_offset + 30 + name_len + extra_len)
          data = f.read(entry.compressed_size) || ''.b
          out = case entry.method
                when 0 then data
                when 8 then inflate(data)
                else raise PackageError, "nepodržana metoda kompresije #{entry.method} za #{name}"
                end
          raise PackageError, "CRC se ne podudara za #{name}" unless Zlib.crc32(out) == entry.crc

          out
        end
      end

      def read_text(name)
        read(name).force_encoding('UTF-8')
      end

      private

      def inflate(data)
        z = Zlib::Inflate.new(-Zlib::MAX_WBITS)
        out = z.inflate(data)
        z.finish
        out
      rescue Zlib::Error => e
        raise PackageError, "oštećeni podaci arhive (#{e.message})"
      ensure
        z&.close
      end

      def read_directory
        size = File.size(@path)
        raise PackageError, 'datoteka je premala da bi bila arhiva paketa' if size < 22

        File.open(@path, 'rb') do |f|
          tail_len = [size, 65_557].min
          f.seek(size - tail_len)
          tail = f.read(tail_len)
          idx = tail.rindex([EOCD_SIG].pack('V'))
          raise PackageError, 'datoteka nije zip arhiva (kraj centralnog direktorija nije pronađen)' unless idx

          eocd = tail[idx, 22]
          count = eocd[10, 2].unpack1('v')
          cd_size, cd_offset = eocd[12, 8].unpack('VV')
          raise PackageError, 'Zip64 arhive nisu podržane' if cd_offset == 0xFFFFFFFF

          f.seek(cd_offset)
          cd = f.read(cd_size) || ''
          parse_central_directory(cd, count)
        end
      end

      def parse_central_directory(cd, count)
        entries = {}
        pos = 0
        count.times do
          raise PackageError, 'oštećena arhiva (centralni direktorij)' unless cd[pos, 4]&.unpack1('V') == CDIR_SIG

          method = cd[pos + 10, 2].unpack1('v')
          crc, csize, usize = cd[pos + 16, 12].unpack('VVV')
          name_len, extra_len, comment_len = cd[pos + 28, 6].unpack('vvv')
          offset = cd[pos + 42, 4].unpack1('V')
          name = cd[pos + 46, name_len].force_encoding('UTF-8')
          entries[name] = Entry.new(name, method, csize, usize, offset, crc) unless name.end_with?('/')
          pos += 46 + name_len + extra_len + comment_len
        end
        entries
      end
    end
  end
end
