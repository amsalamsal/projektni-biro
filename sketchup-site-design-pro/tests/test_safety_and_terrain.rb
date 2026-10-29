# frozen_string_literal: true

require_relative 'test_helper'
require_relative 'support/fake_sketchup'
%w[settings materials su_canvas model_index scenes importer editing].each do |f|
  require File.join(SdpTest::ROOT, 'sketchup-site-design-pro', 'src', 'pb_site_design_pro', 'sketchup', f)
end

# Backups, recovery journal and terrain surfaces (core and SketchUp adapter against the API fake).
class TestSafetyAndTerrain < Minitest::Test
  include SdpTest
  DICT = 'SiteDesignPro'

  def setup
    @tmp = Dir.mktmpdir
    @model = Sketchup::Model.new
    Sketchup.active_model = @model
    @catalog = CatalogFiles.new(File.join(SHARED, 'catalog'))
  end

  def teardown
    FileUtils.rm_rf(@tmp)
  end

  def import(path, policy: { modified: 'keep', deleted: 'mark' }, backup: true)
    imp = Importer.new(@model, path, catalog: @catalog, lod: 'standard', policy: policy)
    imp.plan!
    [imp, imp.apply!(create_scenes: false, backup: backup)]
  end

  def terrain_groups
    @model.definitions.flat_map { |d| d.instances.to_a }.select { |g| g.valid? && g.get_attribute(DICT, 'terrainId') }
  end

  # ---- backups ----

  def test_backups_are_named_verified_pruned_and_never_touch_the_source
    src = File.join(@tmp, 'Projekat šđč.skp')
    File.write(src, 'model v1')
    now = Time.new(2026, 3, 1, 10, 0, 0)
    mgr = BackupManager.new(keep: 3, max_age_days: 10, clock: -> { now })
    made = 5.times.map do
      path = mgr.backup_file(src, 'Uvoz paketa')
      now += 60
      path
    end
    assert(made.all? { |p| File.basename(p).include?('_uvoz-paketa.skp') })
    left = mgr.list(src)
    assert_equal 3, left.length
    assert_equal made.last, left.first[:path]
    assert_equal 'model v1', File.read(src)

    now += 40 * 86_400
    mgr.prune(src)
    assert_equal 1, mgr.list(src).length, 'the newest backup always survives'
    assert_raises(IOError) { mgr.backup_file(File.join(@tmp, 'nema.skp'), 'x') }
  end

  def test_journal_reports_interrupted_operations_and_survives_a_torn_line
    j = RecoveryJournal.new(File.join(@tmp, 'j.jsonl'))
    a = j.begin_operation('Uvoz', 'plan.skp', 'kopija.skp')
    b = j.begin_operation('Ažuriranje', 'plan.skp', nil)
    j.end_operation(a, 'Completed')
    File.open(j.path, 'a') { |f| f.write('{"OperationId":"tor') }
    open = j.unfinished('PLAN.skp')
    assert_equal [b], open.map { |e| e['OperationId'] }
    j.acknowledge(b, 'pregledano')
    assert_empty j.unfinished
    assert(j.read.any? { |e| e['OperationId'] == b && e['State'] == 'RolledBack' && e['Operation'] == 'Ažuriranje' })
  end

  def test_import_of_a_saved_model_makes_a_backup_and_journal_entries
    @model.path = File.join(@tmp, 'situacija.skp')
    File.write(@model.path, 'saved model')
    imp, report = import(SAMPLE_ZIP)
    assert_equal 0, report.count(:error), report.to_text
    refute_nil imp.backup_path
    assert File.file?(imp.backup_path)
    assert_equal [imp.backup_path], @model.saved_copies
    assert(report.issues.any? { |i| i.code == 'SDP-B002' })
    journal = Safety.journal
    entries = journal.read.select { |e| e['Document'] == @model.path }
    assert_equal %w[Started Completed], entries.map { |e| e['State'] }
    assert_empty journal.unfinished(@model.path)
  end

  def test_unsaved_model_import_reports_that_no_backup_was_possible
    imp, report = import(SAMPLE_ZIP)
    assert_nil imp.backup_path
    assert(report.issues.any? { |i| i.code == 'SDP-B001' })
  end

  def test_failed_import_is_journalled_as_rolled_back
    imp = Importer.new(@model, SAMPLE_ZIP, catalog: @catalog, lod: 'standard', policy: { modified: 'keep', deleted: 'mark' })
    imp.plan!
    def imp.store_project_metadata
      raise 'simulirani prekid'
    end
    report = imp.apply!(create_scenes: false, backup: false)
    assert report.errors?
    entry = Safety.journal.read.reverse.find { |e| e['State'] == 'RolledBack' }
    assert_includes entry['Message'], 'simulirani prekid'
    assert_equal [:abort], @model.operations.last
  end

  # ---- terrain ----

  def test_terrain_reader_rejects_bad_triangles
    rep = Report.new
    bad = JSON.generate('lengthUnit' => 'm', 'surfaces' => [{ 'id' => 't', 'name' => 'T', 'vertices' => [[0, 0, 0], [1, 0, 0], [0, 1, 0]], 'triangles' => [[0, 1, 5]] }])
    assert_raises(PackageError) { TerrainReader.parse(bad, rep) }
    empty = JSON.generate('lengthUnit' => 'm', 'surfaces' => [{ 'id' => 't', 'name' => 'T', 'vertices' => [], 'triangles' => [] }])
    assert_empty TerrainReader.parse(empty, rep)
    assert(rep.issues.any? { |i| i.code == 'SDP-T100' })
  end

  def test_terrain_builds_as_one_open_smooth_mesh_with_package_elevations
    pkg, = PackageReader.read(SAMPLE_ZIP)
    surface = pkg.terrains.first
    canvas = RecordingCanvas.new
    TerrainBuilder.build(surface, canvas, 'terrainId' => surface.id)
    mesh = canvas.find_all { |n| n.kind == :mesh }.first
    assert_equal surface.triangle_count, mesh.polygons.length
    refute mesh.solid
    assert mesh.smooth
    zs = mesh.polygons.flatten(1).map { |p| p[2] }
    assert_in_delta surface.min_z, zs.min, 1e-9
    assert_in_delta surface.max_z, zs.max, 1e-9
  end

  def test_terrain_import_reimport_and_removal_follow_the_policies
    _, report = import(SAMPLE_ZIP)
    assert_equal 1, report.stats['terrains_created']
    groups = terrain_groups
    assert_equal 1, groups.length
    g = groups.first
    assert_equal 'Postojeći teren', g.name
    container = g.parent.instances.first
    assert_equal 'terrain', container.get_attribute(DICT, 'modelGroup')
    assert_equal 'VU Teren', container.layer.name
    refute_nil @model.materials.find { |m| m.get_attribute(DICT, 'materialKey') == 'terrain' }
    # the terrain group is not an element: it must never be planned as a deleted element
    assert_nil(ModelIndex.managed_entities(@model).find { |e| e.get_attribute(DICT, 'terrainId') })

    imp, report = import(SAMPLE_ZIP)
    assert_equal 0, report.stats['terrains_created']
    assert_equal [:unchanged], imp.terrain_plan.map(&:kind)
    assert_equal 1, terrain_groups.length

    dir = File.join(@tmp, 'bez-terena')
    FileUtils.cp_r(SAMPLE_DIR, dir)
    man = JSON.parse(File.read(File.join(dir, 'manifest.json')))
    man.delete('terrain')
    File.write(File.join(dir, 'manifest.json'), JSON.generate(man))
    imp, = import(dir, policy: { modified: 'keep', deleted: 'delete' })
    assert_equal [:delete], imp.terrain_plan.map(&:kind)
    assert_empty terrain_groups
  end

  def test_changed_terrain_updates_and_local_edit_is_kept_by_policy
    import(SAMPLE_ZIP)
    dir = File.join(@tmp, 'teren2')
    FileUtils.cp_r(SAMPLE_DIR, dir)
    t = JSON.parse(File.read(File.join(dir, 'terrain.json')))
    t['surfaces'][0]['vertices'][0][2] += 0.5
    File.write(File.join(dir, 'terrain.json'), JSON.generate(t))

    g = terrain_groups.first
    g.transformation = Geom::Transformation.translation([0, 0, 10]) # local edit in SketchUp
    imp, report = import(dir)
    assert_equal [:conflict], imp.terrain_plan.map(&:kind)
    assert(report.issues.any? { |i| i.code == 'SDP-I014' })

    imp, = import(dir, policy: { modified: 'overwrite', deleted: 'mark' })
    assert_equal [:update], imp.terrain_plan.map(&:kind)
    assert_equal 1, terrain_groups.length
  end
end
