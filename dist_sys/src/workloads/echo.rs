use tokio::io::Stdout;

use crate::{
    types::{base::BaseData, echo::EchoBody},
    utils::print_json_to_stdout,
    Environment,
};

pub async fn run_echo(
    writer: &mut Stdout,
    node_id: &str,
    env: &Environment,
    line: &str,
) -> anyhow::Result<()> {
    let msg_id = env.msg_id;
    let echo_data: BaseData<EchoBody> = serde_json::from_str(line)?;

    let echo_response: BaseData<EchoBody> = BaseData {
        src: node_id.to_string(),
        dest: echo_data.src,
        body: EchoBody {
            r#type: "echo_ok".to_string(),
            msg_id,
            in_reply_to: Some(echo_data.body.msg_id),
            echo: echo_data.body.echo,
        },
    };

    print_json_to_stdout(writer, echo_response).await?;
    Ok(())
}
