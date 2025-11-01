///Makes a simple get request to the provided url.  
///Return an `Option<serde_json::Value>`
#[allow(dead_code)]
pub async fn get_request_json(url: &str) -> anyhow::Result<serde_json::Value> {
    serde_json::from_str(reqwest::get(url).await?.text().await?.as_str())
        .map_err(|err| anyhow::anyhow!(err))
}
