use crate::handlers::{Message, ReplyConfig};

//--------TELEGRAM CODE
use super::*;
use anyhow::Context;
use async_std::task;
use async_trait::async_trait;
use configuration::get_client_token;
use frankenstein::{
    AllowedUpdate, AsyncApi, AsyncTelegramApi, ChatType, GetUpdatesParams, Message as TMessage,
    SendMessageParams, UpdateContent,
};
use once_cell::sync::OnceCell;
use regex::Regex;
use std::time::Duration;
//--- Waiting time for failed connections
const WAITTIME: u64 = 10;

pub static API: OnceCell<AsyncApi> = OnceCell::new();

///Main Starting point for the telegram api.
pub async fn main(
    sender: Sender<(Message, Box<dyn handlers::Conversation>, String)>,
) -> anyhow::Result<!> {
    let source = "TELEGRAM_CLIENT";

    let token = get_client_token("telegram_token")
        .ok_or_else(|| anyhow::anyhow!("Failed to load Telegram Token"))?;

    //FRANKENSTEIN
    let api = AsyncApi::new(token);
    let Ok(_) = api.get_me().await else {
        return Err(anyhow::anyhow!("Telegram failed"));
    };

    util::logger::show_status("Telegram is connected!\n");

    #[allow(clippy::map_err_ignore)]
    API.set(api)
        .map_err(|_| anyhow::anyhow!("Telegram API already initialized"))?;

    let update_params_builder =
        GetUpdatesParams::builder().allowed_updates(vec![AllowedUpdate::Message]);

    let mut update_params = update_params_builder.clone().build();

    loop {
        match API.get().context(source)?.get_updates(&update_params).await {
            Ok(response) => {
                for update in response.result {
                    if let UpdateContent::Message(message) = update.content {
                        if let (Some(author), Some(text)) = (&message.from, &message.text) {
                            // Print received text message to stdout.
                            util::logger::show_status(
                                format!("TELEGRAM: <{}>: {}", author.first_name, text,).as_str(),
                            );

                            if let Some((msg, start_conversation)) = filter(&message).await {
                                if let Err(err) = sender
                                    .send_async((
                                        Message {
                                            user_name: author.first_name.clone(),
                                            user_id: author.id.to_string(),
                                            start_conversation,
                                        },
                                        Box::new(TelegramMessage {
                                            user_id: author.id,
                                            user_name: author.first_name.clone(),
                                            chat_id: message.chat.id,
                                            start_conversation,
                                        }),
                                        msg,
                                    ))
                                    .await
                                {
                                    error!(source, "Error sending message: {}", err);
                                }
                            }

                            update_params = update_params_builder
                                .clone()
                                .offset(update.update_id + 1)
                                .build();
                        }
                    }
                }
            }
            Err(err) => {
                error!(
                    source,
                    "Hit problems fetching updates, stopping for {} seconds. error is {}",
                    WAITTIME,
                    err
                );
                task::sleep(Duration::from_secs(WAITTIME)).await;
                error!(source, "Resuming");
            }
        }
    }
}

///Filter basically does some spring cleaning.
/// - checks whether the update is actually a message or some other type.
/// - trims leading and trailing spaces ("   /hellow    @machinelifeformbot   world  " becomes "/hellow    @machinelifeformbot   world").
/// - removes / from start if it's there ("/hellow    @machinelifeformbot   world" becomes "hellow    @machinelifeformbot   world").
/// - removes mentions of the bot from the message ("hellow    @machinelifeformbot   world" becomes "hellow      world").
/// - replaces redundant spaces with single spaces using regex ("hellow      world" becomes "hellow world").
async fn filter(message: &TMessage) -> Option<(String, bool)> {
    if let (Some(_), Some(data)) = (&message.from, &message.text) {
        let myname_result = API.get()?.get_me().await;
        if let Ok(myname) = myname_result {
            if let Some(name) = myname.result.username {
                //-----------------------remove self mention from message
                let handle = format!("@{}", name.as_str());
                let mut msg: &str = &data.replace(handle.as_str(), "");
                msg = msg.trim().trim_start_matches('/').trim();
                let msg: &str = &msg.to_lowercase();
                let space_trimmer = Regex::new(r"\s+").unwrap();

                let msg: String = space_trimmer.replace_all(msg, " ").into();
                //-----------------------check if message is from a group chat.......

                return Some((
                    msg,
                    matches!(&message.chat.type_field, ChatType::Private)
                        || data.contains(handle.as_str()),
                ));
            }
        }
    }
    None
}

#[derive(Clone)]
struct TelegramMessage {
    user_name: String,
    user_id: u64,
    chat_id: i64,
    start_conversation: bool,
}

#[async_trait]
impl handlers::Conversation for TelegramMessage {
    fn get_name(&self) -> &str {
        self.user_name.as_str()
    }
    fn get_id(&self) -> String {
        format!("{}", self.user_id)
    }
    async fn send_message(&self, message: ReplyConfig) -> anyhow::Result<()> {
        send_message(self.chat_id, message).await
    }
    fn start_conversation(&self) -> bool {
        self.start_conversation
    }
    fn dyn_clone(&self) -> Box<dyn handlers::Conversation> {
        Box::new(self.clone())
    }
}

//--------UTILS
async fn send_message(chat_id: i64, message: ReplyConfig) -> anyhow::Result<()> {
    if let Some(api) = API.get() {
        match message {
            ReplyConfig::SingleMsg(msg) => match msg {
                handlers::Msg::Text(text) => {
                    let send_message_params = SendMessageParams::builder()
                        .chat_id(chat_id)
                        .text(text)
                        .build();

                    if let Err(err) = api.send_message(&send_message_params).await {
                        println!("Failed to send message: {:?}", err);
                    }
                }
                handlers::Msg::File(url) => {
                    let send_message_params = SendMessageParams::builder()
                        .chat_id(chat_id)
                        .text(url)
                        .build();

                    if let Err(err) = api.send_message(&send_message_params).await {
                        println!("Failed to send message: {:?}", err);
                    }
                }
            },
            ReplyConfig::MultiMsg(msg_list) => {
                for msg in msg_list {
                    //---Need send here because spawn would send messages out of order
                    match msg {
                        handlers::Msg::Text(text) => {
                            let send_message_params = SendMessageParams::builder()
                                .chat_id(chat_id)
                                .text(text)
                                .build();

                            if let Err(err) = api.send_message(&send_message_params).await {
                                println!("Failed to send message: {:?}", err);
                            }
                        }
                        handlers::Msg::File(url) => {
                            let send_message_params = SendMessageParams::builder()
                                .chat_id(chat_id)
                                .text(url)
                                .build();

                            if let Err(err) = api.send_message(&send_message_params).await {
                                println!("Failed to send message: {:?}", err);
                            }
                        }
                    }
                }
            }
        }
    }
    Ok(())
}
