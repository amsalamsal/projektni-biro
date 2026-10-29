# frozen_string_literal: true

require_relative 'test_helper'

class TestSampleImport < Minitest::Test
  include SdpTest

  def setup
    @pkg, @rep = PackageReader.read(SAMPLE_ZIP)
  end

  def test_sample_builds_completely_at_all_lods
    %w[conceptual standard presentation].each do |lod|
      canvas, ctx = build_all(@pkg, lod: lod)
      assert_equal 0, ctx.report.count(:error), ctx.report.to_text
      assert_equal @pkg.elements.length, canvas.top_level.length
      assert_empty canvas.manifold_errors, lod
    end
  end

  def test_sample_dimensions_positions_and_elevations
    canvas, = build_all(@pkg)
    nodes = canvas.top_level.zip(@pkg.elements).to_h { |n, e| [e.id, n] }
    building = @pkg.elements.find { |e| e.name == 'Building A' }
    min, max = canvas.bounds(nodes[building.id])
    assert_equal [15.0, 45.0, 0.15], min.map { |v| v.round(6) }
    assert_equal [55.0, 63.0, 18.15], max.map { |v| v.round(6) }

    street_lights = @pkg.elements.select { |e| e.type_id == 'lighting.street_light' }
    assert_equal 5, street_lights.length
    street_lights.each do |e|
      lmin, lmax = canvas.bounds(nodes[e.id])
      assert_in_delta 33.5, e.geometry[:position][1], 1e-9
      assert_operator lmin[1], :<, 33.5 - 1.0, 'luminaire arm points toward the road (south)'
      assert_in_delta 8.0, lmax[2], 0.01
    end

    containers = @pkg.elements.select { |e| e.type_id.start_with?('utility.waste_container_ug') }
    assert_equal 4, containers.length
    containers.each do |e|
      cmin, = canvas.bounds(nodes[e.id])
      assert_in_delta 0.15 - e.num('bodyDepth'), cmin[2], 1e-9, 'underground body keeps its burial depth'
    end

    channel = @pkg.elements.find { |e| e.type_id == 'water.drainage_channel' }
    cmin, cmax = canvas.bounds(nodes[channel.id])
    assert_in_delta 0.25, cmax[2], 1e-9
    assert_in_delta(-0.30 - 0.25, cmin[2], 1e-9)
  end

  def test_sample_reuses_component_definitions
    canvas, = build_all(@pkg)
    stats = canvas.stats
    assert_operator stats[:instance], :>, 300
    assert_operator stats[:definitions], :<, 40
  end

  def test_sample_review_items_travel_in_the_package
    zip = ZipReader.new(SAMPLE_ZIP)
    report = JSON.parse(zip.read_text('preview/validation-report.json'))
    assert_equal 0, report['errors']
    assert_equal 12, report['reviews']
  end

  def test_large_project_builds_with_shared_definitions
    specs = Array.new(5000) do |i|
      type = %w[landscape.tree_deciduous lighting.pedestrian_light furniture.bench landscape.shrub road.bollard][i % 5]
      { type: type, geometry: { 'kind' => 'point', 'position' => [(i % 70) * 6.0, (i / 70) * 6.0, 0.0], 'rotation' => (i % 8) * Math::PI / 4 } }
    end
    pkg, = package(specs)
    t = Time.now
    canvas, ctx = build_all(pkg)
    elapsed = Time.now - t
    assert_equal 0, ctx.report.count(:error)
    assert_equal 5000, canvas.stats[:instance]
    assert_equal 5, canvas.definitions.length
    assert_operator elapsed, :<, 30, "core build of 5000 elements took #{elapsed.round(2)} s"
  end
end

class TestChangeSet < Minitest::Test
  include SdpTest

  def change_set
    ChangeSet.new(project_id: 'p', package_id: 'k', origin: [1.0, 2.0, 3.0])
  end

  def test_moves_rotations_and_parameters
    cs = change_set
    imported = { position: [1.0, 2.0, 0.0], rotation: 0.0 }
    assert_nil cs.add(id: 'a', type_id: 't', base_content_hash: 'h', imported: imported, current: { position: [1.0004, 2.0, 0.0], rotation: 0.00001 })
    moved = cs.add(id: 'b', type_id: 't', base_content_hash: 'h', imported: imported, current: { position: [3.0, 2.0, 0.0], rotation: Math::PI })
    assert_equal [3.0, 2.0, 0.0], moved.position
    assert_in_delta Math::PI, moved.rotation, 1e-9
    turned = cs.add(id: 'c', type_id: 't', base_content_hash: 'h', imported: imported, current: { position: [1.0, 2.0, 0.0], rotation: -Math::PI / 2 })
    assert_nil turned.position
    assert_in_delta 1.5 * Math::PI, turned.rotation, 1e-9
    params = cs.add(id: 'd', type_id: 't', base_content_hash: 'h', params: { 'length' => 2.4 })
    assert_equal({ 'length' => 2.4 }, params.parameters)
    doc = cs.to_h
    assert_equal 'site-design-pro-changes', doc['format']
    assert_equal 3, doc['changes'].length
    assert_equal [1.0, 2.0, 3.0], doc['coordinateSystem']['origin']
  end

  def test_scale_and_polygon_edits_are_reported
    cs = change_set
    cs.add(id: 'a', type_id: 't', base_content_hash: 'h', imported: { position: [0, 0, 0], rotation: 0 },
           current: { position: [5.0, 0, 0], rotation: 0, scaled: true })
    cs.add(id: 'b', type_id: 't', base_content_hash: 'h', kind: 'polygon', geometry_modified: true)
    codes = cs.report.issues.map(&:code)
    assert_includes codes, 'SDP-X010'
    assert_includes codes, 'SDP-X011'
    assert_equal 1, cs.changes.length
  end

  def test_catalog_files_model_groups
    cat = CatalogFiles.new(File.join(SHARED, 'catalog'))
    assert_equal 'Planting and vegetation', cat.model_group('planting').name
    assert_equal 'SDP Underground', cat.model_group('underground').tag
    assert_equal 'Future group', cat.model_group('future_group').name
    assert cat.materials.key?('water')
  end
end
