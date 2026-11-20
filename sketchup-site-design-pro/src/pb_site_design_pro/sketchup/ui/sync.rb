# frozen_string_literal: true

module ProjektniBiro
  module SiteDesignPro
    # Sinhronizacija s AutoCAD-om: everything about the exchange in one window (what came from AutoCAD and what went
    # back, the version) with the two actions, and the results of the exchange in a window that can be read in
    # full and copied (instead of a message box). SketchUp 2014–2016 (no HtmlDialog) get a message box.
    module Sync
      DICT = 'SiteDesignPro'
      CSS = 'body{font:13px "Segoe UI",Arial,sans-serif;margin:16px;color:#222}h2{font-size:16px;margin:0 0 10px}' \
            'table{border-collapse:collapse;width:100%;margin-bottom:10px}td{padding:3px 0}td.v{text-align:right;font-weight:600}' \
            'p{color:#555;margin:6px 0}pre{white-space:pre-wrap;font:13px "Segoe UI",Arial,sans-serif;margin:0 0 12px}' \
            '.bar{text-align:right;margin-top:14px}button{min-width:110px;padding:5px 12px;margin-left:6px;font:13px "Segoe UI",Arial}' \
            'button.main{font-weight:600}'

      module_function

      def html?
        defined?(UI::HtmlDialog) && UI::HtmlDialog.method_defined?(:set_html)
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

        d = UI::HtmlDialog.new(dialog_title: title, preferences_key: 'pb_sdp_message', width: 560, height: 420, style: UI::HtmlDialog::STYLE_DIALOG)
        d.set_html("<!DOCTYPE html><html><head><meta charset='utf-8'><style>#{CSS}</style></head><body>" \
                   "<h2>#{esc(title)}</h2><pre id='t'>#{esc(text)}</pre><div class='bar'>" \
                   "<button onclick=\"navigator.clipboard&&navigator.clipboard.writeText(document.getElementById('t').innerText)\">Kopiraj</button>" \
                   "<button class='main' onclick='sketchup.close()'>Zatvori</button></div></body></html>")
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

        table = rows.map { |a, b| "<tr><td>#{esc(a)}</td><td class='v'>#{esc(b)}</td></tr>" }.join
        d = UI::HtmlDialog.new(dialog_title: 'Sinhronizacija s AutoCAD-om', preferences_key: 'pb_sdp_sync', width: 560, height: 430, style: UI::HtmlDialog::STYLE_DIALOG)
        d.set_html("<!DOCTYPE html><html><head><meta charset='utf-8'><style>#{CSS}</style></head><body><h2>Sinhronizacija s AutoCAD-om</h2>" \
                   "<table>#{table}</table>#{notes.map { |n| "<p>#{esc(n)}</p>" }.join}<div class='bar'>" \
                   "<button class='main' onclick='sketchup.receive()'>Preuzmi iz AutoCAD-a</button>" \
                   "<button class='main' onclick='sketchup.push()'>Pošalji u AutoCAD</button>" \
                   "<button onclick='sketchup.close()'>Zatvori</button></div></body></html>")
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
