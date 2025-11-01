pub use colored::*;
use std::fs::OpenOptions;
use std::io::prelude::*;

#[macro_export]
macro_rules! info {
    ($a:expr, $b:expr $(, $c:expr)*)=> {{
        println!("{}: {}", $a.green(), format!($b $(, $c)*));
    }};
}

#[macro_export]
macro_rules! warning {
    ($a:expr, $b:expr $(, $c:expr)*)=> {{
        println!("{}: {}", $a.yellow(), format!($b $(, $c)*));
    }};
}

#[macro_export]
macro_rules! error {
    ($a:expr, $b:expr $(, $c:expr)*)=> {{
        println!("{}: {}", $a.red(), format!($b $(, $c)*));
    }};
}

///Returns a closure that logs the message with blue text
#[allow(dead_code)]
fn info(source: &str) -> impl Fn(&str) + '_ {
    move |msg: &str| println!("{}: {}", source.green(), msg.blue())
}
///Returns a closure that logs the message with yellow text
// pub fn warning(source: &str) -> impl Fn(&str) + '_ {
//     move |msg: &str| println!("{}: {}", source.green(), msg.yellow())
// }

///Returns a closure that logs the message with red text
fn error(source: &str) -> impl Fn(&str) + '_ {
    move |msg: &str| println!("{}: {}", source.green(), msg.red())
}
///Returns a closure that logs the message with white on purple text
pub fn status() -> impl Fn(&str) {
    show_status
}
///Logs the message with white on purple text
pub fn show_status(msg: &str) {
    println!("{}", msg.on_white().black());
}
///Logs the provided text to the `action_log.txt` file.  
///Used for when a message is unknown.
pub fn log_message(processed_text: &str) -> anyhow::Result<()> {
    let source = "LOG_MESSAGE";
    let error = error(source);

    OpenOptions::new()
        .read(true)
        .append(true)
        .create(true)
        //Open/Create the action_log.txt file with read, append, create options
        .open("action_log.txt")
        .map_err(|err| {
            error(format!("{err}").as_str());
            anyhow::anyhow!(err)
        })?
        //Attempt to write to file
        .write((format!("{processed_text}\n").as_str()).as_bytes())
        .map(|_| ())
        .map_err(|err| {
            error(format!("{err}").as_str());
            anyhow::anyhow!(err)
        })
}
