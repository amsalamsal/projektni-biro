# frozen_string_literal: true

require 'zlib'

module ProjektniBiro
  module SiteDesignPro
    # Compact fingerprint of an imported entity's state (transformation, definition, entity count, bounds).
    # Stored at import; a different fingerprint on re-import means the user modified the object in SketchUp.
    module Fingerprint
      module_function

      # parts: array of strings / numbers / arrays of numbers. Numbers are rounded to 1e-4 so
      # floating-point noise does not register as an edit.
      def compute(parts)
        text = parts.flatten.map { |p| p.is_a?(Numeric) ? format('%.4f', p.to_f.round(4)).sub(/\A-(0\.0+)\z/, '\1') : p.to_s }.join('|')
        "#{Zlib.crc32(text).to_s(16).rjust(8, '0')}-#{text.length}"
      end
    end
  end
end
