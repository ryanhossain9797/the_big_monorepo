use tokio::io::{AsyncBufReadExt, AsyncWriteExt, BufReader, BufWriter, ReadHalf, WriteHalf};
use tokio::net::TcpStream;

pub async fn read_loop(mut reader: BufReader<ReadHalf<TcpStream>>) {
    let mut input = String::new();
    loop {
        input.clear();
        match reader.read_line(&mut input).await {
            Ok(0) => break, // Connection closed
            Ok(_) => println!("Received: {}", input.trim()),
            Err(e) => {
                eprintln!("Failed to read from server: {}", e);
                break;
            }
        }
    }
}

pub async fn write_loop(mut writer: BufWriter<WriteHalf<TcpStream>>) {
    let mut stdin = BufReader::new(tokio::io::stdin());
    let mut input = String::new();
    loop {
        input.clear();
        if stdin.read_line(&mut input).await.is_ok() {
            match writer.write_all(input.as_bytes()).await {
                Ok(()) => match writer.flush().await {
                    Ok(()) => (),
                    Err(e) => {
                        eprintln!("Failed to flush: {}", e);
                    }
                },
                Err(e) => {
                    eprintln!("Failed to write: {}", e);
                    break;
                }
            }
        }
    }
}
