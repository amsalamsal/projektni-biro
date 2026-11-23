# frozen_string_literal: true

require_relative 'test_helper'
require_relative 'support/fake_sketchup'
require_relative 'support/fake_ui'
$LOAD_PATH.unshift(File.join(__dir__, 'support', 'stubs'))
require File.join(SdpTest::ROOT, 'sketchup-site-design-pro', 'src', 'pb_site_design_pro', 'main')
require 'tmpdir'
require 'fileutils'

# The roads both ways between SketchUp and AutoCAD (Roads::Exchange). The engine-based tests need
# SDP_ROADS_ENGINE_TEST (set by tools/build_and_test.sh).
class TestRoadsExchange < Minitest::Test
  include SdpTest
  R = Roads
  X = Roads::Exchange
  AUTOCAD_PROJECT = '3b8e8c1e-5a8f-4b6e-9d1c-2f7a6e0c4d11'

  def setup
    UI.reset!
    @model = Sketchup::Model.new
    Sketchup.active_model = @model
    @engine = ENV['SDP_ROADS_ENGINE_TEST'].to_s
    ENV['SDP_ROADS_ENGINE'] = @engine
    @tmp = Dir.mktmpdir('sdp-razmjena')
  end

  def teardown
    ENV.delete('SDP_ROADS_ENGINE')
    FileUtils.rm_rf(@tmp)
  end

  def need_engine
    skip 'motor saobraćajnica nije dostupan (SDP_ROADS_ENGINE_TEST)' if @engine.empty?
  end

  def path(*xyz)
    { 'kind' => 'path', 'vertices' => xyz.each_slice(3).map { |v| v.map(&:to_f) }, 'closed' => false }
  end

  def draw_streets(model)
    st = R::SourceStore.new(model)
    st.add(type: R::STREET, params: {}, geometry: path(-60, 0, 0, 60, 0, 0), label: 'U-001')
    st.add(type: R::STREET, params: {}, geometry: path(0, 0, 0, 0, 60, 3), label: 'U-002')
    st
  end

  def street(model, label)
    R::SourceStore.new(model).records.find { |r| r.label == label }
  end

  # ---- without the engine

  def test_road_elements_are_told_apart
    assert X.source_element?(R::STREET, {})
    refute X.source_element?(R::STREET, R::DERIVED_FLAG => true)
    assert X.road_element?('road.carriageway', {})
    assert X.road_element?('surface.asphalt', R::DERIVED_FLAG => true)
    refute X.road_element?('surface.asphalt', {})
  end

  def test_a_package_of_another_project_is_not_linked
    pkg = Package.new(path: 'x', generator: { 'host' => 'AutoCAD' }, project: { 'id' => AUTOCAD_PROJECT }, elements: [], assets: [])
    refute X.linked?(@model, pkg)
    assert_nil X.take_roads!(@model, pkg)
    @model.set_attribute(R::SourceStore::SETTINGS, 'linkedProjectId', AUTOCAD_PROJECT)
    assert X.linked?(@model, pkg)
    pkg2 = Package.new(path: 'x', generator: { 'host' => R::ENGINE_HOST }, project: { 'id' => AUTOCAD_PROJECT }, elements: [], assets: [])
    refute X.linked?(@model, pkg2), 'the packages of the road engine are imported as they are'
  end

  def test_library_elements_go_to_autocad_where_they_are_now
    g = @model.entities.add_group
    g.transformation = Geom::Transformation.translation(Geom::Vector3d.new(Units.m_to_in(5), Units.m_to_in(7), Units.m_to_in(0.5))) *
                       Geom::Transformation.rotation(Geom::Point3d.new(0, 0, 0), Geom::Vector3d.new(0, 0, 1), 0.3)
    { 'id' => '0f8b1c3e-2d4a-4e6b-9a1c-5d7e9f0a1b2c', 'type' => 'furniture.bench', 'source' => 'library',
      'params' => '{"length":2.0}', 'localParams' => '{"length":2.4}', 'label' => 'K-1' }.each { |k, v| g.set_attribute('SiteDesignPro', k, v) }
    other = @model.entities.add_group # of another AutoCAD project: not sent
    { 'id' => '1f8b1c3e-2d4a-4e6b-9a1c-5d7e9f0a1b2c', 'type' => 'furniture.bench', 'source' => 'package', 'projectId' => AUTOCAD_PROJECT }.each { |k, v| other.set_attribute('SiteDesignPro', k, v) }
    recs = X.other_elements(@model, nil, Geom::Transformation.new)
    assert_equal 1, recs.length
    h = recs.first.to_request
    assert_equal 'furniture.bench', h['type']
    assert_in_delta 2.4, h['parameters']['length'], 1e-9, 'parameters changed here'
    pos = h['geometry']['position']
    assert_in_delta 5.0, pos[0], 1e-6
    assert_in_delta 7.0, pos[1], 1e-6
    assert_in_delta 0.5, pos[2], 1e-6
    assert_in_delta 0.3, h['geometry']['rotation'], 1e-6
    assert_equal 2, X.other_elements(@model, AUTOCAD_PROJECT, Geom::Transformation.new).length, 'the elements of the target project go along'
  end

  def test_only_what_was_erased_here_is_offered_for_erasing_in_autocad
    st = draw_streets(@model)
    u1 = street(@model, 'U-001')
    mark = 'aaaaaaaa-1111-4222-8333-444444444444'
    X.remember_known(@model, [u1.id, street(@model, 'U-002').id, mark])
    X.remember_absorbed(@model, mark, u1.id, 1) # a mark from AutoCAD that went into the height of the axis
    st.erase(street(@model, 'U-002').ref)
    recs = R::SourceStore.new(@model).records
    back = X.absorbed_marks(@model, recs, Geom::Transformation.new)
    assert_equal [mark], back.map(&:id), 'the absorbed mark goes back under its own id'
    assert_equal R::LEVEL_MARK, back.first.type
    sent = recs.map(&:id) + back.map(&:id)
    assert_equal [X.known_ids(@model) - sent].flatten.length, 1, 'only the street erased here'
  end

  def test_rebuilding_the_roads_removes_only_road_elements
    tree = @model.entities.add_group
    { 'id' => 't1', 'type' => 'landscape.tree_evergreen', 'projectId' => AUTOCAD_PROJECT }.each { |k, v| tree.set_attribute('SiteDesignPro', k, v) }
    road = @model.entities.add_group
    { 'id' => 'r1', 'type' => 'road.carriageway', 'projectId' => AUTOCAD_PROJECT }.each { |k, v| road.set_attribute('SiteDesignPro', k, v) }
    entry = ->(g, id) { ModelEntry.new(id: id, project_id: AUTOCAD_PROJECT, type_id: g.get_attribute('SiteDesignPro', 'type'), ref: g) }
    plan = Plan.new([PlanAction.new(:delete, nil, entry.call(tree, 't1'), 'x'), PlanAction.new(:delete, nil, entry.call(road, 'r1'), 'x')], {})
    kept = X.only_road_removals(plan)
    assert_equal ['r1'], kept.actions.map { |a| a.entry.id }, 'the trees of the linked AutoCAD project stay when the roads are rebuilt'
  end

  def test_export_needs_roads
    assert_raises(R::Engine::Error) { X.export(@model, File.join(@tmp, 'x.sdpkg')) }
  end

  # ---- with the engine

  def test_sketchup_to_autocad_and_back
    need_engine
    st = draw_streets(@model)
    refute_nil R::Rebuild.run(@model)
    pid = st.settings['projectId']

    # SketchUp -> AutoCAD: the source elements, the generated ones flagged
    file = File.join(@tmp, 'saobracajnice.sdpkg')
    result = X.export(@model, file)
    assert_equal true, result['ok']
    pkg, rep = PackageReader.read(file)
    assert pkg, rep.to_text
    assert_equal R::ENGINE_HOST, pkg.generator['host']
    assert_equal pid, pkg.project_id
    sources = pkg.elements.select { |e| X.source_element?(e.type_id, e.custom) }
    assert_equal %w[U-001 U-002], sources.select { |e| e.type_id == R::STREET }.map(&:label).sort
    assert(sources.any? { |e| e.type_id == R::LEVEL_MARK }, 'heights of U-002 as elevation marks')
    assert(pkg.elements.any? { |e| e.type_id == 'road.carriageway' })
    assert(st.records.all? { |r| r.ref.get_attribute(R::SourceStore::DICT, X::IN_AUTOCAD) == true })

    # AutoCAD -> SketchUp: a drawing of another project with these roads (the package as AutoCAD writes it: the
    # sources and the network it drew from them)
    folder = File.join(@tmp, 'autocad')
    FileUtils.cp_r(File.join(R::Rebuild.engine(pid).work_dir, 'paket'), folder)
    manifest = File.join(folder, 'manifest.json')
    m = JSON.parse(File.read(manifest, encoding: 'UTF-8'))
    m['generator']['host'] = 'AutoCAD 2021'
    m['project']['id'] = AUTOCAD_PROJECT
    # as AutoCAD writes them: the streets of the network carry no 3D of their own (networkSurface)
    m['elements'].each { |e| (e['custom'] ||= {})['networkSurface'] = true if e['type'] == R::STREET }
    File.write(manifest, JSON.generate(m))

    # first the import without taking the roads over (as before), then the automatic takeover in another model
    c = Sketchup::Model.new
    Sketchup.active_model = c
    auto = Importer.new(c, folder)
    auto.plan!
    rep_auto = auto.apply!(create_scenes: false, backup: false, set_north: false)
    assert(rep_auto.issues.any? { |i| i.code == 'SDP-S004' }, rep_auto.to_text)
    assert_empty X.candidates(c), 'the roads of AutoCAD are taken over on import'
    assert_equal %w[U-001 U-002], R::SourceStore.new(c).records.select { |r| r.type == R::STREET }.map(&:label).sort

    Settings.set('autoAdoptRoads', false)
    b = Sketchup::Model.new
    Sketchup.active_model = b
    imp = Importer.new(b, folder)
    imp.plan!
    imp.apply!(create_scenes: false, backup: false, set_north: false)
    Settings.set('autoAdoptRoads', true)
    assert_nil imp.road_items, 'not linked yet: an ordinary import'
    roots = X.candidates(b)
    assert_equal 1, roots.length
    stats, outcome = X.adopt(b, roots.first[0])
    assert outcome, 'the roads are built by the engine'
    assert_equal 2, stats['added']
    assert_equal 2, stats['heights'], 'the marks on the axis of U-002 go back into it'
    assert_equal AUTOCAD_PROJECT, b.get_attribute(R::SourceStore::SETTINGS, 'linkedProjectId')
    u2 = street(b, 'U-002')
    assert_in_delta 3.0, u2.geometry['vertices'][1][2], 1e-6
    left = ModelIndex.managed_entities(b).select { |e| e.get_attribute('SiteDesignPro', 'projectId') == AUTOCAD_PROJECT }
    assert(left.none? { |e| X.road_entity?(e) }, 'the AutoCAD 3D of the roads is gone')
    assert(ModelIndex.managed_entities(b).any? { |e| e.get_attribute('SiteDesignPro', 'type') == 'road.carriageway' })

    # changed in AutoCAD: U-001 has two lanes to the left, U-002 (and its marks) erased
    u2_id = u2.id
    m['elements'].each { |e| e['parameters']['lanesLeft'] = 2.0 if e['label'] == 'U-001' }
    gone = m['elements'].select { |e| e['id'] == u2_id || e['type'] == R::LEVEL_MARK }.map { |e| e['id'] }
    m['elements'].reject! { |e| gone.include?(e['id']) }
    File.write(manifest, JSON.generate(m))
    gpath = File.join(folder, 'geometry.json')
    g = JSON.parse(File.read(gpath, encoding: 'UTF-8'))
    g['elements'].reject! { |e| gone.include?(e['id']) }
    File.write(gpath, JSON.generate(g))

    imp = Importer.new(b, folder)
    imp.plan!
    report = imp.apply!(create_scenes: false, backup: false, set_north: false)
    refute_nil imp.road_items
    assert(report.issues.any? { |i| i.code == 'SDP-S001' }, report.to_text)
    recs = R::SourceStore.new(b).records
    assert_equal ['U-001'], recs.select { |r| r.type == R::STREET }.map(&:label)
    assert_in_delta 2.0, street(b, 'U-001').params['lanesLeft']
    left = ModelIndex.managed_entities(b).select { |e| e.get_attribute('SiteDesignPro', 'projectId') == AUTOCAD_PROJECT }
    assert(left.none? { |e| X.road_entity?(e) }, 'the roads of a linked project are not imported as AutoCAD 3D')

    # and back to AutoCAD: under the AutoCAD project
    back = File.join(@tmp, 'nazad.sdpkg')
    X.export(b, back)
    pkg, = PackageReader.read(back)
    assert_equal AUTOCAD_PROJECT, pkg.project_id
    assert_equal ['U-001'], pkg.elements.select { |e| e.type_id == R::STREET }.map(&:label)
  end
end
