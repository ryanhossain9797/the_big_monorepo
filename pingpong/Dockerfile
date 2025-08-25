# Use the official Rust image as a builder
FROM rust:latest as builder

# Set the working directory
WORKDIR /app

# Copy the manifests
COPY Cargo.toml Cargo.lock ./

# Create a dummy main.rs to build dependencies
RUN mkdir src && echo "fn main() {}" > src/main.rs

# Update lock file to compatible format if needed
RUN cargo update

# Build dependencies only
RUN cargo build --release

# Remove the dummy main.rs and copy the real source code
RUN rm src/main.rs
COPY src ./src

# Build the application
RUN cargo build --release

# Create a new stage with a minimal runtime image
FROM debian:bookworm-slim

# Install ca-certificates for HTTPS requests
RUN apt-get update && apt-get install -y ca-certificates && rm -rf /var/lib/apt/lists/*

# Create a non-root user
RUN useradd -r -s /bin/false pingpong

# Set the working directory
WORKDIR /app

# Copy the binary from the builder stage
COPY --from=builder /app/target/release/pingpong /app/pingpong

# Change ownership to the non-root user
RUN chown pingpong:pingpong /app/pingpong

# Switch to the non-root user
USER pingpong

# Expose the default port
EXPOSE 8080

# Set environment variables
ENV INSTANCE_ID=default

# Run the binary
CMD ["/app/pingpong"]
