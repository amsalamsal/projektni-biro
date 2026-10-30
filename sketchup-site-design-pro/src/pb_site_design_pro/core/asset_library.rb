# frozen_string_literal: true

require 'securerandom'

module ProjektniBiro
  module SiteDesignPro
    # The extension's local 3D model library (Assets/library.json, built by tools/assets/build_library.rb):
    # 14 categories (folders) with subcategories, one asset per element type. The asset ID is the catalog type
    # id, which also names the AutoCAD block, so a model is always linked to its 2D symbol. Works offline.
    class AssetLibrary
      Folder = Struct.new(:id, :name, :name_en)

      attr_reader :dir, :folders, :assets, :version

      def self.default_dir
        File.expand_path('../Assets', __dir__)
      end

      def initialize(dir = AssetLibrary.default_dir, catalog_dir = CatalogFiles.default_dir)
        @dir = dir
        index = File.join(dir, 'library.json')
        raise PackageError, "biblioteka modela nije pronađena (#{index}); ponovo instalirajte proširenje" unless File.file?(index)

        lib = JSON.parse(File.read(index, encoding: 'UTF-8'))
        @version = lib['version']
        @folders = (lib['folders'] || []).map { |f| f.is_a?(Hash) ? Folder.new(f['id'], f['name'], f['nameEn']) : Folder.new(f, f, f) }
        @assets = lib['assets'] || []
        @by_id = @assets.to_h { |a| [a['assetId'], a] }
        types = JSON.parse(File.read(File.join(catalog_dir, 'element-types.json'), encoding: 'UTF-8'))['types']
        @types = types.to_h { |t| [t['id'], t] }
        @builders = JSON.parse(File.read(File.join(catalog_dir, 'builders.json'), encoding: 'UTF-8'))['builders']
      end

      def asset(id)
        @by_id[id]
      end

      # Path of an asset file (model.dae, preview.jpg, asset.json); nil when the file is missing.
      def file(id, kind = 'model')
        a = asset(id)
        return nil unless a

        name = (a['files'] || {})[kind] || { 'model' => 'model.dae', 'preview' => 'preview.jpg' }[kind] || kind
        path = File.join(@dir, a['folder'], id, name)
        File.file?(path) ? path : nil
      end

      # Assets whose model or preview file is missing (reported, never silently replaced).
      def missing_files
        @assets.each_with_object([]) do |a, acc|
          %w[model preview].each { |k| acc << "#{a['assetId']}: #{k}" unless file(a['assetId'], k) }
        end
      end

      # Catalog type definition hash of an asset (parameters with the type's defaults), as stored in packages.
      def type_definition(id)
        t = @types[id]
        raise PackageError, "tip '#{id}' nije u katalogu" unless t

        b = @builders.fetch(t['builder'])
        defaults = t['defaults'] || {}
        params = b['parameters'].map { |p| defaults.key?(p['key']) ? p.merge('default' => defaults[p['key']]) : p }
        { 'id' => t['id'], 'name' => t['name'], 'category' => t['category'], 'subcategory' => t['subcategory'], 'builder' => t['builder'],
          'geometry' => b['geometry'], 'modelGroup' => t['modelGroup'] || 'planting', 'layer' => t['layer'], 'parameters' => params }
      end

      # Default geometry of an asset placed at `origin` (metres): a point, a straight path of `length` along the
      # rotation, or a `width` x `depth` rectangle rotated about the origin.
      def default_geometry(id, origin, rotation = 0.0, width: 4.0, depth: 3.0, length: 6.0)
        kind = type_definition(id)['geometry']
        o = origin.map(&:to_f)
        c = Math.cos(rotation)
        s = Math.sin(rotation)
        at = ->(x, y) { [o[0] + (c * x) - (s * y), o[1] + (s * x) + (c * y), o[2]] }
        case kind
        when 'point' then { kind: 'point', position: o, rotation: rotation.to_f }
        when 'path' then { kind: 'path', vertices: [at.call(0, 0), at.call(length, 0)], bulges: [0.0], closed: false }
        else { kind: 'polygon', outer: { vertices: [at.call(0, 0), at.call(width, 0), at.call(width, depth), at.call(0, depth)], bulges: [0.0] * 4 }, holes: [] }
        end
      end

      # Package element for an asset with parameter overrides and geometry (see #default_geometry).
      def element(id, params: {}, geometry: nil, element_id: nil, name: nil)
        td = TypeDef.new(type_definition(id))
        clean = params.select { |k, _| td.param_def(k) }
        g = geometry || default_geometry(id, [0.0, 0.0, 0.0])
        h = { 'id' => element_id || SecureRandom.uuid, 'type' => id, 'category' => td.category, 'subcategory' => td.subcategory,
              'builder' => td.builder, 'name' => name || td.name, 'modelGroup' => td.model_group, 'parameters' => clean,
              'layer' => @types[id]['layer'], 'revision' => 1, 'contentHash' => '' }
        Element.new(h, g, td)
      end

      # Data for the library dialog: folders with counts, assets with their metadata and preview paths.
      def index_data(in_model = {})
        {
          'version' => @version,
          'folders' => @folders.map { |f| { 'id' => f.id, 'name' => f.name, 'nameEn' => f.name_en, 'count' => @assets.count { |a| a['folder'] == f.id } } },
          'assets' => @assets.map do |a|
            a.slice('assetId', 'name', 'nameEn', 'folder', 'category', 'subcategory', 'geometry', 'dimensions', 'status', 'statusBs', 'source', 'license', 'materials', 'specObjects')
             .merge('kind' => a.dig('model', 'kind'), 'preview' => "../../../Assets/#{a['folder']}/#{a['assetId']}/#{(a['files'] || {})['preview'] || 'preview.jpg'}",
                    'inModel' => in_model[a['assetId']] || 0, 'hasModel' => !file(a['assetId'], 'model').nil?)
          end
        }
      end

      # Parameters of an asset for the dialog form (type defaults applied).
      def parameters(id)
        type_definition(id)['parameters']
      end
    end
  end
end
