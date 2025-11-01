use super::BkashError;
use super::*;
use anyhow::Result;
use anyhow::*;
use chrono::prelude::*;
use rustls::{
    sign::{SignError, SigningKey},
    PrivateKey, SignatureScheme,
};
use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug)]
struct UserInfoRequestBody {
    #[serde(rename = "accessToken")]
    access_token: String,
}

#[derive(Serialize, Deserialize, Debug)]
struct UserInfoRequestSignature {
    #[serde(rename = "accessToken")]
    access_token: String,
}

pub fn query_user_binding(
    config: &BkashDirectDebitConfig,
    access_token: &str,
) -> Result<String, BkashError> {
    let now: DateTime<Utc> = Utc::now();
    let head = DirectDebitRequestHead {
        version: "1.0.0".to_string(),
        timestamp: now.format(TIMESTAMP_FORMATTER).to_string(),
        merchant_shortcode: config.merchant_shortcode.to_string(),
        signature: None,
    };

    let signature_body = UserInfoRequestSignature {
        access_token: access_token.to_string(),
    };

    let request_body = UserInfoRequestBody {
        access_token: access_token.to_string(),
    };

    serde_json::to_string(&head).map_err(BkashError::JsonError)
}
