# frozen_string_literal: true

require_relative 'test_helper'

class TestPackageReader < Minitest::Test
  include SdpTest

  def test_reads_sample_zip_and_folder_identically
    zip, zrep = PackageReader.read(SAMPLE_ZIP)
    dir, drep = PackageReader.read(SAMPLE_DIR)
    refute zrep.errors?, zrep.to_text
    refute drep.errors?, drep.to_text
    assert_equal zip.elements.map(&:id), dir.elements.map(&:id)
    assert_equal zip.elements.map(&:content_hash), dir.elements.map(&:content_hash)
    assert_equal 139, zip.elements.length
  end

  def test_sample_metadata_units_and_origin
    pkg, = PackageReader.read(SAMPLE_ZIP)
    assert_equal 'Primjer: stambeni kompleks', pkg.project_name
    assert_equal 'mm', pkg.units['sourceLengthUnit']
    assert_in_delta 0.001, pkg.units['sourceUnitsToMeters'], 1e-12
    assert_equal [6_550_000.0, 4_860_000.0, 520.0], pkg.origin
    # schema 1.1: elevation datum and terrain
    assert_in_delta 520.15, pkg.building_zero_absolute, 1e-9
    assert_in_delta 520.0, pkg.package_zero_absolute, 1e-9
    assert_equal 1, pkg.terrains.length
    terrain = pkg.terrains.first
    assert_equal 'Postojeći teren', terrain.name
    assert_operator terrain.triangle_count, :>, 100
    assert(terrain.triangles.flatten.all? { |i| i < terrain.vertices.length })
    building = pkg.elements.find { |e| e.name == 'Objekat A' }
    assert_equal 'massing', building.builder
    assert_in_delta 18.0, building.num('height'), 1e-9
    # package frame: metres relative to the project origin (converted from millimetres by the exporter)
    assert_equal [15.0, 45.0, 0.15], building.geometry[:outer][:vertices][0].map { |v| v.round(9) }
  end

  def test_parameters_are_merged_with_type_defaults
    pkg, = package([{ type: 'furniture.bench', parameters: { 'length' => 2.4 } }])
    e = pkg.elements.first
    assert_in_delta 2.4, e.num('length'), 1e-12
    assert_in_delta 0.6, e.num('depth'), 1e-12 # default from the type definition
  end

  def test_invalid_parameter_falls_back_to_default_with_warning
    pkg, rep = package([{ type: 'furniture.bench', parameters: { 'length' => 999.0, 'backrest' => 'yes' } }])
    e = pkg.elements.first
    assert_in_delta 1.8, e.num('length'), 1e-12
    assert_equal true, e.bool('backrest')
    assert_equal 2, rep.issues.count { |i| i.code == 'SDP-P020' }
  end

  def test_newer_minor_version_warns_and_unsupported_major_fails
    m, g = package_strings([{ type: 'furniture.bench' }])
    newer = JSON.parse(m).merge('schemaVersion' => '1.4.0')
    pkg, rep = PackageReader.read_strings(JSON.generate(newer), g)
    refute_nil pkg
    assert(rep.issues.any? { |i| i.code == 'SDP-F002' })

    major = JSON.parse(m).merge('schemaVersion' => '2.0.0')
    pkg, rep = PackageReader.read_strings(JSON.generate(major), g)
    assert_nil pkg
    assert(rep.issues.any? { |i| i.code == 'SDP-F001' && i.message.include?('nepodržana verzija sheme') })
  end

  def test_rejects_foreign_documents_and_wrong_units
    m, g = package_strings([{ type: 'furniture.bench' }])
    pkg, = PackageReader.read_strings(JSON.generate(JSON.parse(m).merge('format' => 'other')), g)
    assert_nil pkg
    pkg, = PackageReader.read_strings(m, JSON.generate(JSON.parse(g).merge('lengthUnit' => 'mm')))
    assert_nil pkg
    pkg, rep = PackageReader.read_strings('{ not json', g)
    assert_nil pkg
    assert rep.errors?
  end

  def test_reports_duplicate_ids_missing_geometry_and_bad_coordinates
    id = SecureRandom.uuid
    m, g = package_strings([{ type: 'furniture.bench', id: id }, { type: 'furniture.bench' }, { type: 'wall.concrete' }])
    man = JSON.parse(m)
    geo = JSON.parse(g)
    man['elements'] << man['elements'][0].dup                        # duplicate id
    man['elements'] << man['elements'][1].merge('id' => SecureRandom.uuid) # no geometry
    geo['elements'][2]['vertices'][1] = [1.0, 'x', 0.0]             # bad coordinate
    pkg, rep = PackageReader.read_strings(JSON.generate(man), JSON.generate(geo))
    codes = rep.issues.map(&:code)
    assert_includes codes, 'SDP-D002'
    assert_includes codes, 'SDP-F011'
    assert_includes codes, 'SDP-G010'
    assert_equal 2, pkg.elements.length
  end

  def test_unsafe_and_missing_assets
    Dir.mktmpdir do |dir|
      m, g = package_strings([{ type: 'furniture.bench' }])
      man = JSON.parse(m)
      man['assets'] = [{ 'id' => 'a', 'path' => 'assets/tex.png', 'kind' => 'texture' },
                       { 'id' => 'b', 'path' => 'assets/../../x.skp', 'kind' => 'component' },
                       { 'id' => 'c', 'path' => 'assets/ok.png', 'kind' => 'texture' }]
      File.write(File.join(dir, 'manifest.json'), JSON.generate(man))
      File.write(File.join(dir, 'geometry.json'), g)
      FileUtils.mkdir_p(File.join(dir, 'assets'))
      File.binwrite(File.join(dir, 'assets', 'ok.png'), 'PNG')
      pkg, rep = PackageReader.read(dir)
      assert(rep.issues.any? { |i| i.code == 'SDP-F021' && i.message.include?('tex.png') })
      assert(rep.issues.any? { |i| i.code == 'SDP-F020' })
      assert_equal 'PNG', pkg.asset_bytes(pkg.asset('c'))
      assert_nil pkg.asset_bytes(pkg.asset('b'))
    end
  end

  def test_corrupt_archive_is_reported
    Dir.mktmpdir do |dir|
      path = File.join(dir, 'broken.sdpkg')
      File.write(path, 'this is not a zip archive at all, sorry')
      pkg, rep = PackageReader.read(path)
      assert_nil pkg
      assert(rep.issues.any? { |i| i.code == 'SDP-F001' })
    end
  end

  def test_zip_crc_mismatch_is_detected
    Dir.mktmpdir do |dir|
      path = File.join(dir, 'tampered.sdpkg')
      data = File.binread(SAMPLE_ZIP)
      zip = ZipReader.new(SAMPLE_ZIP)
      entry = zip.entries['manifest.json']
      offset = entry.local_offset + 30 + 'manifest.json'.bytesize + 10
      data.setbyte(offset, (data.getbyte(offset) + 1) % 256)
      File.binwrite(path, data)
      pkg, rep = PackageReader.read(path)
      assert_nil pkg
      assert(rep.issues.any? { |i| i.message =~ /CRC|inflate|invalid|incorrect|corrupt/i }, rep.to_text)
    end
  end
  def polygon_report(vertices)
    geom = { 'kind' => 'polygon', 'outer' => { 'vertices' => vertices } }
    pkg, report = PackageReader.read_strings(*package_strings([{ type: 'road.pedestrian_crossing', geometry: geom }]))
    [pkg, report]
  end

  def test_polygon_with_one_distinct_point_is_rejected
    pkg, report = polygon_report([[0.0, 0.0, 0.0]] * 4)
    assert_empty pkg.elements
    assert(report.issues.any? { |i| i.code == 'SDP-G005' }, report.to_text)
  end

  def test_polygon_on_a_line_is_rejected
    pkg, report = polygon_report([[0.0, 0.0, 0.0], [5.0, 0.0, 0.0], [10.0, 0.0, 0.0]])
    assert_empty pkg.elements
    assert(report.issues.any? { |i| i.code == 'SDP-G006' }, report.to_text)
  end

  def test_self_intersecting_polygon_is_reported
    pkg, report = polygon_report([[0.0, 0.0, 0.0], [10.0, 10.0, 0.0], [10.0, 0.0, 0.0], [0.0, 4.0, 0.0]])
    assert_equal 1, pkg.elements.length
    assert(report.issues.any? { |i| i.code == 'SDP-G007' && i.severity == :warning }, report.to_text)
  end

  def test_closing_duplicate_vertex_is_accepted
    pkg, report = polygon_report([[0.0, 0.0, 0.0], [10.0, 0.0, 0.0], [10.0, 10.0, 0.0], [0.0, 0.0, 0.0]])
    assert_equal 1, pkg.elements.length
    refute(report.issues.any? { |i| i.code.start_with?('SDP-G') }, report.to_text)
  end
end
