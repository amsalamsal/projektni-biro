# frozen_string_literal: true

# Writes dialog fixture data (tests/ui/fixtures/*.json) from the real sample package using the Ruby core.
require 'fileutils'
require_relative '../../src/pb_site_design_pro/core'
include ProjektniBiro::SiteDesignPro

root = File.expand_path('../../..', __dir__)
out = File.join(__dir__, 'fixtures')
FileUtils.mkdir_p(out)
pkg, rep = PackageReader.read(File.join(root, 'shared/samples/residential-complex.sdpkg'))
plan = ChangePlanner.new.plan(pkg, [])
cat = CatalogFiles.new(File.join(root, 'shared/catalog'))
groups = pkg.elements.group_by(&:model_group).map { |k, v| { 'group' => cat.model_group(k).name, 'count' => v.length } }
preview = {
  'mode' => 'preview',
  'package' => { 'path' => '/projects/sample/residential-complex.sdpkg', 'project' => pkg.project, 'packageId' => pkg.package_id,
                 'schemaVersion' => pkg.schema_version, 'created' => pkg.created_utc, 'generator' => pkg.generator, 'units' => pkg.units,
                 'origin' => pkg.origin, 'trueNorth' => pkg.true_north_degrees, 'crs' => pkg.crs, 'elementCount' => pkg.elements.length,
                 'groups' => groups },
  'validation' => { 'errors' => 0, 'warnings' => 1, 'reviews' => 2,
                    'issues' => [{ 'severity' => 'warning', 'code' => 'SDP-F021', 'elementId' => nil, 'message' => 'Asset missing: assets/tex.png', 'resolution' => 'Re-export with assets.' },
                                 { 'severity' => 'review', 'code' => 'SDP-R001', 'elementId' => pkg.elements[0].id, 'label' => 'UW-001', 'message' => 'Source of dimensions not defined.', 'resolution' => 'Enter manufacturer data.' },
                                 { 'severity' => 'review', 'code' => 'SDP-R001', 'elementId' => pkg.elements[1].id, 'label' => 'UW-002', 'message' => '<script>alert(1)</script> must be escaped', 'resolution' => '' }] },
  'plan' => plan.to_h.merge('summary' => plan.summary, 'changes' => plan.changes?),
  'options' => { 'lod' => 'standard', 'modifiedPolicy' => 'keep', 'deletedPolicy' => 'mark', 'createScenes' => true }
}
File.write(File.join(out, 'preview.json'), JSON.pretty_generate(preview))
report = Report.new('Site Design Pro import')
report.package_path = '/projects/sample/residential-complex.sdpkg'
report.stats.merge!('created' => 139, 'faces' => 4210, 'instances' => 332, 'seconds' => 3.4)
report.add(:review, 'SDP-R001', pkg.elements[0].id, 'Source of dimensions not defined.', 'Enter manufacturer data.', 'UW-001')
report.finished_at = report.started_at + 3.4
File.write(File.join(out, 'report.json'), JSON.pretty_generate('mode' => 'report', 'report' => report.to_h))
bench = pkg.elements.find { |e| e.type_id == 'furniture.bench' }
props = { 'id' => bench.id, 'label' => bench.label, 'name' => bench.name, 'type' => bench.type_id, 'typeName' => bench.type_def.name,
          'category' => bench.category, 'revision' => 1, 'importedAt' => '2026-10-29T12:00:00Z', 'deletedInSource' => false, 'locallyModified' => true,
          'parameters' => bench.type_def.parameters.map { |p| p.merge('value' => bench.params[p['key']], 'edited' => p['key'] == 'length') },
          'properties' => { 'manufacturer' => 'Example', 'notes' => 'South plaza' },
          'materials' => cat.materials.values.map { |m| { 'key' => m.key, 'name' => m.name } } }
File.write(File.join(out, 'properties.json'), JSON.pretty_generate(props))
types = JSON.parse(File.read(File.join(root, 'shared/catalog/element-types.json')))['types'].map { |t| t.slice('id', 'name', 'category', 'subcategory', 'builder') }
library = { 'types' => types, 'categories' => JSON.parse(File.read(File.join(root, 'shared/catalog/categories.json')))['categories'],
            'materials' => JSON.parse(File.read(File.join(root, 'shared/catalog/materials.json')))['materials'],
            'settings' => { 'lod' => 'standard', 'modifiedPolicy' => 'keep', 'deletedPolicy' => 'mark', 'createScenes' => true, 'setNorth' => true,
                            'componentMappings' => { 'furniture.bench' => 'C:/Library/Bench Model A.skp' }, 'favorites' => ['landscape.tree_deciduous'] },
            'inModel' => { 'furniture.bench' => 4, 'landscape.tree_deciduous' => 14 } }
File.write(File.join(out, 'library.json'), JSON.pretty_generate(library))
puts "fixtures written to #{out}"
