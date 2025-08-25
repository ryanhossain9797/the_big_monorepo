# PingPong Service

A lightweight Rust service that implements a ping-pong mechanism between two instances.

## Features

- **HTTP Server**: Runs on a configurable port to respond to ping requests
- **Ping Client**: Sends ping requests to another instance every 10 seconds
- **Health Check**: Provides a `/health` endpoint for monitoring
- **Lightweight**: Pure Rust implementation with minimal dependencies

## Environment Variables

- `HTTP_PORT`: Port for the HTTP server (default: 8080)
- `SIBLING_URL`: URL of the sibling instance to ping (optional)
- `INSTANCE_ID`: Unique identifier for this instance (default: "default")

## Local Development

### Building

```bash
cargo build --release
```

### Running a Single Instance

```bash
# Run without pinging any sibling
cargo run

# Or with custom port
HTTP_PORT=9090 cargo run
```

### Running Two Instances

Terminal 1 (Instance A):
```bash
HTTP_PORT=8080 INSTANCE_ID=instance-a SIBLING_URL=http://localhost:8081 cargo run
```

Terminal 2 (Instance B):
```bash
HTTP_PORT=8081 INSTANCE_ID=instance-b SIBLING_URL=http://localhost:8080 cargo run
```

## API Endpoints

- `GET /ping` - Responds with a pong message and instance information
- `GET /health` - Health check endpoint (returns 200 OK)

### Ping Response Format

```json
{
  "message": "pong",
  "timestamp": "2024-01-15T10:30:00Z",
  "instance_id": "instance-a"
}
```

## Testing

### Manual Testing

You can test the endpoints manually:

```bash
# Test ping endpoint
curl http://localhost:8080/ping

# Test health endpoint
curl http://localhost:8080/health
```

## Dependencies

The project uses pure Rust dependencies with rustls for TLS support:
- **tokio**: Async runtime
- **axum**: HTTP web framework
- **reqwest**: HTTP client (with rustls backend)
- **serde**: Serialization/deserialization
- **chrono**: Date/time handling

No system dependencies (like OpenSSL) are required.

## Docker

### Building the Image

```bash
docker build -t pingpong .
```

### Running with Docker

Single instance:
```bash
docker run -p 8080:8080 pingpong
```

Two instances:
```bash
# Instance A
docker run -p 8080:8080 -e INSTANCE_ID=instance-a -e SIBLING_URL=http://host.docker.internal:8081 pingpong

# Instance B  
docker run -p 8081:8080 -e INSTANCE_ID=instance-b -e SIBLING_URL=http://host.docker.internal:8080 pingpong
```

Note: Use `host.docker.internal` on Docker Desktop or the host IP address on Linux to allow containers to communicate with each other.

## Kubernetes Deployment

This project includes Kubernetes deployment configuration for running in a kind cluster.

### Quick Deploy

```bash
# Deploy both instances to the kind cluster
./deploy.sh deploy
```

### Telepresence Integration

For local development with Telepresence:

```bash
# Intercept instance-a for local development
telepresence intercept pingpong-instance-a --port 8080:8080

# Run locally
cargo run
```

### Deployment Commands

```bash
# Check deployment status
./deploy.sh status

# Test the deployments
./deploy.sh test

# Cleanup deployments
./deploy.sh cleanup
```

### Monitoring with k9s

Use k9s to monitor the deployments and view logs:

```bash
# Start k9s (if installed)
k9s

# Navigate to pods view and select a pingpong pod to see logs
# Press 'l' to view logs, 's' to view shell, etc.
```

The ping-pong activity will be visible in the pod logs every 10 seconds.

## Example Output

When running two instances, you'll see output like:

Instance A:
```
Starting PingPong instance: instance-a
HTTP server will run on port: 8080
Will ping sibling at: http://localhost:8081
Starting HTTP server on 0.0.0.0:8080
🏓 Received ping, responding with pong
✅ Ping sent successfully to http://localhost:8081: pong
```

Instance B:
```
Starting PingPong instance: instance-b
HTTP server will run on port: 8081
Will ping sibling at: http://localhost:8080
Starting HTTP server on 0.0.0.0:8081
🏓 Received ping, responding with pong
✅ Ping sent successfully to http://localhost:8080: pong
```

## Project Structure

```
pingpong/
├── src/main.rs              # Main application code
├── Cargo.toml               # Rust dependencies
├── Dockerfile               # Container image
├── deploy.sh                # Kubernetes deployment script
├── k8s/                     # Kubernetes manifests
│   ├── pingpong-instance-a.yaml
│   └── pingpong-instance-b.yaml
└── README.md                # This file
```
