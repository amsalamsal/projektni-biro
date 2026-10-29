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
        @report = Report.new('SketchUp change export')
      end

      # imported: { position: [x,y,z], rotation: r } stored at import (metres, package frame) or nil
      # current:  { position:, rotation:, tilted: bool, scaled: bool } from the model, or nil
      # params:   parameter edits made in SketchUp ({} when none)
      def add(id:, type_id:, label: nil, base_content_hash:, params: {}, imported: nil, current: nil, kind: 'point', geometry_modified: false)
        change = Change.new(id: id, type_id: type_id, label: label, base_content_hash: base_content_hash,
                            parameters: params.dup, position: nil, rotation: nil)
        if kind == 'point' && imported && current
          if current[:tilted] || current[:scaled]
            @report.add(:warning, 'SDP-X010', id, 'Object was scaled or tilted in SketchUp; only position and rotation about the vertical axis can be sent back.',
                        'Reset scale/tilt or update the element in AutoCAD manually.', label)
          end
          moved = (0..2).any? { |k| (current[:position][k] - imported[:position][k]).abs > POSITION_TOL }
          turned = angle_diff(current[:rotation], imported[:rotation]) > ROTATION_TOL
          change.position = current[:position].map { |v| v.round(6) } if moved
          change.rotation = normalize(current[:rotation]).round(9) if moved || turned
        elsif geometry_modified
          @report.add(:info, 'SDP-X011', id, "Geometry of this #{kind} element was edited in SketchUp; such edits are not transferred to AutoCAD.",
                      'Make the change in AutoCAD and re-import, or keep it as a SketchUp-only customisation.', label)
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
