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

#[derive(Debug, Clone)]
enum PingPongMode {
    PingPong,
    PongOnly,
    PingOnly,
    Neither,
}

impl PingPongMode {
    fn from_config(http_port: &Option<String>, sibling_url: &Option<String>) -> Self {
        match (http_port, sibling_url) {
            (Some(_), Some(_)) => PingPongMode::PingPong,
            (Some(_), None) => PingPongMode::PongOnly,
            (None, Some(_)) => PingPongMode::PingOnly,
            (None, None) => PingPongMode::Neither,
        }
    }

    fn description(&self) -> &'static str {
        match self {
            PingPongMode::PingPong => "PING + PONG",
            PingPongMode::PongOnly => "PONG ONLY",
            PingPongMode::PingOnly => "PING ONLY",
            PingPongMode::Neither => "NEITHER",
        }
    }
}

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
    let mode = PingPongMode::from_config(&http_port, &sibling_url);
    println!("Mode: {}", mode.description());
    
    // Print configuration details
    match &mode {
        PingPongMode::PingPong => {
            println!("HTTP server will run on port: {}", http_port.as_ref().unwrap());
            println!("Will ping sibling at: {}", sibling_url.as_ref().unwrap());
        }
        PingPongMode::PongOnly => {
            println!("HTTP server will run on port: {}", http_port.as_ref().unwrap());
            println!("No sibling URL configured - will not send pings");
        }
        PingPongMode::PingOnly => {
            println!("No HTTP port configured - will not serve pongs");
            println!("Will ping sibling at: {}", sibling_url.as_ref().unwrap());
        }
        PingPongMode::Neither => {
            println!("No HTTP port or sibling URL configured - doing nothing");
        }
    }

    // Create shared state
    let state = Arc::new(AppState {
        instance_id,
        sibling_url: sibling_url.clone(),
    });

    // Run the appropriate mode
    run_mode(mode, http_port, sibling_url, state).await;
}

async fn run_mode(mode: PingPongMode, http_port: Option<String>, sibling_url: Option<String>, state: Arc<AppState>) {
    match mode {
        PingPongMode::PingPong => {
            run_ping_pong_mode(http_port.unwrap(), sibling_url.unwrap(), state).await;
        }
        PingPongMode::PongOnly => {
            run_pong_only_mode(http_port.unwrap(), state).await;
        }
        PingPongMode::PingOnly => {
            run_ping_only_mode(sibling_url.unwrap()).await;
        }
        PingPongMode::Neither => {
            println!("No functionality configured. Exiting.");
        }
    }
}

async fn run_ping_pong_mode(http_port: String, sibling_url: String, state: Arc<AppState>) {
    let app = create_app(state.clone());
    let server_addr = format!("0.0.0.0:{}", http_port);
    println!("Starting HTTP server on {}", server_addr);
    
    let server_handle = tokio::spawn(async move {
        let listener = tokio::net::TcpListener::bind(&server_addr).await.unwrap();
        axum::serve(listener, app).await.unwrap();
    });

    let ping_handle = tokio::spawn(async move {
        run_ping_client(&sibling_url).await;
    });

    tokio::select! {
        _ = server_handle => println!("HTTP server stopped"),
        _ = ping_handle => println!("Ping client stopped"),
    }
}

async fn run_pong_only_mode(http_port: String, state: Arc<AppState>) {
    let app = create_app(state);
    let server_addr = format!("0.0.0.0:{}", http_port);
    println!("Starting HTTP server on {}", server_addr);
    
    let listener = tokio::net::TcpListener::bind(&server_addr).await.unwrap();
    axum::serve(listener, app).await.unwrap();
}

async fn run_ping_only_mode(sibling_url: String) {
    println!("Starting ping client only...");
    run_ping_client(&sibling_url).await;
}

fn create_app(state: Arc<AppState>) -> Router {
    Router::new()
        .route("/ping", get(ping_handler))
        .route("/health", get(health_handler))
        .with_state(state)
}

async fn run_ping_client(sibling_url: &str) {
    println!("Waiting 30 seconds before starting ping client...");
    sleep(Duration::from_secs(30)).await;
    
    loop {
        match send_ping(sibling_url).await {
            Ok(response) => {
                println!("✅ Ping sent successfully to {}: {}", sibling_url, response.message);
            }
            Err(e) => {
                println!("❌ Failed to ping {}: {}", sibling_url, e);
            }
        }
        
        sleep(Duration::from_secs(10)).await;
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
