pub mod discord;
mod shared_utils;
pub mod telegram;
use crate::handlers::Message;

use super::*;
use flume::Sender;

///Just an entry point to start the telegram api.
pub async fn run_telegram(
    sender: Sender<(Message, Box<dyn handlers::Conversation>, String)>,
) -> anyhow::Result<!> {
    telegram::main(sender).await
}

///Just an entry point to start the discord api.
pub async fn run_discord(
    sender: Sender<(Message, Box<dyn handlers::Conversation>, String)>,
) -> anyhow::Result<!> {
    discord::main(sender).await
}
