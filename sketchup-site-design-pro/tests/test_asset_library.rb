# frozen_string_literal: true

require_relative 'test_helper'
require_relative 'support/fake_sketchup'
require 'rexml/document'
%w[settings materials su_canvas model_index editing asset_insert].each do |f|
  require File.join(SdpTest::ROOT, 'sketchup-site-design-pro', 'src', 'pb_site_design_pro', 'sketchup', f)
end

# The 3D model library shipped with the extension (Assets/): completeness against the catalog and the
# specification inventory, file integrity, element construction and insertion/replacement in SketchUp.
class TestAssetLibrary < Minitest::Test
  include SdpTest
  DICT = 'SiteDesignPro'
  ASSETS = File.join(ROOT, 'sketchup-site-design-pro', 'src', 'pb_site_design_pro', 'Assets')
  NOT_MODELS = %w[zone circle_zone outline marker massing].freeze

  def lib
    @lib ||= AssetLibrary.new(ASSETS, File.join(SHARED, 'catalog'))
  end

  def test_fourteen_categories_with_assets
    assert_equal 14, lib.folders.length
    lib.folders.each { |f| assert(lib.assets.any? { |a| a['folder'] == f.id }, "kategorija #{f.name} nema modela") }
  end

  def test_every_object_type_has_a_model_and_preview
    expected = types.reject { |t| NOT_MODELS.include?(t['builder']) && !Models.for(t['id']) }.map { |t| t['id'] } - %w[road.swept_path_placeholder]
    assert_equal expected.sort, lib.assets.map { |a| a['assetId'] }.sort
    assert_empty lib.missing_files
    lib.assets.each do |a|
      refute_equal 'generic', a.dig('model', 'kind'), "#{a['assetId']} ima samo generički prikaz"
      %w[assetId name category subcategory units insertionPoint dimensions source license status].each { |k| refute_nil a[k], "#{a['assetId']}: #{k}" }
    end
  end

  def test_specification_inventory_is_covered
    spec = JSON.parse(File.read(File.join(SHARED, 'assets', 'spec-objects.json'), encoding: 'UTF-8'))
    missing = spec['categories'].flat_map { |c| c['objects'].flat_map { |o| o['assets'].reject { |id| lib.asset(id) }.map { |id| "#{o['name']} -> #{id}" } } }
    assert_empty missing
  end

  def test_dae_files_are_valid_collada_with_existing_textures
    lib.assets.each do |a|
      path = lib.file(a['assetId'], 'model')
      doc = REXML::Document.new(File.read(path))
      assert_equal 'COLLADA', doc.root.name
      assert_equal 'meter', REXML::XPath.first(doc, '//unit').attributes['name']
      assert_equal 'Z_UP', REXML::XPath.first(doc, '//up_axis').text
      REXML::XPath.each(doc, '//init_from') do |n|
        next unless n.text.to_s.include?('.')

        assert File.file?(File.expand_path(n.text, File.dirname(path))), "#{a['assetId']}: tekstura #{n.text}"
      end
    end
  end

  def test_every_asset_builds_closed_solids
    lib.assets.each do |a|
      element = lib.element(a['assetId'])
      canvas = RecordingCanvas.new
      ctx = Builders::Context.new(lod: 'standard')
      refute_nil ElementBuilder.build(element, canvas, ctx), a['assetId']
      assert_empty ctx.report.issues.select { |i| i.severity == :error }.map(&:message), a['assetId']
      assert_empty canvas.manifold_errors, a['assetId']
    end
  end

  def test_default_geometry_follows_origin_and_rotation
    g = lib.default_geometry('cycling.bike_path', [10.0, 5.0, 2.0], Math::PI / 2, length: 8.0)
    assert_equal 'path', g[:kind]
    assert_in_delta 10.0, g[:vertices][1][0], 1e-9
    assert_in_delta 13.0, g[:vertices][1][1], 1e-9
    assert_in_delta 2.0, g[:vertices][1][2], 1e-9
    g = lib.default_geometry('water.swimming_pool', [0.0, 0.0, 0.0], 0.0, width: 10.0, depth: 5.0)
    assert_equal [10.0, 5.0, 0.0], g[:outer][:vertices][2]
    assert_equal({ kind: 'point', position: [1.0, 2.0, 3.0], rotation: 0.5 }, lib.default_geometry('furniture.bench', [1, 2, 3], 0.5))
  end

  def test_unknown_parameters_are_ignored_and_known_ones_applied
    e = lib.element('furniture.bench', params: { 'length' => 2.4, 'teleport' => true })
    assert_in_delta 2.4, e.params['length'], 1e-12
    refute e.params.key?('teleport')
  end

  def test_index_data_for_the_dialog
    d = lib.index_data('furniture.bench' => 3)
    assert_equal 14, d['folders'].length
    assert_equal lib.assets.length, d['folders'].sum { |f| f['count'] }
    bench = d['assets'].find { |a| a['assetId'] == 'furniture.bench' }
    assert_equal 3, bench['inModel']
    assert bench['hasModel']
    assert File.file?(File.expand_path(bench['preview'], File.join(ROOT, 'sketchup-site-design-pro', 'src', 'pb_site_design_pro', 'sketchup', 'ui', 'html')))
  end

  def build_bounds(id, params)
    c = RecordingCanvas.new
    ElementBuilder.build(lib.element(id, params: params), c, Builders::Context.new(lod: 'standard'))
    [c.bounds, c]
  end

  def test_unread_mounting_height_lifts_the_model
    base, = build_bounds('cycling.repair_station', {})
    lifted, = build_bounds('cycling.repair_station', 'baseOffset' => 0.5)
    assert_in_delta base[0][2] + 0.5, lifted[0][2], 1e-9
    assert_in_delta base[1][2] + 0.5, lifted[1][2], 1e-9
  end

  def test_unread_dimensions_scale_the_model_proportionally
    t = TypeDef.new(lib.type_definition('cycling.repair_station'))
    d = t.defaults['depth']
    base, = build_bounds('cycling.repair_station', {})
    wide, = build_bounds('cycling.repair_station', 'depth' => d * 2)
    assert_in_delta (base[1][1] - base[0][1]) * 2, wide[1][1] - wide[0][1], 1e-6
    assert_in_delta base[1][2], wide[1][2], 1e-9, 'height unchanged'
  end

  def test_unread_material_choice_replaces_the_default_material
    _, c = build_bounds('cycling.pump', 'material' => 'brick')
    mats = []
    c.each_node { |n| mats << n.material if n.material }
    c.definitions.each_value { |dn| q = [dn]; until q.empty?; x = q.shift; mats << x.material if x.material; q.concat(x.children); end }
    assert_includes mats, 'brick'
  end

  # ---- SketchUp side (fake API) ----

  def setup
    @model = Sketchup::Model.new
    Sketchup.active_model = @model
    AssetInsert.instance_variable_set(:@library, lib)
  end

  def test_insert_creates_a_managed_library_element
    element = lib.element('furniture.bench', geometry: lib.default_geometry('furniture.bench', [5.0, 1.0, 0.0], 0.3))
    ent = AssetInsert.build(@model, element, @model.entities)
    assert_equal 'furniture.bench', ent.get_attribute(DICT, 'type')
    assert_equal 'library', ent.get_attribute(DICT, 'source')
    assert_equal [5.0, 1.0, 0.0], ent.get_attribute(DICT, 'position')
    refute_nil Editing.type_definition(@model, 'furniture.bench'), 'type definition stored for the properties dialog'
    assert Editing.managed?(ent)
    assert_equal 'furniture.bench', @model.layers.to_a.map(&:name).include?(CatalogFiles.new(File.join(SHARED, 'catalog')).model_group(element.model_group).tag) ? 'furniture.bench' : nil
  end

  def test_polygon_asset_uses_the_selected_face_geometry_when_inserted
    g = { kind: 'polygon', outer: { vertices: [[0.0, 0.0, 0.0], [6.0, 0.0, 0.0], [6.0, 3.0, 0.0], [0.0, 3.0, 0.0]], bulges: [0.0] * 4 }, holes: [] }
    ent = AssetInsert.build(@model, lib.element('surface.concrete_paving', geometry: g), @model.entities)
    stored = PackageReader.parse_geometry(JSON.parse(ent.get_attribute(DICT, 'geometry')))
    assert_equal g[:outer][:vertices], stored[:outer][:vertices]
  end

  def test_replace_swaps_a_library_element_and_keeps_its_place
    element = lib.element('furniture.bench', geometry: lib.default_geometry('furniture.bench', [2.0, 3.0, 0.0], 0.0))
    ent = AssetInsert.build(@model, element, @model.entities)
    @model.selection.add([ent])
    n, skipped = AssetInsert.replace('furniture.bench_backless', {})
    assert_equal 1, n
    assert_empty skipped
    swapped = ModelIndex.managed_entities(@model).find { |e| e.get_attribute(DICT, 'type') == 'furniture.bench_backless' }
    refute_nil swapped
    assert_in_delta 2.0, swapped.get_attribute(DICT, 'position')[0], 1e-6
    assert_nil(ModelIndex.managed_entities(@model).find { |e| e.get_attribute(DICT, 'type') == 'furniture.bench' })
  end

  def test_replace_refuses_elements_from_autocad_packages
    element = lib.element('furniture.bench', geometry: lib.default_geometry('furniture.bench', [0.0, 0.0, 0.0], 0.0))
    ent = AssetInsert.build(@model, element, @model.entities)
    ent.set_attribute(DICT, 'source', nil)
    ent.set_attribute(DICT, 'projectId', 'p1')
    @model.selection.add([ent])
    n, skipped = AssetInsert.replace('furniture.chair', {})
    assert_equal 0, n
    assert_equal 1, skipped.length
    assert_match(/AutoCAD/, skipped.first)
  end

  def test_import_dae_uses_the_library_file
    AssetInsert.import_dae('furniture.bench')
    assert_match(%r{Assets/Furniture_Recreation/furniture.bench/model.dae\z}, @model.imported.first)
  end
end
