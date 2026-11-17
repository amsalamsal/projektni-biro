# Runs the host-independent core the way SketchUp does on an older Ruby (SketchUp 2014-2016: Ruby 2.0,
# 2017-2018: 2.2, 2019-2020: 2.5, 2021-2023: 2.7): reads the sample package, builds every element at every level
# of detail and plans an import into an empty model. Exit code 1 on any error.
# Usage: ruby tools/compat_smoke.rb
ENV['SDP_QUIET'] = '1'
ROOT = File.expand_path('..', File.dirname(__FILE__))
require File.join(ROOT, 'sketchup-site-design-pro/src/pb_site_design_pro/core')
include ProjektniBiro::SiteDesignPro # rubocop:disable Style/MixinUsage

errors = 0
[File.join(ROOT, 'shared/samples/residential-complex.sdpkg'), File.join(ROOT, 'shared/samples/residential-complex')].each do |path|
  next unless File.exist?(path)

  pkg, rep = PackageReader.read(path)
  if pkg.nil? || rep.errors?
    puts "#{path}: paket nije pročitan\n#{rep.to_text}"
    errors += 1
    next
  end

  %w[conceptual standard presentation].each do |lod|
    ctx = Builders::Context.new(lod: lod, materials: pkg.materials)
    canvas = RecordingCanvas.new
    pkg.elements.each { |e| ElementBuilder.build(e, canvas, ctx) }
    n = ctx.report.count(:error)
    errors += n
    puts "#{File.basename(path)} #{lod}: #{pkg.elements.length} elemenata, #{canvas.top_level.length} izgrađeno, grešaka #{n}"
    puts ctx.report.to_text if n > 0
  end

  plan = ChangePlanner.new.plan(pkg, [])
  puts "plan: #{plan.summary}"
end

puts "Ruby #{RUBY_VERSION}: #{errors.zero? ? 'u redu' : "#{errors} grešaka"}"
exit(errors.zero? ? 0 : 1)
