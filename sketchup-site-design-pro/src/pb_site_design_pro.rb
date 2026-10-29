# frozen_string_literal: true

# Vanjsko uređenje PRO (Site Design Pro) za SketchUp – registracija proširenja.
# Uvozi pakete razmjene iz AutoCAD-a i gradi organizovane, izmjenjive 3D modele
# vanjskog uređenja tačnih dimenzija. Podržano: SketchUp 2022 i novije verzije.

require 'sketchup.rb'
require 'extensions.rb'

module ProjektniBiro
  module SiteDesignPro
    PLUGIN_ID = 'pb_site_design_pro'
    PLUGIN_DIR = File.join(File.dirname(__FILE__), PLUGIN_ID)

    require File.join(PLUGIN_DIR, 'version')

    unless file_loaded?(__FILE__)
      ex = SketchupExtension.new('Vanjsko uređenje PRO', File.join(PLUGIN_DIR, 'main'))
      ex.description = 'Uvozi pakete iz AutoCAD-a (Vanjsko uređenje PRO) i gradi organizovane, ' \
                       'izmjenjive 3D modele vanjskog uređenja (teren, parter, zelenilo, rasvjeta, mobilijar, ' \
                       'instalacije, zidovi, nadstrešnice) s ažuriranjem i praćenjem izmjena. SketchUp 2022+.'
      ex.version = VERSION
      ex.copyright = '2026 Projektni biro'
      ex.creator = 'Projektni biro'
      Sketchup.register_extension(ex, true)
      file_loaded(__FILE__)
    end
  end
end
