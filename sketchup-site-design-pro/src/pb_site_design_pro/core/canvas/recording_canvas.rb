# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Canvas implementation that records geometry instead of creating SketchUp entities. Used by the unit
    # tests (and for dry-run previews) to verify dimensions, positions, elevations, component reuse and that
    # every solid mesh is closed and consistently oriented.
    #
    # Canvas contract (shared with the SketchUp canvas). All coordinates are metres in the current frame:
    #   group(name, attributes = {}) { |canvas| ... }          -> node
    #   mesh(polygons, material:, name: nil, smooth: false, solid: true) -> node
    #   edges(points, closed: false, name: nil)                  -> node
    #   instance(key, origin, rotation, name: nil, attributes = {}) { |definition_canvas| ... } -> node
    class RecordingCanvas
      Node = Struct.new(:kind, :name, :material, :polygons, :children, :key, :origin, :rotation, :attributes, :smooth, :solid)

      attr_reader :root, :definitions

      def initialize(root = nil, definitions = nil)
        @root = root || Node.new(:group, 'model', nil, nil, [], nil, nil, nil, {})
        @definitions = definitions || {}
      end

      def group(name, attributes = {})
        node = Node.new(:group, name, nil, nil, [], nil, nil, nil, attributes.dup)
        @root.children << node
        yield RecordingCanvas.new(node, @definitions) if block_given?
        node
      end

      def mesh(polygons, material:, name: nil, smooth: false, solid: true)
        raise ArgumentError, 'mesh without polygons' if polygons.nil? || polygons.empty?

        node = Node.new(:mesh, name, material, polygons, [], nil, nil, nil, {}, smooth, solid)
        @root.children << node
        node
      end

      def edges(points, closed: false, name: nil)
        pts = closed ? points + [points.first] : points
        node = Node.new(:edges, name, nil, [pts], [], nil, nil, nil, {}, false, false)
        @root.children << node
        node
      end

      def instance(key, origin, rotation, name: nil, attributes: {})
        unless @definitions.key?(key)
          defn = Node.new(:group, key, nil, nil, [], nil, nil, nil, {})
          @definitions[key] = defn
          yield RecordingCanvas.new(defn, @definitions) if block_given?
        end
        node = Node.new(:instance, name, nil, nil, [], key, origin.map(&:to_f), rotation.to_f, attributes.dup)
        @root.children << node
        node
      end

      # ---- inspection helpers (tests, previews) ----

      def each_node(node = @root, &block)
        node.children.each do |c|
          block.call(c)
          each_node(c, &block)
        end
      end

      def find_all(name = nil, &predicate)
        out = []
        each_node do |n|
          out << n if (name && n.name == name) || (predicate && predicate.call(n))
        end
        out
      end

      def find(name)
        find_all(name).first
      end

      def top_level
        @root.children
      end

      # Polygons of a node in world coordinates (instances expanded with their transformation).
      def world_polygons(node = @root, xf = nil)
        out = []
        case node.kind
        when :mesh, :edges
          node.polygons.each { |poly| out << transform_poly(poly, xf) }
        when :instance
          defn = @definitions.fetch(node.key)
          inner = compose(xf, node.origin, node.rotation)
          defn.children.each { |c| out.concat(world_polygons(c, inner)) }
        else
          node.children.each { |c| out.concat(world_polygons(c, xf)) }
        end
        out
      end

      def bounds(node = @root)
        pts = world_polygons(node).flat_map { |poly| poly.is_a?(Hash) ? poly[:outer] : poly }
        return nil if pts.empty?

        mins = [0, 1, 2].map { |k| pts.map { |p| p[k] }.min }
        maxs = [0, 1, 2].map { |k| pts.map { |p| p[k] }.max }
        [mins, maxs]
      end

      def stats
        s = Hash.new(0)
        each_node do |n|
          s[n.kind] += 1
          s[:faces] += n.polygons.length if n.kind == :mesh
        end
        s[:definitions] = @definitions.length
        s[:definition_faces] = @definitions.values.sum { |d| count_faces(d) }
        s
      end

      # Returns descriptions of solid meshes that are not closed, consistently oriented manifolds.
      def manifold_errors(node = @root)
        errors = []
        visit = lambda do |n, path|
          if n.kind == :mesh && n.solid
            err = mesh_manifold_error(n.polygons)
            errors << "#{path}/#{n.name || n.material}: #{err}" if err
          end
          n.children.each { |c| visit.call(c, "#{path}/#{n.name}") }
        end
        visit.call(node, '')
        @definitions.each_value { |d| visit.call(d, "def:#{d.name}") } if node.equal?(@root)
        errors
      end

      def self.loops(poly)
        poly.is_a?(Hash) ? [poly[:outer]] + poly[:holes] : [poly]
      end

      private

      def count_faces(node)
        node.children.sum { |c| (c.kind == :mesh ? c.polygons.length : 0) + count_faces(c) }
      end

      def mesh_manifold_error(polygons)
        key = ->(p) { p.map { |v| (v * 1e6).round }.join(',') }
        directed = Hash.new(0)
        polygons.each do |poly|
          RecordingCanvas.loops(poly).each do |loop_pts|
            return 'polygon with fewer than 3 points' if loop_pts.length < 3

            loop_pts.each_with_index do |a, i|
              b = loop_pts[(i + 1) % loop_pts.length]
              ka = key.call(a)
              kb = key.call(b)
              return 'degenerate edge' if ka == kb

              directed[[ka, kb]] += 1
            end
          end
        end
        directed.each do |(a, b), n|
          return 'edge used more than once in the same direction (inconsistent orientation)' if n > 1
          return 'open edge (mesh not closed)' unless directed[[b, a]] == 1
        end
        nil
      end

      def compose(xf, origin, rotation)
        local = lambda do |p|
          c = Math.cos(rotation)
          s = Math.sin(rotation)
          [origin[0] + (p[0] * c) - (p[1] * s), origin[1] + (p[0] * s) + (p[1] * c), origin[2] + p[2]]
        end
        xf ? ->(p) { xf.call(local.call(p)) } : local
      end

      def transform_poly(poly, xf)
        return poly unless xf

        if poly.is_a?(Hash)
          { outer: poly[:outer].map { |p| xf.call(p) }, holes: poly[:holes].map { |h| h.map { |p| xf.call(p) } } }
        else
          poly.map { |p| xf.call(p) }
        end
      end
    end
  end
end
