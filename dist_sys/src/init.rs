use std::collections::HashSet;

use tokio::io::Stdout;

use crate::{
    types::{
        base::BaseData,
        init::{InitBody, InitResponseBody},
    },
    utils::print_json_to_stdout,
};

pub async fn run_init(
    writer: &mut Stdout,
    line: &str,
) -> anyhow::Result<(String, HashSet<String>)> {
    let init_data: BaseData<InitBody> = serde_json::from_str(&line)?;

    let init_response = BaseData {
        src: init_data.body.node_id.clone(),
        dest: init_data.src,
        body: InitResponseBody {
            r#type: "init_ok".to_string(),
            in_reply_to: init_data.body.msg_id,
        },
    };

    print_json_to_stdout(writer, &init_response).await?;

    Ok((init_data.body.node_id, init_data.body.node_ids))
}
