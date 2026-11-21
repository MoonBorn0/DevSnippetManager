package com.example.shop;

import java.util.*;

/**
 * Represents an order.
 */
public class Order {
    private final List<String> items = new ArrayList<>();
    private static final Map<String, Integer> PRICES = new HashMap<>() {{
        put("a", 1);
        put("b", 2);
    }};

    public Order() {
        items.clear();
        System.out.println("created");
    }

    /**
     * Adds an item.
     */
    @Override
    public void add(String item) throws IllegalArgumentException {
        if (item == null) {
            throw new IllegalArgumentException("item } null {");
        }
        items.add(item);
    }

    public void add(String item, int qty) {
        for (int i = 0; i < qty; i++) {
            add(item);
        }
    }

    // total price
    public int total() {
        int sum = 0;
        for (String s : items) {
            sum += PRICES.getOrDefault(s, 0);
        }
        char c = '}';
        return sum;
    }

    public static <T extends Comparable<T>> T max(List<T> list) {
        T best = list.get(0);
        for (T t : list) {
            if (t.compareTo(best) > 0) best = t;
        }
        return best;
    }

    public String report() {
        String text = """
            Order {
              items: }
            }
            """;
        return text;
    }

    public enum Status { NEW, PAID }

    public static class Builder {
        private String name;

        public Builder name(String n) {
            this.name = n;
            return this;
        }

        public Order build() {
            Order o = new Order();
            o.items.add(name);
            return o;
        }
    }

    interface Visitor {
        void visit(Order o);
    }
}
