mod read_write_loop;

use clap::Parser;
use read_write_loop::{read_loop, write_loop};
use tokio::io::{self, AsyncBufReadExt, AsyncWriteExt, BufReader, BufWriter};
use tokio::net::TcpStream;
use tokio::{join, task};

#[derive(Parser, Debug)]
#[command(version, about, long_about = None)]
struct Opt {
    #[arg(short, long)]
    server: String,

    #[arg(short, long)]
    port: u16,

    #[arg(short, long)]
    uname: String,

    #[arg(short, long)]
    name: String,

    #[arg(short, long)]
    channels: String,
}

#[tokio::main]
async fn main() -> io::Result<()> {
    let opt = Opt::parse();
    let address = format!("{}:{}", opt.server, opt.port);

    let stream = TcpStream::connect(address).await?;
    let (reader, writer) = io::split(stream);
    let mut reader = BufReader::new(reader);
    let mut writer = BufWriter::new(writer);

    // Write initial message to the stream
    writer
        .write_all(format!("{},{},{}\n", opt.uname, opt.name, opt.channels).as_bytes())
        .await?;

    writer.flush().await?;

    // Read from the stream
    let mut response = String::new();
    reader.read_line(&mut response).await?;
    println!("{}", response);

    let read_task = task::spawn(read_loop(reader));

    let write_task = task::spawn(write_loop(writer));

    let _ = join!(read_task, write_task);

    Ok(())
}
