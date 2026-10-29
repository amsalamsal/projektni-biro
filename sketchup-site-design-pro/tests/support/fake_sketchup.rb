# frozen_string_literal: true

# Minimal in-memory stand-in for the subset of the SketchUp Ruby API used by the Site Design Pro adapter.
# It lets the importer, update, edit and export workflows run in plain Ruby tests. It does NOT model
# SketchUp's geometry kernel (face merging, edge healing); those behaviours are verified manually in SketchUp.

require 'json'

module Geom
  class Point3d
    attr_reader :x, :y, :z

    def initialize(x = 0.0, y = 0.0, z = 0.0)
      @x, @y, @z = x.to_f, y.to_f, z.to_f
    end

    def to_a
      [@x, @y, @z]
    end
  end

  class Vector3d < Point3d
    def length
      Math.sqrt((x * x) + (y * y) + (z * z))
    end

    def angle_between(o)
      d = ((x * o.x) + (y * o.y) + (z * o.z)) / (length * o.length)
      Math.acos([[d, -1.0].max, 1.0].min)
    end
  end

  # Affine transformation stored as a 3x3 matrix (columns = axes) plus translation; to_a is column-major 4x4.
  class Transformation
    attr_reader :m, :t

    def initialize(m = [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]], t = [0.0, 0.0, 0.0])
      @m = m
      @t = t
    end

    def self.translation(v)
      new([[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]], v.to_a)
    end

    def self.rotation(_point, axis, angle)
      raise ArgumentError, 'fake supports rotation about Z only' unless axis.x.zero? && axis.y.zero?

      c = Math.cos(angle)
      s = Math.sin(angle)
      new([[c, s, 0.0], [-s, c, 0.0], [0.0, 0.0, 1.0]]) # columns: x axis, y axis, z axis
    end

    def self.scaling(f)
      new([[f, 0.0, 0.0], [0.0, f, 0.0], [0.0, 0.0, f]])
    end

    def apply(p)
      a = p.respond_to?(:to_a) ? p.to_a : p
      [0, 1, 2].map { |r| (m[0][r] * a[0]) + (m[1][r] * a[1]) + (m[2][r] * a[2]) + t[r] }
    end

    def *(other)
      cols = other.m.map { |col| [0, 1, 2].map { |r| (m[0][r] * col[0]) + (m[1][r] * col[1]) + (m[2][r] * col[2]) } }
      Transformation.new(cols, apply(other.t))
    end

    def origin
      Point3d.new(*t)
    end

    def xaxis
      v = Vector3d.new(*m[0])
      Vector3d.new(v.x / v.length, v.y / v.length, v.z / v.length)
    end

    def zaxis
      v = Vector3d.new(*m[2])
      Vector3d.new(v.x / v.length, v.y / v.length, v.z / v.length)
    end

    def to_a
      m[0] + [0.0] + m[1] + [0.0] + m[2] + [0.0] + t + [1.0]
    end
  end

  class BoundingBox
    attr_reader :min, :max

    def initialize
      @min = nil
      @max = nil
    end

    def add(*pts)
      pts = pts.first if pts.length == 1 && pts.first.is_a?(Array) && !pts.first.first.is_a?(Numeric)
      pts.each do |p|
        a = p.respond_to?(:to_a) ? p.to_a : p
        @min = @min ? Point3d.new([@min.x, a[0]].min, [@min.y, a[1]].min, [@min.z, a[2]].min) : Point3d.new(*a)
        @max = @max ? Point3d.new([@max.x, a[0]].max, [@max.y, a[1]].max, [@max.z, a[2]].max) : Point3d.new(*a)
      end
      self
    end

    def empty?
      @min.nil?
    end

    def center
      return Point3d.new if empty?

      Point3d.new((min.x + max.x) / 2, (min.y + max.y) / 2, (min.z + max.z) / 2)
    end

    def width
      empty? ? 0.0 : max.x - min.x
    end

    def height
      empty? ? 0.0 : max.y - min.y
    end
  end
end

