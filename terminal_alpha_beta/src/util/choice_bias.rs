use std::fmt::Display;

use rand::Rng;

use super::*;

#[derive(Copy, Clone)]
pub struct ChoiceBias {
    bias: f32,
}

impl ChoiceBias {
    pub fn new() -> Self {
        Self {
            bias: rand::thread_rng().gen(),
        }
    }

    pub fn choose<'a, T>(&self, from: &'a Vec<T>) -> Option<&'a T> {
        from.get((self.bias * (from.len() as f32)).floor() as usize)
    }
}

impl Display for ChoiceBias {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.bias)
    }
}
