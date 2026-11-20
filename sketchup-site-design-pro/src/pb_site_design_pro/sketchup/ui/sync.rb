# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Sinhronizacija s AutoCAD-om: everything about the exchange in one window (what came from AutoCAD and what went
    # back, the version) with the two actions, and the results of the exchange in a window that can be read in
    # full and copied (instead of a message box). SketchUp 2014–2016 (no HtmlDialog) get a message box.
    module Sync
      DICT = 'SiteDesignPro'
      # the stylesheet of all dialogs of the extension (sdp.css), so these windows look like the others
      EXTRA_CSS = 'pre{white-space:pre-wrap;font:inherit;margin:0}.rows td:last-child{text-align:right;font-weight:600}' \
                  '.notes p{color:var(--muted);margin:8px 0 0}'

      module_function

      def html?
        defined?(UI::HtmlDialog) && UI::HtmlDialog.method_defined?(:set_html)
      end

      def css
        @css ||= begin
          File.read(File.join(__dir__, 'html', 'sdp.css'), encoding: 'UTF-8')
        rescue StandardError
          ''
        end
        "#{@css}#{EXTRA_CSS}"
      end

      # One page in the layout of the other dialogs: header with title and subtitle, content, buttons in the footer.
      def page(title, sub, body, buttons)
        "<!DOCTYPE html><html><head><meta charset='utf-8'><style>#{css}</style></head><body>" \
          "<header><h1>#{esc(title)}</h1>#{sub.to_s.empty? ? '' : "<div class='sub'>#{esc(sub)}</div>"}</header>" \
          "<main>#{body}</main><footer>#{buttons}</footer></body></html>"
      end

      def esc(text)
        text.to_s.gsub('&', '&amp;').gsub('<', '&lt;').gsub('>', '&gt;')
      end

      # A result or note in a window: the whole text, selectable, with Kopiraj and Zatvori.
      def message(title, text)
        unless html?
          UI.messagebox(text, MB_MULTILINE, title)
          return
        end

        d = UI::HtmlDialog.new(dialog_title: "#{title} – Vanjsko uređenje PRO", preferences_key: 'pb_sdp_message', width: 580, height: 440, style: UI::HtmlDialog::STYLE_DIALOG)
        d.set_html(page(title, nil, "<pre id='t'>#{esc(text)}</pre>",
                        "<button onclick=\"navigator.clipboard&&navigator.clipboard.writeText(document.getElementById('t').innerText)\">Kopiraj</button>" \
                        "<button class='primary' onclick='sketchup.close()'>Zatvori</button>"))
        d.add_action_callback('close') { |_c, _a| d.close }
        d.show
        d
      end

      def last(key)
        Sketchup.read_default(Settings::SECTION, key, nil).to_s
      end

      def remember(key, path)
        Sketchup.write_default(Settings::SECTION, key, "#{File.basename(path.to_s)} (#{Time.now.strftime('%d.%m.%Y %H:%M')})")
      end

      # The window: project, counts, what was received and sent last, version; Preuzmi / Pošalji / Zatvori.
      def open(model)
        roots = model.entities.grep(Sketchup::Group).select { |g| g.get_attribute(DICT, 'role') == 'projectRoot' && g.get_attribute(DICT, 'roadsProject') != true }
        project = roots.first ? roots.first.get_attribute(DICT, 'projectName').to_s : ''
        elements = ModelIndex.managed_entities(model).length
        roads = Roads::SourceStore.new(model).records.length
        rows = [['Projekat iz AutoCAD-a', project.empty? ? '—' : project], ['Elemenata u modelu', elements.to_s], ['Saobraćajnica (osovine)', roads.to_s],
                ['Zadnje preuzeto iz AutoCAD-a', last('syncReceived').empty? ? '—' : last('syncReceived')],
                ['Zadnje poslano u AutoCAD', last('syncSent').empty? ? '—' : last('syncSent')], ['Verzija dodatka', VERSION]]
        notes = ['Preuzmi iz AutoCAD-a: paket koji je AutoCAD napravio (Sinhronizacija → Pošalji u SketchUp). Ulice i ceste postaju izmjenjive ovdje.',
                 'Pošalji u AutoCAD: saobraćajnice i svi elementi iz biblioteke, gdje su sada i s izmijenjenim parametrima. U AutoCAD-u: Sinhronizacija → Preuzmi iz SketchUpa.',
                 'AutoCAD i SketchUp trebaju imati istu verziju dodatka.']
        unless html?
          r = UI.messagebox("#{rows.map { |a, b| "#{a}: #{b}" }.join("\n")}\n\nDa = Preuzmi iz AutoCAD-a, Ne = Pošalji u AutoCAD", MB_YESNOCANCEL)
          Commands.import_package if r == IDYES
          Roads::Commands.export_to_autocad if r == IDNO
          return
        end

        table = rows.map { |a, b| "<tr><td>#{esc(a)}</td><td>#{esc(b)}</td></tr>" }.join
        d = UI::HtmlDialog.new(dialog_title: 'Sinhronizacija s AutoCAD-om – Vanjsko uređenje PRO', preferences_key: 'pb_sdp_sync', width: 600, height: 470, style: UI::HtmlDialog::STYLE_DIALOG)
        d.set_html(page('Sinhronizacija s AutoCAD-om', project.empty? ? 'Model bez AutoCAD projekta' : project,
                        "<table class='rows'>#{table}</table><div class='notes'>#{notes.map { |n| "<p>#{esc(n)}</p>" }.join}</div>",
                        "<button onclick='sketchup.close()'>Zatvori</button>" \
                        "<button class='primary' onclick='sketchup.receive()'>Preuzmi iz AutoCAD-a</button>" \
                        "<button class='primary' onclick='sketchup.push()'>Pošalji u AutoCAD</button>"))
        d.add_action_callback('receive') do |_c, _a|
          d.close
          Commands.import_package
        end
        d.add_action_callback('push') do |_c, _a|
          d.close
          Roads::Commands.export_to_autocad
        end
        d.add_action_callback('close') { |_c, _a| d.close }
        d.show
        d
      end
    end
  end
end
