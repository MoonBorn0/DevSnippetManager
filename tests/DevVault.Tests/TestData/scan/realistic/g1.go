package x

func Foo(
	a int,
	b interface{},
) error {
	fmt.Println(a)
	fmt.Println(b)
	return nil
}

func Bar(m map[string]struct{}) int {
	x := len(m)
	y := x + 1
	return y
}

func Baz() {
	done := make(chan struct{})
	close(done)
	fmt.Println("x")
}
