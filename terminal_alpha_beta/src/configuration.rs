use std::{convert::AsRef, fs::read_to_string};

use super::*;
use once_cell::sync::Lazy;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;

#[derive(Serialize, Deserialize)]
struct Configuration {
    telegram_token: Option<String>,
    discord_token: Option<String>,
    admin_ids: Option<Vec<String>>,
    mongodb_auth: Option<String>,
    client_tokens: Option<HashMap<String, String>>,
}

static CONFIGURATION: Lazy<Option<Configuration>> = Lazy::new(|| {
    util::logger::show_status("\nLoading configuration");
    let configuration: Configuration =
        serde_json::from_str((read_to_string("configuration.json").ok()?).as_str()).ok()?;

    Some(configuration)
});

pub fn get_client_token(client_key: &str) -> Option<&'static str> {
    CONFIGURATION
        .as_ref()?
        .client_tokens
        .as_ref()?
        .get(client_key)
        .map(AsRef::as_ref)
}

pub fn get_mongo_auth() -> Option<&'static str> {
    CONFIGURATION.as_ref()?.mongodb_auth.as_deref()
}

pub fn get_admin_ids() -> Option<&'static Vec<String>> {
    CONFIGURATION.as_ref()?.admin_ids.as_ref()
}
