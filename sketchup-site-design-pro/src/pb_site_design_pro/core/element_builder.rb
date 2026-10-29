# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Builds one package element on a canvas: point elements become component instances (definitions shared
    # between elements with equal parameters), path and polygon elements become groups in the package frame.
    module ElementBuilder
      module_function

      def supported?(element)
        !Builders.for(element.builder).nil?
      end

      # Returns the created canvas node, or nil when the element could not be reconstructed (reported).
      def build(element, canvas, ctx, attributes = {})
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
    end
  end
end
