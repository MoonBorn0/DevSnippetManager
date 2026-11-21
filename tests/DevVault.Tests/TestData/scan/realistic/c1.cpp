class A {
public:
  double area() const override {
    return 1.0;
  }
  int f() const noexcept {
    return 2;
  }
  void g() override {
    int x = 1;
    x++;
  }
  void h() final {
    int y = 1;
    y++;
  }
};
