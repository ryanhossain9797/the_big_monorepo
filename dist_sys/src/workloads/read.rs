use tokio::io::Stdout;

use crate::{
    types::{base::BaseData, read::ReadBody},
    utils::print_json_to_stdout,
    Environment,
};

pub async fn run_read(
    writer: &mut Stdout,
    node_id: &str,
    env: &Environment,
    line: &str,
) -> anyhow::Result<()> {
    let msg_id = env.msg_id;
    let generate_data: BaseData<ReadBody> = serde_json::from_str(&line)?;

    let read_response = BaseData {
        src: node_id.to_string(),
        dest: generate_data.src,
        body: ReadBody {
            r#type: "read_ok".to_string(),
            msg_id,
            in_reply_to: Some(generate_data.body.msg_id),
            messages: env.received_messages.keys().cloned().collect(),
        },
    };

    print_json_to_stdout(writer, read_response).await?;
    Ok(())
}
