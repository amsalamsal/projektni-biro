# frozen_string_literal: true

require 'digest'
require 'tmpdir'

module ProjektniBiro
  module SiteDesignPro
    # Automatic updates of the SketchUp extension from the GitHub releases of the project (the same latest.json as
    # AutoCAD reads). Once a day, some seconds after SketchUp starts, the newest version is read; a newer one is offered
    # in a window (or installed right away in the automatic mode). The .rbz is checked (SHA-256) before SketchUp installs
    # it; the new version runs after SketchUp is restarted. Needs SketchUp 2017+ (Sketchup::Http); older ones skip it.
    module Updater
      INTERVAL = 20 * 3600
      HEADERS = { 'User-Agent' => "VanjskoUredjenjePRO-SketchUp/#{VERSION}" }.freeze

      module_function

      def available?
        defined?(Sketchup::Http::Request) ? true : false
      end

      def mode
        m = Settings.get('updates').to_s
        Updates::MODES.include?(m) ? m : 'ask'
      end

      def mode_text(m = mode)
        { 'ask' => 'pita prije instalacije', 'auto' => 'automatski', 'off' => 'isključeno (samo na zahtjev)' }[m]
      end

      # At start: a check once a day, a little later so that SketchUp is not held up.
      def start
        return unless available? && mode != 'off'
        return if Time.now.to_i - Settings.get('updatesLastCheck').to_i < INTERVAL

        UI.start_timer(20, false) { check(false) }
      end

      # Reads latest.json; then offers (or installs) a newer version. manual: called from the menu, so the outcome is
      # always shown, also "you have the newest".
      def check(manual)
        unless available?
          Sync.message('Ažuriranja', 'Ova verzija SketchUpa ne podržava automatska ažuriranja (potreban je SketchUp 2017 ili noviji).') if manual
          return
        end

        get(Updates::LATEST_URL) do |ok, body|
          Settings.set('updatesLastCheck', Time.now.to_i) if ok
          begin
            raise ArgumentError, body unless ok

            found(Updates.parse(body), manual)
          rescue StandardError => e
            Log.warn("Provjera ažuriranja nije uspjela: #{e.message}")
            Sync.message('Ažuriranja', "Provjera nove verzije nije uspjela:\n#{e.message}\n\nProvjerite internet vezu.") if manual
          end
        end
      end

      def found(info, manual)
        unless Updates.newer?(info['version'], VERSION)
          Sync.message('Ažuriranja', "Imate najnoviju verziju (#{VERSION}).\n\nAžuriranja: #{mode_text}.") if manual
          return
        end
        return if !manual && info['version'] == Settings.get('updatesSkip').to_s
        return install(info) if mode == 'auto' && !manual

        offer(info, manual)
      end

      # The window: what is new; Instaliraj / Preskoči ovu verziju / Kasnije (a message box before SketchUp 2017).
      def offer(info, manual)
        notes = info['notes'].first(14).map { |n| "• #{n}" }.join("\n")
        text = "Instalirana verzija: #{VERSION}\nNova verzija: #{info['version']}#{info['date'].empty? ? '' : " (#{info['date']})"}\n\n" \
               "#{notes}\n\nNova verzija radi nakon ponovnog pokretanja SketchUpa. AutoCAD dio se ažurira sam u AutoCAD-u."
        unless Sync.html?
          install(info) if UI.messagebox("#{text}\n\nInstalirati sada?", MB_YESNO) == IDYES
          return
        end

        d = UI::HtmlDialog.new(dialog_title: 'Ažuriranje – Vanjsko uređenje PRO', preferences_key: 'pb_sdp_update', width: 560, height: 460, style: UI::HtmlDialog::STYLE_DIALOG)
        buttons = "<button onclick='sketchup.later()'>Kasnije</button>"
        buttons = "<button onclick='sketchup.skip()'>Preskoči ovu verziju</button>#{buttons}" unless manual
        d.set_html(Sync.page("Dostupna je nova verzija #{info['version']}", "Instalirana: #{VERSION}", "<pre>#{Sync.esc(text)}</pre>",
                             "#{buttons}<button class='primary' onclick='sketchup.install()'>Instaliraj</button>"))
        d.add_action_callback('install') do |_c|
          d.close
          install(info)
        end
        d.add_action_callback('skip') do |_c|
          Settings.set('updatesSkip', info['version'])
          d.close
        end
        d.add_action_callback('later') { |_c| d.close }
        d.center if d.respond_to?(:center)
        d.show
        d
      end

      # Downloads the .rbz, checks it and lets SketchUp install it.
      def install(info)
        Sketchup.status_text = "Vanjsko uređenje PRO: preuzimanje verzije #{info['version']}..."
        get(info['url'], binary: true) do |ok, body|
          begin
            raise ArgumentError, body unless ok

            path = File.join(Dir.tmpdir, "pb_site_design_pro-#{info['version']}.rbz")
            File.open(path, 'wb') { |f| f.write(body) }
            sha = Digest::SHA256.file(path).hexdigest
            raise ArgumentError, 'preuzeti paket je oštećen (SHA-256 se ne slaže), ništa nije instalirano' unless sha == info['sha256']

            installed = begin
              Sketchup.install_from_archive(path, false)
            rescue ArgumentError
              Sketchup.install_from_archive(path)
            end
            raise ArgumentError, 'SketchUp nije instalirao paket' unless installed

            Log.info("Ažurirano na #{info['version']}")
            Sync.message('Ažuriranje', "Vanjsko uređenje PRO #{info['version']} je instalirano.\n\nZatvorite i ponovo pokrenite SketchUp da bi nova verzija radila.")
          rescue StandardError => e
            Log.warn("Ažuriranje nije instalirano: #{e.message}")
            Sync.message('Ažuriranje', "Ažuriranje nije uspjelo:\n#{e.message}\n\nInstalirana verzija radi i dalje. Novu verziju možete instalirati i ručno " \
                                       "(Proširenja → Extension Manager → Install Extension).")
          ensure
            Sketchup.status_text = ''
          end
        end
      end

      # Načini: pita / automatski / isključeno.
      def settings
        choice = UI.inputbox(['Nove verzije'], [mode_text], [Updates::MODES.map { |m| mode_text(m) }.join('|')], 'Ažuriranja')
        return unless choice

        m = Updates::MODES.find { |x| mode_text(x) == choice[0] }
        Settings.set('updates', m) if m
      end

      # GET with redirects (GitHub sends the files from another address); yields [ok, body or error message].
      def get(url, binary: false, hops: 0, &block)
        req = Sketchup::Http::Request.new(url, Sketchup::Http::GET)
        req.headers = HEADERS
        (@requests ||= []) << req # the request must live until it answers
        req.start do |_r, res|
          @requests.delete(req)
          status = res.status_code.to_i
          location = res.headers.find { |k, _v| k.to_s.casecmp('location').zero? }
          if status >= 300 && status < 400 && location && hops < 5
            get(location[1].to_s, binary: binary, hops: hops + 1, &block)
          elsif status == 200
            body = res.body.to_s
            body = body.dup.force_encoding(Encoding::BINARY) if binary
            block.call(true, body)
          else
            block.call(false, "server je odgovorio #{status} (#{url})")
          end
        end
      rescue StandardError => e
        block.call(false, e.message)
      end
    end
  end
end
