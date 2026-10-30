# frozen_string_literal: true

# Minimal stand-in for SketchUp's UI layer (menus, toolbars, HtmlDialog, message boxes, tools) so main.rb, the
# commands and every dialog callback can run in plain Ruby. Records what the extension asked the UI to do.
MF_ENABLED = 0
MF_GRAYED = 2
MB_YESNO = 4
IDYES = 6
VK_LEFT = 37
VK_RIGHT = 39
GL_LINES = 1
GL_LINE_LOOP = 2

module UI
  @log = []
  @answers = {}
  class << self
    attr_reader :log, :answers

    def reset!
      @log = []
      @answers = {}
      @menus = nil
      @dialogs = []
      @context_handlers = []
    end

    def dialogs
      @dialogs ||= []
    end

    def menu(name)
      (@menus ||= {})[name] ||= Menu.new(name)
    end

    def messagebox(text, type = 0)
      @log << [:messagebox, text]
      type == MB_YESNO ? (@answers[:yesno] || 7) : 1
    end

    def openpanel(*args)
      @log << [:openpanel, args]
      @answers[:openpanel]
    end

    def savepanel(*args)
      @log << [:savepanel, args]
      @answers[:savepanel]
    end

    def inputbox(*args)
      @log << [:inputbox, args]
      @answers[:inputbox]
    end

    def openURL(url)
      @log << [:openURL, url]
      true
    end

    def beep
      @log << [:beep]
    end

    def start_timer(*_args, &_block)
      1
    end

    def add_context_menu_handler(&block)
      (@context_handlers ||= []) << block
    end

    def context_handlers
      @context_handlers ||= []
    end
  end

  class Menu
    attr_reader :items, :name

    def initialize(name)
      @name = name
      @items = []
    end

    def add_submenu(name)
      m = Menu.new(name)
      @items << m
      m
    end

    def add_item(item, &block)
      @items << (block ? [item, block] : item)
      @items.length
    end

    def add_separator
      @items << :separator
    end
  end

  class Command
    attr_accessor :tooltip, :status_bar_text, :small_icon, :large_icon
    attr_reader :text, :validation

    def initialize(text, &block)
      @text = text
      @block = block
    end

    def set_validation_proc(&block)
      @validation = block
    end

    def call
      @block.call
    end
  end

  class Toolbar
    attr_reader :items

    def initialize(name)
      @name = name
      @items = []
    end

    def add_item(cmd)
      @items << cmd
    end

    def restore; end
  end

  class HtmlDialog
    STYLE_DIALOG = 0
    attr_reader :options, :file, :scripts, :callbacks

    def initialize(options)
      @options = options
      @callbacks = {}
      @scripts = []
      @closed = false
      UI.dialogs << self
    end

    def set_file(path)
      raise "dialog page missing: #{path}" unless File.file?(path)

      @file = path
    end

    def add_action_callback(name, &block)
      @callbacks[name] = block
    end

    # Simulates window.sketchup.<name>(json) from the page.
    def fire(name, data = nil)
      cb = @callbacks.fetch(name) { raise "no callback '#{name}' in #{File.basename(@file)}" }
      cb.call(nil, data.nil? ? '' : JSON.generate(data))
    end

    def execute_script(js)
      @scripts << js
    end

    # Last object pushed with sdp.render(...).
    def last_render
      js = @scripts.reverse.find { |s| s.start_with?('sdp.render(') }
      js && JSON.parse(js.sub(/\Asdp\.render\(/, '').sub(/\)\z/, ''))
    end

    def show
      @shown = true
    end

    def shown?
      @shown == true
    end

    def close
      @closed = true
    end

    def closed?
      @closed
    end
  end
end

module Sketchup
  class << self
    attr_accessor :vcb_label, :vcb_value
  end

  # InputPoint at a scripted position (inches).
  class InputPoint
    attr_accessor :position

    def initialize
      @position = nil
    end

    def pick(_view, x, y)
      @position = Geom::Point3d.new(x.to_f, y.to_f, 0.0)
      true
    end

    def valid?
      !@position.nil?
    end

    def tooltip
      ''
    end

    def draw(_view); end
  end
end

module Sketchup
  class View
    attr_accessor :drawing_color, :line_width, :tooltip
    attr_reader :drawn

    def draw(mode, pts)
      (@drawn ||= []) << [mode, pts]
    end

    def invalidate
      self
    end
  end
end
