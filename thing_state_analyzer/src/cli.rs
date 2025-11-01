use clap::Parser;
use std::path::PathBuf;

#[derive(Parser)]
struct File {
    ///ThingState file to load
    #[clap(parse(from_os_str))]
    path: std::path::PathBuf,
}

pub fn get_file() -> PathBuf {
    let initial_input = File::parse();
    initial_input.path
}
