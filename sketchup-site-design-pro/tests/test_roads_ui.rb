# frozen_string_literal: true

require_relative 'test_helper'
require_relative 'support/fake_sketchup'
require_relative 'support/fake_ui'
$LOAD_PATH.unshift(File.join(__dir__, 'support', 'stubs'))
require File.join(SdpTest::ROOT, 'sketchup-site-design-pro', 'src', 'pb_site_design_pro', 'main')

# The road commands driven like a user: the form windows, the drawing tools (clicks in metres) and, when the road
# engine is available (SDP_ROADS_ENGINE_TEST, set by tools/build_and_test.sh), the whole build and import.
class TestRoadsUi < Minitest::Test
  include SdpTest
  R = Roads
  HTML = File.join(ROOT, 'sketchup-site-design-pro', 'src', 'pb_site_design_pro', 'sketchup', 'ui', 'html')

  def setup
    UI.reset!
    @model = Sketchup::Model.new
    Sketchup.active_model = @model
    @view = Sketchup::View.new
    @cmds = Commands.install
    @engine = ENV['SDP_ROADS_ENGINE_TEST'].to_s
    ENV['SDP_ROADS_ENGINE'] = @engine
  end

  def teardown
    ENV.delete('SDP_ROADS_ENGINE')
  end

  def need_engine
    skip 'motor saobraćajnica nije dostupan (SDP_ROADS_ENGINE_TEST)' if @engine.empty?
  end

  def errors
    UI.log.select { |e| e[0] == :messagebox && e[1] =~ /greška|error/i }
  end

  def roads_menu
    UI.menu('Extensions').items.first.items.find { |i| i.respond_to?(:name) && i.name == 'Saobraćajnice' }
  end

  def run_command(text)
    item = roads_menu.items.find { |i| i.respond_to?(:text) && i.text == text }
    assert item, "naredba #{text}"
    item.call
  end

  def form(values = {})
    d = UI.dialogs.last
    assert_equal File.join(HTML, 'roads.html'), d.file
    page = File.read(d.file, encoding: 'UTF-8').scan(/sdp\.call\('(\w+)'/).flatten.uniq.sort
    assert_equal page, (page & d.callbacks.keys).sort
    d.fire('ready')
    data = d.last_render
    refute_empty data['fields']
    v = data['values'].merge(values)
    d.fire('change', v) if data['live']
    d.fire('ok', v)
    v
  end

  def click(tool, x, y)
    tool.onMouseMove(0, Units.m_to_in(x), Units.m_to_in(y), @view)
    tool.onLButtonDown(0, Units.m_to_in(x), Units.m_to_in(y), @view)
  end

  def test_menu_toolbar_and_icons
    items = roads_menu.items.reject { |i| i == :separator }.map(&:text)
    ['Ulica', 'Uredi ulicu', 'Kružni tok', 'Pješački prijelaz', 'Kota saobraćajnice', 'Rampa za invalide', 'Garažna rampa',
     'Presjek rampe', 'Osvježi saobraćajnice', 'Ulica iz odabranih linija'].each { |t| assert_includes items, t }
    roads_menu.items.reject { |i| i == :separator }.each do |c|
      next unless c.small_icon

      assert File.file?(c.small_icon), c.small_icon
      assert File.file?(c.large_icon), c.large_icon
    end
    assert(ObjectSpace.each_object(UI::Toolbar).any? { |t| t.items.length == 11 })
  end

  def test_street_is_drawn_and_kept_even_without_the_engine
    ENV['SDP_ROADS_ENGINE'] = 'nepostojeci-motor-xyz'
    run_command('Ulica')
    form('lanesLeft' => 2, 'sidewalkRight' => 2.5)
    tool = @model.tool
    assert_kind_of R::Tools::PolylineTool, tool
    click(tool, 0, 0)
    click(tool, 40, 0)
    tool.onMouseMove(0, Units.m_to_in(55), 0, @view)
    tool.onUserText('20;1,5', @view) # 20 m on in the direction of the cursor, 1,50 m high
    tool.draw(@view)
    tool.onReturn(@view)
    rec = R::SourceStore.new(@model).records.first
    assert_equal R::STREET, rec.type
    assert_equal 3, rec.geometry['vertices'].length
    assert_in_delta 60.0, rec.geometry['vertices'][2][0], 1e-6
    assert_in_delta 1.5, rec.geometry['vertices'][2][2], 1e-6
    assert_in_delta 2.0, rec.params['lanesLeft']
    assert(UI.log.any? { |e| e[0] == :messagebox && e[1] =~ /izmjena je sačuvana/ })
  end

  def test_whole_network_with_the_engine
    need_engine
    run_command('Ulica')
    form
    t = @model.tool
    click(t, -60, 0)
    click(t, 60, 0)
    t.onReturn(@view)
    assert_empty errors, errors.inspect

    run_command('Ulica')
    form('heights' => 'same', 'height' => 0.0)
    t = @model.tool
    click(t, 0, 0)
    click(t, 0, 50)
    t.onReturn(@view)

    run_command('Pješački prijelaz')
    click(@model.tool, -30, 1)
    form('bikeLane' => true)

    run_command('Garažna rampa')
    form('kind' => 'straight', 'endLevel' => -2.5, 'flatLength' => 5.0)
    click(@model.tool, 30, -6)
    click(@model.tool, 30, -26)

    run_command('Presjek rampe')
    click(@model.tool, 50, -40)

    run_command('Kružni tok')
    form('arms' => 3)
    click(@model.tool, 0, 150)
    assert_empty errors, errors.inspect

    run_command('Osvježi saobraćajnice')
    assert_empty errors, errors.inspect
    types = ModelIndex.managed_entities(@model).map { |e| e.get_attribute('SiteDesignPro', 'type') }
    %w[road.carriageway road.sidewalk road.street_kerb road.garage_ramp_surface road.splitter_island road.roundabout_apron].each do |t|
      assert_includes types, t
    end
    msg = R::Commands.last_message
    assert_match(/kolovoz/, msg)
    st = R::SourceStore.new(@model)
    assert_equal st.signature, st.built_signature

    # Uredi ulicu on the selected axis: three lanes to the right
    g = st.records.find { |r| r.label == 'U-001' }.ref
    @model.selection.clear
    @model.selection.add(g)
    run_command('Uredi ulicu')
    form('lanesRight' => 3)
    assert_in_delta 3.0, R::SourceStore.new(@model).records.find { |r| r.label == 'U-001' }.params['lanesRight']
    assert_empty errors, errors.inspect
  end

  def test_a_ramp_below_the_technical_minimum_is_refused
    need_engine
    run_command('Garažna rampa')
    form('kind' => 'straight', 'endLevel' => -4.0)
    click(@model.tool, 0, 0)
    click(@model.tool, 0, -12) # 4 m over 12 m: 33 %
    assert_equal [:abort], @model.operations.last(1).map(&:first), 'the change is undone (SketchUp abort_operation)'
    assert(UI.log.any? { |e| e[0] == :messagebox && e[1] =~ /tehničkog minimuma/ && e[1] =~ /uzdužni nagib/ }, UI.log.inspect)

    # a fresh model (the test fake does not undo an aborted operation)
    @model = Sketchup::Model.new
    Sketchup.active_model = @model
    run_command('Garažna rampa')
    form('kind' => 'straight', 'endLevel' => -1.0)
    click(@model.tool, 0, 0)
    click(@model.tool, 0, -12) # 1 m over 12 m: well below 17 % in every part
    assert_equal [:commit], @model.operations.last(1).map(&:first), UI.log.inspect
  end
end
