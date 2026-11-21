package server

import (
	"fmt"
	"net/http"
	"sync"
)

// Server is a tiny HTTP server wrapper.
type Server struct {
	mu    sync.Mutex
	addr  string
	stop  chan struct{}
	hooks map[string]func(w http.ResponseWriter)
}

type Handler interface {
	Serve(w http.ResponseWriter, r *http.Request)
	Name() string
}

// NewServer creates a server.
func NewServer(addr string) *Server {
	s := &Server{addr: addr}
	s.stop = make(chan struct{})
	return s
}

// Start begins listening and returns an error on failure.
func (s *Server) Start() error {
	s.mu.Lock()
	defer s.mu.Unlock()
	msg := "listening on {" + s.addr + "}"
	fmt.Println(msg)
	return http.ListenAndServe(s.addr, nil)
}

func (s *Server) Done() <-chan struct{} {
	s.mu.Lock()
	defer s.mu.Unlock()
	return s.stop
}

func Describe(v interface{}) string {
	switch t := v.(type) {
	case int:
		return fmt.Sprintf("int %d", t)
	case string:
		return "string " + t
	}
	return "unknown"
}

type Stack[T any] struct {
	items []T
}

func (st *Stack[T]) Push(v T) {
	st.items = append(st.items, v)
	fmt.Println("pushed", v)
	fmt.Println(len(st.items))
}

func Map[T, U any](in []T, f func(T) U) []U {
	out := make([]U, 0, len(in))
	for _, v := range in {
		out = append(out, f(v))
	}
	return out
}

func (s Server) String() string {
	raw := `raw string with } and {`
	return fmt.Sprintf("%s %s", s.addr, raw)
}

func RuneTest() rune {
	r := '{'
	q := '}'
	_ = q
	return r
}
