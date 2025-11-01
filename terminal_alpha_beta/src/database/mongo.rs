use super::*;
use configuration::get_mongo_auth;
use mongodb::{options::ClientOptions, Client, Database};
use once_cell::sync::OnceCell;

static MONGO: OnceCell<Database> = OnceCell::new();

pub async fn initialize() -> anyhow::Result<()> {
    if MONGO.get().is_some() {
        return Ok(());
    }

    // no one else has initialized it yet, so
    MONGO
        .set(
            Client::with_options(
                ClientOptions::parse(
                    get_mongo_auth()
                        .ok_or_else(|| anyhow::anyhow!("Couldn't retrieve MongoDB Auth"))?,
                )
                .await?,
            )?
            .database("terminal"),
        )
        .map(|_| util::logger::show_status("\nMongoDB Initialized"))
        .map_err(|db| anyhow::anyhow!(format!("Already initialized {}", db.name())))
}

pub fn get() -> Option<&'static Database> {
    let source = "MONGO_GET";

    MONGO.get().map(|db| {
        info!(source, "DB already initialized");
        db
    })
}
