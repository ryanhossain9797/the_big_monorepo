#![feature(async_closure)]
#![feature(never_type)]
#![feature(let_else)]
#![warn(clippy::all)]
#![warn(clippy::pedantic)]
#![allow(clippy::wildcard_imports)]

mod cli;
mod thing_state;
mod utils;

use async_std::{fs::*, io};
// use async_std::prelude::*;
// use async_std::task;

#[async_std::main]
async fn main() -> anyhow::Result<()> {
    let path = cli::get_file();
    let file = read_to_string(path).await?;
    let cleared = utils::remove_all_comments(&file)?;
    drop(file);

    let states = thing_state::find_all_states(&cleared)?;

    println!("loaded {} states", states.len());

    let stdin = io::stdin();

    let mut buffer = String::new();

    println!("Enter ThingState name\n or \"exit\" to close");

    loop {
        println!("ThingState => ");
        stdin.read_line(&mut buffer).await?;

        let cmd = buffer.trim_end();

        match cmd {
            "exit" => {
                break;
            }
            state_name => {
                if states.contains_key(state_name) {
                    println!("\n-------------------------\n");
                    states[state_name].print_indented(0);
                    println!("\n-------------------------\n");
                } else {
                    println!("state not found, try again");
                }
            }
        }

        buffer.clear();
    }

    Ok(())
}
