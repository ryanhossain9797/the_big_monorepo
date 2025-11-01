use super::*;
use async_std::fs::OpenOptions;
use async_std::prelude::*;

pub async fn _download_file(url: &str, name: &str) -> Option<String> {
    let source = "DOWNLOAD_FILE";

    if let Ok(response) = reqwest::get(url).await {
        let path: &str = &format!("temp/{}.gif", name);
        match OpenOptions::new().write(true).create(true).open(path).await {
            Ok(mut file) => {
                if let Ok(data) = response.bytes().await.as_ref() {
                    if file.write_all(data).await.is_ok() {
                        return Some(path.to_string());
                    }
                }
            }
            Err(err) => error!(source, "couldn't open file because: {}", err),
        }
    }
    None
}
