pub mod mongo;
use super::*;
pub async fn initialize() -> anyhow::Result<()> {
    mongo::initialize().await
}
