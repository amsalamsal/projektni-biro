# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Parametric 3D builders. Each builder turns one element (package frame, metres) into canvas geometry.
    # Point builders draw a component definition in the element's local frame (insertion at the origin,
    # rotation applied by the instance); path and polygon builders draw in the package frame.
    module Builders
      @registry = {}

      class << self
        def register(id, klass)
          @registry[id] = klass
        end

        def for(id)
          @registry[id]
        end

        def ids
          @registry.keys.sort
        end
      end

      # Shared build settings: level of detail, valid materials and an issue sink.
      class Context
        LODS = %w[conceptual standard presentation].freeze

        attr_reader :lod, :materials, :report

        def initialize(lod: 'standard', materials: {}, report: nil)
          raise ArgumentError, "nepoznat nivo detalja #{lod}" unless LODS.include?(lod.to_s)

          @lod = lod.to_s
          @materials = materials
          @report = report || Report.new
        end

        def conceptual?
          @lod == 'conceptual'
        end

        def presentation?
          @lod == 'presentation'
        end

        def segments(base = 16)
          Mesh.segments_for(@lod, base)
        end
      end

      class Base
        GEOMETRY = nil

        def self.geometry
          self::GEOMETRY
        end

        def initialize(ctx)
          @ctx = ctx
        end

        attr_reader :ctx

        # Component definition key for point builders: equal keys share one definition.
        def definition_key(element)
          parts = element.params.keys.sort.map { |k| "#{k}=#{fmt(element.params[k])}" }
          "#{element.builder}|#{@ctx.lod}|#{parts.join(';')}"
        end

        protected

        def fmt(v)
          v.is_a?(Float) ? format('%.6g', v) : v.to_s
        end

        # Material key from a material parameter, validated against the package materials.
        def mat(element, key, fallback = 'default')
          value = element.param(key)
          return fallback if value.nil? || value.to_s.empty?
          return value if @ctx.materials.empty? || @ctx.materials.key?(value)

          @ctx.report.add(:warning, 'SDP-M001', element.id, "Materijal '#{value}' nije definisan; koristi se '#{fallback}'.",
                          'Dodajte materijal u korisničku datoteku materijala u AutoCAD-u i ponovo izvezite.', element.label)
          fallback
        end

        def warn(element, code, message, resolution = '')
          @ctx.report.add(:warning, code, element.id, message, resolution, element.label)
        end

        # Tessellated outer ring and holes of a polygon element: [[outer3d], [[hole3d], ...]].
        def polygon_rings(element)
          g = element.geometry
          tol = @ctx.presentation? ? 0.01 : 0.03
          outer = Geometry.dedupe(Geometry.tessellate(g[:outer][:vertices], g[:outer][:bulges], true, tol), true)
          holes = g[:holes].map { |h| Geometry.dedupe(Geometry.tessellate(h[:vertices], h[:bulges], true, tol), true) }
          [outer, holes.reject { |h| h.length < 3 }]
        end

        # Tessellated path points (3D).
        def path_points(element)
          g = element.geometry
          tol = @ctx.presentation? ? 0.01 : 0.03
          Geometry.dedupe(Geometry.tessellate(g[:vertices], g[:bulges], g[:closed], tol), g[:closed])
        end

        def base_z(points)
          points.first[2] || 0.0
        end

        def rect_profile(left, right, bottom, top)
          [[left, bottom], [left, top], [-right, top], [-right, bottom]]
        end

        def circle_profile(r, n, w0 = 0.0)
          Array.new(n) { |i| t = 2 * Math::PI * i / n; [Math.cos(t) * r, w0 + (Math.sin(t) * r)] }
        end
      end

      class PointBuilder < Base
        GEOMETRY = 'point'
      end

      class PathBuilder < Base
        GEOMETRY = 'path'
      end

      class PolygonBuilder < Base
        GEOMETRY = 'polygon'
      end
    end
  end
end
