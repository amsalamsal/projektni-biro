# frozen_string_literal: true

# Builds the local 3D model library of the SketchUp extension from the asset models and catalog:
#
#   <out>/<Folder>/<asset_id>/model.dae    COLLADA model (metres, Z up) – SketchUp imports it natively
#   <out>/<Folder>/<asset_id>/asset.json   metadata: ID, names, category, dimensions, materials, source, licence, status
#   <out>/<Folder>/<asset_id>/mesh.json    triangle mesh for the preview renderer (not shipped)
#   <out>/library.json                     index of all assets (what the library dialog reads)
#   <out>/inventory.json                   spec objects -> assets with completion status
#
# Usage: ruby tools/assets/build_library.rb <out-dir> [asset-id ...]
require 'json'
require 'fileutils'
require 'securerandom'

ENV['SDP_QUIET'] = '1'
ROOT = File.expand_path('../..', __dir__)
require File.join(ROOT, 'sketchup-site-design-pro/src/pb_site_design_pro/core')
include ProjektniBiro::SiteDesignPro # rubocop:disable Style/MixinUsage

OUT = ARGV[0] or abort 'usage: build_library.rb <out-dir> [asset-id ...]'
ONLY = ARGV[1..] || []
CAT = File.join(ROOT, 'shared/catalog')
SPEC = JSON.parse(File.read(File.join(ROOT, 'shared/assets/spec-objects.json'), encoding: 'UTF-8'))
TYPES = JSON.parse(File.read(File.join(CAT, 'element-types.json'), encoding: 'UTF-8'))['types']
BUILDERS = JSON.parse(File.read(File.join(CAT, 'builders.json'), encoding: 'UTF-8'))['builders']
MATERIALS = JSON.parse(File.read(File.join(CAT, 'materials.json'), encoding: 'UTF-8'))['materials']
CATEGORIES = JSON.parse(File.read(File.join(CAT, 'categories.json'), encoding: 'UTF-8'))['categories']

# catalog category -> library folder (the 14 categories of the specification)
FOLDERS = {
  'surfaces' => 'Infrastructure', 'roads' => 'Traffic', 'cycling' => 'Bicycle', 'water' => 'Water', 'landscape' => 'Greenery',
  'utilities' => 'Municipal', 'lighting' => 'Lighting', 'walls' => 'Fences_Walls', 'structures' => 'Canopies',
  'furniture' => 'Furniture_Recreation', 'sustainability' => 'Eco_Technologies', 'climate' => 'Outdoor_Climate',
  'decorative' => 'Decorative', 'information' => 'Information_Security'
}.freeze
# drawing aids, zones and references are not 3D models
NOT_MODELS = %w[zone circle_zone outline marker massing].freeze
EXCLUDED = %w[road.swept_path_placeholder utility.trench utility.service_corridor].freeze
# Builders whose parametric geometry already is the real object (textured surfaces, strips, paint, pipes).
PARAMETRIC_REAL = %w[slab linear_slab stripes pipe].freeze

STATUS = {
  not_started: ['Not started', 'Nije započeto'], custom: ['Custom model created', 'Napravljen vlastiti model'],
  imported: ['Imported into library', 'Uvezeno u biblioteku'], tested: ['Tested in SketchUp', 'Testirano u SketchUpu'],
  complete: ['Complete', 'Završeno'], blocked: ['Blocked', 'Blokirano']
}.freeze

def type_definition(t)
  b = BUILDERS.fetch(t['builder'])
  defaults = t['defaults'] || {}
  params = b['parameters'].map { |p| defaults.key?(p['key']) ? p.merge('default' => defaults[p['key']]) : p }
  { 'id' => t['id'], 'name' => t['name'], 'category' => t['category'], 'subcategory' => t['subcategory'], 'builder' => t['builder'],
    'geometry' => b['geometry'], 'modelGroup' => t['modelGroup'] || 'planting', 'parameters' => params }
end

