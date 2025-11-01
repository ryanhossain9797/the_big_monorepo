use std::sync::Arc;

use async_std::sync::RwLock;
use dashmap::DashMap;
use once_cell::sync::Lazy;

use crate::handlers::Env;

pub static ENV: Lazy<Env> = Lazy::new(Env::new);
