# frozen_string_literal: true

require_relative 'test_helper'

class TestGeometry < Minitest::Test
  include SdpTest

  def test_units_are_exact
    assert_in_delta 39.37007874015748, Units.m_to_in(1.0), 1e-12
    assert_in_delta 1.0, Units.in_to_m(Units.m_to_in(1.0)), 1e-15
    assert_in_delta 2.5, Units.m_to_in(2.5) * 0.0254, 1e-12
    assert_equal 0.001, Units.meters_per_unit('mm')
    assert_raises(ArgumentError) { Units.meters_per_unit('furlong') }
  end

  def test_bulge_conventions_match_autocad
    arc = Geometry.arc_from_bulge([0.0, 0.0], [2.0, 0.0], 1.0)
    assert_in_delta 1.0, arc[:radius], 1e-12
    assert_in_delta Math::PI, arc[:sweep], 1e-12
    mid, tangent = Geometry.segment_point([0.0, 0.0, 0.0], [2.0, 0.0, 0.0], 1.0, 0.5)
    assert_in_delta 1.0, mid[0], 1e-12
    assert_in_delta(-1.0, mid[1], 1e-12) # positive bulge: counter-clockwise, passes below the chord
    assert_in_delta 0.0, Math.sin(tangent), 1e-12
    assert_in_delta Math::PI, Geometry.segment_length([0.0, 0.0], [2.0, 0.0], 1.0), 1e-12
  end

  def test_tessellation_keeps_endpoints_and_interpolates_z
    pts = Geometry.tessellate([[0.0, 0.0, 0.0], [10.0, 0.0, 1.0]], [0.5], false, 0.01)
    assert_operator pts.length, :>, 10
    assert_equal [0.0, 0.0, 0.0], pts.first
    assert_equal [10.0, 0.0, 1.0], pts.last
    assert(pts.all? { |p| p[2].between?(0.0, 1.0) })
  end

  def test_offsets_and_buffers
    sq = [[0.0, 0.0], [10.0, 0.0], [10.0, 10.0], [0.0, 10.0]]
    out = Geometry.offset_ring(sq, 1.0)
    assert_in_delta 144.0, Geometry.signed_area(out), 1e-9
    inner = Geometry.offset_ring(sq, -2.0)
    assert_in_delta 36.0, Geometry.signed_area(inner), 1e-9
    assert_nil Geometry.offset_ring(sq, -6.0), 'collapsing offsets return nil'
    strip = Geometry.buffer_open([[0.0, 0.0, 0.0], [10.0, 0.0, 0.0]], 1.0, 2.0)
    assert_in_delta 30.0, Geometry.signed_area(strip), 1e-9
  end

  def test_stations_place_corners_and_even_runs
    st = Geometry.stations([[0.0, 0.0, 0.0], [10.0, 0.0, 0.0], [10.0, 5.0, 0.0]], nil, false, 2.5)
    assert_equal 7, st.length
    assert_equal [10.0, 0.0, 0.0], st[4].map { |v| v.round(9) }
    closed = Geometry.stations([[0.0, 0.0, 0.0], [4.0, 0.0, 0.0], [4.0, 4.0, 0.0], [0.0, 4.0, 0.0]], nil, true, 2.0)
    assert_equal 8, closed.length
  end

  def test_oriented_rect_points_into_area
    pts = [[0.0, 0.0, 0.0], [8.0, 0.0, 0.0], [8.0, -3.0, 0.0], [0.0, -3.0, 0.0]] # area to the right of the first edge
    r = Geometry.oriented_rect(pts)
    assert_in_delta 8.0, r[:length_u], 1e-12
    assert_in_delta 3.0, r[:length_v], 1e-12
    assert_in_delta(-1.0, r[:v][1], 1e-12)
    assert Geometry.rectangle?(pts)
  end

  def test_mesh_primitives_are_closed
    canvas = RecordingCanvas.new
    canvas.mesh(Mesh.box(0, 0, 0, 1, 2, 3, 0.4), material: 'm')
    canvas.mesh(Mesh.cylinder([0, 0, 0], 1, 2, 12), material: 'm')
    canvas.mesh(Mesh.tube([0, 0, 0], [3, 4, 5], 0.2, 8), material: 'm')
    canvas.mesh(Mesh.ellipsoid([0, 0, 0], 1, 2, 3, 12, 6), material: 'm')
    canvas.mesh(Mesh.cone([0, 0, 0], 1, 2, 10), material: 'm')
    canvas.mesh(Mesh.block([0, 0, 0], [1, 0, 0.2], [0, 1, 0], [0, 0, 1]), material: 'm')
    canvas.mesh(Mesh.prism([[0, 0], [5, 0], [5, 5], [0, 5]], 0, 1, [[[1, 1], [2, 1], [2, 2], [1, 2]]]), material: 'm')
    canvas.mesh(Mesh.sweep([[0, 0, 0], [5, 0, 0], [5, 5, 1], [0, 8, 1]], [[0.5, 0], [0.5, 1], [-0.5, 1], [-0.5, 0]]), material: 'm')
    canvas.mesh(Mesh.sweep([[0, 0, 0], [5, 0, 0], [5, 5, 0], [0, 5, 0]], [[0.5, 0], [0.5, 1], [-0.5, 1], [-0.5, 0]], true), material: 'm')
    canvas.mesh(Mesh.extrude_profile([0, 0, 0], [1, 0], [0, 1], [[0, 0], [0, 1], [2, 1], [2, 0]], 3), material: 'm')
    assert_empty canvas.manifold_errors
  end

  def test_fingerprint_ignores_noise
    a = Fingerprint.compute(['inst', 'def', [1.0, 2.0, 3.000001]])
    b = Fingerprint.compute(['inst', 'def', [1.0, 2.0, 3.000004]])
    c = Fingerprint.compute(['inst', 'def', [1.0, 2.0, 3.01]])
    assert_equal a, b
    refute_equal a, c
    assert_equal Fingerprint.compute([-0.00001]), Fingerprint.compute([0.0])
  end
end
