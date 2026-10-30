# frozen_string_literal: true

# Checks that every model reacts to its parameters: each parameter of each library asset is changed and the
# model rebuilt; a parameter that changes nothing must be listed below as descriptive (data for plans and
# schedules) or as a known case where the sample value cannot show the effect. Exit code 1 otherwise.
# Usage: ruby tools/assets/check_params.rb
ENV['SDP_QUIET'] = '1'
ROOT = File.expand_path('../..', __dir__)
require File.join(ROOT, 'sketchup-site-design-pro/src/pb_site_design_pro/core')
include ProjektniBiro::SiteDesignPro # rubocop:disable Style/MixinUsage

# Data only (not 3D shape): power, clearances, volumes, design intent, joint pattern shown by the texture.
DESCRIPTIVE = %w[power serviceClearance jointPattern requiredClearWidth nominalVolume coverageRadius status matureCanopyDiameter
                 matureHeight gradient flowDirection safetyClearance maxSlope modulePower ratedPower excavationMargin liftingClearance
                 installation bikesPerStand].freeze
# [type, parameter] whose effect the change in this check cannot show (value capped, part hidden by another default,
# or the option is drawn the same way) - each was reviewed.
KNOWN = {
  'footingWidth' => :all, 'footingDepth' => :all, # only together (both > 0)
  'postCount' => :all, # capped at 2 by the catalog
  'postSpacing' => :all, 'columnSpacing' => :all, 'riserHeight' => :all, 'spacing' => :all, # same count on the short sample
  'material' => %w[utility.recycling_island utility.trench utility.service_corridor climate.heated_zone decorative.mural], # 2D zone colour
  'height' => %w[utility.recycling_island climate.heated_zone], 'baseOffset' => %w[utility.recycling_island], # zone height
  'roofSlope' => %w[structure.pergola structure.bioclimatic_pergola structure.bus_shelter structure.pavilion], # no cover / flat roof by default
  'coverMaterial' => %w[structure.pergola structure.bioclimatic_pergola], 'rafterSpacing' => %w[structure.retractable_shade],
  'wallMaterial' => %w[structure.gazebo structure.pavilion], 'roofThickness' => %w[structure.gazebo], # no walls; pyramid roof
  'shaftDiameter' => :all, 'accessShaft' => :all, 'coverDepth' => :all, 'shape' => %w[water.rainwater_collector], # above ground by default
  'length' => %w[water.rainwater_collector utility.water_tank], 'width' => %w[water.rainwater_tank utility.water_tank], # round tank: min(l, w)
  'benchWidth' => :all, 'benches' => %w[recreation.table_tennis], 'coverSize' => :all, # no benches by default; cover capped by opening
  'headLength' => %w[lighting.pedestrian_light furniture.outdoor_shower], 'headHeight' => %w[info.speaker], # round heads
  'openingSide' => %w[road.vehicle_barrier wall.access_barrier], 'leafThickness' => %w[road.vehicle_barrier wall.access_barrier], # barrier arm
  'gateType' => %w[road.vehicle_barrier], 'solarPanel' => %w[lighting.solar_light], 'backHeight' => %w[furniture.bench_backless],
  'plantHeight' => %w[landscape.planting_bed landscape.lawn_area], 'plantSpacing' => %w[landscape.lawn_area], 'plantMaterial' => %w[landscape.lawn_area],
  'waterMaterial' => %w[water.rain_garden water.bioswale], 'depth' => %w[recreation.playground_unit], # no water by default; tower depth capped
  'plateShape' => %w[recreation.basketball_hoop info.site_map], 'postHeight' => %w[info.site_map], # backboard; tilted map table
  'columnMaterial' => %w[utility.waste_container_semi], 'columnDiameter' => %w[utility.waste_container_semi], # semi: no column
  'bodyWidth' => %w[utility.waste_container_semi], 'bodyLength' => %w[utility.waste_container_semi] # round body: min(l, w)
}.freeze

lib = AssetLibrary.new(File.join(ROOT, 'sketchup-site-design-pro/src/pb_site_design_pro/Assets'), File.join(ROOT, 'shared/catalog'))
sig = lambda do |id, params|
  c = RecordingCanvas.new
  ElementBuilder.build(lib.element(id, params: params), c, Builders::Context.new(lod: 'presentation'))
  mats = []
  c.each_node { |n| mats << n.material if n.material }
  c.definitions.each_value { |d| q = [d]; until q.empty?; x = q.shift; mats << x.material if x.material; q.concat(x.children); end }
  [c.world_polygons.flat_map { |p| p.is_a?(Hash) ? p[:outer] : p }.map { |q| q.map { |v| v.round(4) } }.hash, mats.uniq.sort]
end
problems = []
checked = 0
lib.assets.each do |a|
  id = a['assetId']
  base = sig.call(id, {})
  lib.parameters(id).each do |p|
    key = p['key']
    next if DESCRIPTIVE.include?(key) || KNOWN[key] == :all || (KNOWN[key].is_a?(Array) && KNOWN[key].include?(id))

    v = p['default']
    alt = case p['type']
          when 'length', 'number', 'angle' then v.is_a?(Numeric) ? (v.zero? ? 0.5 : v * 1.3) : 1.0
          when 'integer' then (v || 1) + 1
          when 'bool' then !v
          when 'enum' then ((p['options'] || []) - [v]).first
          when 'material' then v == 'brick' ? 'concrete' : 'brick'
          end
    next if alt.nil?

    alt = [alt, p['max'].to_f].min if p['max'] && alt.is_a?(Numeric)
    next if alt == v

    checked += 1
    problems << "#{id}: parametar '#{key}' ne mijenja model" if sig.call(id, key => alt) == base
  end
end
puts problems
puts "Parametara provjereno: #{checked}, bez efekta: #{problems.length}"
exit(problems.empty? ? 0 : 1)
