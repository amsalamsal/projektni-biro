# frozen_string_literal: true

require 'tmpdir'

module ProjektniBiro
  module SiteDesignPro
    # Creates and reuses SketchUp materials for package material keys ("VU <naziv>").
    class MaterialLibrary
      DICT = 'SiteDesignPro'
      # Seamless textures shipped with the extension (tools/assets/make_textures.py, own work).
      TEXTURE_DIR = File.expand_path('../textures', __dir__)

      def initialize(model, package_materials, catalog_materials, package = nil)
        @model = model
        @catalog = catalog_materials
        @defs = catalog_materials.merge(package_materials)
        @package = package
        @cache = {}
      end

      def get(key)
        return nil if key.nil?

        @cache[key] ||= find_or_create(@defs.key?(key) ? key : 'default')
      end

      private

      def find_or_create(key)
        existing = @model.materials.find { |m| m.get_attribute(DICT, 'materialKey') == key }
        return existing if existing

        d = @defs[key] || Material.new('default', 'Zadani materijal', [200, 200, 200], 1.0, nil, nil)
        library = library_material(key, d)
        return library if library

        mat = @model.materials.add("VU #{d.name}")
        mat.color = Sketchup::Color.new(*d.color)
        mat.alpha = d.alpha if d.alpha && d.alpha < 1.0
        apply_texture(mat, d)
        mat.set_attribute(DICT, 'materialKey', key)
        mat
      end

      # A material of SketchUp's own library named by the catalog ("sketchup": "Asphalt New"), loaded from its .skm
      # file; nil when the library or the file is not found (the catalog colour and texture are used instead).
      def library_material(key, d)
        name = (d.respond_to?(:library) && d.library) || @catalog[key]&.library
        return nil if name.nil? || name.to_s.empty? || !@model.materials.respond_to?(:load)

        file = MaterialLibrary.skm_file(name.to_s)
        return nil unless file

        mat = @model.materials.load(file)
        return nil unless mat

        mat.set_attribute(DICT, 'materialKey', key)
        mat
      rescue StandardError => e # a missing library material must never stop the geometry from being built
        Log.warn("Materijal #{name} iz biblioteke SketchUpa nije učitan: #{e.message}")
        nil
      end

      # Path of a .skm file of SketchUp's material library by its name (letter case, spaces and underscores ignored).
      def self.skm_file(name)
        want = name.downcase.gsub(/[\s_]+/, '')
        skm_index[want]
      end

      def self.skm_index
        @skm_index ||= begin
          index = {}
          skm_roots.each do |root|
            Dir.glob(File.join(root.tr('\\', '/'), '**', '*.skm')).each do |f|
              index[File.basename(f, '.skm').downcase.gsub(/[\s_]+/, '')] ||= f
            end
          end
          index
        end
      end

      def self.skm_roots
        roots = []
        if defined?(Sketchup)
          found = Sketchup.find_support_file('Materials')
          roots << found if found
          roots << File.join(Sketchup.get_resource_path(''), '..', 'Materials') if Sketchup.respond_to?(:get_resource_path)
          ver = Sketchup.version.to_i
          [ENV['ProgramData'], ENV['APPDATA']].compact.each { |b| roots << File.join(b, 'SketchUp', "SketchUp 20#{ver}", 'SketchUp', 'Materials') }
        end
        roots.select { |r| File.directory?(r) }.uniq
      rescue StandardError
        []
      end

      # Texture source, in order: the package's own asset, then the extension's bundled texture named by the
      # package material or by the catalog material with the same key. A material without any keeps its colour.
      def apply_texture(mat, d)
        file, size = package_texture(d) || bundled_texture(d)
        return unless file

        mat.texture = file
        mat.texture.size = [Units.m_to_in(size[0]), Units.m_to_in(size[1])] if size && mat.texture
      rescue StandardError => e # a texture must never stop the geometry from being built
        Log.warn("Tekstura materijala #{d.key} nije primijenjena: #{e.message}")
      end

      def package_texture(d)
        return nil unless d.texture && @package

        asset = @package.assets.find { |a| a.path == d.texture }
        bytes = asset && @package.asset_bytes(asset)
        return nil unless bytes

        file = File.join(Dir.tmpdir, "sdp_tex_#{d.key}#{File.extname(d.texture)}")
        File.binwrite(file, bytes)
        [file, d.texture_size]
      end

      def bundled_texture(d)
        cat = @catalog[d.key]
        [d, cat].compact.each do |src|
          next unless src.texture

          file = File.join(TEXTURE_DIR, File.basename(src.texture))
          return [file, src.texture_size || cat&.texture_size] if File.file?(file)
        end
        nil
      end
    end
  end
end
