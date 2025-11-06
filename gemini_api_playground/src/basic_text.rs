use crate::prepare_gemini_client;
use gemini_rust::Gemini;
use std::io::{self, Write};

enum UserInput {
    Prompt(String),
    Quit,
}

fn get_user_input() -> Result<UserInput, Box<dyn std::error::Error>> {
    print!("\nEnter your prompt (or 'quit' to exit): ");
    io::stdout().flush()?;

    let mut input = String::new();
    io::stdin().read_line(&mut input)?;
    let input = input.trim();

    if input.eq_ignore_ascii_case("quit") {
        Ok(UserInput::Quit)
    } else if input.is_empty() {
        get_user_input()
    } else {
        Ok(UserInput::Prompt(input.to_string()))
    }
}

async fn generate_text(
    client: &Gemini,
    prompt: &str,
) -> Result<String, Box<dyn std::error::Error>> {
    let response = client
        .generate_content()
        .with_user_message(prompt)
        .execute()
        .await?;

    Ok(response.text().to_string())
}

pub async fn do_main() -> Result<(), Box<dyn std::error::Error>> {
    let client = prepare_gemini_client()?;

    loop {
        match get_user_input()? {
            UserInput::Quit => {
                println!("Goodbye");
                break;
            }
            UserInput::Prompt(prompt) => {
                let response = generate_text(&client, &prompt).await?;

                println!("Response: {response}\n");
            }
        }
    }

    Ok(())
}
