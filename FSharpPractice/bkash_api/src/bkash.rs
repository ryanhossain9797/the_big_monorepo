pub mod prepare;

use anyhow::Result;
use anyhow::*;
use chrono::prelude::*;
use reqwest::{Request, RequestBuilder};
use rustls::{
    sign::{SignError, SigningKey},
    PrivateKey, SignatureScheme,
};
use serde::{Deserialize, Serialize};
use std::error::Error;

pub enum BkashError {
    JsonError(serde_json::Error),
}

const TIMESTAMP_FORMATTER: &str = "%F %T";

const SIGNATURE_SCHEME: [rustls::SignatureScheme; 1] = [rustls::SignatureScheme::RSA_PKCS1_SHA256];

fn generate_data_for_signature(header: &str, body: &str) -> String {
    format!("params={body}&&verify={header}")
}

fn generate_signature(private_key: &str, data: &str) -> anyhow::Result<String> {
    let private_key: PrivateKey = PrivateKey(base64::decode(private_key)?);

    let signer_with_scheme = rustls::sign::RsaSigningKey::new(&private_key)?
        .choose_scheme(&SIGNATURE_SCHEME)
        .ok_or_else(|| anyhow!("scheme error"))?;

    let signature = base64::encode(signer_with_scheme.sign(data.as_bytes())?);

    Ok(signature)
}
/*
type BkashDirectDebitConfig = {
    ApiEndPoint:       NonemptyString
    MerchantShortCode: NonemptyString
    AuthClientId:      NonemptyString
    PrivateKey:        NonemptyString
    PublicKey:         NonemptyString
    CallbackUrl:       NonemptyString
}
*/

#[derive(Serialize, Deserialize, Debug)]
pub struct BkashDirectDebitConfig {
    #[serde(rename = "ApiEndPoint")]
    pub api_endpoint: String,
    #[serde(rename = "MerchantShortCode")]
    pub merchant_shortcode: String,
    #[serde(rename = "AuthClientId")]
    pub auth_client_id: String,
    #[serde(rename = "PrivateKey")]
    pub private_key: String,
    #[serde(rename = "PublicKey")]
    pub public_key: String,
    #[serde(rename = "CallbackUrl")]
    pub callback_url: String,
}

#[derive(Serialize, Deserialize, Debug)]
struct DirectDebitRequestHead {
    #[serde(rename = "merchantShortCode")]
    merchant_shortcode: String,

    #[serde(rename = "version")]
    version: String,

    #[serde(rename = "timestamp")]
    timestamp: String,

    #[serde(skip_serializing_if = "Option::is_none")]
    #[serde(rename = "signature")]
    signature: Option<String>,
}

//--------------------------- INQUIRY

#[derive(Serialize, Deserialize, Debug)]
struct InquiryBody {
    #[serde(rename = "paymentRequestId")]
    pub payment_request_id: String,
}

#[derive(Serialize, Deserialize, Debug)]
struct InquirySignature {
    #[serde(rename = "paymentRequestId")]
    pub payment_request_id: String,
}

#[derive(Serialize, Deserialize, Debug)]
struct InquiryApiRequest {
    #[serde(rename = "verify")]
    pub verify: DirectDebitRequestHead,
    #[serde(rename = "params")]
    pub params: InquiryBody,
}

pub async fn query_bkash_direct_debit_payment(
    config: &BkashDirectDebitConfig,
    payment_request_id: &str,
) -> anyhow::Result<String> {
    let now: DateTime<Utc> = Utc::now();
    let header_for_signature = DirectDebitRequestHead {
        version: "1.0.0".to_string(),
        timestamp: now.format(TIMESTAMP_FORMATTER).to_string(),
        merchant_shortcode: config.merchant_shortcode.to_string(),
        signature: None,
    };

    let inquiry_signature = InquirySignature {
        payment_request_id: payment_request_id.to_string(),
    };

    let inquiry_body = InquiryBody {
        payment_request_id: payment_request_id.to_string(),
    };

    let inquiry_body_serialized = serde_json::to_string(&inquiry_signature)?;
    let inquiry_signature_serialized = serde_json::to_string(&header_for_signature)?;

    let signature = generate_signature(
        &config.private_key,
        &generate_data_for_signature(&inquiry_signature_serialized, &inquiry_body_serialized),
    )?;

    let header = DirectDebitRequestHead {
        signature: Some(signature.clone()),
        ..header_for_signature
    };

    let request = serde_json::to_string(&InquiryApiRequest {
        verify: header,
        params: inquiry_body,
    })?;

    let client = reqwest::Client::new();

    let response = client
        .post(format!("{}/payments/inquiryPayment", config.api_endpoint))
        .body(request)
        .header("request-time", now.format(TIMESTAMP_FORMATTER).to_string())
        .header("signature", format!("signature={signature}"))
        .header("client-id", "Default")
        .send()
        .await?;

    let response_text: String = response.text().await?;

    Ok(response_text)
}
