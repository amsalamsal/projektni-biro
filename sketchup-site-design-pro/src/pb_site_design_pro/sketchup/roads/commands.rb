# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Roads
      # The road commands in SketchUp (menu Proširenja > Vanjsko uređenje PRO > Saobraćajnice and the toolbar
      # "Vanjsko uređenje PRO – Saobraćajnice"): the same commands as the AutoCAD panel Crtanje for roads.
      module Commands
        ICONS = File.expand_path('../../icons', __dir__)

        @last_message = nil

        class << self
          attr_accessor :last_message
        end

        module_function

        def model
          Sketchup.active_model
        end

        def store
          SourceStore.new(model)
        end

        def base
          store.settings['base']
        end

        # ---- running a change with the rebuild in the same undo step

        # Runs the block (changes of the source elements) and builds the roads, as one undoable step. A failing
        # engine keeps the change (the roads can be rebuilt with Osvježi); a failing import undoes everything.
        def change(title)
          m = model
          before = Rebuild.violations_before(m)
          m.start_operation("Saobraćajnice: #{title}", true)
          begin
            yield store
            outcome = Rebuild.run(m, operation: :none)
            broken = Rebuild.new_violations(before, outcome)
            unless broken.empty?
              m.abort_operation
              Commands.last_message = "Rampa ispod tehničkog minimuma:\n#{broken.join("\n")}"
              UI.messagebox("#{title}: rampa ispod tehničkog minimuma se ne može projektovati.\n\n#{broken.join("\n")}\n\nIzmjena je poništena.", MB_MULTILINE, 'Vanjsko uređenje PRO – saobraćajnice')
              return nil
            end

            m.commit_operation
            report(outcome, title)
          rescue Engine::Error => e
            m.commit_operation
            Log.warn("Saobraćajnice: #{e.message}")
            UI.messagebox("#{title}: izmjena je sačuvana, ali saobraćajnice nisu izgrađene:\n#{e.message}\n\nNakon ispravke: Saobraćajnice > Osvježi.")
          rescue StandardError => e
            m.abort_operation
            Log.error("Saobraćajnice: #{title}", e)
            UI.messagebox("#{title}: greška – #{e.message}\nIzmjena je poništena. Detalji su u dnevniku (log).")
          end
        end

        def report(outcome, title, always: false)
          return unless outcome

          text = Rebuild.message(outcome)
          Commands.last_message = text
          Sketchup.status_text = "#{title}: #{outcome.summary}"
          if always || !outcome.warnings.empty?
            UI.messagebox("#{title}\n\n#{text}", MB_MULTILINE, 'Vanjsko uređenje PRO – saobraćajnice')
          end
        end

        def refresh
          outcome = Rebuild.run(model, operation: :own)
          report(outcome, 'Osvježi saobraćajnice', always: true)
        rescue Engine::Error => e
          UI.messagebox("Saobraćajnice nisu izgrađene:\n#{e.message}")
        end

        # ---- previews while drawing

        def street_preview(spec)
          levels = %i[carriageway kerb bike sidewalk edge]
          lambda do |pts|
            flat = pts.map { |p| [p[0], p[1], p[2] || 0.0] }
            lines = []
            [true, false].each do |left|
              last = nil
              levels.each do |lv|
                d = spec.extent(lv, left)
                next if last && (d - last).abs < 1e-6

                last = d
                lines << [Geometry.offset_line(flat, left ? d : -d), lv]
              end
            end
            lines
          end
        end

        def band_preview(half)
          ->(pts) { [[Geometry.offset_line(pts, half), :carriageway], [Geometry.offset_line(pts, -half), :carriageway]] }
        end

        def heights_option(v)
          h = Roads.to_relative(Roads.parse_number(v['height'].to_s) || v['height'].to_f, base)
          [(v['heights'] || 'click').to_sym, h]
        end

        # ---- Ulica

        def street
          s = store
          defaults = s.last_params('street', StreetSpec::DEFAULTS.merge('heights' => 'click', 'height' => 0.0))
          Forms.ask('Ulica', Forms.street_fields, defaults, summary: Forms.method(:street_summary),
                                                             intro: 'Presjek ulice, zatim crtanje osovine (sredina kolovoza) u smjeru vožnje desnih traka.',
                                                             ok: 'Crtaj osovinu') do |v|
            spec = StreetSpec.new(v)
            notes = spec.normalize!
            s.remember('street', spec.to_params.merge('heights' => v['heights'], 'height' => v['height']))
            heights, height = heights_option(v)
            Sketchup.status_text = notes.join(' ') unless notes.empty?
            tool = Tools::PolylineTool.new("Ulica: #{spec.describe}. Kliknite tačke osovine.", heights: heights, height: height, base: base,
                                                                                                  preview: street_preview(spec)) do |pts, closed|
              change('Ulica') { |st| st.add(type: STREET, params: spec.to_params, geometry: Geo.path(pts, closed: closed)) }
            end
            model.select_tool(tool)
          end
        end

        # ---- Ulica iz odabranih linija (the AutoCAD option Odaberi)

        def street_from_selection
          edges = model.selection.grep(Sketchup::Edge)
          if edges.empty?
            UI.messagebox('Odaberite linije (ivice) koje čine osovinu ulice, pa ponovo pokrenite naredbu.')
            return
          end
          tr = model.respond_to?(:edit_transform) ? model.edit_transform : Geom::Transformation.new
          pts = SourceStore.new(model).send(:chain_points, edges)
          if pts.nil? || pts.length < 2
            UI.messagebox('Odabrane linije ne čine jednu neprekinutu liniju (osovinu bez grananja).')
            return
          end
          pts = pts.map { |p| SourceStore.xf(tr, p) }
          closed = pts.length > 3 && Geometry.dist(pts.first, pts.last) < 1e-4
          pts.pop if closed
          s = store
          defaults = s.last_params('street', StreetSpec::DEFAULTS)
          Forms.ask('Ulica iz linija', Forms.street_fields(new_street: false), defaults, summary: Forms.method(:street_summary), ok: 'Napravi ulicu') do |v|
            spec = StreetSpec.new(v)
            spec.normalize!
            s.remember('street', spec.to_params)
            change('Ulica') { |st| st.add(type: STREET, params: spec.to_params, geometry: Geo.path(pts, closed: closed)) }
          end
        end

        # ---- Cesta (access road, driveway, service route)

        def simple_road
          s = store
          defaults = s.last_params('road', 'type' => 'road.driveway', 'width' => 3.5, 'heights' => 'click', 'height' => 0.0)
          Forms.ask('Cesta', Forms.simple_road_fields, defaults, ok: 'Crtaj osovinu',
                                                                 intro: 'Jednostavna cesta zadana širinom (kolni prilaz, pristupna saobraćajnica, servisni put); spaja se s ulicama.') do |v|
            type = SIMPLE_ROADS.include?(v['type']) ? v['type'] : 'road.driveway'
            width = [[v['width'].to_f, 1.0].max, 20.0].min
            s.remember('road', v)
            heights, height = heights_option(v)
            tool = Tools::PolylineTool.new("#{TYPE_NAMES[type]} #{Roads.num(width)} m: kliknite tačke osovine.", heights: heights, height: height, base: base,
                                                                                                                 preview: band_preview(width / 2)) do |pts, closed|
              change(TYPE_NAMES[type]) { |st| st.add(type: type, params: { 'width' => width }, geometry: Geo.path(pts, closed: closed)) }
            end
            model.select_tool(tool)
          end
        end

        # ---- Uredi ulicu

        def selected_source(types = NETWORK_ROADS)
          sel = model.selection.to_a
          sel.length == 1 && sel[0].is_a?(Sketchup::Group) && types.include?(sel[0].get_attribute(SourceStore::DICT, 'type')) ? sel[0] : nil
        end

        def edit_street
          g = selected_source
          return edit_street_record(g) if g

          if store.records.none? { |r| Roads.network_road?(r.type) }
            UI.messagebox('U modelu nema ulica ni cesta. Nova ulica: Saobraćajnice > Ulica.')
            return
          end
          model.select_tool(Tools::PickTool.new('Uredi ulicu: kliknite ulicu ili cestu.') do |p, _view|
            rec, hit = store.nearest_road(p)
            if rec.nil? || hit[:dist] > SourceStore.half_width(rec) + 2
              Sketchup.status_text = 'Na tom mjestu nema ulice ni ceste.'
              next :continue
            end
            model.select_tool(nil)
            edit_street_record(rec.ref)
            :chain
          end)
        end

        def edit_street_record(group)
          type = group.get_attribute(SourceStore::DICT, 'type')
          params = JSON.parse(group.get_attribute(SourceStore::DICT, 'params', '{}'))
          label = group.get_attribute(SourceStore::DICT, 'label', '')
          intro = type == STREET ? "Ulica #{label}: #{StreetSpec.new(params).tap(&:normalize!).describe}." : "#{TYPE_NAMES[type]} #{label} je zadana samo širinom; ovdje joj možete dati trake, trotoare i staze (postaje ulica)."
          values = type == STREET ? StreetSpec.new(params).values : StreetSpec::DEFAULTS
          Forms.ask("Uredi ulicu #{label}", Forms.street_fields(new_street: false), values, intro: intro, summary: Forms.method(:street_summary), ok: 'Primijeni') do |v|
            spec = StreetSpec.new(v)
            spec.normalize!
            store.remember('street', spec.to_params)
            change('Uredi ulicu') { |st| st.update(group, params: spec.to_params, type: STREET, name: type == STREET ? nil : TYPE_NAMES[STREET]) }
          end
        end

        # ---- Kružni tok

        def roundabout
          s = store
          defaults = s.last_params('roundabout', StreetSpec::DEFAULTS.merge('islandRadius' => 15.0, 'apron' => 1.2, 'arms' => 4, 'armLength' => 40.0, 'angleByClick' => false))
          Forms.ask('Kružni tok', Forms.roundabout_fields, defaults, summary: Forms.method(:roundabout_summary), ok: 'Odaberi centar',
                                                                     intro: 'Cijeli kružni tok iz jedne tačke: prsten, otok, ivičnjaci, razdjelni otoci, prijelazi, oznake i krakovi.') do |v|
            s.remember('roundabout', v)
            place = lambda do |centre, angle|
              build_roundabout(v, centre, angle)
              :done
            end
            model.select_tool(Tools::PickTool.new('Kružni tok: kliknite centar.') do |c, _view|
              if v['angleByClick'] == true
                model.select_tool(Tools::PickTool.new('Kružni tok: kliknite smjer prvog kraka.', from: c) do |q, _vw|
                  place.call(c, Math.atan2(q[1] - c[1], q[0] - c[0]))
                end)
                :chain
              else
                place.call(c, 0.0)
              end
            end)
          end
        end

        def build_roundabout(v, centre, angle)
          arm = StreetSpec.new(v.merge('direction' => 'two_way'))
          arm.normalize!
          options = { 'centre' => centre, 'islandRadius' => v['islandRadius'].to_f, 'apron' => v['apron'].to_f, 'arms' => v['arms'].to_i,
                      'armLength' => v['armLength'].to_f, 'firstArmAngle' => angle, 'seed' => Roads.new_id, 'arm' => arm.to_params }
          parts = Rebuild.engine(store.settings['projectId']).roundabout(options)
          change('Kružni tok') do |st|
            n = st.records.count { |r| r.type == STREET && r.name.to_s.start_with?('Kružni tok') } + 1
            container = st.add_container("Kružni tok #{n}", 'centre' => JSON.generate(centre))
            parts['elements'].each do |e|
              st.add(type: e['type'], params: e['parameters'], geometry: e['geometry'], name: e['name'], id: e['id'], parent: container)
            end
          end
          Sketchup.status_text = "Kružni tok: vanjski poluprečnik #{Roads.num(parts['outerRadius'])} m. #{(parts['notes'] || []).join(' ')}"
        rescue Engine::Error => e
          UI.messagebox("Kružni tok nije napravljen:\n#{e.message}")
        end

        # ---- Pješački prijelaz

        def crossing
          model.select_tool(Tools::PickTool.new('Pješački prijelaz: kliknite na ulicu ili cestu (mjesto prijelaza).') do |p, _view|
            rec, hit = store.nearest_road(p)
            on_road = false
            values = store.last_params('crossing', 'lanes' => 2, 'laneWidth' => 3.0, 'length' => 4.0, 'bikeLane' => false)
            centre = p
            dir = nil
            if rec && rec.type == STREET
              spec = StreetSpec.new(rec.params)
              spec.normalize!
              if hit[:dist] <= [spec.carriageway(true), spec.carriageway(false)].max + 1.0
                on_road = true
                values = values.merge('lanes' => spec.total_lanes, 'laneWidth' => spec['laneWidth'])
                dir = hit[:dir]
                off = (spec.carriageway(true) - spec.carriageway(false)) / 2
                centre = Geometry.add(hit[:foot], Geometry.scale(Geometry.perp(dir), off))
              end
            elsif rec && hit[:dist] <= Roads.simple_halfwidth(rec.params) + 1.0
              on_road = true
              w = Roads.simple_halfwidth(rec.params) * 2
              values = values.merge('lanes' => w >= 5 ? 2 : 1, 'laneWidth' => w >= 5 ? w / 2 : w)
              dir = hit[:dir]
              centre = hit[:foot]
            end
            model.select_tool(nil)
            ask_crossing(values, centre, dir, on_road, rec)
            :chain
          end)
        end

        def ask_crossing(values, centre, dir, on_road, rec)
          intro = on_road ? "#{rec.label}: prijelaz ide okomito na osovinu, preko cijelog kolovoza." : 'Prijelaz nije na ulici: smjer ulice se zadaje drugim klikom.'
          Forms.ask('Pješački prijelaz', Forms.crossing_fields, values, intro: intro, ok: on_road ? 'Napravi' : 'Zadaj smjer') do |v|
            store.remember('crossing', v)
            make = lambda do |d|
              ring = Plan.crossing_ring(centre, d, [[v['lanes'].to_i, 1].max, 6].min, [v['laneWidth'].to_f, 1.0].max, [v['length'].to_f, 1.0].max)
              change('Pješački prijelaz') do |st|
                st.add(type: CROSSING, params: { 'stripeWidth' => 0.5, 'gapWidth' => 0.5, 'bikeLane' => v['bikeLane'] == true }, geometry: Geo.polygon(ring))
              end
            end
            if dir
              make.call(dir)
            else
              model.select_tool(Tools::PickTool.new('Pješački prijelaz: kliknite smjer ulice.', from: centre) do |q, _vw|
                d = Geometry.norm(Geometry.sub(q, centre))
                next :continue if Geometry.len(d) < 1e-9

                make.call(d)
                :done
              end)
            end
          end
        end

        # ---- Kota saobraćajnice, Rampa za invalide: points, the roads rebuilt when the tool ends

        def point_marks(type, prompt)
          added = 0
          last = (store.last_params('level', 'v' => 0.0)['v'] || 0.0).to_f
          tool = Tools::PickTool.new(prompt) do |p, _view|
            params = {}
            if type == LEVEL_MARK
              b = base
              default = p[2].abs > 0.001 ? p[2] : last
              r = UI.inputbox(["Visina (Z modela#{b ? "; Z = 0 je #{Roads.num(b)} m n.v., može i m n.v." : ''})"], [Roads.num(default)], 'Kota saobraćajnice')
              next :continue unless r

              v = Roads.parse_number(r[0])
              next :continue if v.nil?

              last = Roads.to_relative(v, b)
              params['level'] = last
              store.remember('level', 'v' => last)
              Sketchup.status_text = "Kota #{Roads.level_text(last)}#{b ? " = #{Roads.num(b + last)} m n.v." : ''}. Sljedeća tačka ili Enter za kraj."
            end
            m = model
            m.start_operation(TYPE_NAMES[type], true)
            store.add(type: type, params: params, geometry: Geo.point([p[0], p[1], type == LEVEL_MARK ? params['level'] : p[2]]))
            m.commit_operation
            added += 1
            :continue
          end
          tool.define_singleton_method(:deactivate) do |view|
            super(view)
            UI.start_timer(0, false) { Commands.after_points(added) } if added > 0
          end
          model.select_tool(tool)
        end

        def after_points(added)
          outcome = Rebuild.run(model, operation: :transparent)
          report(outcome, "Dodano: #{added}")
        rescue Engine::Error => e
          UI.messagebox("Saobraćajnice nisu izgrađene:\n#{e.message}")
        end

        def level_mark
          point_marks(LEVEL_MARK, 'Kota saobraćajnice: kliknite mjesto na ulici (Enter za kraj). Između kota visina se mijenja linearno.')
        end

        def curb_ramp
          point_marks(CURB_RAMP_MARK, 'Rampa za invalide (120 cm): kliknite sredinu – na trotoaru (ta strana) ili na kolovozu (obje strane). Enter za kraj.')
        end

        # ---- Visine saobraćajnica

        def levels
          s = store
          st = s.settings
          values = { 'projectName' => st['projectName'], 'baseText' => st['base'] ? Roads.num(st['base']) : '', 'flat' => st['flat'], 'autoRefresh' => st['autoRefresh'] }
          Forms.ask('Visine saobraćajnica', Forms.levels_fields, values, ok: 'Primijeni') do |v|
            b = Roads.parse_number(v['baseText'].to_s)
            m = model
            m.start_operation('Saobraćajnice: visine', true)
            s.set('projectName', v['projectName'].to_s.strip.empty? ? 'Saobraćajnice' : v['projectName'].to_s.strip)
            s.set('base', b)
            s.set('flat', [true, 'true'].include?(v['flat']))
            s.set('autoRefresh', v['autoRefresh'] == true)
            m.commit_operation
            refresh
          end
        end

        # ---- Garažna rampa

        def garage_ramp
          s = store
          defaults = s.last_params('ramp', 'kind' => 'straight', 'direction' => 'one_way', 'laneWidth' => 3.0, 'margin' => 0.25, 'island' => true,
                                           'bend' => 10.0, 'turn' => 'left', 'turns' => 1.0, 'startAtRoad' => true, 'startLevel' => 0.0, 'endLevel' => -3.0,
                                           'transition' => 0.0, 'flatLength' => 0.0, 'ceilingFrom' => 0.0, 'ceilingLevel' => -0.3, 'minClearance' => 2.1,
                                           'drain' => 'bottom', 'walls' => true, 'heated' => false)
          Forms.ask('Garažna rampa', Forms.ramp_fields, defaults, summary: Forms.method(:ramp_summary), ok: 'Crtaj rampu',
                                                                  intro: 'Pravolinijska, polukružna, spiralna ili kombinovana rampa: srednja linija, zaštitni razmak, strelice, L / h / nagib (najviše 17 %).') do |v|
            s.remember('ramp', v)
            draw_ramp(v)
          end
        end

        def road_infos
          store.records.select { |r| Roads.network_road?(r.type) && r.kind == 'path' }.map do |r|
            { axis: r.axis_points, closed: r.geometry['closed'] == true, half: SourceStore.half_width(r), rec: r }
          end
        end

        def ramp_preview(params)
          half = params['width'] / 2
          lambda do |pts, bulges = nil|
            line = Geometry.tessellate(pts.map { |p| [p[0], p[1], p[2] || 0.0] }, bulges, false, 0.05)
            [[Geometry.offset_line(line, half), :carriageway], [Geometry.offset_line(line, -half), :carriageway],
             [Geometry.offset_line(line, half - params['margin']), :margin], [Geometry.offset_line(line, -(half - params['margin'])), :margin]]
          end
        end

        def draw_ramp(v)
          params, notes = Ramp.normalize(v)
          Sketchup.status_text = notes.join(' ') unless notes.empty?
          roads = road_infos
          preview = ramp_preview(params)
          create = lambda do |pts, bulges|
            finish_ramp(params, v, pts, bulges, roads)
          end
          case params['kind']
          when 'straight'
            model.select_tool(Tools::PickTool.new('Garažna rampa: početak (sredina, gornji kraj).') do |a, _view|
              square = Plan.square_to_road(a, roads)
              Sketchup.status_text = 'Početak rampe je uz saobraćajnicu: plava linija pokazuje smjer okomit na nju.' if square
              helper = square ? ->(_cur) { [[[Geometry.sub(a, Geometry.scale(square, 40)) + [a[2]], Geometry.add(a, Geometry.scale(square, 40)) + [a[2]]], :helper]] } : nil
              model.select_tool(Tools::PickTool.new('Garažna rampa: kraj (sredina, donji kraj).', from: a,
                                                                                               snap: ->(cur) { Plan.pull(a, cur, square) },
                                                                                               preview: lambda { |cur|
                                                                                                 (helper ? helper.call(cur) : []) + preview.call([a, cur])
                                                                                               }) do |b, _vw|
                next :continue if Geometry.dist(a, b) < 0.5

                create.call([a, b], nil)
                :done
              end)
              :chain
            end)
          when 'combined'
            bend = [v['bend'].to_f, 3.0].max
            snap = lambda do |pts, cur|
              next cur unless pts.length == 1

              Plan.pull(pts[0], cur, Plan.square_to_road(pts[0], roads))
            end
            tool = Tools::PolylineTool.new("Garažna rampa: tačke srednje linije (krivine R #{Roads.num(bend)} m).", heights: :same, height: 0.0, closable: false,
                                                                                                                   snap: snap, preview: lambda { |pts|
                                                                                                                     fp, fb = Ramp.fillet_path(pts, bend)
                                                                                                                     preview.call(fp, fb)
                                                                                                                   }) do |pts, _closed|
              fp, fb = Ramp.fillet_path(pts, bend)
              create.call(fp, fb)
            end
            model.select_tool(tool)
          else
            left = v['turn'] != 'right'
            halves = params['kind'] == 'spiral' ? [(v['turns'].to_f * 2).round, 2].max : 1
            model.select_tool(Tools::PickTool.new('Garažna rampa: središte rampe.') do |c, _view|
              model.select_tool(Tools::PickTool.new('Garažna rampa: početak na srednjoj liniji.', from: c, preview: lambda { |cur|
                pts, bulges = Ramp.circle_path(c, cur, left, halves)
                preview.call(pts, bulges)
              }) do |st, _vw|
                next :continue if Geometry.dist(c, st) < 1.0

                pts, bulges = Ramp.circle_path(c, st, left, halves)
                create.call(pts, bulges)
                :done
              end)
              :chain
            end)
          end
        end

        def finish_ramp(params, v, pts, bulges, roads)
          b = base
          start = if v['startAtRoad'] == true
                    near = roads.map { |r| [r, Plan.nearest(r[:axis], pts[0], r[:closed])] }.select { |r, h| h && h[:dist] <= r[:half] + 8 }.min_by { |_r, h| h[:dist] }
                    near ? near[1][:z] : pts[0][2].to_f
                  else
                    Roads.to_relative(v['startLevel'].to_f, b)
                  end
          p = params.merge('startLevel' => start.round(4), 'endLevel' => Roads.to_relative(v['endLevel'].to_f, b), 'ceilingLevel' => Roads.to_relative(v['ceilingLevel'].to_f, b))
          length = Ramp.length(pts, bulges || Array.new(pts.length, 0.0))
          if Ramp.curved?(p['kind']) && p['kind'] != 'combined'
            radius = Geometry.dist(pts[0], pts[1]) / 2
            Sketchup.status_text = "UPOZORENJE: unutrašnji poluprečnik rampe je #{Roads.num(radius - (p['width'] / 2))} m (preporučeno najmanje 5 m)." if radius - (p['width'] / 2) < 5
          end
          geometry = Geo.path(pts.map { |q| [q[0], q[1], start] }, bulges: bulges)
          change('Garažna rampa') { |st| st.add(type: GARAGE_RAMP, params: p, geometry: geometry, name: 'Garažna rampa') }
          slope = length > 1e-6 ? (p['endLevel'] - p['startLevel']).abs / length : 0
          Sketchup.status_text = "Garažna rampa: L=#{Roads.num(length)} m, h=#{Roads.num((p['endLevel'] - p['startLevel']).abs)} m, #{Roads.num(slope * 100)} %."
        end

        # ---- Presjek rampe

        def ramp_section
          ramps = store.records.select { |r| r.type == GARAGE_RAMP }
          if ramps.empty?
            UI.messagebox('U modelu nema garažne rampe. Nova rampa: Saobraćajnice > Garažna rampa.')
            return
          end
          place = lambda do |ramp|
            model.select_tool(Tools::PickTool.new("Uzdužni presjek rampe #{ramp.label}: kliknite mjesto (lijevi kraj donje linije).") do |p, _vw|
              change('Presjek rampe') do |st|
                st.add(type: RAMP_SECTION, params: { 'reference' => ramp.id }, geometry: Geo.point([p[0], p[1], 0.0]), name: "Uzdužni presjek rampe #{ramp.label}")
              end
              :done
            end)
          end
          return place.call(ramps[0]) if ramps.length == 1

          model.select_tool(Tools::PickTool.new('Presjek rampe: kliknite rampu.') do |p, _view|
            best = ramps.min_by { |r| (Plan.nearest(r.axis_points, p) || { dist: Float::INFINITY })[:dist] }
            place.call(best)
            :chain
          end)
        end

        def show_last
          UI.messagebox(Commands.last_message || 'Saobraćajnice još nisu građene u ovoj sesiji.', MB_MULTILINE, 'Vanjsko uređenje PRO – saobraćajnice')
        end

        # ---- both ways with AutoCAD

        def export_to_autocad
          m = model
          if store.records.empty?
            hint = Exchange.candidates(m).empty? ? '' : "\n\nUlice uvezene iz AutoCAD-a prvo preuzmite: Saobraćajnice > Preuzmi saobraćajnice iz AutoCAD-a."
            return UI.messagebox("U modelu nema saobraćajnica nacrtanih alatima Saobraćajnice.#{hint}")
          end

          name = m.path.to_s.empty? ? 'saobracajnice' : "#{File.basename(m.path, '.*')}_saobracajnice"
          dir = m.path.to_s.empty? ? nil : File.dirname(m.path)
          path = UI.savepanel('Izvoz saobraćajnica za AutoCAD', dir, "#{name}.sdpkg")
          return unless path

          path = "#{path}.sdpkg" unless File.extname(path).casecmp('.sdpkg').zero?
          result = Exchange.export(m, path)
          notes = (result['notes'] || []).first(8).map { |n| "• #{n}" }
          UI.messagebox("Izvezeno: #{result['elements']} elemenata (#{result['roads']} ulica i cesta) u\n#{result['package']}\n\n" \
                        "U AutoCAD-u: Uvoz paketa (SDPUVOZPAKETA). Postojeće saobraćajnice se zamjenjuju (Zamijeni), nove se dodaju, " \
                        "a AutoCAD sam ponovo crta kolovoze, ivičnjake, trotoare, oznake i rampe." +
                        (notes.empty? ? '' : "\n\nNapomene:\n#{notes.join("\n")}"), MB_MULTILINE, 'Vanjsko uređenje PRO – saobraćajnice')
        rescue Engine::Error => e
          UI.messagebox("Izvoz saobraćajnica nije uspio:\n#{e.message}")
        end

        def adopt_from_autocad
          m = model
          list = Exchange.candidates(m)
          return UI.messagebox('U modelu nema uvezenog AutoCAD projekta sa saobraćajnicama (Uvoz paketa).') if list.empty?

          root, count = list.max_by { |_r, n| n }
          name = root.get_attribute(Exchange::DICT, 'projectName').to_s
          answer = UI.messagebox("Preuzeti #{count} saobraćajnih elemenata projekta „#{name}” u alate Saobraćajnice?\n\n" \
                                 "Ulice, ceste, prijelazi, kote i rampe postaju izmjenjive ovdje; njihov 3D iz AutoCAD-a se zamjenjuje " \
                                 "3D-om alata Saobraćajnice. Sljedeći uvozi tog projekta iz AutoCAD-a donose izmjene saobraćajnica, " \
                                 "a Izvoz saobraćajnica za AutoCAD ih vraća u crtež.", MB_YESNO)
          return unless answer == IDYES

          stats, outcome = Exchange.adopt(m, root)
          text = "Preuzeto: #{Exchange.stats_text(stats)}."
          text += "\n\n#{Rebuild.message(outcome)}" if outcome
          text += "\n\nSaobraćajnice nisu izgrađene; Saobraćajnice > Osvježi saobraćajnice." unless outcome
          Commands.last_message = text
          UI.messagebox(text, MB_MULTILINE, 'Vanjsko uređenje PRO – saobraćajnice')
        end

        def select_axes
          r = store.root(false)
          return UI.messagebox('U modelu nema osovina saobraćajnica.') unless r

          model.selection.clear
          model.selection.add(r)
        end

        # ---- menu, toolbar, observer

        def icon(name, size)
          svg = File.join(ICONS, "#{name}.svg")
          png = File.join(ICONS, "#{name}_#{size}.png")
          Sketchup.version.to_i >= 16 && Sketchup.platform == :platform_win && File.file?(svg) ? svg : png
        end

        def command(text, tip, icon_name, &block)
          cmd = UI::Command.new(text) do
            begin
              block.call
            rescue StandardError => e
              Log.error("Naredba '#{text}' nije uspjela", e)
              UI.messagebox("#{text}: greška – #{e.message}\nDetalji su u dnevniku (log).")
            end
          end
          cmd.tooltip = text
          cmd.status_bar_text = tip
          if icon_name
            cmd.small_icon = icon(icon_name, 16)
            cmd.large_icon = icon(icon_name, 24)
          end
          cmd
        end

        def install(parent_menu)
          cmds = [
            [:street, command('Ulica', 'Ulica iz osovine: 1–3 trake, jednosmjerna ili dvosmjerna, trotoari, biciklističke staze, srednje ostrvo, ivičnjaci, linije i strelice. Ulice se same spajaju, uglovi raskrsnica se zaobljuju.', 'street') { street }],
            [:edit, command('Uredi ulicu', 'Trake, širine, trotoari, staze, radijus i strelice ulice (odaberite osovinu ili kliknite ulicu); obična cesta postaje ulica.', 'streetedit') { edit_street }],
            [:roundabout, command('Kružni tok', 'Cijeli kružni tok iz jedne tačke: prsten, otok, ivičnjaci, razdjelni otoci, prijelazi, oznake i krakovi.', 'roundabout') { roundabout }],
            [:crossing, command('Pješački prijelaz', 'Bijele pruge 0,50 m s razmakom 0,50 m preko svih traka; na ulici se sam okrene i uzme trake.', 'crossing') { crossing }],
            [:road, command('Cesta (kolni prilaz, pristupna, servisna)', 'Cesta zadana širinom; spaja se s ulicama (kolni prilaz dobija zaobljenje i spuštenu stazu).', 'road') { simple_road }],
            [:level, command('Kota saobraćajnice', 'Visina na ulici; između kota visina se mijenja linearno, nagib preko 10 % se javlja.', 'level') { level_mark }],
            [:curb, command('Rampa za invalide', 'Rampa 120 cm gdje kliknete: na trotoaru s te strane, na kolovozu s obje strane.', 'curbramp') { curb_ramp }],
            [:ramp, command('Garažna rampa', 'Pravolinijska, polukružna, spiralna ili kombinovana rampa, jednosmjerna ili dvosmjerna, s visinama, pločom iznad i opremom.', 'garageramp') { garage_ramp }],
            [:section, command('Presjek rampe', 'Uzdužni presjek garažne rampe pored tlocrta: nagibi i dužine dionica, kote, ploča iznad i svijetla visina.', 'rampsection') { ramp_section }],
            [:levels, command('Visine i postavke saobraćajnica', 'Nadmorska visina Z = 0, ravan ili neravan teren, automatsko osvježavanje.', 'levels') { levels }],
            [:refresh, command('Osvježi saobraćajnice', 'Ponovo gradi sve saobraćajnice iz osovina (radi se i samo nakon izmjene osovina).', 'roadsrefresh') { refresh }]
          ]
          sub = parent_menu.add_submenu('Saobraćajnice')
          cmds.each_with_index do |(key, cmd), i|
            sub.add_item(cmd)
            sub.add_separator if %i[crossing curb section].include?(key) && i < cmds.length - 1
          end
          sub.add_separator
          sub.add_item(command('Ulica iz odabranih linija', 'Odabrane ivice (linija bez grananja) postaju osovina ulice.', nil) { street_from_selection })
          sub.add_item(command('Odaberi osovine', 'Odabire grupu s osovinama (sakrivanje: oznaka „VU Saobraćajnice – osovine”).', nil) { select_axes })
          sub.add_item(command('Izvještaj posljednje izgradnje', 'Napomene i upozorenja posljednje izgradnje saobraćajnica.', nil) { show_last })
          sub.add_separator
          sub.add_item(command('Izvoz saobraćajnica za AutoCAD…', 'Ulice, ceste, kružni tokovi, prijelazi, kote, rampe za invalide i garažne rampe kao paket za AutoCAD (Uvoz paketa); AutoCAD sam crta kolovoze, ivičnjake i oznake.', nil) { export_to_autocad })
          sub.add_item(command('Preuzmi saobraćajnice iz AutoCAD-a', 'Ulice i ostale saobraćajnice uvezenog AutoCAD projekta postaju izmjenjive alatima Saobraćajnice; sljedeći uvozi iz AutoCAD-a donose njihove izmjene.', nil) { adopt_from_autocad })

          tb = UI::Toolbar.new('Vanjsko uređenje PRO – Saobraćajnice')
          cmds.each { |_k, c| tb.add_item(c) }
          tb.get_last_state == TB_NEVER_SHOWN ? tb.show : tb.restore

          UI.add_context_menu_handler do |menu|
            g = selected_source
            next unless g

            menu.add_item('Uredi ulicu (Vanjsko uređenje PRO)') { edit_street_record(g) }
          end
          Watcher.attach(Sketchup.active_model)
          Sketchup.add_observer(AppWatcher.new)
          cmds
        end
      end

      # Rebuilds the roads after their source groups were moved, rotated, copied, edited or erased with the
      # SketchUp tools (the AutoCAD road watcher). The rebuild is merged into the same undo step.
      class Watcher < (defined?(Sketchup::ModelObserver) ? Sketchup::ModelObserver : Object)
        @attached = {}

        class << self
          attr_reader :attached
        end

        def self.attach(model)
          return unless model && model.respond_to?(:add_observer)
          return if @attached[model.object_id]

          @attached[model.object_id] = true
          model.add_observer(new)
        end

        def onTransactionCommit(model)
          schedule(model)
        end

        def onTransactionEmpty(model)
          schedule(model)
        end

        def schedule(model)
          return if Rebuild.busy || @pending

          @pending = true
          UI.start_timer(0.3, false) do
            @pending = false
            check(model)
          end
        end

        def check(model)
          return if Rebuild.busy || (model.respond_to?(:valid?) && !model.valid?)

          store = SourceStore.new(model)
          return unless store.root(false)
          return unless model.get_attribute(SourceStore::SETTINGS, 'autoRefresh', true) != false

          sig = store.signature
          return if sig == store.built_signature

          before = Rebuild.violations_before(model)
          outcome = Rebuild.run(model, operation: :transparent)
          Sketchup.status_text = "Saobraćajnice osvježene: #{outcome.summary}" if outcome
          broken = Rebuild.new_violations(before, outcome)
          unless broken.empty?
            UI.messagebox("Rampa je ispod tehničkog minimuma i takva se ne može projektovati:\n\n#{broken.join("\n")}\n\nVratite izmjenu (Uredi > Poništi) ili ispravite rampu.", MB_MULTILINE, 'Vanjsko uređenje PRO – saobraćajnice')
            return
          end
          UI.messagebox("Saobraćajnice\n\n#{Rebuild.message(outcome)}", MB_MULTILINE) if outcome && !outcome.warnings.empty?
        rescue StandardError => e
          Log.warn("Automatsko osvježavanje saobraćajnica: #{e.message}")
          Sketchup.status_text = "Saobraćajnice nisu osvježene: #{e.message}"
        end
      end

      class AppWatcher < (defined?(Sketchup::AppObserver) ? Sketchup::AppObserver : Object)
        def onNewModel(model)
          Watcher.attach(model)
        end

        def onOpenModel(model)
          Watcher.attach(model)
        end
      end
    end
  end
end
