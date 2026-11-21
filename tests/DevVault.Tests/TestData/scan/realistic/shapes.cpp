#include <iostream>
#include <vector>
#include <string>

namespace geo {

template <typename T>
class Stack {
public:
    void push(const T& v) {
        items_.push_back(v);
        count_++;
        std::cout << "push" << std::endl;
    }

    T pop();

private:
    std::vector<T> items_;
    int count_ = 0;
};

template <typename T>
T Stack<T>::pop() {
    T v = items_.back();
    items_.pop_back();
    count_--;
    return v;
}

class Shape {
public:
    Shape(std::string name) : name_(name), area_(0.0) {
        std::cout << "Shape " << name_ << std::endl;
        area_ = compute();
    }
    virtual ~Shape() {
        std::cout << "bye" << std::endl;
        name_.clear();
    }
    virtual double area() const = 0;
    double compute() const noexcept {
        auto f = [](int x) { return x * 2; };
        return f(2);
    }
    std::string raw() const {
        return R"(raw { string })";
    }
protected:
    std::string name_;
    double area_;
};

class Circle : public Shape {
public:
    double area() const override {
        return 3.14 * r_ * r_;
    }
private:
    double r_ = 1;
};

struct Pair {
    int a;
    int b;
};

} // namespace geo

int geo_helper(int x)
{
    if (x > 0) {
        return x;
    }
    return -x;
}

void Free::method(int a) {
    std::cout << a << "}" << std::endl;
    std::cout << "x" << std::endl;
}

int main() {
    geo::Pair p{1, 2};
    std::cout << p.a << std::endl;
    return 0;
}
