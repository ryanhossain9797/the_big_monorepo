mod appointment_confirmation;
mod basic_text;

use gemini_rust::Gemini;
use std::env;

fn prepare_gemini_client() -> Result<Gemini, Box<dyn std::error::Error>> {
    let api_key = env::var("GEMINI_API_KEY").expect("env var GEMINI_API_KEY not found");

    let client = Gemini::new(api_key)?;
    Ok(client)
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Choose which example to run:
    // basic_text::do_main().await
    appointment_confirmation::do_main().await
}
