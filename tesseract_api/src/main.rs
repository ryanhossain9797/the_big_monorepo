mod base_64_adapter;
mod core;

use actix_web::{web, App, HttpResponse, HttpServer, Responder};
use serde::{Deserialize, Serialize};

#[derive(Debug, Deserialize)]
struct RequestType {
    image_base_64: String,
}

#[derive(Debug, Serialize)]
struct ResponseType {
    result: String,
}

async fn read_text(request: web::Json<RequestType>) -> impl Responder {
    use base_64_adapter::*;

    let text_result = get_text_from_image_b64(request.image_base_64.clone());

    match text_result {
        Ok(text) => {
            let response_type = ResponseType {
                result: format!("{text}"),
            };

            HttpResponse::Ok().json(response_type)
        }
        Err(err_msg) => HttpResponse::InternalServerError().json(ResponseType {
            result: format!("{err_msg}"),
        }),
    }
}

async fn to_black_and_white(request: web::Json<RequestType>) -> impl Responder {
    use base_64_adapter::*;

    let text_result = get_b_and_w_from_image_b64(request.image_base_64.clone());

    match text_result {
        Ok(text) => {
            let response_type = ResponseType {
                result: format!("{text}"),
            };

            HttpResponse::Ok().json(response_type)
        }
        Err(err_msg) => HttpResponse::InternalServerError().json(ResponseType {
            result: format!("{err_msg}"),
        }),
    }
}

async fn health() -> HttpResponse {
    HttpResponse::Ok().finish()
}

#[actix_web::main]
async fn main() -> std::io::Result<()> {
    HttpServer::new(|| {
        App::new()
            .route("/health", web::get().to(health))
            .route("/to_black_and_white", web::post().to(to_black_and_white))
            .route("/read_text", web::post().to(read_text))
    })
    .bind("0.0.0.0:9876")?
    .run()
    .await
}
