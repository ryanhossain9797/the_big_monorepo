use super::*;
use once_cell::sync::Lazy;
use serde::{Deserialize, Serialize};
use std::fs::read_to_string;

#[derive(Serialize, Deserialize)]
pub struct Device {
    pub name: String,
    device_type: u32,
    pub ip: String,
    pub port: u16,
    users: Vec<String>,
}

#[derive(Serialize, Deserialize)]
struct Devices {
    devices: Option<Vec<Device>>,
}

///RESPONSES: Response json holding all the responses.  
///Put in a json so they can be modified without recompiling the bot.  
///Loaded at startup, Restart Bot to reload.
static DEVICES: Lazy<Option<Devices>> = Lazy::new(|| {
    util::logger::show_status("\nLoading JSON responses");
    let devices =
        serde_json::from_str((read_to_string("data/home_devices.json").ok()?).as_str()).ok()?;

    Some(devices)
});

pub fn get_device(name: &str, user_id: &str) -> anyhow::Result<&'static Device> {
    DEVICES
        .as_ref()
        .ok_or_else(|| anyhow::anyhow!("No device data found"))?
        .devices
        .as_ref()
        .ok_or_else(|| anyhow::anyhow!("No device list found"))?
        .iter()
        .find(|device| device.name == name && device.users.contains(&user_id.to_string()))
        .ok_or_else(|| anyhow::anyhow!("No device with that name and user"))
}
