# frozen_string_literal: true

require_relative 'test_helper'

class TestBuilders < Minitest::Test
  include SdpTest

  def build_one(type, parameters: {}, geometry: nil, lod: 'standard')
    pkg, = package([{ type: type, parameters: parameters, geometry: geometry }])
    canvas, ctx = build_all(pkg, lod: lod)
    node = canvas.top_level.first
    [canvas, node, ctx, pkg.elements.first]
  end

  def bbox(canvas, node)
    canvas.bounds(node).map { |v| v.map { |x| x.round(6) } }
  end

  def test_every_builder_is_registered
    assert_equal builders.keys.sort, Builders.ids & builders.keys.sort
    assert_empty builders.keys - Builders.ids
  end

  def test_every_catalog_type_builds_closed_solids_at_every_lod
    %w[conceptual standard presentation].each do |lod|
      pkg, rep = package(types.map { |t| { type: t['id'] } })
      refute rep.errors?, rep.to_text
      canvas, ctx = build_all(pkg, lod: lod)
      errors = ctx.report.issues.select { |i| i.severity == :error }
      assert_empty errors, "#{lod}: #{errors.map(&:to_s).join("\n")}"
      assert_equal types.length, canvas.top_level.length, "#{lod}: every type produces one element node"
      assert_empty canvas.manifold_errors, "#{lod}: #{canvas.manifold_errors.first(5).join("\n")}"
      canvas.top_level.each { |n| refute_nil canvas.bounds(n), "#{n.name} has no geometry" }
    end
  end

  def test_tree_dimensions
    canvas, node, = build_one('landscape.tree_deciduous', parameters: { 'height' => 10.0, 'canopyDiameter' => 6.0 },
                                                             geometry: { 'kind' => 'point', 'position' => [5.0, 7.0, 1.5], 'rotation' => 0.0 })
    min, max = bbox(canvas, node)
    assert_in_delta 1.5, min[2], 1e-6       # stands on its insertion elevation
    assert_in_delta 11.5, max[2], 1e-6      # overall height
    assert_in_delta 6.0, max[0] - min[0], 1e-6
    # organic crown: exact envelope, centred on the trunk within 2 % of the canopy diameter
    assert_in_delta 5.0, (max[0] + min[0]) / 2, 0.12
    assert_in_delta 7.0, (max[1] + min[1]) / 2, 0.12
  end

  def test_conical_and_conceptual_trees_keep_overall_size
    upright = { 'kind' => 'point', 'position' => [0.0, 0.0, 0.0], 'rotation' => 0.0 }
    canvas, node, = build_one('landscape.tree_evergreen', parameters: { 'height' => 9.0, 'canopyDiameter' => 4.0 }, geometry: upright)
    min, max = bbox(canvas, node)
    assert_in_delta 9.0, max[2] - min[2], 1e-6
    assert_in_delta 4.0, max[0] - min[0], 0.05
    canvas, node, = build_one('landscape.tree_deciduous', parameters: { 'height' => 9.0, 'canopyDiameter' => 4.0 }, lod: 'conceptual')
    min, max = bbox(canvas, node)
    assert_in_delta 9.0, max[2] - min[2], 1e-6
  end

  def test_identical_points_share_one_component_definition
    pkg, = package(Array.new(50) { |i| { type: 'landscape.tree_deciduous', geometry: { 'kind' => 'point', 'position' => [i * 6.0, 0.0, 0.0], 'rotation' => 0.0 } } } +
                   [{ type: 'landscape.tree_deciduous', parameters: { 'height' => 12.0 } }])
    canvas, = build_all(pkg)
    assert_equal 51, canvas.stats[:instance]
    assert_equal 2, canvas.definitions.length
  end

  def test_rotation_is_applied_about_the_insertion_point
    geometry = { 'kind' => 'point', 'position' => [10.0, 20.0, 0.0], 'rotation' => Math::PI / 2 }
    canvas, node, = build_one('furniture.bench', parameters: { 'length' => 2.0, 'depth' => 0.6 }, geometry: geometry)
    min, max = bbox(canvas, node)
    assert_in_delta 0.6, max[0] - min[0], 1e-6 # length now runs along world Y
    assert_in_delta 2.0, max[1] - min[1], 1e-6
    back = canvas.world_polygons(node).select { |_p| true }
    refute_empty back
    # backrest (local +Y) points to world -X after a 90 degree rotation
    backrest = canvas.definitions.values.first.children.find { |c| c.name == 'Naslon' }
    bx = backrest.polygons.flatten(1).map { |p| p[1] }.max
    assert_operator bx, :>, 0.2
    world_backrest = canvas.world_polygons(node).flatten(1).select { |p| p[2] > 0.6 }
    assert(world_backrest.all? { |p| p[0] < 10.0 }, 'backrest should be on the -X side')
  end

  def test_bench_dimensions
    canvas, node, = build_one('furniture.bench', parameters: { 'length' => 1.8, 'depth' => 0.6, 'backHeight' => 0.85 },
                                                   geometry: { 'kind' => 'point', 'position' => [0.0, 0.0, 0.0], 'rotation' => 0.0 })
    min, max = bbox(canvas, node)
    assert_in_delta 1.8, max[0] - min[0], 1e-6
    assert_in_delta 0.6, max[1] - min[1], 1e-6
    assert_in_delta 0.85, max[2], 1e-6
    assert_in_delta 0.0, min[2], 1e-6
  end

  def test_slab_thickness_and_top_offset
    geometry = { 'kind' => 'polygon', 'outer' => { 'vertices' => [[0.0, 0.0, 0.15], [10.0, 0.0, 0.15], [10.0, 4.0, 0.15], [0.0, 4.0, 0.15]] } }
    canvas, node, = build_one('surface.concrete_paving', parameters: { 'thickness' => 0.08, 'topOffset' => 0.02 }, geometry: geometry)
    min, max = bbox(canvas, node)
    assert_in_delta 0.17, max[2], 1e-9
    assert_in_delta 0.09, min[2], 1e-9
    assert_equal [10.0, 4.0], [max[0] - min[0], max[1] - min[1]].map { |v| v.round(9) }
  end

  def test_polygon_with_hole_and_arc
    geometry = { 'kind' => 'polygon',
                 'outer' => { 'vertices' => [[0.0, 0.0, 0.0], [10.0, 0.0, 0.0], [10.0, 10.0, 0.0], [0.0, 10.0, 0.0]], 'bulges' => [0.0, 0.0, 1.0, 0.0] },
                 'holes' => [{ 'vertices' => [[2.0, 2.0, 0.0], [4.0, 2.0, 0.0], [4.0, 4.0, 0.0], [2.0, 4.0, 0.0]] }] }
    canvas, node, = build_one('surface.lawn', geometry: geometry)
    min, max = bbox(canvas, node)
    assert_in_delta 15.0, max[1], 0.031 # semicircle on the north edge (radius 5), chord tolerance 0.03 m
    mesh = canvas.find_all { |n| n.kind == :mesh }.first
    assert(mesh.polygons.any? { |p| p.is_a?(Hash) && p[:holes].length == 1 })
    assert_empty canvas.manifold_errors
  end

  def test_underground_container_preserves_burial_depth
    geometry = { 'kind' => 'point', 'position' => [0.0, 0.0, 0.15], 'rotation' => 0.0 }
    canvas, node, = build_one('utility.waste_container_ug_5m3', parameters: { 'bodyDepth' => 2.4, 'aboveGroundHeight' => 1.0 }, geometry: geometry)
    min, max = bbox(canvas, node)
    assert_in_delta 0.15 - 2.4, min[2], 1e-9
    assert_in_delta 1.15, max[2], 1e-9
    body = canvas.definitions.values.first.children.find { |c| c.name.start_with?('Tijelo kontejnera') }
    zs = body.polygons.flatten(1).map { |p| p[2] }
    assert_in_delta(-2.4, zs.min, 1e-9)
    assert_operator zs.max, :<, 0.0, 'body stays below ground'
  end

  def test_semi_underground_and_excavation_envelope
    canvas, node, = build_one('utility.waste_container_semi', parameters: { 'showExcavation' => true, 'excavationMargin' => 0.5 })
    min, max = bbox(canvas, node)
    assert_in_delta(-1.7, min[2], 1e-9) # body depth 1.5 + 0.2 excavation allowance
    assert_operator max[2], :>, 1.2
    assert(canvas.definitions.values.any? { |d| d.children.any? { |c| c.name == 'Gabarit iskopa' } })
  end

  def test_light_pole_height_and_arm_direction
    geometry = { 'kind' => 'point', 'position' => [0.0, 0.0, 0.0], 'rotation' => -Math::PI / 2 }
    canvas, node, = build_one('lighting.street_light', parameters: { 'poleHeight' => 8.0, 'armLength' => 1.0 }, geometry: geometry)
    min, max = bbox(canvas, node)
    assert_in_delta 8.0, max[2], 0.01
    assert_operator min[1], :<, -1.0, 'arm and luminaire point to world -Y'
    assert_operator max[1], :<, 0.2
  end

  def test_fence_posts_follow_spacing_and_curves
    geometry = { 'kind' => 'path', 'vertices' => [[0.0, 0.0, 0.0], [10.0, 0.0, 0.0], [10.0, 6.0, 0.0]], 'closed' => false }
    canvas, node, = build_one('wall.mesh_fence', parameters: { 'postSpacing' => 2.5, 'height' => 2.0 }, geometry: geometry)
    posts = canvas.find_all { |n| n.kind == :instance && n.name == 'Stub' }
    assert_equal 8, posts.length # 10 m -> 4 bays of 2.5 m, 6 m -> 3 bays of 2.0 m, shared corner post
    assert_includes posts.map { |p| p.origin.map { |v| v.round(6) } }, [10.0, 0.0, 0.0]
    min, max = bbox(canvas, node)
    assert_in_delta 2.02, max[2], 1e-9
    assert_in_delta 0.0, min[2], 1e-9
    panels = canvas.find_all { |n| n.kind == :instance && n.name == 'Panel' }
    assert_equal 7, panels.length
    assert_equal 2, panels.map(&:key).uniq.length, 'panels of equal length share a definition'
  end

  def test_fence_follows_terrain
    geometry = { 'kind' => 'path', 'vertices' => [[0.0, 0.0, 0.0], [10.0, 0.0, 1.0]], 'closed' => false }
    canvas, node, = build_one('wall.mesh_fence', parameters: { 'postSpacing' => 2.5 }, geometry: geometry)
    posts = canvas.find_all { |n| n.kind == :instance && n.name == 'Stub' }
    assert_equal [0.0, 0.25, 0.5, 0.75, 1.0], posts.map { |p| p.origin[2].round(6) }
    assert_empty canvas.manifold_errors
    assert_in_delta 1.0 + 2.02, canvas.bounds(node)[1][2], 1e-6 # post height 2.0 + 0.02 cap at the high end
  end

  def test_channel_depth_follows_gradient
    geometry = { 'kind' => 'path', 'vertices' => [[0.0, 0.0, 0.25], [100.0, 0.0, -0.25]], 'closed' => false }
    canvas, node, = build_one('water.drainage_channel', parameters: { 'width' => 0.2, 'depth' => 0.3 }, geometry: geometry)
    min, max = bbox(canvas, node)
    assert_in_delta 0.25, max[2], 1e-9
    assert_in_delta(-0.55, min[2], 1e-9)
    assert_in_delta 0.2, max[1] - min[1], 1e-9
  end

  def test_pipe_axis_offset
    geometry = { 'kind' => 'path', 'vertices' => [[0.0, 0.0, 0.0], [20.0, 0.0, 0.0]], 'closed' => false }
    canvas, node, = build_one('water.drain_pipe', parameters: { 'diameter' => 0.2, 'axisOffset' => -1.2 }, geometry: geometry)
    min, max = bbox(canvas, node)
    assert_in_delta(-1.3, min[2], 1e-6)
    assert_in_delta(-1.1, max[2], 1e-6)
  end

  def test_wall_alignment_and_footing
    geometry = { 'kind' => 'path', 'vertices' => [[0.0, 0.0, 0.0], [10.0, 0.0, 0.0]], 'closed' => false }
    canvas, node, = build_one('wall.retaining', parameters: { 'alignment' => 'left', 'thickness' => 0.3, 'footingWidth' => 1.2, 'footingDepth' => 0.4 }, geometry: geometry)
    wall = canvas.find('Zid')
    ys = wall.polygons.flatten(1).map { |p| p[1] }
    assert_in_delta 0.0, ys.min, 1e-9
    assert_in_delta 0.3, ys.max, 1e-9
    min, = bbox(canvas, node)
    assert_in_delta(-0.4, min[2], 1e-9)
    footing = canvas.find('Temelj')
    fy = footing.polygons.flatten(1).map { |p| p[1] }
    assert_in_delta 0.15 - 0.6, fy.min, 1e-9 # centred on the wall
  end

  def test_pool_has_walls_floor_and_water
    canvas, node, = build_one('water.swimming_pool', parameters: { 'depth' => 1.5, 'waterLevelOffset' => 0.1, 'wallThickness' => 0.25, 'floorThickness' => 0.25 })
    min, max = bbox(canvas, node)
    assert_in_delta(-1.75, min[2], 1e-9)
    assert_in_delta 0.0, max[2], 1e-9
    assert_in_delta 8.5, max[0] - min[0], 1e-9
    water = canvas.find('Voda')
    assert_in_delta(-0.1, water.polygons.flatten(1).map { |p| p[2] }.max, 1e-9)
  end

  def test_pergola_structure
    canvas, node, = build_one('structure.pergola', parameters: { 'height' => 2.6, 'columnSpacing' => 3.5, 'rafterSpacing' => 0.6 })
    columns = canvas.find_all { |n| n.kind == :instance && n.name == 'Stub' }
    assert_equal 10, columns.length # 8 m sides: 4 columns each (3 bays <= 3.5 m); 5 m sides: one intermediate each
    rafters = canvas.find_all { |n| n.kind == :instance && n.name == 'Rožnjača' }
    assert_equal 14, rafters.length
    min, max = bbox(canvas, node)
    assert_in_delta 2.6 + 0.25 + 0.15, max[2], 1e-6
    assert_in_delta 8.0, max[0] - min[0], 1e-6
    assert_in_delta 5.0, max[1] - min[1], 1e-6
  end

  def test_parking_bay_extends_along_local_y
    geometry = { 'kind' => 'point', 'position' => [0.0, 0.0, 0.0], 'rotation' => 0.0 }
    canvas, node, = build_one('road.parking_bay', geometry: geometry)
    min, max = bbox(canvas, node)
    assert_in_delta 0.0, min[1], 1e-9
    assert_in_delta 5.0, max[1], 1e-9
    assert_in_delta 2.5 + 0.12, max[0] - min[0], 1e-9
    assert_in_delta 0.003, max[2], 1e-12
  end

  def test_steps_and_ramp_rise
    canvas, node, = build_one('level.steps', parameters: { 'totalRise' => 0.6, 'riserHeight' => 0.15 })
    assert_in_delta 0.6, canvas.bounds(node)[1][2], 1e-9
    assert_match(/4 stepenika/, canvas.find_all { |n| n.kind == :mesh }.first.name)
    canvas, node, = build_one('level.ramp', parameters: { 'rise' => 0.3, 'thickness' => 0.2 })
    min, max = canvas.bounds(node)
    assert_in_delta 0.3, max[2], 1e-9
    assert_in_delta(-0.2, min[2], 1e-9)
  end

  def test_basin_slopes_and_water
    canvas, node, = build_one('water.retention_basin', parameters: { 'depth' => 1.0, 'sideSlope' => 1.0, 'waterDepth' => 0.5 })
    min, = canvas.bounds(node)
    assert_in_delta(-1.0, min[2], 1e-9)
    water = canvas.find('Vodena površina')
    zs = water.polygons.flatten(1).map { |p| p[2] }.uniq
    assert_equal [-0.5], zs.map { |z| z.round(9) }
    floor = canvas.find('Korito').polygons.first
    assert_in_delta 6.0, floor.map { |p| p[0] }.max - floor.map { |p| p[0] }.min, 1e-9
  end

  def test_basin_too_narrow_is_steepened_with_warning
    _, _, ctx, = build_one('water.retention_basin', parameters: { 'depth' => 2.0, 'sideSlope' => 3.0 })
    assert(ctx.report.issues.any? { |i| i.code == 'SDP-B031' })
  end

  def test_unknown_builder_is_reported_not_raised
    pkg, = package([{ type: 'furniture.bench' }])
    el = pkg.elements.first
    el.instance_variable_set(:@builder, 'teleporter')
    el.instance_variable_set(:@type_id, 'user.teleporter') # a user type without its own model
    ctx = context(pkg)
    assert_nil ElementBuilder.build(el, RecordingCanvas.new, ctx)
    assert(ctx.report.issues.any? { |i| i.code == 'SDP-B100' })
  end

  def test_lod_changes_detail_not_size
    sizes = %w[conceptual standard presentation].map do |lod|
      canvas, node, = build_one('furniture.bench', lod: lod)
      [canvas.bounds(node).map { |c| c.map { |v| v.round(9) } }, canvas.stats[:definition_faces]]
    end
    assert_equal 1, sizes.map(&:first).uniq.length
    faces = %w[conceptual standard presentation].map do |lod|
      canvas, = build_one('landscape.tree_deciduous', lod: lod)
      canvas.stats[:definition_faces]
    end
    assert_operator faces[0], :<, faces[1]
    assert_operator faces[1], :<, faces[2]
  end

  def test_material_fallback_is_reported
    _, _, ctx, = build_one('furniture.bench', parameters: {})
    assert_empty ctx.report.issues
    pkg, = package([{ type: 'surface.gravel' }])
    pkg.elements.first.params['material'] = 'moon_dust'
    ctx = context(pkg)
    canvas = RecordingCanvas.new
    ElementBuilder.build(pkg.elements.first, canvas, ctx)
    assert(ctx.report.issues.any? { |i| i.code == 'SDP-M001' })
    assert_equal 'default', canvas.find('Površina').material
  end
end
