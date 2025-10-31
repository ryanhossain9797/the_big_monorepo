# LLM Crate Playground

A Rust-based command-line interface for running and interacting with large language models (LLMs) locally using GGUF format models via llama.cpp bindings.

## What This Project Does

This is a minimal, interactive LLM chat application that allows you to:
- Load and run quantized LLM models (GGUF format) entirely on your local machine
- Have conversational interactions with various language models (Qwen, Llama, Mistral, etc.)
- **Generate structured JSON output** with guaranteed schema compliance using GBNF grammars
- Parse natural language commands into structured data (e.g., device control)
- Switch between different models easily
- Run inference on CPU without requiring a GPU

The project uses Rust bindings to llama.cpp, providing efficient local inference with low-level control over model parameters, sampling strategies, and context management.

> **Note**: This project is currently configured as a **device control parser** that converts natural language commands into structured JSON. See [DEVICE_CONTROL.md](DEVICE_CONTROL.md) for details.

## Key Features

- **Structured JSON Output**: Uses GBNF grammar to guarantee valid JSON with consistent schema
- **Device Control Parser**: Parses natural language commands into structured data (see [DEVICE_CONTROL.md](DEVICE_CONTROL.md))
- **Interactive Chat Loop**: Continuous conversation with the loaded model
- **Configurable Models**: Switch models via environment variable or code
- **ChatML Prompt Format**: Optimized for instruction-following models like Qwen2.5
- **Grammar-Constrained Generation**: Enforce output formats using GBNF grammars
- **Efficient Sampling**: Configurable temperature and sampling strategies
- **Performance Metrics**: Displays generation time for each response
- **CPU Optimized**: Automatically uses all available CPU cores
- **Clean Context Management**: Clears KV cache between prompts for fresh responses

## Prerequisites

