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
    let http_port = env::var("HTTP_PORT").unwrap_or_else(|_| "8080".to_string());
    let sibling_url = env::var("SIBLING_URL").ok();
    let instance_id = env::var("INSTANCE_ID").unwrap_or_else(|_| "default".to_string());

    println!("Starting PingPong instance: {}", instance_id);
    println!("HTTP server will run on port: {}", http_port);
    if let Some(url) = &sibling_url {
        println!("Will ping sibling at: {}", url);
    } else {
        println!("No sibling URL configured - will not send pings");
    }

    // Create shared state
    let state = Arc::new(AppState {
        instance_id,
        sibling_url,
    });

    // Create router
    let app = Router::new()
        .route("/ping", get(ping_handler))
        .route("/health", get(health_handler))
        .with_state(state.clone());

    // Start HTTP server
    let server_addr = format!("0.0.0.0:{}", http_port);
    println!("Starting HTTP server on {}", server_addr);
    
    let server_handle = tokio::spawn(async move {
        let listener = tokio::net::TcpListener::bind(&server_addr).await.unwrap();
        axum::serve(listener, app).await.unwrap();
    });

    // Start ping client if sibling URL is configured
    if let Some(sibling_url) = &state.sibling_url {
        let ping_url = sibling_url.clone();
        
        let ping_handle = tokio::spawn(async move {
            loop {
                sleep(Duration::from_secs(10)).await;
                
                match send_ping(&ping_url).await {
                    Ok(response) => {
                        println!("✅ Ping sent successfully to {}: {}", ping_url, response.message);
                    }
                    Err(e) => {
                        println!("❌ Failed to ping {}: {}", ping_url, e);
                    }
                }
            }
        });

        // Wait for either task to complete (they shouldn't)
        tokio::select! {
            _ = server_handle => println!("HTTP server stopped"),
            _ = ping_handle => println!("Ping client stopped"),
        }
    } else {
        // Just run the HTTP server if no sibling URL
        server_handle.await.unwrap();
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
