# frozen_string_literal: true

require 'securerandom'
require 'fileutils'

module ProjektniBiro
  module SiteDesignPro
    # Road tools (Saobraćajnice) without AutoCAD: the host-independent part. The streets, roads, crossings, marks
    # and ramps drawn in SketchUp are kept as source elements (the same types and parameters as in AutoCAD); the road
    # engine (sdp-ceste, the same C# road network as in AutoCAD) builds the carriageways, kerbs, sidewalks, bike
    # lanes, markings, arrows and ramps from them as a package, which is imported like any package.
    module Roads
      STREET = 'road.street'
      CROSSING = 'road.pedestrian_crossing'
      LEVEL_MARK = 'road.level_mark'
      CURB_RAMP_MARK = 'road.curb_ramp_mark'
      GARAGE_RAMP = 'road.garage_ramp'
      RAMP_SECTION = 'road.ramp_section'
      HATCHED_AREA = 'road.hatched_area'
      SIMPLE_ROADS = %w[road.access_road road.driveway road.service_route].freeze
      NETWORK_ROADS = ([STREET] + SIMPLE_ROADS).freeze

      # Types the roads are made from (drawn by the user; the same list as RoadElements.SourceTypes in C#).
      SOURCE_TYPES = (NETWORK_ROADS + [CROSSING, LEVEL_MARK, CURB_RAMP_MARK, GARAGE_RAMP, RAMP_SECTION, HATCHED_AREA,
                                        'road.splitter_island', 'road.roundabout_apron']).freeze
      # Types only the road network makes (RoadElements.NetworkOnlyTypes).
      NETWORK_ONLY_TYPES = %w[road.carriageway road.street_bike_lane road.street_kerb road.sidewalk road.sidewalk_edge
                              road.garage_ramp_surface road.curb_ramp road.bike_ramp road.bike_crossing].freeze
      # Custom flag of an element the network or a garage ramp generated (RoadElements.DerivedFlag).
      DERIVED_FLAG = 'derivedFromRoads'
      # Generator host of the packages of the road engine (RoadEngine.Host).
      ENGINE_HOST = 'SketchUp – saobraćajnice (sdp-ceste)'

      KERB = 0.15
      EDGE = 0.10
      MAX_ROAD_SLOPE = 0.10
      MAX_RAMP_SLOPE = 0.17
      ABSOLUTE_THRESHOLD = 100.0

      LABEL_PREFIX = {
        STREET => 'U', CROSSING => 'PC', LEVEL_MARK => 'KS', CURB_RAMP_MARK => 'OR', GARAGE_RAMP => 'GR',
        RAMP_SECTION => 'UP', HATCHED_AREA => 'HM', 'road.access_road' => 'R', 'road.driveway' => 'R', 'road.service_route' => 'R'
      }.freeze

      TYPE_NAMES = {
        STREET => 'Ulica', CROSSING => 'Pješački prijelaz', LEVEL_MARK => 'Kota saobraćajnice',
        CURB_RAMP_MARK => 'Rampa za invalide', GARAGE_RAMP => 'Garažna rampa', RAMP_SECTION => 'Uzdužni presjek rampe',
        HATCHED_AREA => 'Šrafirana površina', 'road.access_road' => 'Interna pristupna saobraćajnica',
        'road.driveway' => 'Kolni prilaz', 'road.service_route' => 'Servisni put'
      }.freeze

      module_function

      def network_road?(type)
        NETWORK_ROADS.include?(type)
      end

      def new_id
        SecureRandom.uuid.downcase
      end

      # Bosnian number: decimal comma, given decimals.
      def num(v, decimals = 2)
        format("%.#{decimals}f", v.to_f).tr('.', ',')
      end

      # Number typed by the user: decimal comma or point, optional unit "m".
      def parse_number(text)
        t = text.to_s.strip.sub(/\s*m\z/i, '').tr(',', '.')
        return nil if t.empty? || t !~ /\A[+-]?(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?\z/

        t.to_f
      end

      # A height typed as an absolute elevation (e.g. 501,00 m n.v. with Z = 0 at 500,00) as the height in the model.
      def to_relative(value, base)
        return value if base.nil?
        return value unless base.abs >= ABSOLUTE_THRESHOLD && value.abs >= ABSOLUTE_THRESHOLD && (value - base).abs < ABSOLUTE_THRESHOLD

        value - base
      end

      # "+1,50", "−0,30", "±0,00"
      def level_text(v)
        return '±0,00' if v.abs < 0.005

        (v.positive? ? '+' : '−') + num(v.abs)
      end

      # Cross-section of a street (the parameters of road.street; the same ranges as RoadSpec.Normalize in C#).
      class StreetSpec
        FIELDS = %w[direction lanesLeft lanesRight laneWidth sidewalkLeft sidewalkRight bikeLeft bikeRight cornerRadius
                    arrows arrowSpacing median medianTo medianTaper].freeze
        DEFAULTS = {
          'direction' => 'two_way', 'lanesLeft' => 1, 'lanesRight' => 1, 'laneWidth' => 3.0, 'sidewalkLeft' => 1.6,
          'sidewalkRight' => 1.6, 'bikeLeft' => 0.0, 'bikeRight' => 0.0, 'cornerRadius' => 6.0, 'arrows' => true,
          'arrowSpacing' => 40.0, 'median' => 0.0, 'medianTo' => 0.0, 'medianTaper' => 0.0
        }.freeze
        RANGES = {
          'lanesLeft' => [1, 3, 'Broj traka lijevo'], 'lanesRight' => [1, 3, 'Broj traka desno'],
          'laneWidth' => [2.5, 4.5, 'Širina saobraćajne trake'], 'sidewalkLeft' => [0.0, 5.0, 'Širina lijevog trotoara'],
          'sidewalkRight' => [0.0, 5.0, 'Širina desnog trotoara'], 'bikeLeft' => [0.0, 2.0, 'Širina lijeve biciklističke staze'],
          'bikeRight' => [0.0, 2.0, 'Širina desne biciklističke staze'], 'cornerRadius' => [1.0, 30.0, 'Radijus ivičnjaka'],
          'arrowSpacing' => [10.0, 200.0, 'Razmak strelica'], 'median' => [0.0, 6.0, 'Širina srednjeg ostrva'],
          'medianTo' => [0.0, 10_000.0, 'Srednje ostrvo do'], 'medianTaper' => [0.0, 500.0, 'Dužina spajanja traka']
        }.freeze

        attr_reader :values

        def initialize(params = {})
          @values = DEFAULTS.dup
          (params || {}).each { |k, v| @values[k.to_s] = v if FIELDS.include?(k.to_s) }
        end

        def [](key)
          @values[key.to_s]
        end

        def one_way?
          @values['direction'] == 'one_way'
        end

        # Clamps to the supported ranges; returns the notes about what was changed.
        def normalize!
          notes = []
          @values['direction'] = @values['direction'].to_s == 'one_way' ? 'one_way' : 'two_way'
          @values['arrows'] = [true, 'true', 1, '1'].include?(@values['arrows'])
          RANGES.each do |key, (lo, hi, what)|
            raw = @values[key]
            v = raw.is_a?(Numeric) ? raw.to_f : (Roads.parse_number(raw) || DEFAULTS[key].to_f)
            v = lo unless v.finite?
            r = [[v, lo].max, hi].min
            r = r.round if lo.is_a?(Integer)
            notes << "#{what} je ograničen#{key.start_with?('lanes') ? '' : 'a'} na #{lo.is_a?(Integer) ? r : Roads.num(r)}." if (r - v).abs > 1e-9
            @values[key] = r
          end
          notes
        end

        def lanes_left
          one_way? ? 0 : @values['lanesLeft'].to_i
        end

        def lanes_right
          @values['lanesRight'].to_i
        end

        def total_lanes
          one_way? ? lanes_right : lanes_left + lanes_right
        end

        def half_median
          one_way? ? 0.0 : @values['median'].to_f / 2
        end

        def carriageway(left)
          lw = @values['laneWidth'].to_f
          return total_lanes * lw / 2 if one_way?

          half_median + ((left ? lanes_left : lanes_right) * lw)
        end

        # Distance from the axis to the outer edge of a level (:carriageway, :kerb, :bike, :sidewalk, :edge).
        def extent(level, left)
          c = carriageway(left)
          k = c + KERB
          bike = (left ? @values['bikeLeft'] : @values['bikeRight']).to_f
          b = k + bike
          sw = (left ? @values['sidewalkLeft'] : @values['sidewalkRight']).to_f
          s = b + sw
          e = s + (sw + bike > 0 ? EDGE : 0)
          { carriageway: c, kerb: k, bike: b, sidewalk: s, edge: e }.fetch(level)
        end

        def total_width
          extent(:edge, true) + extent(:edge, false)
        end

        def to_params
          v = @values
          {
            'direction' => v['direction'], 'lanesLeft' => v['lanesLeft'].to_f, 'lanesRight' => v['lanesRight'].to_f,
            'laneWidth' => v['laneWidth'].to_f, 'sidewalkLeft' => v['sidewalkLeft'].to_f, 'sidewalkRight' => v['sidewalkRight'].to_f,
            'bikeLeft' => v['bikeLeft'].to_f, 'bikeRight' => v['bikeRight'].to_f, 'cornerRadius' => v['cornerRadius'].to_f,
            'arrows' => v['arrows'] == true, 'arrowSpacing' => v['arrowSpacing'].to_f, 'median' => v['median'].to_f,
            'medianTo' => v['medianTo'].to_f, 'medianTaper' => v['medianTaper'].to_f
          }
        end

        # "dvosmjerna, 1 + 1 traka × 3,00 m, trotoari 1,60 / 1,60 m, ..." as the AutoCAD command writes it.
        def describe
          lanes = one_way? ? "jednosmjerna, #{Roads.count(total_lanes, 'traka', 'trake', 'traka')}" : "dvosmjerna, #{lanes_left} + #{Roads.count(lanes_right, 'traka', 'trake', 'traka')}"
          text = "#{lanes} × #{Roads.num(@values['laneWidth'])} m, trotoari #{Roads.num(@values['sidewalkLeft'])} / #{Roads.num(@values['sidewalkRight'])} m, " \
                 "biciklističke staze #{Roads.num(@values['bikeLeft'])} / #{Roads.num(@values['bikeRight'])} m, radijus ivičnjaka #{Roads.num(@values['cornerRadius'])} m"
          text += ", srednje ostrvo #{Roads.num(@values['median'])} m" if !one_way? && @values['median'].to_f > 0
          text + (@values['arrows'] == true ? ", strelice svakih #{Roads.num(@values['arrowSpacing'], 0)} m" : ', bez strelica')
        end
      end

      def count(n, one, few, many)
        m10 = n % 10
        m100 = n % 100
        word = if m10 == 1 && m100 != 11 then one
               elsif m10.between?(2, 4) && !m100.between?(12, 14) then few
               else many
               end
        "#{n} #{word}"
      end

      # Width of a simple road (access road, driveway, service route): one carriageway, two-way with a centre line
      # from 5 m up (as RoadElements.Spec in C#).
      def simple_halfwidth(params)
        w = [(params['width'] || 5.5).to_f, 1.0].max
        w / 2
      end

      # Garage ramp parameters as in AutoCAD (Garažna rampa): ranges of the kind and the total width.
      module Ramp
        KINDS = %w[straight semicircular spiral combined].freeze
        KIND_NAMES = { 'straight' => 'Pravolinijska', 'semicircular' => 'Polukružna', 'spiral' => 'Spiralna (kružna)', 'combined' => 'Kombinovana' }.freeze

        module_function

        def lane_range(kind)
          case kind
          when 'combined' then [2.5, 5.0]
          when 'straight' then [2.5, 3.5]
          else [3.5, 5.0]
          end
        end

        def curved?(kind)
          kind != 'straight'
        end

        # Clamped parameters with the total width; returns [params, notes].
        def normalize(p)
          p = p.dup
          notes = []
          kind = KINDS.include?(p['kind']) ? p['kind'] : 'straight'
          p['kind'] = kind
          p['direction'] = p['direction'] == 'two_way' ? 'two_way' : 'one_way'
          lo, hi = lane_range(kind)
          lw = p['laneWidth'].to_f
          lw = curved?(kind) ? 4.0 : 3.0 unless lw.positive?
          if lw < lo || lw > hi
            lw = [[lw, lo].max, hi].min
            notes << "Širina trake je ograničena na #{Roads.num(lw)} m."
          end
          p['laneWidth'] = lw
          m = p['margin'].to_f
          p['margin'] = [[m, curved?(kind) ? 0.25 : 0.0].max, 0.5].min
          p['island'] = p['island'] == true && !curved?(kind) && p['direction'] == 'two_way'
          p['transition'] = [[p['transition'].to_f, 0.0].max, 8.0].min
          p['flatLength'] = [[p['flatLength'].to_f, 0.0].max, 10.0].min
          p['ceilingFrom'] = [p['ceilingFrom'].to_f, 0.0].max
          p['ceilingLevel'] = p['ceilingLevel'].to_f
          p['minClearance'] = [[(p['minClearance'] || 2.1).to_f, 1.8].max, 5.0].min
          p['drain'] = %w[none bottom both].include?(p['drain']) ? p['drain'] : 'bottom'
          p['heated'] = p['heated'] == true
          p['walls'] = p['walls'] != false
          p['startLevel'] = p['startLevel'].to_f
          p['endLevel'] = p['endLevel'].to_f
          p['width'] = width(p)
          [p, notes]
        end

        def width(p)
          ((p['direction'] == 'two_way' ? 2 : 1) * p['laneWidth'].to_f) + (2 * p['margin'].to_f)
        end

        # Centreline through the clicked points with the bends rounded by arcs of one radius (GarageRamp.FilletPath).
        def fillet_path(points, radius)
          pts = [points[0]]
          bulges = [0.0]
          (1...(points.length - 1)).each do |i|
            a = points[i - 1]
            b = points[i]
            c = points[i + 1]
            din = Geometry.norm(Geometry.sub(b, a))
            dout = Geometry.norm(Geometry.sub(c, b))
            turn = Math.atan2(Geometry.cross(din, dout), Geometry.dot(din, dout))
            room = [Geometry.dist(a, b) * (i == 1 ? 0.9 : 0.45), Geometry.dist(b, c) * (i == points.length - 2 ? 0.9 : 0.45)].min
            tangent = [room, radius * Math.tan(turn.abs / 2)].min
            if radius < 0.1 || turn.abs < 0.01 || turn.abs > 3.0 || tangent < 0.05
              pts << b
              bulges << 0.0
              next
            end
            pts << Geometry.sub(b, Geometry.scale(din, tangent))
            bulges << Math.tan(turn / 4)
            pts << Geometry.add(b, Geometry.scale(dout, tangent))
            bulges << 0.0
          end
          pts << points[-1]
          bulges << 0.0
          [pts, bulges]
        end

        # Semicircular (halves = 1) or spiral (halves = 2 × turns) centreline: alternating ends of a diameter
        # through the start point, each half a bulge of ±1.
        def circle_path(centre, start, left, halves)
          opposite = [(2 * centre[0]) - start[0], (2 * centre[1]) - start[1]]
          pts = []
          bulges = []
          (0..halves).each do |i|
            pts << (i.even? ? start : opposite)
            bulges << (i == halves ? 0.0 : (left ? 1.0 : -1.0))
          end
          [pts, bulges]
        end

        def length(points, bulges)
          (0...(points.length - 1)).inject(0.0) { |s, i| s + Geometry.segment_length(points[i], points[i + 1], bulges[i]) }
        end
      end

      # 2D helpers on road axes (metres; points [x, y] or [x, y, z]).
      module Plan
        module_function

        # Nearest point of a polyline: { dist:, foot:, dir:, station:, segment: } or nil.
        def nearest(points, p, closed = false)
          best = nil
          station = 0.0
          n = closed ? points.length : points.length - 1
          n.times do |i|
            a = points[i]
            b = points[(i + 1) % points.length]
            ab = Geometry.sub(b, a)
            l2 = Geometry.dot(ab, ab)
            seg = Math.sqrt(l2)
            if l2 > 1e-12
              t = [[Geometry.dot(Geometry.sub(p, a), ab).to_f / l2, 0.0].max, 1.0].min
              foot = [a[0] + (ab[0] * t), a[1] + (ab[1] * t)]
              d = Geometry.dist(foot, p)
              if best.nil? || d < best[:dist]
                z = (a[2] || 0.0) + (((b[2] || 0.0) - (a[2] || 0.0)) * t)
                best = { dist: d, foot: foot, dir: Geometry.norm(ab), station: station + (seg * t), segment: i, z: z }
              end
            end
            station += seg
          end
          best
        end

        # Crossing outline as the AutoCAD command builds it: bars 0,50 m with gaps 0,50 m over the carriageway,
        # centred on it; the first edge runs along the road.
        def crossing_ring(centre, dir, lanes, lane_width, length)
          bars = [((lanes * lane_width) + 0.5).floor, 1].max
          across = (bars * 0.5) + ((bars - 1) * 0.5)
          side = Geometry.perp(dir)
          c0 = Geometry.sub(Geometry.sub(centre, Geometry.scale(dir, length / 2)), Geometry.scale(side, across / 2))
          [c0, Geometry.add(c0, Geometry.scale(dir, length)),
           Geometry.add(Geometry.add(c0, Geometry.scale(dir, length)), Geometry.scale(side, across)),
           Geometry.add(c0, Geometry.scale(side, across))].map { |q| [q[0], q[1], 0.0] }
        end

        # Direction square to the nearest road when the point lies on or beside it (within its outer edge and 3 m).
        def square_to_road(p, roads)
          best = nil
          best_gap = Float::INFINITY
          roads.each do |r|
            hit = nearest(r[:axis], p, r[:closed])
            next unless hit

            gap = hit[:dist] - r[:half]
            next unless gap < best_gap && gap < 3.0

            best_gap = gap
            best = Geometry.perp(hit[:dir])
          end
          best
        end

        # Point pulled onto the helper line through `first` along `n` when within 6° of it.
        def pull(first, cur, n)
          return cur unless n

          v = Geometry.sub(cur, first)
          l = Geometry.len(v)
          along = Geometry.dot(v, n)
          return cur if l < 1e-6 || along.abs / l < Math.cos(6 * Math::PI / 180)

          q = Geometry.add(first, Geometry.scale(n, along))
          [q[0], q[1], cur[2] || 0.0]
        end
      end

      # A source element as the road engine takes it.
      Record = Compat.kw_struct(:id, :type, :name, :label, :params, :geometry, :ref) do
        def kind
          geometry['kind']
        end

        def to_request
          g = JSON.parse(JSON.generate(geometry))
          g['id'] = id
          h = { 'id' => id, 'type' => type, 'name' => name.to_s, 'parameters' => params || {}, 'geometry' => g }
          h['label'] = label unless label.to_s.empty?
          h
        end

        # Axis points (tessellated, metres, with z) of a path.
        def axis_points
          return [] unless kind == 'path'

          Geometry.tessellate(geometry['vertices'], geometry['bulges'], geometry['closed'] == true, 0.05)
        end
      end

      # Builds the request the road engine reads.
      module Request
        module_function

        # extra: further elements (any type of the library) as they are, never flattened
        def build(records, project_id: nil, project_name: 'Saobraćajnice', base: nil, flat: false, origin: nil, extra: [])
          elements = records.map do |r|
            h = r.to_request
            if flat
              next nil if r.type == LEVEL_MARK

              h['geometry'] = flatten(h['geometry'])
            end
            h
          end.compact
          elements.concat(extra.map(&:to_request))
          req = { 'format' => 'sdp-roads-request', 'version' => 1, 'project' => { 'id' => project_id, 'name' => project_name }, 'elements' => elements }
          req['base'] = base.to_f unless base.nil?
          req['origin'] = origin.map(&:to_f) if origin
          req
        end

        def flatten(g)
          g = JSON.parse(JSON.generate(g))
          flat = ->(v) { [v[0], v[1], 0.0] }
          case g['kind']
          when 'point' then g['position'] = flat.call(g['position'])
          when 'path' then g['vertices'] = g['vertices'].map(&flat)
          when 'polygon'
            g['outer']['vertices'] = g['outer']['vertices'].map(&flat)
            (g['holes'] || []).each { |h| h['vertices'] = h['vertices'].map(&flat) }
          end
          g
        end
      end

      # Geometry hashes (geometry.json format, metres).
      module Geo
        module_function

        def path(points, closed: false, bulges: nil)
          g = { 'kind' => 'path', 'vertices' => points.map { |p| [p[0].to_f, p[1].to_f, (p[2] || 0.0).to_f] }, 'closed' => closed }
          if bulges && bulges.any? { |b| b.to_f.abs > 1e-12 }
            n = closed ? points.length : points.length - 1
            g['bulges'] = (0...n).map { |i| (bulges[i] || 0.0).to_f }
          end
          g
        end

        def polygon(points)
          { 'kind' => 'polygon', 'outer' => { 'vertices' => points.map { |p| [p[0].to_f, p[1].to_f, (p[2] || 0.0).to_f] } } }
        end

        def point(p, rotation = 0.0)
          { 'kind' => 'point', 'position' => [p[0].to_f, p[1].to_f, (p[2] || 0.0).to_f], 'rotation' => rotation.to_f }
        end

        # Applies f([x, y, z]) -> [x, y, z] to every coordinate.
        def map(g, &f)
          g = JSON.parse(JSON.generate(g))
          case g['kind']
          when 'point' then g['position'] = f.call(g['position'])
          when 'path' then g['vertices'] = g['vertices'].map(&f)
          when 'polygon'
            g['outer']['vertices'] = g['outer']['vertices'].map(&f)
            (g['holes'] || []).each { |h| h['vertices'] = h['vertices'].map(&f) }
          end
          g
        end
      end

      # Runs the road engine (sdp-ceste) next to the extension: engine/win/sdp-ceste.exe on Windows (.NET Framework,
      # part of Windows), else `dotnet engine/net8/sdp-ceste.dll` (.NET 8 runtime). SDP_ROADS_ENGINE overrides it
      # (a command line, e.g. for the tests).
      class Engine
        class Error < StandardError; end

        def self.default_command(plugin_dir)
          env = ENV['SDP_ROADS_ENGINE'].to_s
          return env.split(' ') unless env.empty?

          exe = File.join(plugin_dir, 'engine', 'win', 'sdp-ceste.exe')
          dll = File.join(plugin_dir, 'engine', 'net8', 'sdp-ceste.dll')
          windows = RUBY_PLATFORM =~ /mswin|mingw|cygwin/
          return [exe] if windows && File.file?(exe)
          return ['dotnet', dll] if File.file?(dll)

          nil
        end

        attr_reader :command, :work_dir

        def initialize(command, work_dir)
          @command = command
          @work_dir = work_dir
        end

        def available?
          !@command.nil?
        end

        # Builds the roads: returns [result hash, package folder].
        def build(request)
          raise Error, missing unless available?

          FileUtils.mkdir_p(@work_dir)
          req = File.join(@work_dir, 'request.json')
          out = File.join(@work_dir, 'paket')
          FileUtils.rm_rf(out)
          File.open(req, 'wb') { |f| f.write(JSON.generate(request)) }
          ok = run(['build', '--in', req, '--out', out])
          result_file = File.join(out, 'result.json')
          result = File.file?(result_file) ? JSON.parse(File.read(result_file, encoding: 'UTF-8')) : nil
          unless result && result['ok'] == true
            raise Error, result['error'] if result && result['error']
            raise Error, "motor saobraćajnica se ne može pokrenuti (#{@command.join(' ')})" if ok.nil?

            raise Error, 'motor saobraćajnica nije uspio (nema rezultata)'
          end

          [result, out]
        end

        # The roads of the request as a package for AutoCAD (.sdpkg at `path`): returns the result hash.
        def export(request, path)
          raise Error, missing unless available?

          FileUtils.mkdir_p(@work_dir)
          req = File.join(@work_dir, 'izvoz-zahtjev.json')
          res = File.join(@work_dir, 'izvoz-rezultat.json')
          File.delete(res) if File.file?(res)
          File.open(req, 'wb') { |f| f.write(JSON.generate(request)) }
          ok = run(['export', '--in', req, '--out', path, '--result', res])
          result = File.file?(res) ? JSON.parse(File.read(res, encoding: 'UTF-8')) : nil
          unless result && result['ok'] == true
            raise Error, result['error'] if result && result['error']
            raise Error, "motor saobraćajnica se ne može pokrenuti (#{@command.join(' ')})" if ok.nil?

            raise Error, 'motor saobraćajnica nije napravio paket'
          end

          result
        end

        # Parts of a roundabout (source elements).
        def roundabout(options)
          raise Error, missing unless available?

          FileUtils.mkdir_p(@work_dir)
          inp = File.join(@work_dir, 'kruzni-tok.json')
          out = File.join(@work_dir, 'kruzni-tok-dijelovi.json')
          File.delete(out) if File.file?(out)
          File.open(inp, 'wb') { |f| f.write(JSON.generate(options)) }
          run(['roundabout', '--in', inp, '--out', out])
          result = File.file?(out) ? JSON.parse(File.read(out, encoding: 'UTF-8')) : nil
          raise Error, (result && result['error']) || 'motor saobraćajnica nije napravio kružni tok' unless result && result['ok'] == true

          result
        end

        private

        def missing
          'motor saobraćajnica (sdp-ceste) nije pronađen uz proširenje. Na Windowsu je to engine\\win\\sdp-ceste.exe ' \
            '(reinstalirajte proširenje); na macOS-u treba .NET 8 runtime (dotnet).'
        end

        def run(args)
          cmd = (@command + args).map { |a| a.to_s.dup.force_encoding('UTF-8') }
          system(*cmd)
        rescue SystemCallError => e
          raise Error, "motor saobraćajnica se ne može pokrenuti: #{e.message}"
        end
      end
    end
  end
end