### System Requirements
- **Rust**: 1.70+ (install from [rustup.rs](https://rustup.rs))
- **RAM**: Minimum 8GB (16GB+ recommended for larger models)
- **CPU**: Modern multi-core processor
- **Storage**: Several GB for model files

### Additional Dependencies
- **C++ Compiler**: Required for llama-cpp-2 compilation
  - Linux: `build-essential`, `clang`
  - macOS: Xcode command line tools
  - Windows: Visual Studio with C++ tools

## Quick Start

### 1. Clone and Setup
```bash
git clone <repository-url>
cd llm_crate_playground
```

### 2. Download a Model

You need at least one GGUF model in the `models/` directory. See [models/README.md](models/README.md) for detailed instructions.

**Quick Download (Qwen2.5-14B default):**
```bash
# Create models directory if it doesn't exist
mkdir -p models

# Download using huggingface-cli (requires: pip install huggingface-hub)
huggingface-cli download Qwen/Qwen2.5-14B-Instruct-GGUF \
  qwen2.5-14b-instruct-q4_k_m.gguf \
  --local-dir models/ \
  --local-dir-use-symlinks False
```

**Or download a smaller model for testing:**
```bash
# Llama 3.2 1B (much smaller, ~0.7GB)
huggingface-cli download bartowski/Llama-3.2-1B-Instruct-GGUF \
  Llama-3.2-1B-Instruct-Q4_K_M.gguf \
  --local-dir models/ \
  --local-dir-use-symlinks False
```

### 3. Build and Run
```bash
# Build the project
cargo build --release

# Run with default model (Qwen2.5-14B)
cargo run --release

# Or run with a specific model
MODEL_PATH=models/Llama-3.2-1B-Instruct-Q4_K_M.gguf cargo run --release
```

## Usage

Once running, simply type device control commands:

```
Loading model from: models/Qwen2.5-14B-Instruct-Q4_K_M.gguf
Model loaded successfully! Type 'quit' or 'exit' to quit.

You: Set ac to 27
{"ResponseMessage":"Setting AC temperature to 27 degrees","IsSuccess":true,"Device":"ac","Property":"temperature","Value":27}

(Generated in 856.32ms)

You: Turn on the lights
{"ResponseMessage":"Turning on the lights","IsSuccess":true,"Device":"light","Property":"power","Value":"on"}

(Generated in 623.45ms)

You: Hello
{"ResponseMessage":"Please provide a device control command","IsSuccess":false,"Device":null,"Property":null,"Value":null}

(Generated in 489.21ms)

You: quit
Goodbye!
```

### Commands
- Type any device control command (e.g., "Set ac to 27", "Turn off the fan")
- `quit` or `exit` - Exit the application
- Press Enter on empty line - Skip (continue)

### Understanding the Output

Every response is a JSON object with this structure:
```json
{
  "ResponseMessage": "Human-friendly message",
  "IsSuccess": true/false,
  "Device": "device_name or null",
  "Property": "property_name or null",
  "Value": number/string/null
}
```

See [DEVICE_CONTROL.md](DEVICE_CONTROL.md) for detailed documentation, examples, and how to parse the output programmatically.

## Configuration

### Model Selection

**Environment Variable (Recommended):**
```bash
MODEL_PATH=models/your-model.gguf cargo run --release
```

**Edit Default (src/main.rs:15):**
```rust
let model_path = std::env::var("MODEL_PATH")
    .unwrap_or_else(|_| "models/your-model.gguf".to_string());
```

### Model Parameters

Edit `src/main.rs` to adjust:

**Context Size (line 28):**
```rust
.with_n_ctx(NonZeroU32::new(2048)) // Tokens of context (increase for longer conversations)
```

**Thread Count (lines 29-30):**
```rust
.with_n_threads(num_cpus::get() as i32) // CPU cores for inference
.with_n_threads_batch(num_cpus::get() as i32) // CPU cores for batch processing
```

**Temperature (line 79):**
```rust
LlamaSampler::temp(0.3) // Lower = more focused, Higher = more creative (0.0-2.0)
```

**Max Response Length (line 82):**
```rust
let max_tokens = 1000; // Maximum tokens to generate per response
```

### Prompt Format

The current implementation uses **ChatML format** (optimized for Qwen models):

```rust
<|im_start|>system
{system_message}<|im_end|>
<|im_start|>user
{user_prompt}<|im_end|>
<|im_start|>assistant
```

If using non-Qwen models (e.g., Llama), you may need to adjust the prompt format in `src/main.rs:59-62`.

## Project Structure

```
llm_crate_playground/
├── Cargo.toml          # Rust dependencies (llama-cpp-2, num_cpus)
├── README.md           # This file
├── src/
│   └── main.rs         # Main application logic
├── models/             # Directory for GGUF model files
│   ├── README.md       # Model download and setup guide
│   ├── Qwen2.5-14B-Instruct-Q4_K_M.gguf
│   └── Llama-3.2-1B-Instruct-Q4_K_M.gguf
└── target/             # Build artifacts (gitignored)
```

## Technical Details

### Dependencies
- **llama-cpp-2** (v0.1.74): Rust bindings to llama.cpp for efficient LLM inference
- **num_cpus** (v1.16): CPU core detection for optimal thread usage

### How It Works
1. **Model Loading**: Loads GGUF model from disk using llama.cpp
2. **Context Creation**: Initializes inference context with specified parameters
3. **Tokenization**: Converts text prompts to tokens using model's tokenizer
4. **Batch Processing**: Processes prompt tokens in batches for efficiency
5. **Sampling**: Uses temperature-based sampling to generate next tokens
6. **Decoding**: Converts generated tokens back to text
7. **Streaming Output**: Displays tokens as they're generated for real-time feedback

### Performance Characteristics
- **First Token Latency**: Depends on prompt length and CPU speed (~500ms - 2s)
- **Generation Speed**: Typically 5-20 tokens/second on modern CPUs
- **Memory Usage**: Model size + context size (~9GB for Qwen2.5-14B-Q4_K_M)
- **CPU Utilization**: Near 100% during generation

## Troubleshooting

### Build Issues

**"Could not find libclang" error:**
```bash
# Ubuntu/Debian
sudo apt-get install libclang-dev

# macOS
xcode-select --install

# Or set LIBCLANG_PATH manually
export LIBCLANG_PATH=/usr/lib/llvm-14/lib
```

**Long compile times:**
- First build compiles llama.cpp (takes 5-10 minutes)
- Subsequent builds are much faster
- Use `cargo build` (debug) for faster iteration during development

### Runtime Issues

**"No such file or directory" when loading model:**
- Verify the model file exists in the `models/` directory
- Check the filename matches exactly (case-sensitive)
- Ensure the file downloaded completely

**Out of memory errors:**
- Use a smaller model or lower quantization (Q4_K_M → Q3_K_M)
- Reduce context size in main.rs (2048 → 1024)
- Close other memory-intensive applications

**Slow generation:**
- This is expected on CPU inference
- Try a smaller model (1B instead of 14B)
- Reduce n_threads if system is thermal throttling

**Poor quality responses:**
- Verify correct prompt format for your model
- Try adjusting temperature (0.3 → 0.7 for more creativity)
- Use higher quantization (Q4_K_M → Q5_K_M or Q6_K)

## For LLM Assistants

When helping users with this project:

**Project Type**: Rust CLI application for local LLM inference
**Core Library**: llama-cpp-2 (Rust bindings to llama.cpp)
**Model Format**: GGUF (quantized models for CPU inference)
**Current Default**: Qwen2.5-14B-Instruct-Q4_K_M

**Key Files:**
- `src/main.rs:15` - Model path configuration
- `src/main.rs:28` - Context size (affects memory usage and max conversation length)
- `src/main.rs:59-62` - Prompt format (model-specific)
- `src/main.rs:79` - Temperature setting (affects creativity)
- `models/README.md` - Model download instructions

**Common Modifications:**
- Changing models: Set `MODEL_PATH` environment variable
- Adjusting response style: Modify temperature at line 79
- Extending context: Increase n_ctx at line 28 (requires more RAM)
- Supporting new model families: Update prompt format at lines 59-62

## Resources

- **llama.cpp**: https://github.com/ggerganov/llama.cpp
- **llama-cpp-2 crate**: https://crates.io/crates/llama-cpp-2
- **GGUF models**: https://huggingface.co/models?library=gguf
- **Model benchmarks**: https://huggingface.co/spaces/lmsys/chatbot-arena-leaderboard

## License

[Specify your license here]

## Contributing

[Add contribution guidelines if applicable]
