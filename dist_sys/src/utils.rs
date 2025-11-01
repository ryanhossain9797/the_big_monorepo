use serde::Serialize;
use tokio::io::{AsyncWriteExt, Stdout};

pub async fn print_json_to_stdout<T: Serialize>(
    writer: &mut Stdout,
    data: T,
) -> anyhow::Result<()> {
    let json = serde_json::to_string(&data)?;

    eprintln!("OUTPUT: {json}");

    writer.write_all(format!("{json}\n").as_bytes()).await?;
    Ok(())
}

pub fn read_json_from_string<T: for<'a> serde::Deserialize<'a>>(line: &str) -> anyhow::Result<T> {
    let data = serde_json::from_str::<T>(line)?;

    Ok(data)
}
