# frozen_string_literal: true

require 'tmpdir'

module ProjektniBiro
  module SiteDesignPro
    # Creates and reuses SketchUp materials for package material keys ("SDP <name>").
    class MaterialLibrary
      DICT = 'SiteDesignPro'

      def initialize(model, package_materials, catalog_materials, package = nil)
        @model = model
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

        d = @defs[key] || Material.new('default', 'Default', [200, 200, 200], 1.0, nil, nil)
        mat = @model.materials.add("SDP #{d.name}")
        mat.color = Sketchup::Color.new(*d.color)
        mat.alpha = d.alpha if d.alpha && d.alpha < 1.0
        apply_texture(mat, d)
        mat.set_attribute(DICT, 'materialKey', key)
        mat
      end

      def apply_texture(mat, d)
        return unless d.texture && @package

        asset = @package.assets.find { |a| a.path == d.texture }
        bytes = asset && @package.asset_bytes(asset)
        return unless bytes

        file = File.join(Dir.tmpdir, "sdp_tex_#{d.key}#{File.extname(d.texture)}")
        File.binwrite(file, bytes)
        mat.texture = file
        if d.texture_size && mat.texture
          mat.texture.size = [Units.m_to_in(d.texture_size[0]), Units.m_to_in(d.texture_size[1])]
        end
      rescue SystemCallError, IOError, ArgumentError => e
        Log.warn("Texture for material #{d.key} could not be applied: #{e.message}")
      end
    end
  end
end
