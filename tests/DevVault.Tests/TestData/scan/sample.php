<?php
class OrderController {
    // Sipariş özeti döner
    public function show($id) {
        $order = findOrder($id);
        return json_encode($order);
    }
}
