# frozen_string_literal: true

require_relative 'test_helper'
require_relative 'support/fake_sketchup'
%w[settings materials su_canvas model_index scenes importer editing].each do |f|
  require File.join(SdpTest::ROOT, 'sketchup-site-design-pro', 'src', 'pb_site_design_pro', 'sketchup', f)
end

# End-to-end adapter workflows (import, re-import, update, conflicts, deletion, edits, reverse export)
# against the in-memory fake SketchUp API in tests/support/fake_sketchup.rb.
class TestSketchupAdapter < Minitest::Test
  include SdpTest
  DICT = 'SiteDesignPro'

  def setup
    @model = Sketchup::Model.new
    Sketchup.active_model = @model
    @catalog = CatalogFiles.new(File.join(SHARED, 'catalog'))
    @tmp = Dir.mktmpdir
  end

  def teardown
    FileUtils.rm_rf(@tmp)
  end

  def import(path, **opts)
    imp = Importer.new(@model, path, catalog: @catalog, lod: opts.fetch(:lod, 'standard'),
                                     policy: opts.fetch(:policy, { modified: 'keep', deleted: 'mark' }))
    imp.plan!
    report = imp.apply!(create_scenes: opts.fetch(:scenes, false))
    [imp, report]
  end

  def managed
    ModelIndex.managed_entities(@model)
  end

  def find(id)
    managed.find { |e| e.get_attribute(DICT, 'id') == id }
  end

  # Writes a modified copy of the sample package (folder form) and returns its path.
  def modified_sample
    dir = File.join(@tmp, 'pkg')
    FileUtils.cp_r(SAMPLE_DIR, dir)
    man = JSON.parse(File.read(File.join(dir, 'manifest.json')))
    geo = JSON.parse(File.read(File.join(dir, 'geometry.json')))
    yield man, geo
    File.write(File.join(dir, 'manifest.json'), JSON.generate(man))
    File.write(File.join(dir, 'geometry.json'), JSON.generate(geo))
    dir
  end

  def test_first_import_builds_organised_model
    _, report = import(SAMPLE_ZIP, scenes: true)
    assert_equal 0, report.count(:error), report.to_text
    assert_equal 139, report.stats['created']
    assert_equal 139, managed.length
    root = @model.entities.grep(Sketchup::Group).find { |g| g.get_attribute(DICT, 'role') == 'projectRoot' }
    assert_equal 'Sample Residential Complex (Site Design Pro)', root.name
    containers = root.entities.grep(Sketchup::Group)
    names = containers.map(&:name)
    assert_includes names, 'Planting and vegetation'
    assert_includes names, 'Underground utilities'
    containers.each { |c| assert c.layer&.name&.start_with?('SDP '), "#{c.name} is tagged" }
    assert_equal 10, @model.pages.length
    under = @model.pages['SDP 08 Underground utilities']
    assert under.xray
    assert_equal false, under.layer_visibility['SDP Surfaces']
    assert_equal true, under.layer_visibility['SDP Underground']
    assert(@model.materials.any? { |m| m.name == 'SDP Lawn' })
    assert_equal [6_550_000.0, 4_860_000.0, 520.0], root.get_attribute(DICT, 'origin')
    assert_equal [[:start, 'Site Design Pro import'], [:commit]], @model.operations
  end

  def test_units_conversion_positions_in_inches
    import(SAMPLE_ZIP)
    light = managed.find { |e| e.get_attribute(DICT, 'type') == 'lighting.street_light' }
    pos = JSON.parse(JSON.generate(light.get_attribute(DICT, 'position')))
    assert_in_delta Units.m_to_in(pos[0]), light.transformation.origin.x, 1e-6
    assert_in_delta Units.m_to_in(33.5), light.transformation.origin.y, 1e-6
    tree = managed.find { |e| e.get_attribute(DICT, 'type') == 'landscape.tree_deciduous' }
    bb = tree.bounds
    assert_in_delta Units.m_to_in(7.0), bb.max.z - bb.min.z, 1e-6, 'tree height in SketchUp inches'
  end

  def test_faces_on_ground_plane_are_oriented_up
    import(SAMPLE_ZIP)
    bay = managed.find { |e| e.get_attribute(DICT, 'type') == 'road.parking_bay' }
    faces = bay.definition.entities.grep(Sketchup::Group).flat_map { |g| g.entities.grep(Sketchup::Face) }
    bottoms = faces.select { |f| f.points.all? { |p| p.z.abs < 1e-9 } }
    refute_empty bottoms
    assert(bottoms.all? { |f| f.normal.z < 0 }, 'bottom faces point down')
    tops = faces.select { |f| f.points.all? { |p| p.z > 0.1 } }
    assert(tops.all? { |f| f.normal.z > 0 }, 'top faces point up')
  end

  def test_repeated_import_creates_no_duplicates
    import(SAMPLE_ZIP)
    count = managed.length
    entities = @model.definitions.sum { |d| d.instances.length }
    imp, report = import(SAMPLE_ZIP)
    refute imp.plan.changes?
    assert_equal 139, report.stats['unchanged']
    assert_equal count, managed.length
    assert_equal entities, @model.definitions.sum { |d| d.instances.length }
  end

  def test_update_add_delete_and_unrelated_geometry_preserved
    import(SAMPLE_ZIP)
    user = @model.entities.add_group
    user.name = 'My own sketch'
    bench = JSON.parse(File.read(File.join(SAMPLE_DIR, 'manifest.json')))['elements'].find { |e| e['type'] == 'furniture.bench' }
    removed = JSON.parse(File.read(File.join(SAMPLE_DIR, 'manifest.json')))['elements'].find { |e| e['type'] == 'furniture.waste_bin' }
    new_id = SecureRandom.uuid
    dir = modified_sample do |man, geo|
      e = man['elements'].find { |x| x['id'] == bench['id'] }
      e['parameters']['length'] = 2.4
      e['contentHash'] = 'f' * 64
      man['elements'].reject! { |x| x['id'] == removed['id'] }
      geo['elements'].reject! { |x| x['id'] == removed['id'] }
      man['elements'] << e.merge('id' => new_id, 'label' => 'FB-099')
      geo['elements'] << geo['elements'].find { |g| g['id'] == bench['id'] }.merge('id' => new_id, 'position' => [70.0, 41.5, 0.15])
    end
    imp, report = import(dir)
    assert_equal 1, report.stats['updated']
    assert_equal 1, report.stats['created']
    assert_equal 1, report.stats['marked_deleted']
    updated = find(bench['id'])
    assert_in_delta Units.m_to_in(2.4), updated.definition.bounds.max.x - updated.definition.bounds.min.x, 1e-6
    gone = find(removed['id'])
    assert gone.get_attribute(DICT, 'deletedInSource')
    assert_equal 'SDP Deleted in source', gone.layer.name
    refute gone.layer.visible?
    assert user.valid?
    assert_includes @model.entities.to_a, user
    assert_equal 140, managed.length
    assert imp.plan.changes?
  end

  def test_local_edit_is_kept_and_conflicts_are_reported
    import(SAMPLE_ZIP)
    bench = managed.find { |e| e.get_attribute(DICT, 'type') == 'furniture.bench' }
    id = bench.get_attribute(DICT, 'id')
    bench.transformation = Geom::Transformation.translation(Geom::Vector3d.new(100, 0, 0)) * bench.transformation
    imp, report = import(SAMPLE_ZIP)
    assert_equal 1, report.stats['kept_local']
    refute imp.plan.changes?

    dir = modified_sample do |man, _geo|
      e = man['elements'].find { |x| x['id'] == id }
      e['contentHash'] = 'e' * 64
    end
    _, report = import(dir)
    assert_equal 1, report.stats['conflicts']
    assert(report.issues.any? { |i| i.code == 'SDP-I010' && i.element_id == id })
    moved_x = find(id).transformation.origin.x

    _, report = import(dir, policy: { modified: 'overwrite', deleted: 'mark' })
    assert_equal 1, report.stats['updated']
    refute_in_delta moved_x, find(id).transformation.origin.x, 1e-6, 'overwrite restores the AutoCAD position'
  end

  def test_copies_made_in_sketchup_are_detached
    import(SAMPLE_ZIP)
    tree = managed.find { |e| e.get_attribute(DICT, 'type') == 'landscape.tree_evergreen' }
    copy = tree.parent.entities.add_instance(tree.definition, Geom::Transformation.translation(Geom::Vector3d.new(500, 0, 0)) * tree.transformation)
    tree.attribute_dictionary(DICT).each { |k, v| copy.set_attribute(DICT, k, v) }
    _, report = import(SAMPLE_ZIP)
    assert_equal 1, report.stats['detached']
    assert_nil copy.get_attribute(DICT, 'id')
    assert tree.get_attribute(DICT, 'id')
  end

  def test_delete_policy_removes_entities
    import(SAMPLE_ZIP)
    bin = managed.find { |e| e.get_attribute(DICT, 'type') == 'furniture.waste_bin' }
    id = bin.get_attribute(DICT, 'id')
    dir = modified_sample do |man, geo|
      man['elements'].reject! { |x| x['id'] == id }
      geo['elements'].reject! { |x| x['id'] == id }
    end
    _, report = import(dir, policy: { modified: 'keep', deleted: 'delete' })
    assert_equal 1, report.stats['deleted']
    assert_nil find(id)
    refute bin.valid?
  end

  def test_parameter_edit_in_sketchup_and_reverse_export
    import(SAMPLE_ZIP)
    bench = managed.find { |e| e.get_attribute(DICT, 'type') == 'furniture.bench' }
    id = bench.get_attribute(DICT, 'id')
    info = Editing.describe(@model, bench)
    assert_equal 'Bench with backrest', info['typeName']
    assert(info['parameters'].any? { |p| p['key'] == 'length' && (p['value'] - 1.8).abs < 1e-9 })

    entity, rep = Editing.apply_parameters(@model, bench, { 'length' => '2.2', 'backrest' => 'false' }, catalog: @catalog)
    refute rep.errors?, rep.to_text
    assert_same bench, entity, 'point elements keep their instance'
    assert_in_delta Units.m_to_in(2.2), bench.definition.bounds.max.x - bench.definition.bounds.min.x, 1e-6
    assert_equal({ 'length' => 2.2, 'backrest' => false }, JSON.parse(bench.get_attribute(DICT, 'localParams')))
    assert Editing.describe(@model, bench)['locallyModified']

    _, bad = Editing.apply_parameters(@model, bench, { 'length' => 99.0 }, catalog: @catalog)
    assert bad.errors?

    light = managed.find { |e| e.get_attribute(DICT, 'type') == 'lighting.street_light' }
    light.transformation = Geom::Transformation.translation(Geom::Vector3d.new(Units.m_to_in(2.0), 0, 0)) * light.transformation
    cs = Editing.collect_changes(@model, '%s' % managed.first.get_attribute(DICT, 'projectId'))
    by_id = cs.changes.to_h { |c| [c.id, c] }
    assert_equal({ 'length' => 2.2, 'backrest' => false }, by_id[id].parameters)
    lc = by_id[light.get_attribute(DICT, 'id')]
    start = light.get_attribute(DICT, 'position')
    assert_in_delta start[0] + 2.0, lc.position[0], 1e-6
    assert_in_delta start[1], lc.position[1], 1e-6
    assert_equal 2, cs.changes.length
    doc = JSON.parse(cs.to_json)
    assert_equal 'site-design-pro-changes', doc['format']
  end

  def test_area_element_parameter_edit_rebuilds_group
    import(SAMPLE_ZIP)
    paving = managed.find { |e| e.get_attribute(DICT, 'type') == 'surface.concrete_paving' }
    id = paving.get_attribute(DICT, 'id')
    before = paving.bounds.min.z
    entity, rep = Editing.apply_parameters(@model, paving, { 'thickness' => 0.2 }, catalog: @catalog)
    refute rep.errors?, rep.to_text
    refute paving.valid?
    assert_equal id, entity.get_attribute(DICT, 'id')
    assert_in_delta before - Units.m_to_in(0.12), entity.bounds.min.z, 1e-6
    assert_equal 1, managed.count { |e| e.get_attribute(DICT, 'id') == id }
  end

  def test_failed_import_is_rolled_back
    imp = Importer.new(@model, SAMPLE_ZIP, catalog: @catalog)
    imp.plan!
    def imp.store_project_metadata
      raise 'simulated failure'
    end
    report = imp.apply!(create_scenes: false)
    assert_equal [:abort], @model.operations.last
    assert(report.issues.any? { |i| i.code == 'SDP-I999' })
  end

  def test_component_reuse_across_imports
    import(SAMPLE_ZIP)
    defs = @model.definitions.count { |d| d.get_attribute(DICT, 'definitionKey') }
    other = modified_sample do |man, geo|
      man['project']['id'] = '99999999-9999-4999-8999-999999999999'
      man['project']['name'] = 'Second project'
      ids = man['elements'].to_h { |e| [e['id'], SecureRandom.uuid] }
      man['elements'].each { |e| e['id'] = ids[e['id']] }
      geo['elements'].each { |g| g['id'] = ids[g['id']] }
    end
    import(other)
    assert_equal defs, @model.definitions.count { |d| d.get_attribute(DICT, 'definitionKey') }, 'second project reuses all definitions'
    assert_equal 278, managed.length
    roots = @model.entities.grep(Sketchup::Group).select { |g| g.get_attribute(DICT, 'role') == 'projectRoot' }
    assert_equal 2, roots.length
  end
end
