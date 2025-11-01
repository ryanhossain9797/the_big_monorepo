use std::{
    collections::{HashMap, HashSet},
    thread::spawn,
    time::{Duration, SystemTime},
};

use tokio::{
    io::Stdout,
    sync::mpsc::{UnboundedReceiver, UnboundedSender},
    time::Instant,
};

use crate::{
    types::{
        base::BaseData,
        broadcast::{BroadcastBody, BroadcastQueueAction},
    },
    utils::print_json_to_stdout,
    Environment,
};

const RETRY_MILLIS: u64 = 500;

pub async fn outbound_broadcast_queue(
    mut receiver: UnboundedReceiver<BroadcastQueueAction>,
    sender: UnboundedSender<BroadcastQueueAction>,
) -> anyhow::Result<!> {
    let initial_sender = sender.clone();
    tokio::spawn(async move {
        tokio::time::sleep(Duration::from_secs(2)).await;
        initial_sender.send(BroadcastQueueAction::ResendBroadcast)
    });

    let mut awaiting_ack = HashMap::<usize, (SystemTime, BaseData<BroadcastBody>)>::new();

    let mut writer = tokio::io::stdout();
    while let Some(broadcast) = receiver.recv().await {
        match broadcast {
            BroadcastQueueAction::SendBroadCast(broadcast) => {
                awaiting_ack.insert(
                    broadcast.body.msg_id,
                    (SystemTime::now(), broadcast.clone()),
                );
                print_json_to_stdout(&mut writer, broadcast).await?;
                eprintln!("remaining ack: {:?}", awaiting_ack)
            }
            BroadcastQueueAction::Ack(msg_id) => {
                awaiting_ack.remove(&msg_id);
                eprintln!("remaining ack: {:?}", awaiting_ack)
            }
            BroadcastQueueAction::ResendBroadcast => {
                let now = SystemTime::now();

                eprintln!("Rechecking at: {:?}", now);

                let before = SystemTime::now()
                    .checked_sub(Duration::from_millis(RETRY_MILLIS))
                    .expect("2 seconds should be ok");

                let retries: Vec<BaseData<BroadcastBody>> = awaiting_ack
                    .iter()
                    .filter_map(|(id, (time, data))| match *time < before {
                        true => Some(data.clone()),
                        false => None,
                    })
                    .collect();

                eprintln!("Rechecking {} items", retries.len());

                for broadcast in retries {
                    print_json_to_stdout(&mut writer, broadcast).await?
                }

                let sender = sender.clone();

                tokio::spawn(async move {
                    tokio::time::sleep(Duration::from_millis(RETRY_MILLIS)).await;
                    sender.send(BroadcastQueueAction::ResendBroadcast)
                });
            }
        }
    }

    Err(anyhow::anyhow!("Unreachable"))
}

pub async fn run_broadcast(
    writer: &mut Stdout,
    node_id: &str,
    env: &mut Environment,
    line: &str,
) -> anyhow::Result<()> {
    let msg_id = env.msg_id;
    let generate_data: BaseData<BroadcastBody> = serde_json::from_str(&line)?;

    let message = generate_data
        .body
        .message
        .ok_or_else(|| anyhow::anyhow!("No Message"))?;

    let broadcast_response = BaseData {
        src: node_id.to_string(),
        dest: generate_data.src.clone(),
        body: BroadcastBody {
            r#type: "broadcast_ok".to_string(),
            msg_id,
            in_reply_to: Some(generate_data.body.msg_id),
            message: None,
        },
    };

    print_json_to_stdout(writer, broadcast_response).await?;

    let old_sent = env
        .received_messages
        .get(&message)
        .map(|s| s.clone())
        .unwrap_or_default();

    let mut sent = old_sent.clone();

    sent.insert(generate_data.src.clone());

    for neighbor in env
        .neighbors
        .iter()
        .filter(|n: &&String| !old_sent.contains(*n))
    {
        let broadcast = BaseData {
            src: node_id.to_string(),
            dest: neighbor.clone(),
            body: BroadcastBody {
                r#type: "broadcast".to_string(),
                msg_id,
                in_reply_to: None,
                message: Some(message),
            },
        };

        env.broadcast_sender
            .send(BroadcastQueueAction::SendBroadCast(broadcast))?;

        sent.insert(neighbor.clone());

        env.msg_id += 1;
    }

    env.received_messages.insert(message, sent);

    Ok(())
}

pub async fn run_broadcast_ack(
    node_id: &str,
    env: &mut Environment,
    line: &str,
) -> anyhow::Result<()> {
    let generate_data: BaseData<BroadcastBody> = serde_json::from_str(&line)?;

    match generate_data.dest == node_id {
        true => env.broadcast_sender.send(BroadcastQueueAction::Ack(
            generate_data
                .body
                .in_reply_to
                .expect("ok ack should be in reply to a message"),
        ))?,
        false => {
            eprintln!("Acknowledgememt for different node received");
            Err(anyhow::anyhow!(
                "Acknowledgememt for different node received"
            ))?
        }
    }

    Ok(())
}
