use crate::handlers::ReplyConfig;

//--------DISCORD CODE
use super::*;
use configuration::get_client_token;
use regex::Regex;
use serenity::{
    async_trait,
    http::Http as SerenityHttp,
    model::{
        channel::Message as DMessage,
        gateway::Ready,
        id::{ChannelId, UserId},
    },
    prelude::*,
};
use std::{sync::Arc, time::Duration};

///Main Starting point for the Discord api.
pub async fn main(
    sender: Sender<(Message, Box<dyn handlers::Conversation>, String)>,
) -> anyhow::Result<!> {
    // Configure the client with your Discord bot token in the environment.
    let token = get_client_token("discord_token")
        .ok_or_else(|| anyhow::anyhow!("Failed to load Discord Token"))?;

    let intents = GatewayIntents::DIRECT_MESSAGES;

    // Create a new instance of the Client, logging in as a bot. This will
    let mut client = Client::builder(token, intents)
        .event_handler(Handler { sender })
        .await?;

    // Finally, start a single shard, and start listening to events
    // Shards will automatically attempt to reconnect, and will perform
    // exponential backoff until it reconnects.
    client.start().await?;
    Err(anyhow::anyhow!("Discord failed"))
}

struct Handler {
    sender: Sender<(Message, Box<dyn handlers::Conversation>, String)>,
}

#[async_trait]
impl EventHandler for Handler {
    // Set a handler for the `message` event - so that whenever a new message
    // is received - the closure (or function) passed will be called.
    async fn message(&self, ctx: Context, message: DMessage) {
        let source = "DISCORD_CLIENT";
        util::logger::show_status(
            format!("DISCORD: <{}>: {}", message.author.name, message.content).as_str(),
        );

        if !message.author.bot {
            if let Some((msg, start_conversation)) = filter(&message, &ctx).await {
                if let Err(err) = self
                    .sender
                    .send_async((
                        Message {
                            user_name: message.author.name.clone(),
                            user_id: message.author.id.to_string(),
                            start_conversation,
                        },
                        Box::new(DiscordMessage {
                            user_id: message.author.id,
                            user_name: message.author.name,
                            chat_id: message.channel_id,
                            http: ctx.http,
                            start_conversation,
                        }),
                        msg,
                    ))
                    .await
                {
                    error!(source, "Error sending message: {}", err);
                }
            }
        }
    }

    // In this case, just print what the current user's username is.
    async fn ready(&self, _: Context, _: Ready) {
        util::logger::show_status("Discord is connected!\n");
    }
}

///Filter basically does some spring cleaning.
/// - checks whether the update is actually a message or some other type.
/// - trims leading and trailing spaces ("   /hellow    @machinelifeformbot   world  " becomes "/hellow    @machinelifeformbot   world").
/// - removes / from start if it's there ("/hellow    @machinelifeformbot   world" becomes "hellow    @machinelifeformbot   world").
/// - removes mentions of the bot from the message ("hellow    @machinelifeformbot   world" becomes "hellow      world").
/// - replaces redundant spaces with single spaces using regex ("hellow      world" becomes "hellow world").
async fn filter(message: &DMessage, ctx: &Context) -> Option<(String, bool)> {
    let source = "DISCORD";

    let Ok(info) = ctx.http.get_current_application_info().await else {
        error!(source, "Problem occurred while fetching self ID");
        return None;
    };

    let id: i64 = info.id.into();
    //-----------------------remove self mention from message
    let handle = format!("<@{}>", &id);

    let msg = message
        .content
        .replace(handle.as_str(), "")
        .trim()
        .trim_start_matches('/')
        .trim()
        .to_lowercase();

    let space_trimmer = Regex::new(r"\s+").unwrap();

    let msg: String = space_trimmer.replace_all(&msg, " ").into();
    //-----------------------check if message is from a group chat.......
    Some((
        msg,
        message.is_private() || message.content.contains(handle.as_str()),
    ))
}

#[derive(Clone)]
struct DiscordMessage {
    user_id: UserId,
    user_name: String,
    chat_id: ChannelId,
    http: Arc<SerenityHttp>,
    start_conversation: bool,
}

#[async_trait]
impl handlers::Conversation for DiscordMessage {
    fn get_name(&self) -> &str {
        self.user_name.as_str()
    }
    fn get_id(&self) -> String {
        let id: i64 = self.user_id.into();
        format!("{}", id)
    }
    async fn send_message(&self, message: ReplyConfig) -> anyhow::Result<()> {
        send_message(self.http.as_ref(), self.chat_id, message).await
    }
    fn start_conversation(&self) -> bool {
        self.start_conversation
    }
    fn dyn_clone(&self) -> Box<dyn handlers::Conversation> {
        Box::new(self.clone())
    }
}

async fn send_message(
    http: &SerenityHttp,
    chat_id: ChannelId,
    message: ReplyConfig,
) -> anyhow::Result<()> {
    match message {
        ReplyConfig::SingleMsg(msg) => match msg {
            handlers::Msg::Text(text) => {
                chat_id.say(http, text).await?;
            }
            handlers::Msg::File(url) => {
                chat_id.say(http, url).await?;
            }
        },
        ReplyConfig::MultiMsg(msg_list) => {
            for msg in msg_list {
                //---Need send here because spawn would send messages out of order
                match msg {
                    handlers::Msg::Text(text) => {
                        chat_id.say(http, text).await?;
                    }
                    handlers::Msg::File(url) => {
                        chat_id.say(http, url).await?;
                    }
                }
                std::thread::sleep(Duration::from_millis(500));
            }
        } // _ => {}
    }
    Ok(())
}
//--------UTILS
