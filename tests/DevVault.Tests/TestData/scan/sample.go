package server

import "fmt"

// Sunucuyu başlatır
func (s *Server) Start(port int) error {
    fmt.Printf("listening on %d\n", port)
    return nil
}

func HealthCheck() string {
    status := "ok"
    return status
}
