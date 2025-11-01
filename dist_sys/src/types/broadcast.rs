use serde::{Deserialize, Serialize};

use super::base::{BaseBody, BaseData};

pub enum BroadcastQueueAction {
    SendBroadCast(BaseData<BroadcastBody>),
    Ack(usize),
    ResendBroadcast
}

#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct BroadcastBody {
    pub r#type: String,
    pub msg_id: usize,
    pub in_reply_to: Option<usize>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub message: Option<usize>,
}
