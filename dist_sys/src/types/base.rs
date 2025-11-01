use serde::{Deserialize, Serialize};

#[derive(Deserialize, Debug, Clone)]
pub struct BaseBody {
    pub r#type: String,
}

#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct BaseData<Body> {
    pub src: String,
    pub dest: String,
    pub body: Body,
}
