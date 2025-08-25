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

## Usage

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

## Testing

### Manual Testing

You can test the endpoints manually:

```bash
# Test ping endpoint
curl http://localhost:8080/ping

# Test health endpoint
curl http://localhost:8080/health
```

### Automated Testing

Use the provided test script to run two instances and see the ping-pong activity:

```bash
./test_pingpong.sh
```

This script will:
1. Start two instances on ports 8080 and 8081
2. Test the health and ping endpoints
3. Watch the ping-pong activity for 30 seconds
4. Clean up the processes

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
