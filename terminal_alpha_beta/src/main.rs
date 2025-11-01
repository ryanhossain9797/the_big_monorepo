#![feature(async_closure)]
#![feature(never_type)]
#![feature(async_fn_in_trait)]
#![feature(try_blocks)]
#![warn(clippy::all)]
#![warn(clippy::pedantic)]
#![allow(clippy::wildcard_imports)]
#![allow(clippy::match_bool)]
#![allow(clippy::single_match)]
#![allow(clippy::single_match_else)]
#![allow(clippy::too_many_lines)]

mod clients;
mod configuration;
mod database;
mod handlers;
mod repositories;
mod second_gen;
mod services;
mod util;

use async_std::task;
use clients::*;
use colored::*;
use futures::future;
use repositories::*;
use services::*;
use tap::prelude::*;

#[async_std::main]
async fn main() {
    let source = "MAIN";

    let status = util::logger::status();
    {
        status("Starting up Terminal Alpha Beta\n");
        status("-----Starting TELEGRAM and DISCORD-----\n"); //---Prints the Date of compilation, added at compile time

        if let Some(date) = option_env!("COMPILED_AT") {
            status(&format!("Compile date {date}\n"));
        }

        status("Initializing everything");

        handlers::initialize();
        drop(database::initialize().await);

        status("\nInitialized Everything\n");
    }
    let (sender, receiver) = handlers::init_sender();
    /*
    Wait for tasks to finish,
    Which is hopefully never, because that would mean it crashed.
    */
    let clients_result = future::try_join_all(vec![
        task::spawn(services(receiver)),
        task::spawn(clients(sender)),
    ])
    .await;

    if let Err(err) = clients_result {
        error!(source, "One or more services have failed {}", err);
    }
}

async fn services(receiver: handlers::MsgReceiver) -> anyhow::Result<!> {
    future::try_join_all(vec![task::spawn(handlers::receiver(receiver))]).await?;

    Err(anyhow::anyhow!("Services failed"))
}

async fn clients(sender: handlers::MsgSender) -> anyhow::Result<!> {
    future::try_join_all(
        sender
            .pipe(|s| (s.clone(), s))
            .pipe(|(ts, ds)| vec![task::spawn(run_telegram(ts)), task::spawn(run_discord(ds))]),
    )
    .await?;

    Err(anyhow::anyhow!("Clients failed"))
}
