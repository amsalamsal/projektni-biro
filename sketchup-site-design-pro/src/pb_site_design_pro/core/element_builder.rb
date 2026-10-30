# frozen_string_literal: true

require 'delegate'

module ProjektniBiro
  module SiteDesignPro
    # Builds one package element on a canvas: point elements become component instances (definitions shared
    # between elements with equal parameters), path and polygon elements become groups in the package frame.
    module ElementBuilder
      module_function

      def supported?(element)
        !model_for(element).nil? || !Builders.for(element.builder).nil?
      end

      # Detailed asset model of the element's type, when one exists for its geometry kind.
      def model_for(element)
        m = Models.for(element.type_id)
        m && m.geometry == element.kind ? m : nil
      end

      # Component definition key of a model: equal type, version, level of detail and parameters share one definition.
      def model_key(model, element, ctx)
        parts = element.params.keys.sort.map { |k| v = element.params[k]; "#{k}=#{v.is_a?(Float) ? format('%.6g', v) : v}" }
        "model:#{model.type_id}|v#{model.version}|#{ctx.lod}|#{parts.join(';')}"
      end

      # Returns the created canvas node, or nil when the element could not be reconstructed (reported).
      def build(element, canvas, ctx, attributes = {})
        model = model_for(element)
        return build_model(model, element, canvas, ctx, attributes) if model

        klass = Builders.for(element.builder)
        unless klass
          ctx.report.add(:warning, 'SDP-B100', element.id, "Nema 3D generatora za '#{element.builder}' (tip #{element.type_id}); element je preskočen.",
                         'Ažurirajte Vanjsko uređenje PRO za SketchUp ili tipu dodijelite komponentu u biblioteci.', element.label)
          return nil
        end
        if klass.geometry != element.kind
          ctx.report.add(:error, 'SDP-B101', element.id, "Generator '#{element.builder}' očekuje geometriju #{klass.geometry}, a dobio je #{element.kind}.",
                         'Ponovo izvezite paket.', element.label)
          return nil
        end

        builder = klass.new(ctx)
        if element.kind == 'point'
          g = element.geometry
          canvas.instance(builder.definition_key(element), g[:position], g[:rotation], name: element.display_name, attributes: attributes) do |dc|
            builder.build(element, dc)
          end
        else
          canvas.group(element.display_name, attributes) { |gc| builder.build(element, gc) }
        end
      rescue ArgumentError, ZeroDivisionError, FloatDomainError, TypeError, NoMethodError, KeyError, RangeError => e
        ctx.report.add(:error, 'SDP-B999', element.id, "Rekonstrukcija geometrije nije uspjela: #{e.class}: #{e.message}",
                       'Provjerite geometriju i parametre elementa u AutoCAD-u (SDPVALIDATE); prijavite grešku ako se ponavlja.', element.label)
        nil
      end

      def build_model(model, element, canvas, ctx, attributes)
        adj = adjustments(model, element, ctx)
        draw = lambda do |c|
          c = AdjustCanvas.new(c, adj[:scale], adj[:remap]) if adj[:scale] || !adj[:remap].empty?
          model.block.call(Models::Kit.new(c, ctx, element), element)
        end
        if element.kind == 'point'
          g = element.geometry
          origin = [g[:position][0], g[:position][1], (g[:position][2] || 0.0) + adj[:lift]]
          canvas.instance(model_key(model, element, ctx), origin, g[:rotation], name: element.display_name, attributes: attributes, &draw)
        else
          canvas.group(element.display_name, attributes, &draw)
        end
      rescue ArgumentError, ZeroDivisionError, FloatDomainError, TypeError, NoMethodError, KeyError, RangeError => e
        ctx.report.add(:error, 'SDP-B998', element.id, "Model '#{model.type_id}' nije izgrađen: #{e.class}: #{e.message}",
                       'Prijavite grešku; element se može privremeno prikazati generičkim modelom (ukloniti model iz biblioteke).', element.label)
        nil
      end

      # Catalog parameters a model must honour even when its generator does not read them: mounting height
      # (baseOffset), main dimensions (scaled proportionally to the type default) and material choices (the
      # default material of an unread material parameter is replaced by the chosen one). Determined by building
      # the model once with a parameter tracker; skipped when the element uses only defaults.
      DIMENSIONS = { 'width' => [0], 'depth' => [1], 'height' => [2], 'diameter' => [0, 1] }.freeze

      def adjustments(model, element, ctx)
        none = { lift: 0.0, scale: nil, remap: {} }
        td = element.type_def
        return none unless td

        defaults = td.defaults
        changed = td.parameters.select do |p|
          v = element.params[p['key']]
          next false if v.nil? || v == p['default']

          %w[baseOffset coverLevelOffset].include?(p['key']) || DIMENSIONS.key?(p['key']) || p['type'] == 'material'
        end
        return none if changed.empty?

        read = params_read(model, element, ctx)
        out = { lift: 0.0, scale: nil, remap: {} }
        changed.each do |p|
          key = p['key']
          next if read.include?(key)

          v = element.params[key]
          d = defaults[key]
          if %w[baseOffset coverLevelOffset].include?(key) && element.kind == 'point' && v.is_a?(Numeric)
            out[:lift] += v.to_f - (d.is_a?(Numeric) ? d.to_f : 0.0)
          elsif DIMENSIONS.key?(key) && element.kind == 'point' && v.is_a?(Numeric) && d.is_a?(Numeric) && d.positive? && v.positive?
            out[:scale] ||= [1.0, 1.0, 1.0]
            DIMENSIONS[key].each { |i| out[:scale][i] *= v.to_f / d }
          elsif p['type'] == 'material' && d.is_a?(String) && !d.empty?
            out[:remap][d] = v.to_s
          end
        end
        out
      end

      # Parameter keys the model reads for this element (cached per definition key).
      def params_read(model, element, ctx)
        @params_read ||= {}
        @params_read[model_key(model, element, ctx)] ||= begin
          tracker = ParamTracker.new(element)
          probe = Builders::Context.new(lod: ctx.lod, materials: ctx.materials)
          model.block.call(Models::Kit.new(RecordingCanvas.new, probe, tracker), tracker)
          tracker.read
        rescue StandardError
          tracker ? tracker.read : []
        end
      end
    end

    # Element wrapper recording which parameters are read.
    class ParamTracker < SimpleDelegator
      attr_reader :read

      def initialize(element)
        super
        @read = []
      end

      %i[param num bool text].each do |m|
        define_method(m) do |key, *rest|
          @read << key unless @read.include?(key)
          __getobj__.send(m, key, *rest)
        end
      end
    end

    # Canvas wrapper applying a plan/height scale (about the insertion point) and a material remap; component
    # parts are drawn inline so the adjustment applies to them as well.
    class AdjustCanvas
      def initialize(canvas, scale, remap, xf = nil)
        @c = canvas
        @scale = scale
        @remap = remap
        @xf = xf || (scale ? ->(p) { [p[0] * scale[0], p[1] * scale[1], (p[2] || 0.0) * scale[2]] } : ->(p) { p })
      end

      def mesh(polygons, material:, name: nil, smooth: false, solid: true)
        polys = polygons.map { |p| p.is_a?(Hash) ? { outer: p[:outer].map(&@xf), holes: p[:holes].map { |h| h.map(&@xf) } } : p.map(&@xf) }
        @c.mesh(polys, material: @remap.fetch(material, material), name: name, smooth: smooth, solid: solid)
      end

      def edges(points, closed: false, name: nil)
        @c.edges(points.map(&@xf), closed: closed, name: name)
      end

      def group(name, attributes = {})
        @c.group(name, attributes) { |gc| yield AdjustCanvas.new(gc, @scale, @remap, @xf) if block_given? }
      end

      def instance(_key, origin, rotation, name: nil, attributes: {})
        c = Math.cos(rotation)
        s = Math.sin(rotation)
        outer = @xf
        xf = ->(p) { outer.call([origin[0] + (c * p[0]) - (s * p[1]), origin[1] + (s * p[0]) + (c * p[1]), (origin[2] || 0.0) + (p[2] || 0.0)]) }
        @c.group(name, attributes) { |gc| yield AdjustCanvas.new(gc, @scale, @remap, xf) if block_given? }
      end
    end
  end
end
