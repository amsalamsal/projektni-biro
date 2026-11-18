# frozen_string_literal: true

require_relative 'test_helper'
require_relative 'support/fake_sketchup'
require_relative 'support/fake_ui'
%w[settings materials su_canvas model_index scenes importer editing roads/store roads/rebuild].each do |f|
  require File.join(SdpTest::ROOT, 'sketchup-site-design-pro', 'src', 'pb_site_design_pro', 'sketchup', f)
end

# The road tools without AutoCAD (Saobraćajnice): cross-section rules, source elements in the model, the request to
# the road engine and the import of what it builds.
class TestRoads < Minitest::Test
  include SdpTest
  R = Roads
  DICT = 'SDP_Road'

  def setup
    @model = Sketchup::Model.new
    Sketchup.active_model = @model
    @store = R::SourceStore.new(@model)
  end

  # ---- cross-section

  def test_street_spec_clamps_like_autocad_and_measures_the_section
    s = R::StreetSpec.new('lanesLeft' => 5, 'laneWidth' => '3,25', 'sidewalkLeft' => 7, 'bikeRight' => 1.5, 'cornerRadius' => 0.5)
    notes = s.normalize!
    assert_equal 3, s['lanesLeft']
    assert_in_delta 3.25, s['laneWidth']
    assert_in_delta 5.0, s['sidewalkLeft']
    assert_in_delta 1.0, s['cornerRadius']
    assert_equal 3, notes.length
    assert_in_delta 3 * 3.25, s.carriageway(true)
    assert_in_delta 3.25 + 0.15 + 1.5 + 1.6 + 0.10, s.extent(:edge, false)
    assert_match(/dvosmjerna, 3 \+ 1 traka × 3,25 m/, s.describe)

    one = R::StreetSpec.new('direction' => 'one_way', 'lanesRight' => 2, 'median' => 4)
    one.normalize!
    assert_in_delta 3.0, one.carriageway(true) # one-way: the axis in the middle, no median
    assert_equal 0, one.lanes_left
  end

  def test_relative_heights_and_numbers
    assert_in_delta 1.0, R.to_relative(501.0, 500.0)
    assert_in_delta(-1.0, R.to_relative(-1.0, 500.0))
    assert_in_delta 501.0, R.to_relative(501.0, nil)
    assert_in_delta 2.5, R.parse_number('2,5 m')
    assert_nil R.parse_number('abc')
    assert_equal '−1,50', R.level_text(-1.5)
    assert_equal '2 trake', R.count(2, 'traka', 'trake', 'traka')
  end

  def test_ramp_fillet_crossing_and_square_to_road
    pts, bulges = R::Ramp.fillet_path([[0, 0], [20, 0], [20, 20]], 8)
    assert_equal 4, pts.length
    assert_in_delta Math.tan(Math::PI / 8), bulges[1], 1e-9
    assert_in_delta 12.0, pts[1][0], 1e-9

    p, notes = R::Ramp.normalize('kind' => 'semicircular', 'laneWidth' => 3.0, 'margin' => 0.1, 'direction' => 'two_way', 'island' => true)
    assert_in_delta 3.5, p['laneWidth']
    assert_in_delta 0.25, p['margin']
    refute p['island']
    assert_in_delta 7.5, p['width']
    refute_empty notes

    ring = R::Plan.crossing_ring([0, 0], [1, 0], 2, 3.0, 4.0)
    assert_equal 4, ring.length
    assert_in_delta 4.0, Geometry.dist(ring[0], ring[1]) # first edge along the road
    assert_in_delta 5.5, Geometry.dist(ring[1], ring[2]) # 6 bars 0,50 m with 5 gaps over 6 m

    road = { axis: [[-50, 0, 0], [50, 0, 0]], closed: false, half: 4.85 }
    n = R::Plan.square_to_road([5, -6], [road])
    assert_in_delta 1.0, n[1].abs
    q = R::Plan.pull([5, -6, 0], [5.5, -16, 0], n)
    assert_in_delta 5.0, q[0], 1e-9
    assert_nil R::Plan.square_to_road([5, -20], [road])
  end

  # ---- source elements in the model

  def street(points, params = {})
    @store.add(type: R::STREET, params: R::StreetSpec.new(params).to_params, geometry: R::Geo.path(points))
  end

  def test_sources_round_trip_with_moves_copies_and_edited_vertices
    g = street([[10, 0, 0], [60, 0, 1.5], [60, 40, 1.5]], 'lanesLeft' => 2)
    assert_equal 'U-001', g.get_attribute(DICT, 'label')
    recs = @store.records
    assert_equal 1, recs.length
    v = recs[0].geometry['vertices']
    assert_in_delta 60.0, v[1][0], 1e-6
    assert_in_delta 1.5, v[1][2], 1e-6
    assert_in_delta 2.0, recs[0].params['lanesLeft']

    # the group moved 5 m east (SketchUp Move): the axis moves with it
    sig = @store.signature
    g.transformation = Geom::Transformation.translation(Geom::Vector3d.new(Units.m_to_in(15), 0, 0))
    refute_equal sig, @store.signature
    assert_in_delta 65.0, @store.records[0].geometry['vertices'][1][0], 1e-6

    # a copy gets its own id and label
    copy = @store.root.entities.add_group
    g.attribute_dictionary(DICT).each { |k, val| copy.set_attribute(DICT, k, val) }
    g.entities.grep(Sketchup::Edge).each { |e| copy.entities.add_edges(e.points) }
    recs = @store.records
    assert_equal 2, recs.length
    refute_equal recs[0].id, recs[1].id
    assert_equal 'U-001k', recs[1].label

    # a vertex moved inside the group (also up): the axis follows the edges
    copy.erase!
    copy.parent.entities.remove(copy) if copy.parent.respond_to?(:entities)
    edges = g.entities.grep(Sketchup::Edge)
    moved = Geom::Point3d.new(Units.m_to_in(50), Units.m_to_in(10), Units.m_to_in(3))
    old = edges[0].points[1]
    edges.each { |e| e.points.map! { |p| p.to_a == old.to_a ? moved : p } }
    v = @store.records.first.geometry['vertices']
    assert_equal 3, v.length
    assert_in_delta 15 + 50.0, v[1][0], 1e-4 # the group origin is at 15 m, the vertex moved to 50 m from it
    assert_in_delta 3.0, v[1][2], 1e-4
    assert_in_delta 15.0, v[0][0], 1e-4 # the first vertex is the group origin
  end

  def test_update_changes_parameters_and_keeps_the_place
    g = street([[0, 0, 0], [40, 0, 0]])
    g.transformation = Geom::Transformation.translation(Geom::Vector3d.new(Units.m_to_in(3), Units.m_to_in(4), 0))
    rec = @store.update(g, params: R::StreetSpec.new('lanesRight' => 3).to_params)
    assert_in_delta 3.0, rec.params['lanesRight']
    v = @store.records.first.geometry['vertices']
    assert_in_delta 3.0, v[0][0], 1e-6
    assert_in_delta 43.0, v[1][0], 1e-6
    assert_in_delta 4.0, v[1][1], 1e-6
  end

  def test_request_for_the_engine_flat_terrain_and_containers
    street([[0, 0, 0], [50, 0, 2.0]])
    box = @store.add_container('Kružni tok 1')
    box.transformation = Geom::Transformation.translation(Geom::Vector3d.new(Units.m_to_in(100), 0, 0))
    @store.add(type: R::LEVEL_MARK, params: { 'level' => 1.0 }, geometry: R::Geo.point([10, 0, 1.0]), parent: box)
    recs = @store.records
    mark = recs.find { |r| r.type == R::LEVEL_MARK }
    assert_in_delta 10.0, mark.geometry['position'][0], 1e-6 # stored relative to the container, read in the model frame

    req = R::Request.build(recs, project_id: 'p', project_name: 'Test', base: 500.0)
    assert_equal 'sdp-roads-request', req['format']
    assert_equal 2, req['elements'].length
    assert_in_delta 500.0, req['base']
    assert_equal req['elements'][0]['id'], req['elements'][0]['geometry']['id']
    flat = R::Request.build(recs, project_id: 'p', flat: true)
    assert_equal [R::STREET], flat['elements'].map { |e| e['type'] }
    assert(flat['elements'][0]['geometry']['vertices'].all? { |q| q[2].zero? })
  end

  def test_nearest_road_and_half_width
    street([[0, 0, 0], [100, 0, 0]], 'sidewalkLeft' => 2.0)
    @store.add(type: 'road.driveway', params: { 'width' => 3.5 }, geometry: R::Geo.path([[50, 0, 0], [50, -30, 0]]))
    rec, hit = @store.nearest_road([20, 3, 0])
    assert_equal R::STREET, rec.type
    assert_in_delta 3.0, hit[:dist]
    assert_in_delta 3.0 + 0.15 + 2.0 + 0.1, R::SourceStore.half_width(rec)
    rec, = @store.nearest_road([51, -20, 0])
    assert_equal 'road.driveway', rec.type
  end

  # ---- engine and import

  class StubEngine
    def initialize(folder)
      @folder = folder
    end
    attr_reader :request

    def build(request)
      @request = request
      [JSON.parse(File.read(File.join(@folder, 'result.json'), encoding: 'UTF-8')), @folder]
    end
  end

  def sample_package
    dir = ENV['SDP_ROADS_SAMPLE'].to_s
    skip 'SDP_ROADS_SAMPLE (paket motora saobraćajnica iz build_and_test.sh) nije zadan' if dir.empty? || !File.file?(File.join(dir, 'result.json'))
    dir
  end

  def test_rebuild_imports_the_engine_package_and_draws_the_sections
    dir = sample_package
    street([[0, 0, 0], [50, 0, 0]])
    engine = StubEngine.new(dir)
    out = R::Rebuild.run(@model, engine: engine)
    assert out, 'rebuild ran'
    assert_equal 1, engine.request['elements'].length
    assert_equal 0, out.report.count(:error), out.report.to_text
    assert out.report.stats['created'] > 20, out.report.stats.inspect
    types = ModelIndex.managed_entities(@model).map { |e| e.get_attribute('SiteDesignPro', 'type') }
    assert_includes types, 'road.carriageway'
    assert_includes types, 'road.garage_ramp_surface'
    overlay = @model.entities.grep(Sketchup::Group).find { |g| g.get_attribute(DICT, 'role') == R::Rebuild::OVERLAY_ROLE }
    assert overlay, 'ramp texts and the section are drawn'
    assert_equal @store.signature, @store.built_signature
    assert_match(/kolovoz/, R::Rebuild.message(out))

    # the same package again: nothing to change
    again = R::Rebuild.run(@model, engine: engine)
    assert_equal 0, again.report.stats['created']
    assert_equal 0, again.report.stats['updated']
  end

  def test_engine_command_and_missing_engine
    ENV['SDP_ROADS_ENGINE'] = ''
    assert_nil R::Engine.default_command(Dir.mktmpdir)
    e = R::Engine.new(nil, Dir.mktmpdir)
    refute e.available?
    assert_raises(R::Engine::Error) { e.build({}) }
    ENV['SDP_ROADS_ENGINE'] = 'dotnet /x/sdp-ceste.dll'
    assert_equal ['dotnet', '/x/sdp-ceste.dll'], R::Engine.default_command('/nowhere')
  ensure
    ENV.delete('SDP_ROADS_ENGINE')
  end
end
