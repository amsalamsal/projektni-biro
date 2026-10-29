# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Creates or refreshes review scenes. Each scene stores tag visibility, camera and (for the underground
    # scene) X-ray rendering. The user's current visibility and camera are restored afterwards.
    module Scenes
      REVIEW = 'review_zones'
      SURFACE_GROUPS = %w[surfaces roads pedestrian_cycling planting buildings water].freeze
      DEFINITIONS = [
        { name: 'SDP 01 Site plan', camera: :plan, hide: %w[review_zones underground] },
        { name: 'SDP 02 3D overview', camera: :iso, hide: %w[review_zones underground] },
        { name: 'SDP 03 Pedestrian areas', camera: :iso, show: %w[pedestrian_cycling surfaces furniture recreation buildings annotations] },
        { name: 'SDP 04 Roads and parking', camera: :plan, show: %w[roads surfaces buildings annotations] },
        { name: 'SDP 05 Landscape and planting', camera: :iso, show: %w[planting surfaces water buildings annotations] },
        { name: 'SDP 06 Lighting', camera: :plan, show: %w[lighting surfaces buildings roads pedestrian_cycling annotations] },
        { name: 'SDP 07 Water and drainage', camera: :iso, show: %w[water drainage surfaces buildings annotations] },
        { name: 'SDP 08 Underground utilities', camera: :below, xray: true, show: %w[underground drainage buildings annotations review_zones] },
        { name: 'SDP 09 Material review', camera: :iso, hide: %w[review_zones] },
        { name: 'SDP 10 Presentation', camera: :perspective, hide: %w[review_zones underground annotations] }
      ].freeze

      module_function

      def create(model, organizer, catalog)
        root = organizer.root
        return if root.nil? || !root.valid?

        tags = catalog.model_groups.values.each_with_object({}) { |g, acc| acc[g.id] = model.layers[g.tag] }
        saved_visibility = model.layers.map { |l| [l, l.visible?] }
        saved_camera = model.active_view.camera
        saved_xray = model.rendering_options['ModelTransparency']
        bounds = root.bounds
        DEFINITIONS.each do |sd|
          tags.each do |id, layer|
            next unless layer

            layer.visible = sd[:show] ? sd[:show].include?(id) : !Array(sd[:hide]).include?(id)
          end
          model.rendering_options['ModelTransparency'] = sd[:xray] ? true : false
          model.active_view.camera = camera(sd[:camera], bounds)
          page = model.pages[sd[:name]]
          if page
            page.update
          else
            model.pages.add(sd[:name])
          end
        end
      ensure
        saved_visibility&.each { |l, v| l.visible = v if l.valid? }
        model.rendering_options['ModelTransparency'] = saved_xray unless saved_xray.nil?
        model.active_view.camera = saved_camera if saved_camera
      end

      def camera(kind, bounds)
        c = bounds.center
        size = [bounds.width, bounds.height, 1.0].max
        case kind
        when :plan
          cam = Sketchup::Camera.new(Geom::Point3d.new(c.x, c.y, c.z + (size * 2)), c, Geom::Vector3d.new(0, 1, 0))
          cam.perspective = false
          cam.height = size * 1.1
          cam
        when :below
          eye = Geom::Point3d.new(c.x - (size * 0.6), c.y - (size * 0.8), bounds.min.z - (size * 0.25))
          Sketchup::Camera.new(eye, Geom::Point3d.new(c.x, c.y, bounds.min.z), Geom::Vector3d.new(0, 0, 1))
        when :perspective
          eye = Geom::Point3d.new(c.x - (size * 0.45), c.y - (size * 0.55), Units.m_to_in(1.7) + bounds.min.z + (size * 0.05))
          Sketchup::Camera.new(eye, c, Geom::Vector3d.new(0, 0, 1))
        else
          eye = Geom::Point3d.new(c.x - size, c.y - size, c.z + (size * 0.8))
          Sketchup::Camera.new(eye, c, Geom::Vector3d.new(0, 0, 1))
        end
      end
    end
  end
end
