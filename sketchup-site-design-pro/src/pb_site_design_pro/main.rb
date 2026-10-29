# frozen_string_literal: true

# Loaded by SketchUp when the extension is enabled (see ../pb_site_design_pro.rb).

require 'sketchup.rb'
require 'json'
require 'zlib'
require 'tmpdir'

require File.join(__dir__, 'core')
%w[
  sketchup/settings
  sketchup/materials
  sketchup/su_canvas
  sketchup/model_index
  sketchup/scenes
  sketchup/importer
  sketchup/editing
  sketchup/ui/dialogs
  sketchup/ui/commands
].each { |f| require File.join(__dir__, f) }

module ProjektniBiro
  module SiteDesignPro
    unless file_loaded?(__FILE__)
      begin
        Commands.install
        Log.info("Site Design Pro #{VERSION} loaded (SketchUp #{Sketchup.version}, Ruby #{RUBY_VERSION})")
      rescue StandardError => e
        Log.error('Site Design Pro failed to initialise', e)
      end
      file_loaded(__FILE__)
    end
  end
end
