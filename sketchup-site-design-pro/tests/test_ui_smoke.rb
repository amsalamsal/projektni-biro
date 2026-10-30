# frozen_string_literal: true

require_relative 'test_helper'
require_relative 'support/fake_sketchup'
require_relative 'support/fake_ui'
$LOAD_PATH.unshift(File.join(__dir__, 'support', 'stubs'))
require File.join(SdpTest::ROOT, 'sketchup-site-design-pro', 'src', 'pb_site_design_pro', 'main')

# Loads the whole extension the way SketchUp does (main.rb) and drives every menu command and every dialog
# callback against the fake API: catches missing methods, wrong names between pages and Ruby, data that
# cannot be sent to a page, and errors that would reach SketchUp.
class TestUiSmoke < Minitest::Test
  include SdpTest
  DICT = 'SiteDesignPro'
  HTML = File.join(ROOT, 'sketchup-site-design-pro', 'src', 'pb_site_design_pro', 'sketchup', 'ui', 'html')
  SAMPLE = File.join(SHARED, 'samples', 'residential-complex.sdpkg')

  def setup
    UI.reset!
    @model = Sketchup::Model.new
    Sketchup.active_model = @model
    @cmds = Commands.install
  end

  def dialog
    UI.dialogs.last
  end

  def errors
    UI.log.select { |e| e[0] == :messagebox && e[1] =~ /greška|nije uspjel|error/i }
  end

  # every sdp.call('name') used by a page must have a Ruby callback (and vice versa)
  def page_calls(file)
    File.read(File.join(HTML, file), encoding: 'UTF-8').scan(/sdp\.call\('(\w+)'/).flatten.uniq.sort
  end

  def test_menu_and_toolbar_are_complete
    menu = UI.menu('Extensions').items.first
    assert_equal 'Vanjsko uređenje PRO', menu.name
    texts = menu.items.reject { |i| i == :separator }.map { |i| i.is_a?(Array) ? i[0] : i.text }
    ['Uvoz paketa...', 'Biblioteka 3D modela...', 'Biblioteka i postavke...', 'Svojstva elementa'].each { |t| assert_includes texts, t }
    @cmds.each_value do |c|
      next unless c.small_icon

      assert File.file?(c.small_icon), "ikona #{c.small_icon}"
      assert File.file?(c.large_icon), "ikona #{c.large_icon}"
    end
  end

  def test_toolbar_is_shown_on_first_install
    tb = ObjectSpace.each_object(UI::Toolbar).find { |t| t.items.length == 9 }
    assert tb.visible?, 'toolbar never shown before must be shown'
  end

  def test_commands_without_model_data_report_instead_of_failing
    %i[update properties export scenes underground report recovery].each do |k|
      UI.log.clear
      @cmds[k].call
      assert_empty errors, "#{k}: #{errors.inspect}"
    end
    assert_equal MF_GRAYED, @cmds[:properties].validation.call
  end

  def test_import_dialog_preview_replan_and_apply
    Dialogs.import(SAMPLE)
    d = dialog
    assert_equal page_calls('import.html'), (page_calls('import.html') & d.callbacks.keys).sort
    d.fire('ready')
    data = d.last_render
    assert_equal 'preview', data['mode']
    d.fire('replan', 'lod' => 'conceptual', 'modifiedPolicy' => 'keep', 'deletedPolicy' => 'mark')
    d.fire('apply', 'lod' => 'standard', 'createScenes' => true, 'backup' => false, 'modifiedPolicy' => 'keep', 'deletedPolicy' => 'mark')
    assert_operator ModelIndex.managed_entities(@model).length, :>, 100
    assert_empty errors, errors.inspect
    # second time: update path, then the report dialog
    @cmds[:report].call
    assert_empty errors, errors.inspect
  end

  def test_properties_dialog_edits_an_imported_element
    Dialogs.import(SAMPLE)
    dialog.fire('ready')
    dialog.fire('apply', 'lod' => 'standard', 'createScenes' => false, 'backup' => false, 'modifiedPolicy' => 'keep', 'deletedPolicy' => 'mark')
    bench = ModelIndex.managed_entities(@model).find { |e| e.get_attribute(DICT, 'type') == 'furniture.bench' }
    @model.selection.add([bench])
    assert_equal MF_ENABLED, @cmds[:properties].validation.call
    @cmds[:properties].call
    d = dialog
    assert_equal page_calls('properties.html'), (page_calls('properties.html') & d.callbacks.keys).sort
    d.fire('ready')
    d.fire('apply', 'edits' => { 'length' => 2.2 })
    data = d.last_render
    assert_empty data['messages'].select { |m| m['severity'] == 'error' }
    d.fire('revert')
    d.fire('selectType')
    d.fire('close')
  end

  def test_settings_library_dialog
    @cmds[:library].call
    d = dialog
    assert_equal page_calls('library.html'), (page_calls('library.html') & d.callbacks.keys).sort
    d.fire('ready')
    d.fire('saveSettings', 'lod' => 'presentation', 'favorites' => ['furniture.bench'])
    assert_equal 'presentation', Settings.get('lod')
    d.fire('clearComponent', 'type' => 'furniture.bench')
    d.fire('mapComponent', 'type' => 'furniture.bench') # cancelled file dialog
    d.fire('selectType', 'type' => 'furniture.bench')
  end

  def test_model_library_dialog_insert_replace_and_tool
    @cmds[:models].call
    d = dialog
    assert_equal page_calls('models.html'), (page_calls('models.html') & d.callbacks.keys).sort
    d.fire('ready')
    assert_equal 'index', d.last_render['mode']
    assert_equal 14, d.last_render['folders'].length
    d.fire('details', 'id' => 'furniture.bench')
    assert_equal 'details', d.last_render['mode']

    # point model: insert tool, rotate, click twice
    d.fire('insert', 'id' => 'furniture.bench', 'params' => { 'length' => 2.0 }, 'shape' => {})
    tool = @model.tool
    assert_kind_of InsertTool, tool
    view = @model.active_view
    tool.activate
    tool.onMouseMove(0, 100, 200, view)
    tool.draw(view)
    assert tool.onKeyDown(VK_LEFT, 1, 0, view)
    tool.onUserText('90', view)
    tool.onLButtonDown(0, 100, 200, view)
    tool.onMouseMove(0, 300, 200, view)
    tool.onLButtonDown(0, 300, 200, view)
    benches = ModelIndex.managed_entities(@model).select { |e| e.get_attribute(DICT, 'type') == 'furniture.bench' }
    assert_equal 2, benches.length
    assert_in_delta Math::PI / 2, benches.first.get_attribute(DICT, 'rotation'), 1e-9
    assert_in_delta 2.0, JSON.parse(benches.first.get_attribute(DICT, 'params'))['length'], 1e-9
    assert_in_delta Units.in_to_m(100), benches.first.get_attribute(DICT, 'position')[0], 1e-9

    # polygon model without a selected face: tool with a default outline
    d.fire('insert', 'id' => 'water.swimming_pool', 'params' => {}, 'shape' => { 'width' => 6, 'depth' => 3 })
    pool_tool = @model.tool
    pool_tool.activate
    pool_tool.onMouseMove(0, 0, 0, view)
    pool_tool.onLButtonDown(0, 0, 0, view)
    pool = ModelIndex.managed_entities(@model).find { |e| e.get_attribute(DICT, 'type') == 'water.swimming_pool' }
    refute_nil pool
    g = PackageReader.parse_geometry(JSON.parse(pool.get_attribute(DICT, 'geometry')))
    assert_equal [6.0, 3.0, 0.0], g[:outer][:vertices][2].map { |v| v.round(9) }

    # replace: nothing selected -> message, then a library bench -> backless bench
    @model.selection.clear
    d.fire('replace', 'id' => 'furniture.chair', 'params' => {})
    assert_equal 'error', d.last_render['kind']
    @model.selection.add([benches.first])
    d.fire('replace', 'id' => 'furniture.bench_backless', 'params' => {})
    assert_equal 'ok', d.last_render['kind'], d.last_render['text']

    d.fire('importDae', 'id' => 'furniture.bench')
    d.fire('openFolder', 'id' => 'furniture.bench')
    d.fire('selectType', 'id' => 'furniture.bench')
    d.fire('details', 'id' => 'no.such.type')
    assert_equal 'error', d.last_render['kind']
    d.fire('close')
    assert d.closed?
  end

  def test_insert_inside_an_edited_group_uses_local_coordinates
    shift = Units.m_to_in(10.0)
    @model.define_singleton_method(:edit_transform) { Geom::Transformation.translation(Geom::Vector3d.new(shift, 0.0, 0.0)) }
    view = @model.active_view
    tool = InsertTool.new('furniture.bench', {}, {})
    tool.activate
    tool.onMouseMove(0, Units.m_to_in(12.0), 0, view) # world x = 12 m, group origin at 10 m
    tool.onLButtonDown(0, 0, 0, view)
    bench = ModelIndex.managed_entities(@model).first
    assert_in_delta 2.0, bench.get_attribute(DICT, 'position')[0], 1e-9
  end

  def test_every_library_asset_can_be_inserted_with_the_tool
    view = @model.active_view
    AssetInsert.library.assets.each do |a|
      tool = InsertTool.new(a['assetId'], {}, {})
      tool.activate
      tool.onMouseMove(0, 0, 0, view)
      tool.draw(view)
      tool.onLButtonDown(0, 0, 0, view)
    end
    assert_empty UI.log.select { |e| e[0] == :messagebox }, UI.log.inspect[0, 500]
    assert_equal AssetInsert.library.assets.length, ModelIndex.managed_entities(@model).length
  end
end
