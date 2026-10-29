# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Unit conversion rules. Packages store metres; SketchUp's internal unit is the inch regardless of the
    # model's display units, so every package length is converted exactly once at the canvas boundary.
    module Units
      INCHES_PER_METER = 1.0 / 0.0254 # 39.37007874015748, exact by definition of the inch
      METERS_PER_INCH = 0.0254

      module_function

      def m_to_in(meters)
        meters * INCHES_PER_METER
      end

      def in_to_m(inches)
        inches * METERS_PER_INCH
      end

      def deg_to_rad(deg)
        deg * Math::PI / 180.0
      end

      def rad_to_deg(rad)
        rad * 180.0 / Math::PI
      end

      # Metres per source unit for codes used in manifest units.sourceLengthUnit.
      def meters_per_unit(code)
        { 'mm' => 0.001, 'cm' => 0.01, 'm' => 1.0, 'in' => 0.0254, 'ft' => 0.3048 }.fetch(code) do
          raise ArgumentError, "unknown length unit #{code.inspect}"
        end
      end
    end
  end
end
