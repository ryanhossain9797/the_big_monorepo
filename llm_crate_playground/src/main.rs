use llama_cpp_2::context::params::LlamaContextParams;
use llama_cpp_2::ggml_time_us;
use llama_cpp_2::llama_backend::LlamaBackend;
use llama_cpp_2::llama_batch::LlamaBatch;
use llama_cpp_2::model::params::LlamaModelParams;
use llama_cpp_2::model::{AddBos, LlamaModel, Special};
use llama_cpp_2::sampling::LlamaSampler;
use std::io::{self, Write};
use std::num::NonZeroU32;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Get model path from environment variable or use default
    // Using Qwen2.5-14B-Instruct - a powerful 14B parameter model
    let model_path = std::env::var("MODEL_PATH")
        .unwrap_or_else(|_| "models/Qwen2.5-14B-Instruct-Q4_K_M.gguf".to_string());

    println!("Loading model from: {}", model_path);

    // Initialize the llama.cpp backend
    let backend = LlamaBackend::init()?;

    // Load the model with default parameters
    let model_params = LlamaModelParams::default();
    let model = LlamaModel::load_from_file(&backend, &model_path, &model_params)?;

    // Create context with configuration
    let ctx_params = LlamaContextParams::default()
        .with_n_ctx(NonZeroU32::new(2048)) // Context size
        .with_n_threads(num_cpus::get() as i32) // Use all CPU cores
        .with_n_threads_batch(num_cpus::get() as i32);

    let mut ctx = model.new_context(&backend, ctx_params)?;

    println!("\nModel loaded successfully! Type 'quit' or 'exit' to quit.\n");

    // Main conversation loop
    loop {
        // Get user input
        print!("You: ");
        io::stdout().flush()?;

        let mut user_msg = String::new();
        io::stdin().read_line(&mut user_msg)?;
        let prompt = user_msg.trim();

        // Check for exit commands
        if prompt.is_empty() {
            continue;
        }
        if prompt.eq_ignore_ascii_case("quit") || prompt.eq_ignore_ascii_case("exit") {
            println!("Goodbye!");
            break;
        }

        // Clear KV cache to start fresh for each prompt
        ctx.clear_kv_cache();

        // Use Qwen2.5's ChatML format for better instruction following
        let conversation_prompt = format!(
            "<|im_start|>system\nYou are a helpful assistant that provides direct, concise answers to questions.<|im_end|>\n<|im_start|>user\n{}<|im_end|>\n<|im_start|>assistant\n",
            prompt
        );

        let tokens = model.str_to_token(&conversation_prompt, AddBos::Always)?;

        // Create a batch and add tokens
        let mut batch = LlamaBatch::new(512, 1);

        for (i, token) in tokens.iter().enumerate() {
            let is_last = i == tokens.len() - 1;
            batch.add(*token, i as i32, &[0], is_last)?;
        }

        // Process the prompt
        ctx.decode(&mut batch)?;

        // Create sampler chain with lower temperature for more focused responses
        let mut sampler =
            LlamaSampler::chain_simple([LlamaSampler::temp(0.3), LlamaSampler::dist(0)]);

        // Generate tokens
        let max_tokens = 1000;
        let mut n_cur = batch.n_tokens();
        let mut response = String::new();

        let start_time = ggml_time_us();

        for _ in 0..max_tokens {
            // Sample next token using the sampler chain
            let new_token = sampler.sample(&ctx, batch.n_tokens() - 1);

            // Check for end of generation
            if model.is_eog_token(new_token) {
                break;
            }

            // Convert token to string and add to response
            let output = model.token_to_str(new_token, Special::Tokenize)?;

            // Stop if we hit the ChatML end token
            if response.contains("<|im_end|>") || output.contains("<|im_end|>") {
                break;
            }

            response.push_str(&output);
            print!("{}", output);
            io::stdout().flush()?;

            // Prepare next batch
            batch.clear();
            batch.add(new_token, n_cur, &[0], true)?;
            n_cur += 1;

            // Decode
            ctx.decode(&mut batch)?;
        }

        let end_time = ggml_time_us();
        let duration_ms = (end_time - start_time) as f64 / 1000.0;

        println!("\n\n(Generated in {:.2}ms)\n", duration_ms);
    }

    Ok(())
}
