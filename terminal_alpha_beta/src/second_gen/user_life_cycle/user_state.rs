use super::*;
use async_std::sync::{Mutex, RwLock, RwLockWriteGuard};
use async_std::task;

use dashmap::mapref::entry::VacantEntry;
use dashmap::mapref::one::Ref;
use dashmap::DashMap;
use once_cell::sync::Lazy;
use std::fmt;
use std::time::Instant;
pub use user_state_model::{NoteState, UserState, UserStateWithData, UserTransitionOutput};

static RECORDS: Lazy<DashMap<String, Arc<RwLock<UserStateWithData>>>> = Lazy::new(DashMap::new);

pub fn initialize() {
    Lazy::force(&RECORDS);
}

mod user_state_model {
    use futures::Future;

    use super::*;
    ///A user state record holds an individual user's state.
    ///Last holds when it was last updated.
    pub struct UserStateWithData {
        pub current_state: UserState,
        pub last_active_on: Instant,
        pub scheduled_cancellers: Vec<ScheduledCanceller>,
    }
    impl UserStateWithData {
        pub fn new(state: UserState, last: Instant) -> Self {
            Self {
                current_state: state,
                last_active_on: last,
                scheduled_cancellers: Vec::new(),
            }
        }

        pub fn current_state(&self) -> &UserState {
            &self.current_state
        }

        pub fn last_active_on(&self) -> &Instant {
            &self.last_active_on
        }
    }

    pub struct UserTransitionOutput {
        pub user_state: UserState,
        pub replies: Vec<(Arc<Box<dyn Conversation>>, ReplyConfig)>,
        pub side_effect: Option<Pin<Box<dyn Future<Output = Option<UserAction>> + Send>>>,
    }
    impl UserTransitionOutput {
        pub fn new(
            user_state: UserState,
            replies: Vec<(Arc<Box<dyn Conversation>>, ReplyConfig)>,
            side_effect: Option<Pin<Box<dyn Future<Output = Option<UserAction>> + Send>>>,
        ) -> Self {
            Self {
                user_state,
                replies,
                side_effect,
            }
        }
    }

    pub enum NoteState {
        FetchingNotes {
            conversation: Arc<Box<dyn Conversation>>,
            expire_on: Instant,
        },
        AwaitingAction {
            conversation: Arc<Box<dyn Conversation>>,
            expire_on: Instant,
        },
        Adding {
            conversation: Arc<Box<dyn Conversation>>,
            note: String,
            expire_on: Instant,
        },
        Deleting {
            conversation: Arc<Box<dyn Conversation>>,
            note_number: usize,
            expire_on: Instant,
        },
    }

    impl NoteState {
        fn conversation(&self) -> &Arc<Box<dyn Conversation>> {
            match self {
                NoteState::FetchingNotes { conversation, .. }
                | NoteState::AwaitingAction { conversation, .. }
                | NoteState::Adding { conversation, .. }
                | NoteState::Deleting { conversation, .. } => &conversation,
            }
        }
    }

    pub enum UserState {
        AnsweringSingleQuery {
            conversation: Arc<Box<dyn Conversation>>,
            expire_on: Instant,
        },
        Default {
            expire_on: Instant,
        },
        Notes(NoteState),
    }

    impl fmt::Display for UserState {
        fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
            match self {
                UserState::AnsweringSingleQuery { .. } => write!(f, "AnsweringSingleQuery"),
                UserState::Default { .. } => write!(f, "Default"),
                UserState::Notes { .. } => write!(f, "Notes"),
            }
        }
    }
}

///Returns the state of the Provided user
pub fn get_state(id: &str) -> Arc<RwLock<UserStateWithData>> {
    let record = RECORDS.entry(id.to_string()).or_insert_with(|| {
        let expire_on = Instant::now() + Duration::from_secs(20);
        let (scheduled_starter, scheduled_canceller) =
            Scheduled::clear_state(expire_on).run(id.to_string());

        let default_state = Arc::new(RwLock::new(UserStateWithData {
            scheduled_cancellers: vec![scheduled_canceller],
            last_active_on: Instant::now(),
            current_state: UserState::Default { expire_on },
        }));

        scheduled_starter.start();
        default_state
    });

    Arc::clone(&*record)
}
///Returns the state of the Provided user
pub async fn set_state(
    mut state: UserStateWithData,
    mut previous_state: RwLockWriteGuard<'_, UserStateWithData>,
) {
    std::mem::swap(&mut state, &mut previous_state);

    let old_state = state;

    let cancellers = old_state.scheduled_cancellers;
    futures::future::join_all(cancellers.into_iter().map(ScheduledCanceller::cancel)).await;
}

///Remove the Provided user's state
// async fn delete_state(id: &str) {
//     RECORDS.remove(id);
// }

///Remove the Provided user's state
pub fn delete_state(id: &str) {
    let source = "DELETE_STATE";
    info!(source, "Deleting state for {id}");
    RECORDS.remove(id);
}
