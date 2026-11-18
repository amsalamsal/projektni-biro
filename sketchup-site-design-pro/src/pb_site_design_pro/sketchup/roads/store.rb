# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Roads
      # The source elements of the road tools in the model: every street, road, crossing, mark and ramp is a small
      # group (its axis, outline or mark drawn with edges) inside the group "Saobraćajnice – osovine", with the type,
      # parameters and geometry in its attributes. The groups can be moved, rotated, copied and erased with the
      # SketchUp tools, and the vertices of a straight axis moved inside its group (also up and down: the height of
      # the axis there); the roads are rebuilt from them.
      class SourceStore
        DICT = 'SDP_Road'
        SETTINGS = 'SDP_Roads'
        ROOT_ROLE = 'roadsSource'
        ROOT_NAME = 'Saobraćajnice – osovine (Vanjsko uređenje PRO)'
        TAG = 'VU Saobraćajnice – osovine'
        MARK_SIZE = 0.6

        attr_reader :model

        def initialize(model)
          @model = model
        end

        # ---- settings of the road project (stored in the model)

        def settings
          d = @model.attribute_dictionary(SETTINGS, false)
          h = {}
          d.each { |k, v| h[k] = v } if d
          unless h['projectId'].to_s =~ /\A[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\z/
            h['projectId'] = Roads.new_id
            @model.set_attribute(SETTINGS, 'projectId', h['projectId'])
          end
          h['projectName'] ||= 'Saobraćajnice'
          h['flat'] = h['flat'] == true
          h['base'] = h['base'].nil? ? nil : h['base'].to_f
          h['autoRefresh'] = h['autoRefresh'] != false
          h
        end

        def set(key, value)
          @model.set_attribute(SETTINGS, key, value)
        end

        def last_params(key, default)
          raw = @model.get_attribute(SETTINGS, "last_#{key}", nil) || Sketchup.read_default('PB_SiteDesignPro', "roads_#{key}", nil)
          raw ? default.merge(JSON.parse(raw)) : default
        rescue JSON::ParserError
          default
        end

        def remember(key, params)
          text = JSON.generate(params)
          @model.set_attribute(SETTINGS, "last_#{key}", text)
          Sketchup.write_default('PB_SiteDesignPro', "roads_#{key}", text)
        end

        def next_label(type)
          prefix = LABEL_PREFIX[type] || 'E'
          used = records.map(&:label).compact
          n = (@model.get_attribute(SETTINGS, "counter_#{prefix}", 0) || 0).to_i
          loop do
            n += 1
            label = format('%s-%03d', prefix, n)
            next if used.include?(label)

            @model.set_attribute(SETTINGS, "counter_#{prefix}", n)
            return label
          end
        end

        # ---- the groups

        def root(create = true)
          found = @model.entities.grep(Sketchup::Group).find { |g| g.get_attribute(DICT, 'role') == ROOT_ROLE }
          return found if found || !create

          g = @model.entities.add_group
          g.name = ROOT_NAME
          g.set_attribute(DICT, 'role', ROOT_ROLE)
          tag = source_tag
          g.layer = tag if tag
          g
        end

        def source_tag
          layers = @model.layers
          layers[TAG] || layers.add(TAG)
        rescue StandardError
          nil
        end

        # Adds a source element; returns its group. geometry in metres (model frame). parent: a group inside the
        # root (roundabout) or nil.
        def add(type: nil, params: {}, geometry: nil, name: nil, label: nil, id: nil, parent: nil)
          raise ArgumentError, "type i geometry su obavezni" unless type && geometry

          container = parent || root
          rec = Record.new(id: id || Roads.new_id, type: type, name: name || TYPE_NAMES[type] || type,
                           label: label || next_label(type), params: params, geometry: geometry)
          g = container.entities.add_group
          rec.ref = g
          write(g, rec, SourceStore.world_of(container))
          g
        end

        # A group that holds several source elements which belong together (roundabout).
        def add_container(name, attrs = {})
          g = root.entities.add_group
          g.name = name
          g.set_attribute(DICT, 'role', 'container')
          attrs.each { |k, v| g.set_attribute(DICT, k, v) }
          g
        end

        # New parameters (and name) of a source element; the geometry stays.
        def update(group, params: nil, name: nil, geometry: nil, type: nil)
          world = SourceStore.world_of(group)
          rec = read(group, world)
          return nil unless rec

          rec.params = params if params
          rec.type = type if type
          rec.name = name if name
          rec.geometry = geometry if geometry
          write(group, rec, world * group.transformation.inverse, keep_place: geometry.nil?)
          rec
        end

        # All source elements with their geometry in the model frame (metres). Copies made in SketchUp get new ids.
        def records
          list = []
          r = root(false)
          return list unless r && r.valid?

          seen = {}
          walk(r.entities, r.transformation) do |g, tr|
            rec = read(g, tr)
            next unless rec

            if seen[rec.id]
              rec.id = Roads.new_id
              rec.label = "#{rec.label}k" unless rec.label.to_s.empty?
              g.set_attribute(DICT, 'id', rec.id)
              g.set_attribute(DICT, 'label', rec.label)
            end
            seen[rec.id] = true
            list << rec
          end
          list
        end

        def find(id)
          records.find { |r| r.id == id }
        end

        # A cheap fingerprint of the source groups: changes when one is added, erased, moved, edited or changed.
        def signature
          r = root(false)
          return '' unless r && r.valid?

          parts = [r.transformation.to_a.map { |v| v.round(4) }.join(',')]
          walk(r.entities, r.transformation) do |g, _tr|
            b = g.bounds
            parts << [g.get_attribute(DICT, 'id'), g.get_attribute(DICT, 'params'), g.transformation.to_a.map { |v| v.round(4) }.join(','),
                      b.empty? ? '' : (b.min.to_a + b.max.to_a).map { |v| v.to_f.round(3) }.join(','), g.entities.length].join('|')
          end
          parts.join(';')
        end

        def built_signature
          @model.get_attribute(SETTINGS, 'builtSignature', nil)
        end

        def built_signature=(value)
          @model.set_attribute(SETTINGS, 'builtSignature', value)
        end

        def erase(group)
          group.erase! if group && group.valid?
        end

        # The road (network road record) whose axis is nearest to a point (metres): [record, hit] or nil.
        def nearest_road(p, types = NETWORK_ROADS, list = records)
          best = nil
          list.each do |rec|
            next unless types.include?(rec.type) && rec.kind == 'path'

            hit = Plan.nearest(rec.axis_points, p, rec.geometry['closed'] == true)
            best = [rec, hit] if hit && (best.nil? || hit[:dist] < best[1][:dist])
          end
          best
        end

        # Transformation from the local frame of an entity (group) to the model.
        def self.world_of(entity)
          tr = entity.transformation
          parent = entity.parent
          while parent.is_a?(Sketchup::ComponentDefinition) && (inst = parent.instances.first)
            tr = inst.transformation * tr
            parent = inst.parent
          end
          tr
        end

        def self.xf(tr, v)
          p = Geom::Point3d.new(Units.m_to_in(v[0].to_f), Units.m_to_in(v[1].to_f), Units.m_to_in((v[2] || 0).to_f)).transform(tr)
          [Units.in_to_m(p.x.to_f), Units.in_to_m(p.y.to_f), Units.in_to_m(p.z.to_f)]
        end

        # Half width (to the outer edge) of a network road record.
        def self.half_width(rec)
          if rec.type == STREET
            s = StreetSpec.new(rec.params)
            [s.extent(:edge, true), s.extent(:edge, false)].max
          else
            Roads.simple_halfwidth(rec.params)
          end
        end

        private

        def walk(entities, tr, &block)
          entities.grep(Sketchup::Group).each do |g|
            next unless g.valid?

            t = tr * g.transformation
            if g.get_attribute(DICT, 'role') == 'container'
              walk(g.entities, t, &block)
            elsif g.get_attribute(DICT, 'id')
              block.call(g, t)
            end
          end
        end

        def read(g, world)
          id = g.get_attribute(DICT, 'id')
          return nil unless id

          local = JSON.parse(g.get_attribute(DICT, 'geometry', '{}'))
          local = edited_path(g, local) if local['kind'] == 'path'
          geometry = Geo.map(local) { |v| SourceStore.xf(world, v) }
          Record.new(id: id, type: g.get_attribute(DICT, 'type'), name: g.get_attribute(DICT, 'name', ''), label: g.get_attribute(DICT, 'label', ''),
                     params: JSON.parse(g.get_attribute(DICT, 'params', '{}')), geometry: geometry, ref: g)
        rescue JSON::ParserError => e
          Log.warn("Izvorni element saobraćajnice nije čitljiv: #{e.message}")
          nil
        end

        # The vertices of a straight axis moved inside its group: the chain of its edges (in the order of the stored axis).
        def edited_path(g, local)
          return local if local['bulges'] || g.get_attribute(DICT, 'drawn').nil?

          edges = g.entities.grep(Sketchup::Edge)
          current = edges_signature(edges)
          return local if current == g.get_attribute(DICT, 'drawn')

          chain = chain_points(edges)
          return local if chain.nil? || chain.length < 2

          first = local['vertices'].first
          chain.reverse! if Geometry.dist(chain.last, first) < Geometry.dist(chain.first, first)
          closed = local['closed'] == true && chain.length > 2 && Geometry.dist(chain.first, chain.last) < 1e-4
          chain.pop if closed
          local.merge('vertices' => chain, 'closed' => closed)
        end

        def edges_signature(edges)
          edges.map { |e| edge_points(e).map { |p| [p.x, p.y, p.z].map { |c| Units.in_to_m(c.to_f).round(4) } }.sort }.sort.inspect
        end

        def edge_points(e)
          [e.start.position, e.end.position]
        end

        # One open or closed chain of edges as points (metres, group frame), or nil when the edges branch.
        def chain_points(edges)
          key = ->(p) { [p.x, p.y, p.z].map { |c| (Units.in_to_m(c.to_f) * 1e4).round } }
          adj = Hash.new { |h, k| h[k] = [] }
          pos = {}
          edges.each do |e|
            a, b = edge_points(e)
            ka = key.call(a)
            kb = key.call(b)
            next if ka == kb

            adj[ka] << kb
            adj[kb] << ka
            pos[ka] = a
            pos[kb] = b
          end
          return nil if adj.empty? || adj.values.any? { |n| n.length > 2 }

          start = adj.keys.find { |k| adj[k].length == 1 } || adj.keys.first
          out = [start]
          prev = nil
          cur = start
          loop do
            nxt = adj[cur].find { |k| k != prev }
            break if nxt.nil? || (nxt == start && out.length > 2)
            break if out.include?(nxt)

            out << nxt
            prev = cur
            cur = nxt
          end
          out << start if adj[start].length == 2 && adj[cur].include?(start) # closed
          out.map { |k| p = pos[k]; [Units.in_to_m(p.x.to_f), Units.in_to_m(p.y.to_f), Units.in_to_m(p.z.to_f)] }
        end

        # Draws the element in its group (parent_world: transformation of the group's parent to the model). A new
        # place puts the group origin at the first point; keep_place keeps the group where it is.
        def write(g, rec, parent_world, keep_place: false)
          unless keep_place
            o = SourceStore.xf(parent_world.inverse, first_point(rec.geometry))
            g.transformation = Geom::Transformation.translation(Geom::Vector3d.new(Units.m_to_in(o[0]), Units.m_to_in(o[1]), Units.m_to_in(o[2])))
          end
          to_local = (parent_world * g.transformation).inverse
          local = Geo.map(rec.geometry) { |v| SourceStore.xf(to_local, v) }
          g.entities.clear!
          draw(g.entities, local, rec.type)
          g.name = [rec.label, rec.name].reject { |x| x.to_s.empty? }.join(' ')
          { 'id' => rec.id, 'type' => rec.type, 'name' => rec.name.to_s, 'label' => rec.label.to_s,
            'params' => JSON.generate(rec.params || {}), 'geometry' => JSON.generate(local) }.each { |k, v| g.set_attribute(DICT, k, v) }
          g.set_attribute(DICT, 'drawn', local['kind'] == 'path' && local['bulges'].nil? ? edges_signature(g.entities.grep(Sketchup::Edge)) : nil)
          tag = source_tag
          g.layer = tag if tag && g.respond_to?(:layer=)
          g
        end

        def first_point(geometry)
          case geometry['kind']
          when 'point' then geometry['position']
          when 'path' then geometry['vertices'].first
          else geometry['outer']['vertices'].first
          end
        end

        def pt(v)
          Geom::Point3d.new(Units.m_to_in(v[0]), Units.m_to_in(v[1]), Units.m_to_in(v[2] || 0))
        end

        def draw(entities, g, type)
          case g['kind']
          when 'path'
            pts = Geometry.tessellate(g['vertices'], g['bulges'], g['closed'] == true, 0.05)
            pts += [pts.first] if g['closed'] == true && pts.length > 2
            entities.add_edges(pts.map { |v| pt(v) }) if pts.length >= 2
          when 'polygon'
            ring = Geometry.tessellate(g['outer']['vertices'], g['outer']['bulges'], true, 0.05)
            entities.add_edges((ring + [ring.first]).map { |v| pt(v) }) if ring.length >= 3
            (g['holes'] || []).each do |h|
              hr = Geometry.tessellate(h['vertices'], h['bulges'], true, 0.05)
              entities.add_edges((hr + [hr.first]).map { |v| pt(v) }) if hr.length >= 3
            end
          when 'point'
            p = g['position']
            s = type == LEVEL_MARK ? MARK_SIZE : MARK_SIZE / 2
            entities.add_edges(pt([p[0] - s, p[1], p[2]]), pt([p[0] + s, p[1], p[2]]))
            entities.add_edges(pt([p[0], p[1] - s, p[2]]), pt([p[0], p[1] + s, p[2]]))
            entities.add_edges(pt(p), pt([p[0], p[1], (p[2] || 0) + (s * 1.5)]))
          end
        end
      end
    end
  end
end
