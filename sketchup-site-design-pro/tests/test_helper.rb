# frozen_string_literal: true

require 'minitest/autorun'

ENV['SDP_QUIET'] = '1' # adapter log lines go to the log file only
require 'json'
require 'securerandom'
require 'tmpdir'
require 'fileutils'
require_relative '../src/pb_site_design_pro/core'

module SdpTest
  include ProjektniBiro::SiteDesignPro

  ROOT = File.expand_path('../..', __dir__)
  SHARED = File.join(ROOT, 'shared')
  SAMPLE_ZIP = File.join(SHARED, 'samples', 'residential-complex.sdpkg')
  SAMPLE_DIR = File.join(SHARED, 'samples', 'residential-complex')

  module_function

  def catalog_json(name)
    JSON.parse(File.read(File.join(SHARED, 'catalog', name), encoding: 'UTF-8'))
  end

  def builders
    @builders ||= catalog_json('builders.json')['builders']
  end

  def types
    @types ||= catalog_json('element-types.json')['types']
  end

  def materials
    @materials ||= catalog_json('materials.json')['materials']
  end

  # Type definition JSON (as written by the AutoCAD exporter) for a catalog type id.
  def type_definition(type_id)
    t = types.find { |x| x['id'] == type_id } or raise "unknown type #{type_id}"
    b = builders.fetch(t['builder'])
    defaults = t['defaults'] || {}
    params = b['parameters'].map do |p|
      p = p.dup
      p['default'] = defaults[p['key']] if defaults.key?(p['key'])
      p
    end
    { 'id' => t['id'], 'name' => t['name'], 'category' => t['category'], 'subcategory' => t['subcategory'],
      'builder' => t['builder'], 'geometry' => b['geometry'], 'modelGroup' => t['modelGroup'] || 'planting', 'parameters' => params }
  end

  def default_geometry(kind)
    case kind
    when 'point' then { 'kind' => 'point', 'position' => [0.0, 0.0, 0.0], 'rotation' => 0.3 }
    when 'path' then { 'kind' => 'path', 'vertices' => [[0.0, 0.0, 0.0], [10.0, 0.0, 0.0], [10.0, 8.0, 0.5]], 'bulges' => [0.0, 0.2], 'closed' => false }
    else { 'kind' => 'polygon', 'outer' => { 'vertices' => [[0.0, 0.0, 0.0], [8.0, 0.0, 0.0], [8.0, 5.0, 0.0], [0.0, 5.0, 0.0]] } }
    end
  end

  # Builds manifest/geometry JSON strings. specs: [{ type:, id:, geometry:, parameters:, hash:, label: }]
  def package_strings(specs, project_id: '11111111-1111-4111-8111-111111111111', package_id: nil)
    used = specs.map { |s| s[:type] }.uniq
    tdefs = used.map { |t| type_definition(t) }
    elements = specs.map do |s|
      td = tdefs.find { |t| t['id'] == s[:type] }
      h = { 'id' => s[:id] || SecureRandom.uuid, 'type' => s[:type], 'category' => td['category'], 'subcategory' => td['subcategory'],
            'builder' => td['builder'], 'name' => td['name'], 'revision' => 1, 'contentHash' => s[:hash] || ('0' * 64),
            'modelGroup' => td['modelGroup'], 'parameters' => s[:parameters] || {} }
      h['label'] = s[:label] if s[:label]
      h
    end
    geoms = specs.zip(elements).map do |s, e|
      geom = (s[:geometry] || default_geometry(type_definition(s[:type])['geometry'])).dup
      geom.merge('id' => e['id'])
    end
    manifest = {
      'format' => 'site-design-pro-package', 'schemaVersion' => '1.0.0', 'packageId' => package_id || SecureRandom.uuid,
      'createdUtc' => '2026-01-01T00:00:00Z', 'generator' => { 'application' => 'test', 'version' => '0' },
      'project' => { 'id' => project_id, 'name' => 'Test' },
      'units' => { 'length' => 'm', 'angle' => 'rad', 'sourceLengthUnit' => 'm', 'sourceUnitsToMeters' => 1.0 },
      'coordinateSystem' => { 'origin' => [0.0, 0.0, 0.0], 'trueNorthDegrees' => 0.0 },
      'catalog' => { 'id' => 'sdp-core', 'version' => '1.0.0' },
      'typeDefinitions' => tdefs,
      'materials' => materials,
      'elements' => elements
    }
    geometry = { 'schemaVersion' => '1.0.0', 'lengthUnit' => 'm', 'elements' => geoms }
    [JSON.generate(manifest), JSON.generate(geometry)]
  end

  def package(specs, **opts)
    pkg, report = PackageReader.read_strings(*package_strings(specs, **opts))
    raise "package invalid: #{report.to_text}" unless pkg

    [pkg, report]
  end

  def context(pkg, lod: 'standard')
    Builders::Context.new(lod: lod, materials: pkg.materials)
  end

  # Builds every element; returns [canvas, context].
  def build_all(pkg, lod: 'standard')
    ctx = context(pkg, lod: lod)
    canvas = RecordingCanvas.new
    pkg.elements.each { |e| ElementBuilder.build(e, canvas, ctx) }
    [canvas, ctx]
  end

  def bounds_of(canvas, node)
    canvas.bounds(node)
  end
end
