# Device Control Parser - Structured Output Example

This project has been configured to parse natural language device control commands and output structured JSON data.

## Output Schema

Every response will be a valid JSON object with these exact fields:

```json
{
  "ResponseMessage": "string - always present",
  "IsSuccess": boolean,
  "Device": "string or null",
  "Property": "string or null",
  "Value": number | string | null
}
```

### Field Descriptions

- **ResponseMessage**: A human-friendly message confirming the action or explaining why it failed (always present)
- **IsSuccess**: `true` if the command was understood and parsed successfully, `false` otherwise
- **Device**: The device name extracted from the command (e.g., "ac", "light", "fan") or `null` if not found
- **Property**: The property being controlled (e.g., "temperature", "brightness", "power") or `null` if not found
- **Value**: The value to set - can be a number, string, or `null` if not found

## Example Commands and Expected Output

### Successful Commands

**Input:** "Set ac to 27"
```json
{
  "ResponseMessage": "Setting AC temperature to 27 degrees",
  "IsSuccess": true,
  "Device": "ac",
  "Property": "temperature",
  "Value": 27
}
```

**Input:** "Turn on the lights"
```json
{
  "ResponseMessage": "Turning on the lights",
  "IsSuccess": true,
  "Device": "light",
  "Property": "power",
  "Value": "on"
}
```

**Input:** "Set living room fan speed to 3"
```json
{
  "ResponseMessage": "Setting living room fan speed to 3",
  "IsSuccess": true,
  "Device": "fan",
  "Property": "speed",
  "Value": 3
}
```

**Input:** "Dim bedroom lights to 50 percent"
```json
{
  "ResponseMessage": "Setting bedroom lights brightness to 50 percent",
  "IsSuccess": true,
  "Device": "light",
  "Property": "brightness",
  "Value": 50
}
```

### Failed/Unclear Commands

**Input:** "Hello"
```json
{
  "ResponseMessage": "Please provide a device control command",
  "IsSuccess": false,
  "Device": null,
  "Property": null,
  "Value": null
}
```

**Input:** "What's the weather?"
```json
{
  "ResponseMessage": "This is not a device control command. Please specify a device and action.",
  "IsSuccess": false,
  "Device": null,
  "Property": null,
  "Value": null
}
```

## How It Works

### GBNF Grammar

The structured output is enforced using a GBNF (GGML BNF) grammar defined in `grammars/device_control.gbnf`. This grammar constrains the model to only generate tokens that result in valid JSON matching the schema.

### Implementation Details

1. **Grammar Loading** (src/main.rs:90)
   ```rust
   let grammar = include_str!("../grammars/device_control.gbnf");
   ```

2. **Sampler Chain** (src/main.rs:93-98)
   ```rust
   let mut sampler = LlamaSampler::chain_simple([
       LlamaSampler::temp(0.3),              // Low temperature for consistency
       LlamaSampler::grammar(&model, grammar, "root"), // Grammar constraint
       LlamaSampler::dist(0),                 // Random sampling
   ]);
   ```

3. **System Prompt** (src/main.rs:60-72)
   - Instructs the model to act as a device control parser
   - Provides examples of valid input/output pairs
   - Emphasizes JSON-only output

4. **Early Stopping** (src/main.rs:128-133)
   - Detects when JSON object is complete (balanced braces)
   - Prevents unnecessary token generation

## Running the Parser

```bash
# Run with default model (Qwen2.5-14B)
cargo run --release

# Or with a smaller model
MODEL_PATH=models/Llama-3.2-1B-Instruct-Q4_K_M.gguf cargo run --release
```

### Example Session

```
Loading model from: models/Qwen2.5-14B-Instruct-Q4_K_M.gguf
Model loaded successfully! Type 'quit' or 'exit' to quit.

You: Set ac to 27
{"ResponseMessage":"Setting AC temperature to 27 degrees","IsSuccess":true,"Device":"ac","Property":"temperature","Value":27}

(Generated in 856.32ms)

You: Turn off the fan
{"ResponseMessage":"Turning off the fan","IsSuccess":true,"Device":"fan","Property":"power","Value":"off"}

(Generated in 623.45ms)

You: Hello there
{"ResponseMessage":"Please provide a device control command","IsSuccess":false,"Device":null,"Property":null,"Value":null}

(Generated in 489.21ms)

You: quit
Goodbye!
```

## Parsing the Output

To work with the structured output programmatically, you can parse the JSON:

### Option 1: Add serde_json to Cargo.toml

```toml
[dependencies]
llama-cpp-2 = "0.1.74"
num_cpus = "1.16"
serde_json = "1.0"
serde = { version = "1.0", features = ["derive"] }
```

### Option 2: Create a struct

```rust
use serde::{Deserialize, Serialize};

#[derive(Debug, Serialize, Deserialize)]
struct DeviceControlResponse {
    #[serde(rename = "ResponseMessage")]
    response_message: String,

    #[serde(rename = "IsSuccess")]
    is_success: bool,

    #[serde(rename = "Device")]
    device: Option<String>,

    #[serde(rename = "Property")]
    property: Option<String>,

    #[serde(rename = "Value")]
    value: Option<serde_json::Value>, // Can be string, number, or null
}

// Parse the response
let parsed: DeviceControlResponse = serde_json::from_str(&response)?;

if parsed.is_success {
    println!("Device: {:?}", parsed.device);
    println!("Property: {:?}", parsed.property);
    println!("Value: {:?}", parsed.value);
} else {
    println!("Failed: {}", parsed.response_message);
}
```

## Customizing the Schema

If you need different fields, modify:

1. **Grammar file**: `grammars/device_control.gbnf`
   - Update field names and types
   - Follow GBNF syntax rules

2. **System prompt**: `src/main.rs:60-72`
   - Update field descriptions
   - Provide new examples

3. **Recompile**: `cargo build --release`

## Benefits of Grammar-Constrained Output

✅ **Guaranteed valid JSON** - No parsing errors or malformed responses
✅ **Consistent schema** - Every response has the exact same structure
✅ **No post-processing** - Output is immediately usable
✅ **Type safety** - Numbers are numbers, booleans are booleans
✅ **Faster inference** - Model doesn't waste tokens on explanations

## Limitations

⚠️ Grammar enforcement can slightly slow generation (typically 10-20%)
⚠️ Model must understand the prompt format (works best with instruction-tuned models)
⚠️ Complex grammars with many optionals can be slower

## Troubleshooting

### Grammar fails to load
- Check GBNF syntax in `grammars/device_control.gbnf`
- Ensure file exists and is readable
- Look for syntax errors (mismatched quotes, invalid rules)

### Model generates invalid JSON despite grammar
- This should not happen - grammar enforces validity
- If it does, check that grammar is loading correctly
- Verify the grammar root rule is "root"

### Poor parsing quality
- Model may not understand device names or properties
- Try providing more examples in system prompt
- Use a larger/better model (14B vs 1B)
- Adjust temperature (higher for more creativity)

### Generation is slow
- Grammar adds overhead (10-20% typical)
- Try a smaller model
- Reduce context size
- Check CPU usage and thermal throttling

## Further Reading

- [GBNF Grammar Documentation](https://github.com/ggml-org/llama.cpp/blob/master/grammars/README.md)
- [llama.cpp Grammars](https://github.com/ggml-org/llama.cpp/tree/master/grammars)
- [JSON Schema to GBNF](https://github.com/adrienbrault/json-schema-to-gbnf)
