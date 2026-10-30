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
  sketchup/asset_insert
  sketchup/ui/dialogs
  sketchup/ui/commands
].each { |f| require File.join(__dir__, f) }

module ProjektniBiro
  module SiteDesignPro
    unless file_loaded?(__FILE__)
      begin
        Commands.install
        Log.info("Vanjsko uređenje PRO #{VERSION} učitano (SketchUp #{Sketchup.version}, Ruby #{RUBY_VERSION})")
        if Sketchup.version.to_i < 22
          UI.messagebox("Vanjsko uređenje PRO zahtijeva SketchUp 2022 ili noviji (pokrenuto: #{Sketchup.version}). " \
                        'Proširenje može raditi nepotpuno.')
        end
      rescue StandardError => e
        Log.error('Vanjsko uređenje PRO se nije pokrenulo', e)
      end
      file_loaded(__FILE__)
    end
  end
end
