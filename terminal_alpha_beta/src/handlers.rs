pub mod intent;
pub mod responses;

use super::*;
use anyhow::Context;
use async_std::task;
use async_trait::async_trait;
use flume::{Receiver, Sender};
use futures::{Stream, StreamExt};
use once_cell::sync::Lazy;
use rand::seq::SliceRandom;
use rand::Rng;
use responses::*;
use second_gen::{user_life_cycle::*, *};
use std::convert::Into;
use std::fmt::Display;
use std::pin::Pin;
use std::{fs::*, sync::Arc, time::Duration};
use util::*;

const NAMES: [&str; 2] = ["Terminal Alpha", "Terminal Beta"];

pub type MsgSender = Sender<(Message, Box<dyn Conversation>, String)>;
pub type MsgReceiver = Receiver<(Message, Box<dyn Conversation>, String)>;

const LONG_WAIT: u64 = 30;
#[allow(dead_code)]
const SHORT_WAIT: u64 = 10;
const WAIT_TIME: u64 = LONG_WAIT;

///HTTP client for..... HTTP things
static CLIENT: Lazy<reqwest::Client> = Lazy::new(|| {
    util::logger::show_status("\nLoading Api Client");
    reqwest::Client::new()
});

// pub async fn state_expiry_service() -> anyhow::Result<!> {
//     state::expiry::service().await
// }

///Initializes a variety of things
///- State management system
///- NLU engine
///- Responses JSON
pub fn initialize() {
    user_life_cycle::initialize();
    Lazy::force(&CLIENT);
    responses::initialize();
}

///ENUM, Represents Message count
///- `SingleMsg` - Contains a Msg Enum
///- `MultiMsg` - Contains a Vector of Msg Enums
///- `NoMsg` - Represnts an empty response
pub enum ReplyConfig {
    SingleMsg(Msg),
    MultiMsg(Vec<Msg>),
    // NoMsg,
}

//When passed an String
//Uses the value as a ReplyConfig::SingleMsg(Msg::Text)
impl From<String> for ReplyConfig {
    fn from(s: String) -> Self {
        ReplyConfig::SingleMsg(Msg::Text(s))
    }
}

//When passed an Option<String>
//Uses the Some value as a ReplyConfig::SingleMsg(Msg::Text)
//Uses the 'response unavailable...' message in case of None as ReplyConfig::SingleMsg(Msg::Text)
impl From<Option<String>> for ReplyConfig {
    fn from(s: Option<String>) -> Self {
        match s {
            Some(msg) => ReplyConfig::SingleMsg(Msg::Text(msg)),
            None => ReplyConfig::SingleMsg(Msg::Text(
                "ForgiVE uS... We SEEM t0 B3... hAVInG i55UEs".to_string(),
            )),
        }
    }
}

//When passed an Vec<String>
//Turns into ReplyConfig::MultiMsg(Vec<Msg::Text()>)
impl From<Vec<String>> for ReplyConfig {
    fn from(s: Vec<String>) -> Self {
        ReplyConfig::MultiMsg(s.into_iter().map(Into::into).collect())
    }
}

//When passed an Vec<Msg>
//Turns into ReplyConfig::MultiMsg(Vec<Msg>)
impl From<Vec<Msg>> for ReplyConfig {
    fn from(s: Vec<Msg>) -> Self {
        ReplyConfig::MultiMsg(s)
    }
}

//ENUM, Represents Message type
//- Text - Contains String text
//- File - Contains String url for file
#[allow(dead_code)]
pub enum Msg {
    Text(String),
    File(String),
}

//When passed an Option<String>
//Uses the Some value as a Msg::Text
//Uses the 'response unavailable...' message in case of None as Msg::Text
impl From<Option<String>> for Msg {
    fn from(s: Option<String>) -> Self {
        match s {
            Some(msg) => Msg::Text(msg),
            None => Msg::Text("ForgiVE uS... We SEEM t0 B3... hAVInG i55UEs".to_string()),
        }
    }
}

//When passed an String
//Uses the value as a Msg::Text
impl From<String> for Msg {
    fn from(s: String) -> Self {
        Msg::Text(s)
    }
}

#[derive(Copy, Clone)]
pub struct Ctx {
    pub name: &'static str,
    pub env: &'static Env,
    pub choice_bias: choice_bias::ChoiceBias,
}

impl Ctx {
    pub fn new(env: &'static Env) -> Self {
        let source = "CTX";

        let ctx = Self {
            name: *NAMES.choose(&mut rand::thread_rng()).unwrap_or(&"No Name"),
            env,
            choice_bias: choice_bias::ChoiceBias::new(),
        };

        info!(source, "generated ctx {}, {}", ctx.name, ctx.choice_bias);
        ctx
    }
}

pub struct Env {
    responses: serde_json::Value,
}
impl Env {
    pub fn new() -> Self {
        let source = "ENV";

        let env = Self {
            responses: serde_json::from_str(
                (read_to_string("data/responses.json").unwrap()).as_str(),
            )
            .unwrap(),
        };
        env
    }
}

pub struct Message {
    pub user_name: String,
    pub user_id: String,
    pub start_conversation: bool,
}

///## Used to generalize Message Updates for various platforms
///All clients sending message updates must implement this
///## functions
///- `fn get_name() -> String` Return user readable name
///- `fn get_id() -> String` Return unique id for user
///- `async fn send_message(message: ReplyConfig)` Sends message to user
///- `fn start_conversation() -> bool` Returns bool indicating whether to start a new conversation
///- `fn dyn_clone() -> Box<dyn BotMessage>` Returns a `Box<dyn >` clone of self
#[async_trait]
pub trait Conversation: Send + Sync {
    ///Returns the user's user readable name. Not the same as id.
    fn get_name(&self) -> &str;

    ///Returns the user's unique id. This is needed to uniquely identify users.
    fn get_id(&self) -> String;

    ///Used to send messages to the sender (user) of this message.
    async fn send_message(&self, message: ReplyConfig) -> anyhow::Result<()>;

    ///Used to check whether a new conversation should be started.  
    ///Sometimes if the user is in a state, Bot will always respond.  
    ///However if not in a state, bot needs to know when it should or should not respond.  
    ///Ex. Won't respond if message is in a group and bot wasn't mentioned.
    fn start_conversation(&self) -> bool;

    ///Returns a `Box<dyn BotMessage>` clone of self
    fn dyn_clone(&self) -> Box<dyn Conversation>;
}

///Returns a sender and receiver channel of `Box<dyn BotMessage>`
pub fn init_sender() -> (MsgSender, MsgReceiver) {
    let (sender, receiver) = flume::bounded::<(Message, Box<dyn Conversation>, String)>(10);
    (sender, receiver)
}

///Distributes incoming requests to separate threads
pub async fn receiver(r: MsgReceiver) -> anyhow::Result<!> {
    let source = "DISTRIBUTOR";

    let env = Arc::new(Env::new());
    while let Ok((message, conversation, text)) = r.recv_async().await {
        let env = Arc::clone(&env);
        //Spawn a new task to handle the message
        //        drop(task::spawn(async move {
        //            handlers::handler(env, message, conversation, text).await;
        //        }));
        drop(task::spawn(async move {
            second_gen::user_life_cycle::handle_update(message, conversation, text).await;
        }));
        info!(source, "Handler Thread Spawned");
    }
    Err(anyhow::anyhow!("Message receiver failed"))
}
