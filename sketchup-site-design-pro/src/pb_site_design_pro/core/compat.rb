# frozen_string_literal: true

# Compatibility with the Ruby of older SketchUp versions (SketchUp 2014-2016: Ruby 2.0, 2017-2018: Ruby 2.2,
# 2019-2020: Ruby 2.5, 2021-2023: Ruby 2.7). Core methods of newer Ruby versions that the extension uses are
# added only where the running Ruby does not have them, so nothing changes on a current SketchUp.

class Numeric
  def positive?
    self > 0 # rubocop:disable Style/NumericPredicate
  end unless method_defined?(:positive?)

  def negative?
    self < 0 # rubocop:disable Style/NumericPredicate
  end unless method_defined?(:negative?)
end

module Enumerable
  def sum(init = 0)
    each.inject(init) { |acc, x| acc + (block_given? ? yield(x) : x) }
  end unless method_defined?(:sum)

  def filter_map
    each_with_object([]) do |x, acc|
      v = yield(x)
      acc << v if v
    end
  end unless method_defined?(:filter_map)

  def filter(&block)
    select(&block)
  end unless method_defined?(:filter)

  def to_h(&block)
    pairs = block ? map(&block) : to_a
    pairs.each_with_object({}) { |(k, v), h| h[k] = v }
  end unless method_defined?(:to_h)
end

class Array
  def sum(init = 0)
    inject(init) { |acc, x| acc + (block_given? ? yield(x) : x) }
  end unless method_defined?(:sum)

  def filter(&block)
    select(&block)
  end unless method_defined?(:filter)

  # Ruby < 2.6: Array#to_h without a block only; Ruby 2.0: no Array#to_h at all
  if !method_defined?(:to_h) || (begin
    [[1, 2]].to_h { |a, b| [b, a] } != { 2 => 1 }
  rescue ArgumentError, TypeError
    true
  end)
    def to_h(&block)
      pairs = block ? map(&block) : self
      pairs.each_with_object({}) { |(k, v), h| h[k] = v }
    end
  end
end

class Hash
  def transform_values
    each_with_object({}) { |(k, v), h| h[k] = yield(v) }
  end unless method_defined?(:transform_values)

  def transform_keys
    each_with_object({}) { |(k, v), h| h[yield(k)] = v }
  end unless method_defined?(:transform_keys)

  def filter(&block)
    select(&block)
  end unless method_defined?(:filter)

  def compact
    reject { |_, v| v.nil? }
  end unless method_defined?(:compact)

  def sum(init = 0)
    inject(init) { |acc, x| acc + (block_given? ? yield(x) : x) }
  end unless method_defined?(:sum)

  # Ruby < 2.6: Hash#to_h without a block only
  if (begin
    { 1 => 2 }.to_h { |a, b| [b, a] } != { 2 => 1 }
  rescue ArgumentError, TypeError
    true
  end)
    alias_method :to_h_without_block, :to_h
    def to_h(&block)
      return to_h_without_block unless block

      map(&block).each_with_object({}) { |(k, v), h| h[k] = v }
    end
  end
end

class Hash
  def dig(key, *rest)
    v = self[key]
    rest.empty? || v.nil? ? v : v.dig(*rest)
  end unless method_defined?(:dig)
end

class Array
  def dig(index, *rest)
    v = self[index]
    rest.empty? || v.nil? ? v : v.dig(*rest)
  end unless method_defined?(:dig)
end

class String
  def match?(pattern, pos = 0)
    !match(pattern, pos).nil?
  end unless method_defined?(:match?)

  def unpack1(format)
    unpack(format)[0]
  end unless method_defined?(:unpack1)

  def casecmp?(other)
    casecmp(other).zero?
  end unless method_defined?(:casecmp?)
end

class Regexp
  def match?(str, pos = 0)
    !match(str, pos).nil?
  end unless method_defined?(:match?)
end

module ProjektniBiro
  module SiteDesignPro
    module Compat
      module_function

      # A Struct built with keyword arguments (Struct.new(..., keyword_init: true) needs Ruby 2.5): new(id: 1, ...)
      # fills the members by name, new(1, ...) by position.
      def kw_struct(*fields, &block)
        klass = Struct.new(*fields, &block)
        klass.class_eval do
          def initialize(*args)
            if args.length == 1 && args[0].is_a?(Hash) && members.length > 1
              h = args[0]
              super(*members.map { |m| h.key?(m) ? h[m] : h[m.to_s] })
            else
              super
            end
          end
        end
        klass
      end
    end
  end
end
