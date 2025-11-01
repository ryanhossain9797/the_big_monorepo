use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct GenerateBody {
    pub r#type: String,
    pub msg_id: usize,
    pub in_reply_to: Option<usize>,
    pub id: Option<String>,
}
