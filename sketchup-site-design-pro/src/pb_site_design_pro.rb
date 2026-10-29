# frozen_string_literal: true

# Site Design Pro for SketchUp - extension registration.
# Imports AutoCAD Site Design Pro exchange packages and reconstructs organised,
# editable, dimensionally accurate 3D site models.

require 'sketchup.rb'
require 'extensions.rb'

module ProjektniBiro
  module SiteDesignPro
    PLUGIN_ID = 'pb_site_design_pro'
    PLUGIN_DIR = File.join(File.dirname(__FILE__), PLUGIN_ID)

    require File.join(PLUGIN_DIR, 'version')

    unless file_loaded?(__FILE__)
      ex = SketchupExtension.new('Site Design Pro', File.join(PLUGIN_DIR, 'main'))
      ex.description = 'Imports AutoCAD Site Design Pro packages and builds organised, ' \
                       'editable 3D site models (surfaces, planting, lighting, furniture, ' \
                       'utilities, walls, structures) with update and change tracking.'
      ex.version = VERSION
      ex.copyright = '2026 Projektni biro'
      ex.creator = 'Projektni biro'
      Sketchup.register_extension(ex, true)
      file_loaded(__FILE__)
    end
  end
end
