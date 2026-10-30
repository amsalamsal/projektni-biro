# frozen_string_literal: true

# Loads the host-independent core of Site Design Pro for SketchUp. Nothing in core/ references the
# SketchUp API, so it runs (and is unit-tested) in plain Ruby as well as inside SketchUp.

require 'json'

module ProjektniBiro
  module SiteDesignPro
    CORE_DIR = File.join(__dir__, 'core')
  end
end

require File.join(__dir__, 'version')
%w[
  report
  units
  geometry
  mesh
  zip_reader
  package
  catalog_files
  fingerprint
  change_planner
  canvas/recording_canvas
  builders/base
  builders/surfaces
  builders/linear
  builders/vegetation
  builders/furniture
  builders/equipment
  builders/utilities
  builders/structures
  builders/water
  builders/markings
  models/kit
  models/vegetation
  models/furniture
  models/lighting
  models/traffic
  models/utilities
  models/equipment
  models/structures
  models/information
  element_builder
  change_set
  terrain
  safety
].each { |f| require File.join(ProjektniBiro::SiteDesignPro::CORE_DIR, f) }
