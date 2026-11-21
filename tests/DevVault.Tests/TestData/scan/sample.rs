pub struct Counter {
    value: i32,
}

impl Counter {
    /// Sayacı artırır
    pub fn increment(&mut self) -> i32 {
        self.value += 1;
        self.value
    }
}

pub fn reset_value(v: i32) -> i32 {
    let next = v * 0;
    next
}