module Sketchup
  @defaults = {}
  @status = ''
  class << self
    attr_accessor :active_model, :status_text

    def read_default(section, key, default = nil)
      @defaults.fetch([section, key], default)
    end

    def write_default(section, key, value)
      @defaults[[section, key]] = value
    end

    def temp_dir
      Dir.tmpdir
    end

    def platform
      :platform_osx
    end

    def version
      'fake'
    end
  end

  class Color
    attr_reader :rgb

    def initialize(*rgb)
      @rgb = rgb
    end
  end

  class AttributeDictionary
    include Enumerable

    def initialize
      @h = {}
    end

    def [](k)
      @h[k]
    end

    def []=(k, v)
      @h[k] = v
    end

    def delete_key(k)
      @h.delete(k)
    end

    def each(&block)
      @h.each(&block)
    end
  end

  class AttributeDictionaries
    def initialize
      @d = {}
    end

    def [](name)
      @d[name]
    end

    def fetch_or_create(name)
      @d[name] ||= AttributeDictionary.new
    end

    def delete(name)
      @d.delete(name)
    end
  end

  class Entity
    attr_accessor :parent

    def initialize
      @valid = true
      @attrs = AttributeDictionaries.new
    end

    def valid?
      @valid
    end

    def deleted!
      @valid = false
    end

    def set_attribute(dict, key, value)
      @attrs.fetch_or_create(dict)[key] = value
    end

    def get_attribute(dict, key, default = nil)
      d = @attrs[dict]
      d && !d[key].nil? ? d[key] : default
    end

    def attribute_dictionary(name, create = false)
      create ? @attrs.fetch_or_create(name) : @attrs[name]
    end

    def attribute_dictionaries
      @attrs
    end
  end

  class Drawingelement < Entity
    attr_accessor :layer, :material

    def erase!
      parent.entities.remove(self)
      deleted!
    end

    def model
      p = parent
      p = p.parent while p && !p.is_a?(Model)
      p
    end
  end

  class Face < Drawingelement
    attr_reader :points

    def initialize(points)
      super()
      @points = points
      n = newell
      len = Math.sqrt(n.sum { |c| c * c })
      raise ArgumentError, 'Points are not planar or face has no area' if len < 1e-12

      @normal = n.map { |c| c / len }
      # SketchUp quirk: faces created on the ground plane face down
      @normal = @normal.map(&:-@) if @points.all? { |p| p.z.abs < 1e-9 } && @normal[2] > 0
    end

    def normal
      Geom::Vector3d.new(*@normal)
    end

    def reverse!
      @normal = @normal.map(&:-@)
      self
    end

    def edges
      []
    end

    private

    def newell
      nx = ny = nz = 0.0
      @points.each_with_index do |a, i|
        b = @points[(i + 1) % @points.length]
        nx += (a.y - b.y) * (a.z + b.z)
        ny += (a.z - b.z) * (a.x + b.x)
        nz += (a.x - b.x) * (a.y + b.y)
      end
      [nx, ny, nz]
    end
  end

  class Edge < Drawingelement
    attr_accessor :soft, :smooth
    attr_reader :points

    def initialize(points)
      super()
      @points = points
    end

    def faces
      []
    end
  end

  class Entities
    include Enumerable
    attr_reader :parent

    def initialize(parent)
      @parent = parent
      @list = []
    end

    def model
      p = @parent
      p = p.parent while p && !p.is_a?(Model)
      p
    end

    def each(&block)
      @list.dup.each(&block)
    end

    def length
      @list.length
    end
    alias size length

    def remove(e)
      @list.delete(e)
    end

    def clear!
      @list.each(&:deleted!)
      @list.clear
    end

    def add(e)
      e.parent = @parent
      @list << e
      e
    end

    def add_group
      d = model.definitions.add_internal('Group', group: true)
      g = Group.new(d)
      d.instances << g
      add(g)
    end

    def add_instance(definition, transformation)
      raise ArgumentError, 'invalid definition' unless definition&.valid?

      inst = ComponentInstance.new(definition, transformation)
      definition.instances << inst
      add(inst)
    end

    def add_face(*pts)
      pts = pts.flatten
      raise ArgumentError, 'Duplicate points in array' if pts.map(&:to_a).uniq.length != pts.length

      add(Face.new(pts))
    end

    def add_edges(*pts)
      pts.flatten.each_cons(2).map { |a, b| add(Edge.new([a, b])) }
    end
  end

  class ComponentDefinition < Entity
    attr_reader :name, :entities, :instances

    def initialize(model, name, group: false)
      super()
      @parent = model
      @name = name
      @group = group
      @entities = Entities.new(self)
      @instances = []
    end

    def group?
      @group
    end

    def bounds
      bb = Geom::BoundingBox.new
      @entities.each do |e|
        case e
        when Face, Edge then bb.add(e.points)
        when Group, ComponentInstance
          sub = e.definition.bounds
          next if sub.empty?

          corners = [sub.min.x, sub.max.x].product([sub.min.y, sub.max.y], [sub.min.z, sub.max.z])
          bb.add(corners.map { |c| e.transformation.apply(c) })
        end
      end
      bb
    end
  end

  class ComponentInstance < Drawingelement
    attr_accessor :definition, :transformation, :name

    def initialize(definition, transformation)
      super()
      @definition = definition
      @transformation = transformation
      @name = ''
    end

    def definition=(d)
      @definition.instances.delete(self)
      @definition = d
      d.instances << self
    end

    def erase!
      @definition.instances.delete(self)
      super
    end

    def bounds
      b = @definition.bounds
      return b if b.empty?

      corners = [b.min.x, b.max.x].product([b.min.y, b.max.y], [b.min.z, b.max.z])
      Geom::BoundingBox.new.add(corners.map { |c| @transformation.apply(c) })
    end
  end

  class Group < ComponentInstance
    def initialize(definition)
      super(definition, Geom::Transformation.new)
    end

    def entities
      definition.entities
    end
  end

  class DefinitionList
    include Enumerable

    def initialize(model)
      @model = model
      @list = []
      @counter = 0
    end

    def each(&block)
      @list.dup.each(&block)
    end

    def add(name)
      d = ComponentDefinition.new(@model, unique(name))
      @list << d
      d
    end

    def add_internal(name, group: false)
      d = ComponentDefinition.new(@model, unique(name), group: group)
      @list << d
      d
    end

    def load(path)
      raise IOError, "cannot load #{path}" unless File.file?(path)

      existing = @list.find { |d| d.get_attribute('fake', 'path') == path }
      return existing if existing

      d = add(File.basename(path, '.skp'))
      d.set_attribute('fake', 'path', path)
      d.entities.add_face(Geom::Point3d.new(0, 0, 1), Geom::Point3d.new(10, 0, 1), Geom::Point3d.new(10, 10, 1))
      d
    end

    def [](name)
      @list.find { |d| d.name == name }
    end

    private

    def unique(name)
      @counter += 1
      @list.any? { |d| d.name == name } ? "#{name}##{@counter}" : name
    end
  end

  class Material < Entity
    attr_accessor :name, :color, :alpha, :texture

    def initialize(name)
      super()
      @name = name
    end
  end

  class Materials
    include Enumerable

    def initialize
      @list = []
    end

    def each(&block)
      @list.each(&block)
    end

    def add(name)
      m = Material.new(name)
      @list << m
      m
    end

    def [](name)
      @list.find { |m| m.name == name }
    end
  end

  class Layer < Entity
    attr_reader :name

    def initialize(name)
      super()
      @name = name
      @visible = true
    end

    def visible?
      @visible
    end

    def visible=(v)
      @visible = v
    end
  end

  class Layers
    include Enumerable

    def initialize
      @list = [Layer.new('Untagged')]
    end

    def each(&block)
      @list.each(&block)
    end

    def add(name)
      self[name] || (@list << Layer.new(name)).last
    end

    def [](name)
      @list.find { |l| l.name == name }
    end
  end

  class Page
    attr_reader :name, :layer_visibility, :camera, :xray

    def initialize(name, model)
      @name = name
      @model = model
      update
    end

    def update(_flags = nil)
      @layer_visibility = @model.layers.map { |l| [l.name, l.visible?] }.to_h
      @camera = @model.active_view.camera
      @xray = @model.rendering_options['ModelTransparency']
      true
    end
  end

  class Pages
    include Enumerable

    def initialize(model)
      @model = model
      @list = []
    end

    def each(&block)
      @list.each(&block)
    end

    def add(name)
      (@list << Page.new(name, @model)).last
    end

    def [](name)
      @list.find { |p| p.name == name }
    end

    def length
      @list.length
    end
  end

  class Camera
    attr_accessor :perspective, :height
    attr_reader :eye, :target, :up

    def initialize(eye, target, up)
      @eye = eye
      @target = target
      @up = up
      @perspective = true
    end
  end

  class View
    attr_accessor :camera

    def initialize
      @camera = Camera.new(Geom::Point3d.new(0, 0, 1000), Geom::Point3d.new, Geom::Vector3d.new(0, 1, 0))
    end
  end

  class Selection
    include Enumerable

    def initialize
      @list = []
    end

    def each(&block)
      @list.each(&block)
    end

    def clear
      @list.clear
    end

    def add(items)
      @list.concat(Array(items))
    end

    def to_a
      @list.dup
    end
  end

  class Model < Entity
    attr_reader :entities, :definitions, :materials, :layers, :pages, :selection, :rendering_options, :shadow_info,
                :active_view, :operations, :saved_copies
    attr_writer :path

    def initialize
      super()
      @entities = Entities.new(self)
      @definitions = DefinitionList.new(self)
      @materials = Materials.new
      @layers = Layers.new
      @pages = Pages.new(self)
      @selection = Selection.new
      @rendering_options = { 'ModelTransparency' => false }
      @shadow_info = { 'NorthAngle' => 0.0 }
      @active_view = View.new
      @operations = []
      @path = ''
      @saved_copies = []
    end

    # Path of the saved .skp ('' for a model that was never saved), as Sketchup::Model#path.
    def path
      @path
    end

    # Model#save_copy (SketchUp 2014+): writes the current state to another file without changing #path.
    def save_copy(target, _version = nil)
      File.write(target, "fake-skp entities=#{@entities.to_a.length}")
      @saved_copies << target
      true
    end

    def start_operation(name, _disable_ui = false, *_rest)
      @operations << [:start, name]
      true
    end

    def commit_operation
      @operations << [:commit]
      true
    end

    def abort_operation
      @operations << [:abort]
      true
    end
  end
end

def file_loaded?(_name)
  true
end

def file_loaded(_name); end