# Representative sample geometry for path and polygon assets (a reusable piece of the surface / run).
def sample_geometry(t, kind)
  case kind
  when 'point' then { 'kind' => 'point', 'position' => [0.0, 0.0, 0.0], 'rotation' => 0.0 }
  when 'path'
    if t['id'].start_with?('road.', 'cycling.') && t['builder'] == 'linear_slab'
      { 'kind' => 'path', 'vertices' => [[0.0, 0.0, 0.0], [12.0, 0.0, 0.0]], 'bulges' => [0.0], 'closed' => false }
    else
      { 'kind' => 'path', 'vertices' => [[0.0, 0.0, 0.0], [4.0, 0.0, 0.0], [6.5, 1.5, 0.0]], 'bulges' => [0.0, 0.0], 'closed' => false }
    end
  else
    w, d = case t['builder']
           when 'pool' then [8.0, 4.0]
           when 'planting_area', 'planter' then [3.0, 2.0]
           when 'pergola', 'sail' then [5.0, 4.0]
           when 'steps', 'ramp' then [3.0, 2.0]
           else [4.0, 3.0]
           end
    { 'kind' => 'polygon', 'outer' => { 'vertices' => [[0.0, 0.0, 0.0], [w, 0.0, 0.0], [w, d, 0.0], [0.0, d, 0.0]] } }
  end
end

def package_for(t)
  td = type_definition(t)
  id = SecureRandom.uuid
  manifest = {
    'format' => 'site-design-pro-package', 'schemaVersion' => '1.0.0', 'packageId' => SecureRandom.uuid, 'createdUtc' => '2026-01-01T00:00:00Z',
    'generator' => { 'application' => 'asset library', 'version' => ProjektniBiro::SiteDesignPro::VERSION },
    'project' => { 'id' => '00000000-0000-4000-8000-000000000000', 'name' => 'Biblioteka' },
    'units' => { 'length' => 'm', 'angle' => 'rad', 'sourceLengthUnit' => 'm', 'sourceUnitsToMeters' => 1.0 },
    'coordinateSystem' => { 'origin' => [0.0, 0.0, 0.0], 'trueNorthDegrees' => 0.0 }, 'catalog' => { 'id' => 'sdp-core', 'version' => '1.0.0' },
    'typeDefinitions' => [td], 'materials' => MATERIALS,
    'elements' => [{ 'id' => id, 'type' => t['id'], 'category' => t['category'], 'subcategory' => t['subcategory'], 'builder' => t['builder'],
                     'name' => t['name'], 'revision' => 1, 'contentHash' => '0' * 64, 'modelGroup' => td['modelGroup'], 'parameters' => {} }]
  }
  geometry = { 'schemaVersion' => '1.0.0', 'lengthUnit' => 'm', 'elements' => [sample_geometry(t, td['geometry']).merge('id' => id)] }
  pkg, rep = PackageReader.read_strings(JSON.generate(manifest), JSON.generate(geometry))
  raise "#{t['id']}: #{rep.to_text}" unless pkg

  pkg
end

# Flattens the recording canvas into world polygons grouped by material: { material => [poly, ...] }.
def flatten(canvas)
  out = Hash.new { |h, k| h[k] = [] }
  walk = lambda do |node, tf, mat|
    node.children.each do |c|
      m = c.material || mat
      case c.kind
      when :mesh then c.polygons.each { |pl| out[m.to_s] << (pl.is_a?(Hash) ? { outer: pl[:outer].map(&tf), holes: pl[:holes].map { |h| h.map(&tf) } } : pl.map(&tf)) }
      when :instance
        ox, oy, oz = c.origin
        cs = Math.cos(c.rotation)
        sn = Math.sin(c.rotation)
        inner = ->(p) { tf.call([ox + (cs * p[0]) - (sn * p[1]), oy + (sn * p[0]) + (cs * p[1]), oz + p[2]]) }
        walk.call(canvas.definitions[c.key], inner, m)
      when :group then walk.call(c, tf, m)
      end
    end
  end
  walk.call(canvas.root, ->(p) { p.map(&:to_f) }, nil)
  out
end

def bbox(polys_by_mat)
  pts = polys_by_mat.values.flatten(1).flat_map { |p| p.is_a?(Hash) ? p[:outer] : p }
  return nil if pts.empty?

  [[0, 1, 2].map { |i| pts.map { |q| q[i] }.min }, [0, 1, 2].map { |i| pts.map { |q| q[i] }.max }]
end

def colour(key)
  m = MATERIALS.find { |x| x['key'] == key }
  c = m ? m['color'] : [170, 170, 170]
  [c[0] / 255.0, c[1] / 255.0, c[2] / 255.0, m && m['alpha'] ? m['alpha'] : 1.0]
end

TEXTURES = File.join(ROOT, 'sketchup-site-design-pro/src/pb_site_design_pro/textures')

