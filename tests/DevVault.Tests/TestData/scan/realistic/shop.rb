require 'json'

# A shopping cart.
module Shop
  class Cart
    attr_reader :items

    # Creates a cart.
    def initialize(items = [])
      @items = items
      @count = items.size
    end

    # Adds an item unless it exists.
    def add(item)
      return if @items.include?(item)
      @items << item
      puts "added #{item} to #{@items.size}"
      self
    end

    def total(prices)
      @items.sum do |i|
        prices.fetch(i, 0)
      end
    end

    def description
      <<~TEXT
        This cart doesn't have items
        and it's fine { }
      TEXT
    end

    def self.build(n)
      cart = new
      n.times { |i| cart.add(i) }
      cart
    end

    class << self
      def default
        new(["x"])
        nil
      end
    end

    private def secret
      42
      43
    end

    def odd?(x)
      if x % 2 == 1
        true
      else
        false
      end
    end

    def endless(x) = x * 2
  end

  def self.helper(a)
    case a
    when 1 then :one
    when 2 then :two
    else :many
    end
  end
end

def top_level(x)
  x = x + 1 if x > 3
  while x < 10 do
    x += 1
  end
  x
end

def last_one
  [1, 2].each do |i|
    puts i
  end
  :done
end
