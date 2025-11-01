//Using a simple actor to keep track of the rate limits
//
//The channel to the actor ensures there is no race condition
//
//There is a rolling window (window size 20s for faster test)
//
//Rolling window implementation could be improved with queues
//or some other way, but not done to avoid pullin in too many external libs

use std::{collections::BTreeMap, time::Duration};

use chrono::Utc;
use tokio::sync::*;

const DEFAULT_RATE_LIMIT_SECONDS: usize = 5;
const RATE_LIMIT_WINDOW_SECONDS: u64 = 20; //for the sake of tests, can be set to 60

#[derive(Eq, Ord, PartialEq, PartialOrd, Clone)]
enum EndpointName {
    AddNums,
}

struct RateLimitStatus {
    max: usize,
    hits: Vec<i64>, //microseconds stored instead of any time type
}

fn maybe_add_default_rate_limit(
    rate_limit_statuses: &mut BTreeMap<EndpointName, RateLimitStatus>,
    endpoint_name: &EndpointName,
) {
    if !(rate_limit_statuses.contains_key(endpoint_name)) {
        rate_limit_statuses.insert(
            endpoint_name.clone(),
            RateLimitStatus {
                max: DEFAULT_RATE_LIMIT_SECONDS,
                hits: Vec::new(),
            },
        );
    }
}

fn remove_expired_hits(rate_limit_status: &mut RateLimitStatus, now: i64) {
    rate_limit_status.hits = rate_limit_status
        .hits
        .iter()
        .cloned()
        .filter(|expiry| *expiry > now)
        .collect();
}

async fn rate_limiter(
    mut receiver: mpsc::Receiver<(EndpointName, oneshot::Sender<bool>)>,
    rate_limits: BTreeMap<EndpointName, usize>,
) {
    let mut rate_limit_statuses = BTreeMap::<EndpointName, RateLimitStatus>::new();

    //initialize overridden rate limits
    rate_limits.into_iter().for_each(|(key, limit)| {
        rate_limit_statuses.insert(
            key,
            RateLimitStatus {
                max: limit,
                hits: Vec::new(),
            },
        );
    });

    // wait for rate limit checks
    while let Some((endpoint_name, response_channel)) = receiver.recv().await {
        maybe_add_default_rate_limit(&mut rate_limit_statuses, &endpoint_name);

        let rate_limit_status = rate_limit_statuses.get_mut(&endpoint_name).unwrap();

        let now = Utc::now();
        let expires_at = (now + Duration::from_secs(RATE_LIMIT_WINDOW_SECONDS)).timestamp_micros();
        let now = now.timestamp_micros();

        remove_expired_hits(rate_limit_status, now);

        let rate_limit_exceeded = rate_limit_status.hits.len() + 1 > rate_limit_status.max;

        if !rate_limit_exceeded {
            rate_limit_status.hits.push(expires_at);
        }
        let _ = response_channel.send(!rate_limit_exceeded);
    }
}

struct App {
    rate_limiter: mpsc::Sender<(EndpointName, oneshot::Sender<bool>)>,
}

impl App {
    pub fn new(rate_limits: BTreeMap<EndpointName, usize>) -> Self {
        let (sender, receiver) = mpsc::channel::<(EndpointName, oneshot::Sender<bool>)>(100);

        //Start the rate limiter actor
        tokio::spawn(rate_limiter(receiver, rate_limits));

        let app = App {
            rate_limiter: sender,
        };

        app
    }

    async fn rate_limit_check(&self, endpoint: EndpointName) -> bool {
        let (sender, receiver) = oneshot::channel();
        let _ = self.rate_limiter.send((endpoint, sender)).await;

        receiver.await.unwrap()
    }

    pub async fn add_nums(&self, a: usize, b: usize) -> Result<usize, String> {
        if !self.rate_limit_check(EndpointName::AddNums).await {
            return Err("Too many requests".to_string());
        }

        Ok(a + b)
    }
}

fn main() {}

#[tokio::test]
async fn test_rate_limit() {
    test_rate_limit_impl().await
}

async fn test_rate_limit_impl() {
    let mut limits = BTreeMap::new();
    limits.insert(EndpointName::AddNums, 3);

    let app = App::new(limits);

    assert!(app.add_nums(1, 2).await.is_ok());
    tokio::time::sleep(core::time::Duration::from_secs(5)).await;
    assert!(app.add_nums(2, 3).await.is_ok());
    tokio::time::sleep(core::time::Duration::from_secs(5)).await;
    assert!(app.add_nums(3, 4).await.is_ok());

    // limit 3 hit
    tokio::time::sleep(core::time::Duration::from_secs(5)).await;
    assert!(app.add_nums(3, 4).await.is_err());

    // after 1 hit cleared
    tokio::time::sleep(core::time::Duration::from_secs(5)).await;
    assert!(app.add_nums(4, 5).await.is_ok());

    // limit 3 hit again
    tokio::time::sleep(core::time::Duration::from_secs(1)).await;
    assert!(app.add_nums(5, 6).await.is_err());

    // limit not yet cleared
    tokio::time::sleep(core::time::Duration::from_secs(1)).await;
    assert!(app.add_nums(5, 6).await.is_err());

    // after 1 hit cleared
    tokio::time::sleep(core::time::Duration::from_secs(3)).await;
    assert!(app.add_nums(5, 6).await.is_ok());

    // limit 3 hit again
    tokio::time::sleep(core::time::Duration::from_secs(1)).await;
    assert!(app.add_nums(5, 6).await.is_err());
}
