# frozen_string_literal: true

# Produces shared/samples/residential-complex-changes.json by running the real SketchUp adapter code
# against the fake SketchUp API: import the sample, edit a bench, move a light, rotate a bench, export.
# The C# test suite applies this file to the AutoCAD-side sample (cross-language reverse exchange test).
require 'tmpdir'
require_relative '../../src/pb_site_design_pro/core'
require_relative 'fake_sketchup'
%w[settings materials su_canvas model_index scenes importer editing].each do |f|
  require File.expand_path("../../src/pb_site_design_pro/sketchup/#{f}", __dir__)
end
include ProjektniBiro::SiteDesignPro

root = File.expand_path('../../..', __dir__)
model = Sketchup::Model.new
Sketchup.active_model = model
catalog = CatalogFiles.new(File.join(root, 'shared/catalog'))
imp = Importer.new(model, File.join(root, 'shared/samples/residential-complex.sdpkg'), catalog: catalog, lod: 'standard',
                                                                                       policy: { modified: 'keep', deleted: 'mark' })
imp.plan!
imp.apply!(create_scenes: false)
managed = ModelIndex.managed_entities(model)
by_label = managed.to_h { |e| [e.get_attribute('SiteDesignPro', 'label'), e] }

Editing.apply_parameters(model, by_label.fetch('FB-001'), { 'length' => 2.2, 'armrests' => true }, catalog: catalog)
light = by_label.fetch('L-001')
light.transformation = Geom::Transformation.translation(Geom::Vector3d.new(Units.m_to_in(2.0), Units.m_to_in(-0.5), 0)) * light.transformation
bench = by_label.fetch('FB-002')
t = bench.transformation
bench.transformation = Geom::Transformation.translation(t.origin) * Geom::Transformation.rotation(nil, Geom::Vector3d.new(0, 0, 1), Math::PI / 2)

cs = Editing.collect_changes(model, imp.package.project_id)
out = File.join(root, 'shared/samples/residential-complex-changes.json')
doc = cs.to_h.merge('createdUtc' => '2026-01-01T00:00:00Z')
File.write(out, JSON.pretty_generate(doc) + "\n")
puts "#{cs.changes.length} changes written to #{out}"
