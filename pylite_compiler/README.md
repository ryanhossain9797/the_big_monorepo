# Pylite

A Rust-based compiler project built with modern tooling and best practices.

## Project Structure

```
pylite/
├── Cargo.toml          # Workspace configuration
├── compiler/           # Main compiler crate
│   ├── Cargo.toml
│   └── src/
│       └── main.rs
└── README.md
```

## Prerequisites

- Rust 1.70+ (for edition 2024 support)
- Cargo (comes with Rust)

## Getting Started

1. **Clone the repository**
   ```bash
   git clone <your-repo-url>
   cd pylite
   ```

2. **Build the project**
   ```bash
   cargo build
   ```

3. **Run the compiler**
   ```bash
   cargo run --bin compiler
   ```

4. **Run tests**
   ```bash
   cargo test
   ```

## Development

### Adding a new crate to the workspace

1. Create a new directory for your crate
2. Add it to the `members` array in the root `Cargo.toml`
3. Create a `Cargo.toml` file in the new crate directory

### Building specific crates

```bash
# Build only the compiler crate
cargo build -p compiler

# Run tests for a specific crate
cargo test -p compiler
```

### Code formatting and linting

```bash
# Format code
cargo fmt

# Run clippy linter
cargo clippy
```

## Workspace Configuration

This project uses Cargo workspaces with:
- **Resolver 2**: Latest dependency resolution algorithm
- **Edition 2024**: Latest Rust edition features
- **Shared dependencies**: Common dependencies can be defined in `[workspace.dependencies]`

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests and ensure code quality
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Author

Raiyan Hossain <ryanhossain9797@gmail.com> 