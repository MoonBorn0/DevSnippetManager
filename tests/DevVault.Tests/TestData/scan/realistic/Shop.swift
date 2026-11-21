import Foundation

/// A cart.
class Cart {
    var items: [String] = []

    init(items: [String]) {
        self.items = items
        print("init")
    }

    /// Adds one.
    func add(_ item: String) -> Int {
        items.append(item)
        let msg = "added \(item) {x}"
        print(msg)
        return items.count
    }

    func total(prices: [String: Int]) -> Int {
        var sum = 0
        for i in items {
            sum += prices[i] ?? 0
        }
        return sum
    }

    private static func helper() -> String {
        return """
        multi { line }
        """
    }

    var count: Int {
        return items.count
    }
}

struct Point {
    var x: Int
    var y: Int

    func dist() -> Double {
        let d = Double(x * x + y * y)
        return d.squareRoot()
    }
}

enum Direction {
    case north
    case south
}

protocol Shape {
    func area() -> Double
}

extension Cart {
    func clear() {
        items.removeAll()
        print("cleared")
    }
}

func free(_ a: Int, b: Int) -> Int {
    let s = a + b
    return s * 2
}

func generic<T: Comparable>(_ a: T, _ b: T) -> T where T: Hashable {
    if a > b {
        return a
    }
    return b
}