# Bundled texture of a material: [file name, tile size in metres] or nil.
def texture(key)
  m = MATERIALS.find { |x| x['key'] == key }
  return nil unless m && m['texture'] && File.file?(File.join(TEXTURES, m['texture']))

  [m['texture'], (m['textureSize'] || [1.0, 1.0]).map(&:to_f)]
end

# Planar (box) projection of a polygon's points by its dominant normal axis, in texture tiles.
def planar_uv(points, size)
  n = [0.0, 0.0, 0.0]
  points.each_with_index do |p, i|
    q = points[(i + 1) % points.length]
    n[0] += (p[1] - q[1]) * (p[2] + q[2])
    n[1] += (p[2] - q[2]) * (p[0] + q[0])
    n[2] += (p[0] - q[0]) * (p[1] + q[1])
  end
  ax = n.map(&:abs).each_with_index.max[1]
  u, v = { 0 => [1, 2], 1 => [0, 2], 2 => [0, 1] }[ax]
  points.map { |q| [(q[u] / size[0]).round(5), (q[v] / size[1]).round(5)] }
end

def fmt(v)
  format('%.5f', v).sub(/\.?0+$/, '')
end

# COLLADA 1.4.1, one <geometry> per material, polygons as <polylist> (holes as <polygons>/<ph>).
def write_dae(path, asset_id, polys_by_mat)
  geoms = []
  effects = []
  images = []
  used_textures = []
  mats = []
  nodes = []
  polys_by_mat.each_with_index do |(mat, polys), gi|
    verts = []
    index = {}
    vid = lambda do |p|
      key = p.map { |v| v.round(6) }
      index[key] ||= (verts << key; verts.length - 1)
    end
    tex = texture(mat)
    uvs = []
    uindex = {}
    uid = ->(t) { uindex[t] ||= (uvs << t; uvs.length - 1) }
    # with a texture every corner is "vertex uv" (offset 0 and 1), else just the vertex
    ring = lambda do |pts, all_pts|
      next pts.map(&vid) unless tex

      pts.map(&vid).zip(planar_uv(all_pts, tex[1]).first(pts.length).map(&uid)).flatten
    end
    simple = polys.reject { |p| p.is_a?(Hash) }.map { |p| ring.call(p, p) }
    holed = polys.select { |p| p.is_a?(Hash) }.map do |p|
      outer_uv = tex ? planar_uv(p[:outer] + p[:holes].flatten(1), tex[1]) : nil
      hole_rings = []
      offset = p[:outer].length
      p[:holes].each do |h|
        hole_rings << (tex ? h.map(&vid).zip(outer_uv[offset, h.length].map(&uid)).flatten : h.map(&vid))
        offset += h.length
      end
      [tex ? p[:outer].map(&vid).zip(outer_uv.first(p[:outer].length).map(&uid)).flatten : p[:outer].map(&vid), hole_rings]
    end
    gid = "g#{gi}"
    mid = "m_#{mat.gsub(/[^A-Za-z0-9_]/, '_')}"
    inputs = %(<input semantic="VERTEX" source="##{gid}-v" offset="0"/>) + (tex ? %(<input semantic="TEXCOORD" source="##{gid}-uv" offset="1" set="0"/>) : '')
    stride = tex ? 2 : 1
    prims = []
    unless simple.empty?
      prims << %(<polylist material="#{mid}" count="#{simple.length}">#{inputs}<vcount>#{simple.map { |r| r.length / stride }.join(' ')}</vcount><p>#{simple.flatten.join(' ')}</p></polylist>)
    end
    unless holed.empty?
      body = holed.map { |o, hs| "<ph><p>#{o.join(' ')}</p>#{hs.map { |h| "<h>#{h.join(' ')}</h>" }.join}</ph>" }.join
      prims << %(<polygons material="#{mid}" count="#{holed.length}">#{inputs}#{body}</polygons>)
    end
    uv_src = tex ? %(<source id="#{gid}-uv"><float_array id="#{gid}-uva" count="#{uvs.length * 2}">#{uvs.flatten.map { |v| fmt(v) }.join(' ')}</float_array><technique_common><accessor source="##{gid}-uva" count="#{uvs.length}" stride="2"><param name="S" type="float"/><param name="T" type="float"/></accessor></technique_common></source>) : ''
    geoms << %(<geometry id="#{gid}"><mesh><source id="#{gid}-p"><float_array id="#{gid}-pa" count="#{verts.length * 3}">#{verts.flatten.map { |v| fmt(v) }.join(' ')}</float_array><technique_common><accessor source="##{gid}-pa" count="#{verts.length}" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>#{uv_src}<vertices id="#{gid}-v"><input semantic="POSITION" source="##{gid}-p"/></vertices>#{prims.join}</mesh></geometry>)
    r, g, b, a = colour(mat)
    if tex
      images << %(<image id="#{mid}-img" name="#{tex[0]}"><init_from>#{tex[0]}</init_from></image>)
      used_textures << tex[0]
    end
    diffuse = if tex
                %(<texture texture="#{mid}-sampler" texcoord="UVSET0"/>)
              else
                %(<color>#{fmt(r)} #{fmt(g)} #{fmt(b)} 1</color>)
              end
    params = tex ? %(<newparam sid="#{mid}-surface"><surface type="2D"><init_from>#{mid}-img</init_from></surface></newparam><newparam sid="#{mid}-sampler"><sampler2D><source>#{mid}-surface</source></sampler2D></newparam>) : ''
    effects << %(<effect id="#{mid}-fx"><profile_COMMON>#{params}<technique sid="common"><lambert><diffuse>#{diffuse}</diffuse>#{a < 1 ? "<transparency><float>#{fmt(a)}</float></transparency>" : ''}</lambert></technique></profile_COMMON></effect>)
    mats << %(<material id="#{mid}" name="#{mat}"><instance_effect url="##{mid}-fx"/></material>)
    bind = tex ? %(<bind_vertex_input semantic="UVSET0" input_semantic="TEXCOORD" input_set="0"/>) : ''
    nodes << %(<node id="n#{gi}" name="#{mat}"><instance_geometry url="##{gid}"><bind_material><technique_common><instance_material symbol="#{mid}" target="##{mid}">#{bind}</instance_material></technique_common></bind_material></instance_geometry></node>)
  end
  File.write(path, <<~DAE)
    <?xml version="1.0" encoding="utf-8"?>
    <COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">
    <asset><contributor><authoring_tool>Vanjsko uređenje PRO #{ProjektniBiro::SiteDesignPro::VERSION}</authoring_tool></contributor><created>2026-01-01T00:00:00Z</created><modified>2026-01-01T00:00:00Z</modified><unit name="meter" meter="1"/><up_axis>Z_UP</up_axis></asset>
    <library_images>#{images.join}</library_images>
    <library_effects>#{effects.join}</library_effects>
    <library_materials>#{mats.join}</library_materials>
    <library_geometries>#{geoms.join}</library_geometries>
    <library_visual_scenes><visual_scene id="scene" name="#{asset_id}"><node id="root" name="#{asset_id}">#{nodes.join}</node></visual_scene></library_visual_scenes>
    <scene><instance_visual_scene url="#scene"/></scene>
    </COLLADA>
  DAE
  used_textures.each { |f| FileUtils.cp(File.join(TEXTURES, f), File.join(File.dirname(path), f)) }
  used_textures
end

# Triangle mesh for the preview renderer: { mat => { c: rgba, p: [[outer, holes], ...] } }.
def write_mesh_json(path, polys_by_mat)
  data = polys_by_mat.to_h do |mat, polys|
    tex = texture(mat)
    [mat, { 'c' => colour(mat), 't' => tex&.first, 's' => tex&.last, 'p' => polys.map { |p| p.is_a?(Hash) ? [p[:outer].map { |q| q.map { |v| v.round(4) } }, p[:holes].map { |h| h.map { |q| q.map { |v| v.round(4) } } }] : [p.map { |q| q.map { |v| v.round(4) } }, []] } }]
  end
  File.write(path, JSON.generate(data))
end

spec_refs = Hash.new { |h, k| h[k] = [] }
SPEC['categories'].each { |c| c['objects'].each { |o| o['assets'].each { |a| spec_refs[a] << { 'category' => c['name'], 'object' => o['name'], 'objectEn' => o['nameEn'] } } } }

FileUtils.mkdir_p(OUT)
library = []
TYPES.each do |t|
  folder = FOLDERS[t['category']]
  next unless folder
  next if NOT_MODELS.include?(t['builder']) || EXCLUDED.include?(t['id'])
  next unless ONLY.empty? || ONLY.include?(t['id'])

  pkg = package_for(t)
  element = pkg.elements.first
  ctx = Builders::Context.new(lod: 'presentation', materials: pkg.materials)
  canvas = RecordingCanvas.new
  ElementBuilder.build(element, canvas, ctx)
  model = ElementBuilder.model_for(element)
  errors = ctx.report.issues.select { |i| i.severity == :error }.map(&:message)
  solid = canvas.manifold_errors
  polys = flatten(canvas)
  dir = File.join(OUT, folder, t['id'])
  FileUtils.mkdir_p(dir)
  textures = write_dae(File.join(dir, 'model.dae'), t['id'], polys)
  write_mesh_json(File.join(dir, 'mesh.json'), polys)
  min, max = bbox(polys) || [[0, 0, 0], [0, 0, 0]]
  real = !model && PARAMETRIC_REAL.include?(t['builder'])
  status = if !errors.empty? || !solid.empty? then :blocked
           elsif model || real then :imported
           else :not_started
           end
  cat = CATEGORIES.find { |c| c['id'] == t['category'] }
  asset = {
    'assetId' => t['id'], 'name' => t['name'], 'nameEn' => t['nameEn'], 'folder' => folder, 'category' => cat ? cat['name'] : t['category'],
    'subcategory' => t['subcategory'], 'geometry' => element.kind, 'builder' => t['builder'],
    'specObjects' => spec_refs[t['id']],
    'model' => if model then { 'kind' => 'custom', 'generator' => "Models #{t['id']} v#{model.version}" }
               elsif real then { 'kind' => 'parametric', 'generator' => "Builder #{t['builder']}" }
               else { 'kind' => 'generic', 'generator' => "Builder #{t['builder']}" }
               end,
    'files' => { 'model' => 'model.dae', 'preview' => 'preview.png', 'textures' => textures },
    'units' => 'm', 'insertionPoint' => element.kind == 'point' ? 'tačka umetanja = podnožje objekta, +X = smjer elementa' : 'uzorak u lokalnim koordinatama (0,0,0)',
    'dimensions' => { 'x' => (max[0] - min[0]).round(3), 'y' => (max[1] - min[1]).round(3), 'z' => (max[2] - min[2]).round(3), 'zMin' => min[2].round(3) },
    'materials' => polys.keys.sort, 'polygons' => polys.values.sum(&:length),
    'source' => if model then 'Vlastiti proceduralni model Vanjsko uređenje PRO (SketchUp Ruby API generator)'
                elsif real then 'Parametarska površina/trasa u stvarnim dimenzijama, s materijalom i teksturom (Vanjsko uređenje PRO)'
                else 'Generički parametarski prikaz (nije realističan model)'
                end,
    'license' => 'Dio projekta Vanjsko uređenje PRO; bez materijala trećih strana',
    'sketchup' => 'SketchUp 2022+: generiše se u proširenju; model.dae se uvozi i ručno (File > Import)',
    'status' => STATUS[status][0], 'statusBs' => STATUS[status][1], 'testedInSketchUp' => false,
    'problems' => errors + solid.first(5)
  }
  File.write(File.join(dir, 'asset.json'), JSON.pretty_generate(asset))
  library << asset
  warn "#{t['id']}: #{asset['statusBs']} (#{asset['polygons']} poligona)#{asset['problems'].empty? ? '' : " – #{asset['problems'].first}"}" if ENV['VERBOSE'] || status == :blocked
end

if ONLY.empty?
  File.write(File.join(OUT, 'library.json'), JSON.pretty_generate({ 'version' => ProjektniBiro::SiteDesignPro::VERSION, 'folders' => FOLDERS.values.uniq,
                                                                    'assets' => library.map { |a| a.reject { |k, _| k == 'problems' } } }))
  by_id = library.to_h { |a| [a['assetId'], a] }
  inventory = SPEC['categories'].map do |c|
    { 'category' => c['name'], 'folder' => c['folder'], 'objects' => c['objects'].map do |o|
      assets = o['assets'].map { |id| a = by_id[id]; { 'assetId' => id, 'status' => a ? a['status'] : 'Not started', 'statusBs' => a ? a['statusBs'] : 'Nije započeto' } }
      { 'name' => o['name'], 'nameEn' => o['nameEn'], 'assets' => assets,
        'status' => assets.all? { |a| a['status'] == 'Imported into library' } ? 'Imported into library' : (assets.any? { |a| a['status'] == 'Blocked' } ? 'Blocked' : 'Not started') }
    end }
  end
  File.write(File.join(OUT, 'inventory.json'), JSON.pretty_generate({ 'categories' => inventory }))
  counts = library.group_by { |a| a['status'] }.transform_values(&:length)
  puts "Modela: #{library.length} | #{counts.map { |k, v| "#{k}: #{v}" }.join(', ')}"
end
