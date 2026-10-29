# frozen_string_literal: true

require 'time'

module ProjektniBiro
  module SiteDesignPro
    # Reverse exchange (SketchUp -> AutoCAD). Supported: parameter edits made with the Site Design Pro
    # edit dialog, and moves/rotations (about the vertical axis) of point elements. Geometry edits of path
    # and polygon elements are not transferred back; they are reported instead.
    class ChangeSet
      FORMAT_ID = 'site-design-pro-changes'
      POSITION_TOL = 0.001 # metres
      ROTATION_TOL = 1e-4  # radians

      Change = Struct.new(:id, :type_id, :label, :base_content_hash, :parameters, :position, :rotation, keyword_init: true)

      attr_reader :changes, :report

      def initialize(project_id:, package_id:, origin:)
        @project_id = project_id
        @package_id = package_id
        @origin = origin
        @changes = []
        @report = Report.new('Izvoz izmjena iz SketchUp-a')
      end

      # imported: { position: [x,y,z], rotation: r } stored at import (metres, package frame) or nil
      # current:  { position:, rotation:, tilted: bool, scaled: bool } from the model, or nil
      # params:   parameter edits made in SketchUp ({} when none)
      def add(id:, type_id:, label: nil, base_content_hash:, params: {}, imported: nil, current: nil, kind: 'point', geometry_modified: false)
        change = Change.new(id: id, type_id: type_id, label: label, base_content_hash: base_content_hash,
                            parameters: params.dup, position: nil, rotation: nil)
        if kind == 'point' && imported && current
          if current[:tilted] || current[:scaled]
            @report.add(:warning, 'SDP-X010', id, 'Objekat je skaliran ili nagnut u SketchUp-u; nazad se šalju samo položaj i rotacija oko vertikalne ose.',
                        'Poništite skaliranje/nagib ili ažurirajte element ručno u AutoCAD-u.', label)
          end
          moved = (0..2).any? { |k| (current[:position][k] - imported[:position][k]).abs > POSITION_TOL }
          turned = angle_diff(current[:rotation], imported[:rotation]) > ROTATION_TOL
          change.position = current[:position].map { |v| v.round(6) } if moved
          change.rotation = normalize(current[:rotation]).round(9) if moved || turned
        elsif geometry_modified
          @report.add(:info, 'SDP-X011', id, "Geometrija ovog elementa (#{kind == 'path' ? 'trasa' : 'površina'}) izmijenjena je u SketchUp-u; takve izmjene se ne prenose u AutoCAD.",
                      'Izmjenu napravite u AutoCAD-u i ponovo uvezite, ili je zadržite samo u SketchUp-u.', label)
        end
        return nil if change.parameters.empty? && change.position.nil? && change.rotation.nil?

        @changes << change
        change
      end

      def empty?
        @changes.empty?
      end

      def to_h
        {
          'format' => FORMAT_ID,
          'schemaVersion' => SCHEMA_VERSION,
          'createdUtc' => Time.now.utc.iso8601,
          'generator' => { 'application' => 'SketchUp Site Design Pro', 'version' => VERSION },
          'projectId' => @project_id,
          'sourcePackageId' => @package_id,
          'units' => { 'length' => 'm', 'angle' => 'rad' },
          'coordinateSystem' => { 'origin' => @origin },
          'changes' => @changes.map do |c|
            h = { 'id' => c.id, 'type' => c.type_id, 'baseContentHash' => c.base_content_hash }
            h['label'] = c.label if c.label
            h['parameters'] = c.parameters unless c.parameters.empty?
            h['position'] = c.position if c.position
            h['rotation'] = c.rotation unless c.rotation.nil?
            h
          end
        }
      end

      def to_json(*_args)
        JSON.pretty_generate(to_h)
      end

      private

      def normalize(a)
        t = a % (2 * Math::PI)
        t.negative? ? t + (2 * Math::PI) : t
      end

      def angle_diff(a, b)
        d = (normalize(a) - normalize(b)).abs
        [d, (2 * Math::PI) - d].min
      end
    end
  end
end
