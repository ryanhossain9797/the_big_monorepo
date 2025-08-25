use axum::{
    extract::State,
    http::StatusCode,
    response::Json,
    routing::get,
    Router,
};
use serde::{Deserialize, Serialize};
use std::env;
use std::sync::Arc;
use tokio::time::{sleep, Duration};

#[derive(Clone, Serialize, Deserialize)]
struct PingResponse {
    message: String,
    timestamp: String,
    instance_id: String,
}

#[derive(Clone)]
struct AppState {
    instance_id: String,
    sibling_url: Option<String>,
}

#[tokio::main]
async fn main() {
    // Get configuration from environment variables
    let http_port = env::var("HTTP_PORT").ok();
    let sibling_url = env::var("SIBLING_URL").ok();
    let instance_id = env::var("INSTANCE_ID").unwrap_or_else(|_| "default".to_string());

    println!("Starting PingPong instance: {}", instance_id);
    
    // Determine mode based on configuration
    let mode = match (&http_port, &sibling_url) {
        (Some(port), Some(url)) => {
            println!("Mode: PING + PONG");
            println!("HTTP server will run on port: {}", port);
            println!("Will ping sibling at: {}", url);
            "ping-pong"
        }
        (Some(port), None) => {
            println!("Mode: PONG ONLY");
            println!("HTTP server will run on port: {}", port);
            println!("No sibling URL configured - will not send pings");
            "pong-only"
        }
        (None, Some(url)) => {
            println!("Mode: PING ONLY");
            println!("No HTTP port configured - will not serve pongs");
            println!("Will ping sibling at: {}", url);
            "ping-only"
        }
        (None, None) => {
            println!("Mode: NEITHER");
            println!("No HTTP port or sibling URL configured - doing nothing");
            "neither"
        }
    };

    // Create shared state
    let state = Arc::new(AppState {
        instance_id,
        sibling_url: sibling_url.clone(),
    });

    match mode {
        "ping-pong" => {
            // Start both HTTP server and ping client
            let app = Router::new()
                .route("/ping", get(ping_handler))
                .route("/health", get(health_handler))
                .with_state(state.clone());

            let server_addr = format!("0.0.0.0:{}", http_port.unwrap());
            println!("Starting HTTP server on {}", server_addr);
            
            let server_handle = tokio::spawn(async move {
                let listener = tokio::net::TcpListener::bind(&server_addr).await.unwrap();
                axum::serve(listener, app).await.unwrap();
            });

            let ping_url = sibling_url.unwrap();
            let ping_handle = tokio::spawn(async move {
                println!("Waiting 30 seconds before starting ping client...");
                sleep(Duration::from_secs(30)).await;
                
                loop {
                    match send_ping(&ping_url).await {
                        Ok(response) => {
                            println!("✅ Ping sent successfully to {}: {}", ping_url, response.message);
                        }
                        Err(e) => {
                            println!("❌ Failed to ping {}: {}", ping_url, e);
                        }
                    }
                    
                    sleep(Duration::from_secs(10)).await;
                }
            });

            tokio::select! {
                _ = server_handle => println!("HTTP server stopped"),
                _ = ping_handle => println!("Ping client stopped"),
            }
        }
        "pong-only" => {
            // Start only HTTP server
            let app = Router::new()
                .route("/ping", get(ping_handler))
                .route("/health", get(health_handler))
                .with_state(state);

            let server_addr = format!("0.0.0.0:{}", http_port.unwrap());
            println!("Starting HTTP server on {}", server_addr);
            
            let listener = tokio::net::TcpListener::bind(&server_addr).await.unwrap();
            axum::serve(listener, app).await.unwrap();
        }
        "ping-only" => {
            // Start only ping client
            let ping_url = sibling_url.unwrap();
            println!("Starting ping client only...");
            
            loop {
                match send_ping(&ping_url).await {
                    Ok(response) => {
                        println!("✅ Ping sent successfully to {}: {}", ping_url, response.message);
                    }
                    Err(e) => {
                        println!("❌ Failed to ping {}: {}", ping_url, e);
                    }
                }
                
                sleep(Duration::from_secs(10)).await;
            }
        }
        "neither" => {
            println!("No functionality configured. Exiting.");
            return;
        }
        _ => {
            println!("Unknown mode: {}. Exiting.", mode);
            return;
        }
    }
}

async fn ping_handler(State(state): State<Arc<AppState>>) -> Json<PingResponse> {
    let response = PingResponse {
        message: "pong".to_string(),
        timestamp: chrono::Utc::now().to_rfc3339(),
        instance_id: state.instance_id.clone(),
    };
    
    println!("🏓 Received ping, responding with pong");
    Json(response)
}

async fn health_handler() -> StatusCode {
    StatusCode::OK
}

async fn send_ping(url: &str) -> Result<PingResponse, Box<dyn std::error::Error>> {
    let client = reqwest::Client::new();
    let response = client
        .get(&format!("{}/ping", url))
        .timeout(Duration::from_secs(5))
        .send()
        .await?;
    
    if response.status().is_success() {
        let ping_response: PingResponse = response.json().await?;
        Ok(ping_response)
    } else {
        Err(format!("HTTP error: {}", response.status()).into())
    }
}
