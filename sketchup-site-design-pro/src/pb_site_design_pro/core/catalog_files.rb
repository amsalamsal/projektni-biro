# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Reads the shared catalog files shipped with the extension (catalog/), used for the model hierarchy,
    # tags and material fallbacks. Element types themselves travel inside each package.
    class CatalogFiles
      ModelGroup = Struct.new(:id, :name, :tag, :order)

      # Labels of descriptive properties not defined in commonProperties (planting data).
      EXTRA_PROPERTY_LABELS = {
        'botanicalName' => 'Botanički naziv', 'commonName' => 'Narodni naziv', 'plantCode' => 'Šifra biljke',
        'plantingSize' => 'Veličina sadnice', 'dataSource' => 'Izvor podataka', 'surfaceRequirement' => 'Zahtjev za podlogu',
        'elevation.mode' => 'Način zadavanja kote', 'elevation.value' => 'Zadana vrijednost kote',
        'elevation.source' => 'Izvor kote', 'elevation.relative' => 'Kota u odnosu na ±0,00 (m)'
      }.freeze

      attr_reader :dir, :model_groups, :categories, :materials, :version, :option_labels, :property_labels

      # Extension catalog folder; during development falls back to the repository's shared/catalog.
      def self.default_dir
        bundled = File.expand_path('../catalog', __dir__)
        return bundled if File.file?(File.join(bundled, 'categories.json'))

        File.expand_path('../../../../shared/catalog', __dir__)
      end

      def initialize(dir = CatalogFiles.default_dir)
        @dir = dir
        cats = JSON.parse(File.read(File.join(dir, 'categories.json'), encoding: 'UTF-8'))
        @version = cats['version']
        @categories = cats['categories'].each_with_object({}) { |c, acc| acc[c['id']] = c }
        @model_groups = cats['modelGroups'].each_with_object({}) do |g, acc|
          acc[g['id']] = ModelGroup.new(g['id'], g['name'], g['tag'], g['order'].to_i)
        end
        @property_labels = EXTRA_PROPERTY_LABELS.merge((cats['commonProperties'] || []).to_h { |p| [p['key'], p['label']] })
        builders_file = File.join(dir, 'builders.json')
        @option_labels = File.file?(builders_file) ? (JSON.parse(File.read(builders_file, encoding: 'UTF-8'))['optionLabels'] || {}) : {}
        mats = JSON.parse(File.read(File.join(dir, 'materials.json'), encoding: 'UTF-8'))
        @materials = mats['materials'].each_with_object({}) do |m, acc|
          acc[m['key']] = Material.new(m['key'], m['name'], m['color'], (m['alpha'] || 1.0).to_f, m['texture'], m['textureSize'])
        end
      end

      # Model group for an id, with a readable fallback for unknown ids (e.g. from a newer catalog).
      def model_group(id)
        @model_groups[id] || begin
          name = id.to_s.empty? ? 'Ostali elementi lokacije' : id.to_s.tr('_', ' ').capitalize
          ModelGroup.new(id.to_s, name, "VU #{name}", 99)
        end
      end

      def category_name(id)
        (@categories[id.to_s] || {})['name'] || id.to_s
      end
    end
  end
end
