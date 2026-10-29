# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Resolved element type definition carried in the package manifest.
    class TypeDef
      attr_reader :id, :name, :category, :subcategory, :builder, :geometry, :model_group, :parameters

      def initialize(h)
        @id = h.fetch('id')
        @name = h['name'] || @id
        @category = h['category'].to_s
        @subcategory = h['subcategory'].to_s
        @builder = h.fetch('builder')
        @geometry = h.fetch('geometry')
        @model_group = h['modelGroup'].to_s
        @parameters = (h['parameters'] || []).map(&:dup)
        @by_key = @parameters.each_with_object({}) { |p, acc| acc[p['key']] = p }
      end

      def param_def(key)
        @by_key[key]
      end

      def defaults
        @parameters.each_with_object({}) { |p, acc| acc[p['key']] = p['default'] }
      end
    end

    Material = Struct.new(:key, :name, :color, :alpha, :texture, :texture_size)

    Asset = Struct.new(:id, :path, :kind, :sha256)

    # A package element with parameters merged over its type defaults.
    class Element
      attr_reader :id, :type_id, :category, :subcategory, :builder, :name, :label, :layer, :model_group,
                  :revision, :content_hash, :params, :raw_params, :properties, :custom, :asset_id, :geometry,
                  :type_def

      def initialize(h, geometry, type_def)
        @id = h.fetch('id')
        @type_id = h.fetch('type')
        @category = h['category'].to_s
        @subcategory = h['subcategory'].to_s
        @builder = h.fetch('builder')
        @name = h['name'].to_s
        @label = h['label']
        @layer = h['layer'].to_s
        @model_group = h['modelGroup'].to_s
        @revision = (h['revision'] || 1).to_i
        @content_hash = h['contentHash'].to_s
        @raw_params = h['parameters'] || {}
        @properties = h['properties'] || {}
        @custom = h['custom'] || {}
        @asset_id = h['assetId']
        @geometry = geometry
        @type_def = type_def
        @params = (type_def ? type_def.defaults : {}).merge(@raw_params)
      end

      def kind
        @geometry[:kind]
      end

      def param(key, fallback = nil)
        v = @params[key]
        v.nil? ? fallback : v
      end

      def num(key, fallback = 0.0)
        v = @params[key]
        v.is_a?(Numeric) ? v.to_f : fallback
      end

      def bool(key, fallback = false)
        v = @params[key]
        v.nil? ? fallback : v == true
      end

      def text(key, fallback = '')
        v = @params[key]
        v.nil? ? fallback : v.to_s
      end

      def base_name
        @name.to_s.empty? ? (@type_def ? @type_def.name : @type_id) : @name
      end

      def display_name
        @label.to_s.empty? ? base_name : "#{@label} #{base_name}"
      end

      # Returns a copy with replaced parameters (used for SketchUp-side parameter edits).
      def with_params(overrides)
        copy = dup
        copy.instance_variable_set(:@params, @params.merge(overrides))
        copy
      end
    end

    # In-memory exchange package (metres, package frame).
    class Package
      attr_reader :path, :schema_version, :package_id, :created_utc, :generator, :project, :units,
                  :origin, :true_north_degrees, :crs, :catalog, :type_defs, :materials, :elements, :assets,
                  :elevation, :terrains

      # Absolute elevation of package z = 0 (m above the vertical datum), or nil when the project did not define it.
      def package_zero_absolute
        v = (@elevation || {})['packageZeroAbsolute']
        v.is_a?(Numeric) ? v.to_f : nil
      end

      def building_zero_absolute
        v = (@elevation || {})['buildingZeroAbsolute']
        v.is_a?(Numeric) ? v.to_f : nil
      end

      def vertical_datum
        (@elevation || {})['verticalDatum'].to_s
      end

      def initialize(attrs)
        @elevation = {}
        @terrains = []
        attrs.each { |k, v| instance_variable_set("@#{k}", v) }
        @by_id = @elements.each_with_object({}) { |e, acc| acc[e.id] = e }
      end

      def element(id)
        @by_id[id]
      end

      def project_id
        @project['id']
      end

      def project_name
        @project['name']
      end

      def asset(id)
        @assets.find { |a| a.id == id }
      end

      # Reads an asset's bytes from the package (zip or folder), or nil when missing.
      def asset_bytes(asset)
        @asset_reader&.call(asset.path)
      end
    end

    # Reads and structurally validates packages (folder, manifest.json path or .sdpkg archive).
    module PackageReader
      UUID = /\A[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\z/.freeze
      FORMAT_ID = 'site-design-pro-package'

      module_function

      # Returns [package_or_nil, report].
      def read(path)
        report = Report.new('Provjera paketa')
        report.package_path = path
        manifest_text, geometry_text, asset_reader, asset_exists = load_documents(path)
        package = parse(manifest_text, geometry_text, report, path, asset_reader, asset_exists)
        [package, report]
      rescue PackageError, JSON::ParserError, SystemCallError, IOError, KeyError, TypeError, ArgumentError, NoMethodError => e
        report.add(:error, 'SDP-F001', nil, "Paket nije moguće pročitati: #{e.message}",
                   'Provjerite da je datoteka paket programa Vanjsko uređenje PRO (.sdpkg ili folder s manifest.json i ' \
                   'geometry.json) i ponovo ga izvezite iz AutoCAD-a.')
        [nil, report]
      end

      def read_strings(manifest_text, geometry_text)
        report = Report.new('Provjera paketa')
        package = parse(manifest_text, geometry_text, report, nil, ->(_p) { nil }, ->(_p) { true })
        [package, report]
      rescue PackageError, JSON::ParserError, KeyError, TypeError, ArgumentError, NoMethodError => e
        report.add(:error, 'SDP-F001', nil, "Sadržaj paketa nije ispravan: #{e.message}", 'Ponovo izvezite paket.')
        [nil, report]
      end

      def load_documents(path)
        if File.file?(path) && File.extname(path).casecmp('.sdpkg').zero?
          zip = ZipReader.new(path)
          [zip.read_text('manifest.json'), zip.read_text('geometry.json'),
           ->(p) { zip.include?(p) ? zip.read(p) : nil }, ->(p) { zip.include?(p) }]
        else
          dir = File.directory?(path) ? path : File.dirname(path)
          manifest = File.join(dir, 'manifest.json')
          geometry = File.join(dir, 'geometry.json')
          raise PackageError, "manifest.json nije pronađen u #{dir}" unless File.file?(manifest)
          raise PackageError, "geometry.json nije pronađen u #{dir}" unless File.file?(geometry)

          root = File.expand_path(dir)
          resolve = lambda do |p|
            full = File.expand_path(p, root)
            full.start_with?(root + File::SEPARATOR) && File.file?(full) ? full : nil
          end
          [File.read(manifest, encoding: 'UTF-8'), File.read(geometry, encoding: 'UTF-8'),
           ->(p) { (f = resolve.call(p)) ? File.binread(f) : nil }, ->(p) { !resolve.call(p).nil? }]
        end
      end

      def parse(manifest_text, geometry_text, report, path, asset_reader, asset_exists)
        m = JSON.parse(manifest_text)
        g = JSON.parse(geometry_text)
        raise PackageError, 'manifest.json nije manifest programa Vanjsko uređenje PRO' unless m['format'] == FORMAT_ID

        version = m.fetch('schemaVersion').to_s
        major, minor, patch = version.split('.').map { |x| Integer(x, 10) }
        raise PackageError, "nepodržana verzija sheme #{version} (ovo proširenje čita #{SUPPORTED_SCHEMA_MAJOR}.x)" unless major == SUPPORTED_SCHEMA_MAJOR

        if ([major, minor, patch] <=> SCHEMA_VERSION.split('.').map(&:to_i)).positive?
          report.add(:warning, 'SDP-F002', nil,
                     "Shema paketa #{version} je novija od ovog proširenja (#{SCHEMA_VERSION}); nepoznata polja se zanemaruju.",
                     'Ažurirajte Vanjsko uređenje PRO za SketchUp.')
        end
        units = m.fetch('units')
        raise PackageError, 'jedinica dužine paketa mora biti metar' unless units['length'] == 'm' && g['lengthUnit'] == 'm'
        raise PackageError, 'packageId nije ispravan UUID' unless m['packageId'].to_s.match?(UUID)

        type_defs = {}
        m.fetch('typeDefinitions').each { |t| type_defs[t['id']] = TypeDef.new(t) }

        materials = {}
        (m['materials'] || []).each do |mt|
          materials[mt['key']] = Material.new(mt['key'], mt['name'], mt['color'], (mt['alpha'] || 1.0).to_f,
                                              mt['texture'], mt['textureSize'])
        end

        geoms = {}
        g.fetch('elements').each do |ge|
          id = ge['id'].to_s
          if geoms.key?(id)
            report.add(:error, 'SDP-F010', id, 'Dupli zapis geometrije.', 'Ponovo izvezite paket.')
            next
          end
          geoms[id] = ge
        end

        elements = []
        seen = {}
        m.fetch('elements').each do |eh|
          id = eh['id'].to_s
          label = eh['label']
          unless id.match?(UUID)
            report.add(:error, 'SDP-D001', id.empty? ? nil : id, 'Element nema ispravan identifikator.',
                       'Pokrenite SDPAUDIT u AutoCAD-u, zatim ponovo izvezite.', label)
            next
          end
          if seen[id]
            report.add(:error, 'SDP-D002', id, 'Dupli identifikator elementa u paketu.',
                       'Pokrenite SDPAUDIT u AutoCAD-u da se poprave kopirani objekti, zatim ponovo izvezite.', label)
            next
          end
          seen[id] = true
          ge = geoms[id]
          unless ge
            report.add(:error, 'SDP-F011', id, 'Element nema zapis geometrije.', 'Ponovo izvezite paket.', label)
            next
          end
          type_def = type_defs[eh['type']]
          begin
            geometry = parse_geometry(ge)
          rescue PackageError, KeyError, TypeError, NoMethodError, ArgumentError => e
            report.add(:error, 'SDP-G010', id, "Neispravna geometrija: #{e.message}", 'Ispravite izvorni objekat u AutoCAD-u i ponovo izvezite.', label)
            next
          end
          if type_def && type_def.geometry != geometry[:kind]
            report.add(:error, 'SDP-F012', id, "Vrsta geometrije '#{geometry[:kind]}' ne odgovara definiciji tipa '#{type_def.geometry}'.",
                       'Ponovo izvezite paket.', label)
            next
          end
          unless type_def
            report.add(:warning, 'SDP-F013', id, "Tip '#{eh['type']}' nema definiciju u manifestu.",
                       'Ponovo izvezite trenutnom verzijom AutoCAD proširenja.', label)
          end
          element = Element.new(eh, geometry, type_def)
          check_parameters(element, report)
          elements << element
        end

        (geoms.keys - seen.keys).each do |orphan|
          report.add(:warning, 'SDP-F014', orphan, 'Zapis geometrije bez elementa u manifestu se zanemaruje.', 'Ponovo izvezite paket.')
        end

        assets = (m['assets'] || []).map do |a|
          asset = Asset.new(a['id'], a['path'].to_s, a['kind'], a['sha256'])
          if asset.path.include?('..') || !asset.path.start_with?('assets/')
            report.add(:error, 'SDP-F020', nil, "Datoteka modela '#{asset.id}' ima nesigurnu putanju '#{asset.path}'.",
                       'Putanje datoteka moraju ostati unutar foldera assets/.')
          elsif !asset_exists.call(asset.path)
            report.add(:warning, 'SDP-F021', nil, "Datoteka modela '#{asset.id}' nedostaje: #{asset.path}",
                       'Ponovo izvezite s modelima; ti elementi se prikazuju parametarskom geometrijom.')
          end
          asset
        end

        cs = m.fetch('coordinateSystem')
        terrains = []
        if m['terrain'].is_a?(Hash)
          bytes = asset_reader.call('terrain.json')
          if bytes.nil?
            report.add(:error, 'SDP-F030', nil, 'Paket navodi teren, ali datoteka terrain.json nedostaje.', 'Ponovo izvezite paket iz AutoCAD-a.')
          else
            terrains = TerrainReader.parse(bytes.dup.force_encoding('UTF-8'), report)
          end
        end
        report.stats['elements'] = elements.length
        report.stats['terrains'] = terrains.length unless terrains.empty?
        Package.new(
          path: path, schema_version: version, package_id: m['packageId'], created_utc: m['createdUtc'],
          generator: m['generator'] || {}, project: m.fetch('project'), units: units,
          origin: cs.fetch('origin').map(&:to_f), true_north_degrees: cs.fetch('trueNorthDegrees').to_f, crs: cs['crs'],
          catalog: m['catalog'] || {}, type_defs: type_defs, materials: materials, elements: elements, assets: assets,
          asset_reader: asset_reader, elevation: cs['elevation'].is_a?(Hash) ? cs['elevation'] : {}, terrains: terrains
        )
      end

      def vec3(a)
        raise PackageError, 'koordinate moraju biti nizovi [x, y, z]' unless a.is_a?(Array) && a.length == 3
        raise PackageError, 'koordinate moraju biti konačni brojevi' unless a.all? { |v| v.is_a?(Numeric) && v.to_f.finite? }

        a.map(&:to_f)
      end

      def ring(h)
        verts = h.fetch('vertices').map { |v| vec3(v) }
        bulges = h['bulges']&.map(&:to_f)
        { vertices: verts, bulges: bulges }
      end

      def parse_geometry(ge)
        case ge.fetch('kind')
        when 'point'
          rot = ge.fetch('rotation')
          raise PackageError, 'rotacija mora biti konačan broj' unless rot.is_a?(Numeric) && rot.to_f.finite?

          { kind: 'point', position: vec3(ge.fetch('position')), rotation: rot.to_f }
        when 'path'
          r = ring(ge)
          raise PackageError, 'trasa treba najmanje dva tjemena' if r[:vertices].length < 2

          { kind: 'path', vertices: r[:vertices], bulges: r[:bulges], closed: ge.fetch('closed') == true }
        when 'polygon'
          outer = ring(ge.fetch('outer'))
          raise PackageError, 'površina treba najmanje tri tjemena' if outer[:vertices].length < 3 && !outer[:bulges]

          { kind: 'polygon', outer: outer, holes: (ge['holes'] || []).map { |h| ring(h) } }
        else
          raise PackageError, "nepoznata vrsta geometrije '#{ge['kind']}'"
        end
      end

      # Schema-format (string-keyed) geometry Hash, the inverse of parse_geometry.
      def geometry_json(g)
        ring = lambda do |r|
          h = { 'vertices' => r[:vertices] }
          h['bulges'] = r[:bulges] if r[:bulges]
          h
        end
        case g[:kind]
        when 'point' then { 'kind' => 'point', 'position' => g[:position], 'rotation' => g[:rotation] }
        when 'path' then ring.call(g).merge('kind' => 'path', 'closed' => g[:closed])
        else { 'kind' => 'polygon', 'outer' => ring.call(g[:outer]), 'holes' => g[:holes].map { |h| ring.call(h) } }
        end
      end

      # Replaces invalid parameter values with type defaults so reconstruction never receives bad input.
      def check_parameters(element, report)
        td = element.type_def
        return unless td

        fixes = {}
        td.parameters.each do |pd|
          key = pd['key']
          v = element.params[key]
          next if v.nil?

          ok = case pd['type']
               when 'length', 'number', 'angle' then v.is_a?(Numeric) && v.to_f.finite? && in_range?(v, pd)
               when 'integer' then v.is_a?(Numeric) && (v - v.round).abs < 1e-9 && in_range?(v, pd)
               when 'bool' then [true, false].include?(v)
               when 'enum' then (pd['options'] || []).include?(v)
               else true
               end
          next if ok

          fixes[key] = pd['default']
          report.add(:warning, 'SDP-P020', element.id,
                     "Parametar '#{pd['label'] || key}' ima neispravnu vrijednost #{v.inspect}; koristi se zadana vrijednost #{pd['default'].inspect}.",
                     'Ispravite vrijednost u AutoCAD-u (SDPEDIT) i ponovo izvezite.', element.label)
        end
        element.params.merge!(fixes) unless fixes.empty?
      end

      def in_range?(v, pd)
        (pd['min'].nil? || v >= pd['min'] - 1e-12) && (pd['max'].nil? || v <= pd['max'] + 1e-12)
      end
    end
  end
end
