# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    module Roads
      # The windows of the road commands: one form page (roads.html) with the fields of each command, the same
      # options and ranges as in AutoCAD.
      module Forms
        module_function

        # Shows a form; the block gets the values (hash) when the user confirms. summary: proc(values) -> text,
        # shown live under the fields.
        def ask(title, fields, values, intro: '', note: '', ok: 'U redu', summary: nil, &done)
          dlg = Dialogs.create("Vanjsko uređenje PRO – #{title}", 'roads.html', "Roads_#{title.gsub(/\W+/, '')}", 620, 640)
          payload = { 'title' => title, 'intro' => intro, 'note' => note, 'okLabel' => ok, 'fields' => fields,
                      'values' => values, 'live' => !summary.nil? }
          payload['summary'] = safe_summary(summary, values) if summary
          dlg.add_action_callback('ready') { |_c| Dialogs.push(dlg, payload) }
          dlg.add_action_callback('change') do |_c, arg|
            Dialogs.push(dlg, 'summaryOnly' => true, 'summary' => safe_summary(summary, Dialogs.parse(arg))) if summary
          end
          dlg.add_action_callback('ok') do |_c, arg|
            v = Dialogs.parse(arg)
            dlg.close
            # the next step (a tool) starts after the window is gone
            UI.start_timer(0, false) do
              begin
                done.call(v)
              rescue StandardError => e
                Log.error("Saobraćajnice: #{title}", e)
                UI.messagebox("#{title}: greška – #{e.message}")
              end
            end
          end
          dlg.add_action_callback('close') { |_c| dlg.close }
          dlg.show
          dlg
        end

        def safe_summary(proc, values)
          proc.call(values)
        rescue StandardError => e
          e.message
        end

        def f(key, label, type, opts = {})
          { 'key' => key, 'label' => label, 'type' => type }.merge(opts)
        end

        def group(key, label)
          { 'key' => key, 'label' => label, 'type' => 'group' }
        end

        TWO_WAY = { 'direction' => 'two_way' }.freeze

        # The cross-section of a street (the AutoCAD window Ulica) plus, for a new street, the heights of its points.
        def street_fields(new_street: true, roundabout: false)
          list = [group('g1', roundabout ? 'Presjek krakova (i traka prstena)' : 'Presjek ulice')]
          unless roundabout
            list << f('direction', 'Smjer', 'radio', 'options' => [%w[two_way Dvosmjerna], %w[one_way Jednosmjerna]])
          end
          list << f('lanesLeft', 'Broj traka lijevo (1–3)', 'int', 'min' => 1, 'max' => 3, 'show' => roundabout ? nil : TWO_WAY,
                    'hint' => roundabout ? 'Trake koje ulaze u kružni tok; toliko traka ima i prsten.' : 'Trake lijevo od osovine voze suprotno od smjera crtanja osovine.')
          list << f('lanesRight', roundabout ? 'Broj traka desno (1–3)' : 'Broj traka desno / ukupno kod jednosmjerne (1–3)', 'int', 'min' => 1, 'max' => 3,
                    'hint' => 'Trake desno od osovine voze u smjeru crtanja osovine.')
          list << f('laneWidth', 'Širina saobraćajne trake', 'number', 'unit' => 'm', 'min' => 2.5, 'max' => 4.5, 'hint' => 'Npr. 3,00 za stambenu ulicu, 3,25–3,50 za sabirnu.')
          unless roundabout
            list << f('median', 'Srednje ostrvo (razdjelni pojas) između smjerova, 0 = nema', 'number', 'unit' => 'm', 'min' => 0, 'max' => 6, 'show' => TWO_WAY)
            list << f('medianTo', 'Srednje ostrvo do (m od početka osovine, 0 = cijelom dužinom)', 'number', 'unit' => 'm', 'min' => 0, 'max' => 10_000, 'show' => TWO_WAY)
            list << f('medianTaper', 'Dužina spajanja traka iza ostrva', 'number', 'unit' => 'm', 'min' => 0, 'max' => 500, 'show' => TWO_WAY)
          end
          list << group('g2', 'Trotoari i biciklističke staze')
          list << f('sidewalkLeft', 'Trotoar lijevo (0–5, 0 = nema)', 'number', 'unit' => 'm', 'min' => 0, 'max' => 5)
          list << f('sidewalkRight', 'Trotoar desno (0–5, 0 = nema)', 'number', 'unit' => 'm', 'min' => 0, 'max' => 5)
          list << f('bikeLeft', 'Biciklistička staza lijevo (0–2)', 'number', 'unit' => 'm', 'min' => 0, 'max' => 2,
                    'hint' => 'Iza ivičnjaka 15 cm, na visini trotoara (bez ivičnjaka prema trotoaru).')
          list << f('bikeRight', 'Biciklistička staza desno (0–2)', 'number', 'unit' => 'm', 'min' => 0, 'max' => 2)
          list << group('g3', 'Raskrsnice i oznake')
          list << f('cornerRadius', 'Radijus ivičnjaka – stvarni (= efektivni)', 'number', 'unit' => 'm', 'min' => 1, 'max' => 30,
                    'hint' => '6,00 m: ulice do 40 km/h, putnička i manja teretna vozila.')
          list << f('arrows', 'Strelice smjera na kolovozu', 'bool')
          list << f('arrowSpacing', 'Razmak strelica', 'number', 'unit' => 'm', 'min' => 10, 'max' => 200, 'show' => { 'arrows' => true })
          if new_street && !roundabout
            list << group('g4', 'Visine tačaka osovine')
            list << f('heights', 'Visine', 'radio', 'options' => [%w[click Iz\ kliknutih\ tačaka\ (3D)], %w[same Sve\ iste], %w[each Upiši\ za\ svaku\ tačku]],
                      'hint' => 'Iz kliknutih tačaka: tačke na terenu ili na drugim cestama daju njihovu visinu. Uzdužni nagib preko 10 % se javlja.')
            list << f('height', 'Visina (Z modela; može i m n.v.)', 'number', 'unit' => 'm', 'min' => -10_000, 'max' => 10_000, 'show' => { 'heights' => %w[same each] })
          end
          list
        end

        def street_summary(values)
          s = StreetSpec.new(values)
          notes = s.normalize!
          text = "#{s.describe}.\nUkupna širina presjeka #{Roads.num(s.total_width)} m (kolovoz #{Roads.num(s.carriageway(true) + s.carriageway(false))} m). " \
                 'Uz kolovoz ivičnjak 15 cm, uz vanjsku ivicu trotoara ivičnjak 10 cm. Ulica se sama spaja s ulicama i cestama koje dodiruje; uglovi raskrsnica se zaobljuju.'
          notes.empty? ? text : "#{text}\n#{notes.join(' ')}"
        end

        def simple_road_fields
          [f('type', 'Vrsta ceste', 'enum', 'options' => SIMPLE_ROADS.map { |t| [t, TYPE_NAMES[t]] }),
           f('width', 'Širina kolovoza', 'number', 'unit' => 'm', 'min' => 1, 'max' => 20,
             'hint' => 'Od 5 m dvosmjerna sa srednjom linijom, uža jednosmjerna; bez ivičnjaka i trotoara.'),
           f('heights', 'Visine', 'radio', 'options' => [%w[click Iz\ kliknutih\ tačaka\ (3D)], %w[same Sve\ iste], %w[each Upiši\ za\ svaku\ tačku]]),
           f('height', 'Visina (Z modela; može i m n.v.)', 'number', 'unit' => 'm', 'min' => -10_000, 'max' => 10_000, 'show' => { 'heights' => %w[same each] })]
        end

        def roundabout_fields
          [group('r1', 'Kružni tok (mini, do 25 km/h)'),
           f('islandRadius', 'Poluprečnik srednjeg otoka – zelenilo (10–30)', 'number', 'unit' => 'm', 'min' => 10, 'max' => 30),
           f('apron', 'Širina prijelaznog prstena (granitna kaldrma)', 'number', 'unit' => 'm', 'min' => 0, 'max' => 5),
           f('arms', 'Broj krakova (0–6)', 'int', 'min' => 0, 'max' => 6),
           f('armLength', 'Dužina krakova od vanjske ivice kružnog toka', 'number', 'unit' => 'm', 'min' => 10, 'max' => 500),
           f('angleByClick', 'Smjer prvog kraka drugim klikom (inače istok, 0°)', 'bool')] + street_fields(roundabout: true)
        end

        def roundabout_summary(v)
          s = StreetSpec.new(v.merge('direction' => 'two_way', 'median' => 1.5))
          s.normalize!
          ri = [[v['islandRadius'].to_f, 10].max, 30].min
          lanes = [s.lanes_left, 1].max
          ro = ri + v['apron'].to_f + KERB + (lanes * s['laneWidth'].to_f)
          "Otok R #{Roads.num(ri)} m, prsten #{lanes} × #{Roads.num(s['laneWidth'])} m, vanjski poluprečnik #{Roads.num(ro)} m; " \
            "krakova #{v['arms'].to_i} × #{Roads.num(v['armLength'])} m. Krakovi: #{s.describe}, razdjelni otoci (srednje ostrvo 1,50 m do 15 m od prstena), " \
            'pješački prijelazi 3 m, linija ustupanja prvenstva i trouglovi.'
        end

        def crossing_fields
          [f('lanes', 'Broj traka (1–6)', 'int', 'min' => 1, 'max' => 6),
           f('laneWidth', 'Širina saobraćajne trake', 'number', 'unit' => 'm', 'min' => 1, 'max' => 6),
           f('length', 'Širina prijelaza – dužina pruga', 'number', 'unit' => 'm', 'min' => 1, 'max' => 20),
           f('bikeLane', 'S biciklističkom stazom 80 cm (lijevo od prijelaza, plava RAL 5010)', 'bool',
             'hint' => 'Pruge 0,50 m s razmakom 0,50 m preko svih traka; rampe za invalide se dodaju same.')]
        end

        def ramp_fields
          [group('k', 'Rampa'),
           f('kind', 'Vrsta rampe', 'radio', 'options' => Ramp::KINDS.map { |k| [k, Ramp::KIND_NAMES[k]] }),
           f('direction', 'Smjer', 'radio', 'options' => [%w[one_way Jednosmjerna], %w[two_way Dvosmjerna]]),
           f('laneWidth', 'Širina trake (pravolinijska 2,5–3,5; zakrivljena 3,5–5,0; kombinovana 2,5–5,0)', 'number', 'unit' => 'm', 'min' => 2.5, 'max' => 5),
           f('margin', 'Zaštitni razmak sa obje strane (0–0,50; zakrivljena najmanje 0,25)', 'number', 'unit' => 'm', 'min' => 0, 'max' => 0.5),
           f('island', 'Srednje ostrvo 60 × 250 cm na dnu', 'bool', 'show' => { 'kind' => 'straight', 'direction' => 'two_way' }),
           f('bend', 'Poluprečnik srednje linije u krivinama', 'number', 'unit' => 'm', 'min' => 3, 'max' => 100, 'show' => { 'kind' => 'combined' }),
           f('turn', 'Smjer okretanja', 'radio', 'options' => [%w[left Lijevo], %w[right Desno]], 'show' => { 'kind' => %w[semicircular spiral] }),
           f('turns', 'Broj punih krugova (npr. 1 ili 1,5)', 'number', 'min' => 0.5, 'max' => 10, 'show' => { 'kind' => 'spiral' }),
           group('h', 'Visine (Z modela; može i m n.v.)'),
           f('startAtRoad', 'Početak na visini ulice uz koju počinje', 'bool'),
           f('startLevel', 'Visina početka', 'number', 'unit' => 'm', 'min' => -10_000, 'max' => 10_000, 'show' => { 'startAtRoad' => false }),
           f('endLevel', 'Visina kraja', 'number', 'unit' => 'm', 'min' => -10_000, 'max' => 10_000,
             'hint' => 'Najveći dozvoljeni nagib garažne rampe je 17 %.'),
           f('transition', 'Blaža prijelazna dionica na vrhu i dnu (pola nagiba; 0 = bez)', 'number', 'unit' => 'm', 'min' => 0, 'max' => 8),
           f('flatLength', 'Blagi dio pred ulicom na vrhu, najviše 5 % (0 = bez)', 'number', 'unit' => 'm', 'min' => 0, 'max' => 10),
           group('c', 'Svijetla visina'),
           f('ceilingFrom', 'Ploča iznad rampe počinje na (m od početka, 0 = nema ploče)', 'number', 'unit' => 'm', 'min' => 0, 'max' => 500),
           f('ceilingLevel', 'Donja ivica ploče iznad rampe', 'number', 'unit' => 'm', 'min' => -10_000, 'max' => 10_000),
           f('minClearance', 'Potrebna svijetla visina', 'number', 'unit' => 'm', 'min' => 1.8, 'max' => 5),
           group('o', 'Oprema (3D)'),
           f('drain', 'Linijska rešetka odvodnje', 'enum', 'options' => [%w[bottom Na\ dnu], %w[both Na\ vrhu\ i\ dnu], %w[none Bez]]),
           f('walls', 'Potporni zidovi s ogradom i LED svjetlima', 'bool'),
           f('heated', 'Grijana rampa', 'bool')]
        end

        def ramp_summary(v)
          p, notes = Ramp.normalize(v)
          text = "#{Ramp::KIND_NAMES[p['kind']]}, #{p['direction'] == 'two_way' ? 'dvosmjerna' : 'jednosmjerna'}, širina #{Roads.num(p['width'])} m " \
                 "(#{p['direction'] == 'two_way' ? 2 : 1} × #{Roads.num(p['laneWidth'])} m + 2 × #{Roads.num(p['margin'])} m)."
          text += case p['kind']
                  when 'straight' then "\nKlik: početak (sredina, gornji kraj), zatim kraj. Uz ulicu se nudi smjer okomit na nju (plava linija)."
                  when 'combined' then "\nKlikovi: tačke srednje linije (Enter za kraj); krivine se zaobljuju poluprečnikom #{Roads.num(v['bend'])} m."
                  else "\nKlik: središte rampe, zatim početak na srednjoj liniji."
                  end
          notes.empty? ? text : "#{text}\n#{notes.join(' ')}"
        end

        def levels_fields
          [f('projectName', 'Naziv (grupa 3D modela)', 'text'),
           f('baseText', 'Nadmorska visina tačke Z = 0 modela (m n.v.; prazno = nije zadana)', 'text',
             'hint' => 'Visine saobraćajnica su Z modela; uz nadmorsku visinu se mogu upisivati i kao m n.v. (npr. 501,00).'),
           f('flat', 'Teren', 'radio', 'options' => [[false, 'Neravan: visine iz osovina i kota'], [true, 'Potpuno ravan: sve na Z = 0']]),
           f('autoRefresh', 'Saobraćajnice se same osvježe nakon pomjeranja, kopiranja ili brisanja osovina', 'bool')]
        end
      end
    end
  end
end
