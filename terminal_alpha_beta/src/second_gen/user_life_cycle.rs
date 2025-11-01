pub mod user_action;
pub mod user_env;
pub mod user_remote_operations;
pub mod user_state;

use super::*;
use crate::handlers::responses::load_reply_from;
use async_std::sync::Mutex;
use async_std::task::{self, JoinHandle};
use core::future::Future;
use futures::channel::oneshot::{self, Receiver, Sender};
use handlers::*;
use once_cell::sync::Lazy;
use std::convert::Into;
use std::ops::Add;
use std::pin::Pin;
use std::time::Instant;
use std::{sync::Arc, time::Duration};
use user_action::*;
use user_env::*;
use user_state::*;
use util::*;

pub fn initialize() {
    user_state::initialize();
}

enum ScheduledActionType {
    Action(UserAction),
    ClearState,
}

struct Scheduled {
    action: ScheduledActionType,
    wait_till: Instant,
}

pub struct ScheduledStarter(Sender<Instant>);

impl ScheduledStarter {
    pub fn start(self) {
        let now = Instant::now();
        let ScheduledStarter(starter) = self;
        let _ = starter.send(now);
    }
}
pub struct ScheduledCanceller(JoinHandle<()>);

impl ScheduledCanceller {
    pub async fn cancel(self) {
        let ScheduledCanceller(canceller) = self;
        canceller.cancel().await;
    }
}

impl Scheduled {
    fn scheduled_action(run_at: Instant, action: UserAction) -> Self {
        Scheduled {
            action: ScheduledActionType::Action(action),
            wait_till: run_at,
        }
    }

    fn clear_state(run_at: Instant) -> Self {
        Scheduled {
            action: ScheduledActionType::ClearState,
            wait_till: run_at,
        }
    }

    fn run(self, id: String) -> (ScheduledStarter, ScheduledCanceller) {
        let (tx, rx) = oneshot::channel::<Instant>();

        let task = async move {
            let start_result = rx.await;
            match start_result {
                Ok(now) => {
                    let wait = self
                        .wait_till
                        .checked_duration_since(now)
                        .unwrap_or(Duration::ZERO);

                    task::sleep(wait).await;
                    match self.action {
                        ScheduledActionType::Action(action) => {
                            let ctx = Ctx::new(&ENV);
                            act(ctx, id, action);
                        }
                        ScheduledActionType::ClearState => {
                            delete_state(&id);
                        }
                    }
                }
                Err(_) => (),
            }
        };
        (
            ScheduledStarter(tx),
            ScheduledCanceller(async_std::task::spawn(task)),
        )
    }
}

///First place to handle messages after distribution
pub async fn handle_update(
    _message: Message,
    conversation: Box<dyn Conversation>,
    processed_text: String,
) {
    let source = "HANDLER_SECOND_GEN";

    info!(source, "Processed text is {}", processed_text);

    //If record from user exists (A Some(record)), some conversation is ongoing
    //So will be replied regardless of groups or mentions and stuff ('will_respond' is ignored)

    let conversation = Arc::new(conversation);

    let ctx = Ctx::new(&ENV);
    let id = conversation.get_id();

    let record = user_state::get_state(&id);
    let state = &record.read().await.current_state;

    let action_result = super::intent::detect(&conversation, state, &processed_text);

    match action_result {
        Some(action) => {
            act(ctx, id, action);
        }
        None => {
            let action = UserAction::Invalid(Arc::clone(&conversation));

            act(ctx, id, action);
        }
    }
}

pub fn act(ctx: Ctx, id: String, action: UserAction) {
    async_std::task::spawn(user_life_cycle(ctx, id, action));
}

async fn user_life_cycle(ctx: Ctx, id: String, action: UserAction) {
    let source = "USER_LIFE_CYCLE";

    let now = Instant::now();

    let record = user_state::get_state(&id);
    let record = record.write().await;
    let user_state = &record.current_state;

    let transition_result =
        transition(id.clone(), now, ctx, user_state, &action).map(|user_transition_output| {
            let scheduled_actions = scheduled_actions(&id, &user_transition_output.user_state);
            (user_transition_output, scheduled_actions)
        });

    match transition_result {
        Ok((user_transition_output, scheduled_actions)) => {
            let reply_count = user_transition_output.replies.len();
            info!(source, "replies: {reply_count}");

            user_transition_output
                .replies
                .into_iter()
                .for_each(|(conversation, reply)| {
                    drop(async_std::task::spawn(async move {
                        drop(conversation.send_message(reply).await);
                    }));
                });

            match user_transition_output.side_effect {
                Some(side_effect) => run_side_effect(id.clone(), side_effect).await,
                None => (),
            }

            info!(source, "scheduled_actions: {}", scheduled_actions.len());
            let scheduled_actions: Vec<(ScheduledStarter, ScheduledCanceller)> = scheduled_actions
                .into_iter()
                .map(|scheduled| scheduled.run(id.clone()))
                .collect();

            let (scheduled_starters, scheduled_cancellers): (Vec<_>, Vec<_>) =
                scheduled_actions.into_iter().unzip();

            let user_state_with_data = UserStateWithData {
                current_state: user_transition_output.user_state,
                last_active_on: now,
                scheduled_cancellers,
            };

            user_state::set_state(user_state_with_data, record).await;

            scheduled_starters
                .into_iter()
                .for_each(ScheduledStarter::start);
        }
        Err(error) => error!(source, "{error}"),
    }
}

async fn run_side_effect(
    id: String,
    side_effect: Pin<Box<dyn Future<Output = Option<UserAction>> + Send>>,
) {
    let action = side_effect.await;
    match action {
        Some(action) => {
            let ctx = Ctx::new(&ENV);
            act(ctx, id, action);
        }
        None => (),
    }
}

fn transition(
    id: String,
    now: Instant,
    ctx: Ctx,
    state: &UserState,
    action: &UserAction,
) -> anyhow::Result<UserTransitionOutput> {
    let source = "TRANSITION";
    let now = Instant::now();
    info!(source, "action: {action}");
    match (state, action) {
        //----------------------------------------------------Single Queries
        (
            UserState::AnsweringSingleQuery { .. } | UserState::Default { .. },
            UserAction::Conversational(conversation, conversational_action),
        ) => {
            let state = UserState::AnsweringSingleQuery {
                conversation: Arc::clone(conversation),
                expire_on: now.add(Duration::from_secs(20)),
            };
            info!(source, "Set state to {state}");

            let reply_key = match conversational_action {
                UserConversationalAction::Greet => "chat-greet",
                UserConversationalAction::About => "chat-about",
                UserConversationalAction::Technology => "chat-technology",
                UserConversationalAction::Creator => "chat-creator",
            };

            let reply_text = load_reply_from(&ctx, reply_key);
            let reply: ReplyConfig = reply_text.into();
            let transition_result =
                UserTransitionOutput::new(state, vec![(Arc::clone(conversation), reply)], None);

            Ok(transition_result)
        }
        //----------------------------------------------------Invalid Single Query
        (
            UserState::AnsweringSingleQuery { .. } | UserState::Default { .. },
            UserAction::Invalid(conversation),
        ) => {
            let state = UserState::AnsweringSingleQuery {
                conversation: Arc::clone(conversation),
                expire_on: now.add(Duration::from_secs(20)),
            };
            info!(source, "Set state to {state}");

            let reply_text = load_reply_from(&ctx, "unknown-question");
            let reply: ReplyConfig = reply_text.into();
            let transition_result =
                UserTransitionOutput::new(state, vec![(Arc::clone(conversation), reply)], None);

            Ok(transition_result)
        }
        //----------------------------------------------------Fetching Notes
        (
            UserState::Default { .. } | UserState::AnsweringSingleQuery { .. },
            UserAction::Notes(conversation),
        ) => {
            let state = UserState::Notes(NoteState::FetchingNotes {
                conversation: Arc::clone(conversation),
                expire_on: now.add(Duration::from_secs(20)),
            });
            let transition_result = UserTransitionOutput::new(
                state,
                Vec::new(),
                Some(Box::pin(user_remote_operations::get_notes(id))),
            );

            Ok(transition_result)
        }
        //----------------------------------------------------Awaiting Action On Notes
        (
            UserState::Notes(NoteState::FetchingNotes { conversation, .. }),
            UserAction::NoteAction(NoteAction::Fetched(note_result)),
        ) => {
            let state = UserState::Notes(NoteState::AwaitingAction {
                conversation: Arc::clone(conversation),
                expire_on: now.add(Duration::from_secs(20)),
            });
            info!(source, "Set state to {state}");

            match note_result {
                Ok(note_message) => {
                    let reply_text = load_reply_from(&ctx, "notes-start");
                    let reply: ReplyConfig = format!("{reply_text}\n{note_message}").into();
                    let transition_result = UserTransitionOutput::new(
                        state,
                        vec![(Arc::clone(conversation), reply)],
                        None,
                    );

                    Ok(transition_result)
                }
                Err(reason) => {
                    error!(source, "{reason}");
                    let reply_text = load_reply_from(&ctx, "notes-fail");
                    let reply: ReplyConfig = reply_text.into();
                    let transition_result = UserTransitionOutput::new(
                        state,
                        vec![(Arc::clone(conversation), reply)],
                        None,
                    );

                    Ok(transition_result)
                }
            }
        }
        //----------------------------------------------------Add Note
        (
            UserState::Notes(NoteState::AwaitingAction { .. }),
            UserAction::NoteAction(NoteAction::Add(conversation, note)),
        ) => {
            let state = UserState::Notes(NoteState::Adding {
                conversation: Arc::clone(conversation),
                note: note.to_string(),
                expire_on: now.add(Duration::from_secs(20)),
            });
            info!(source, "Set state to {state}");
            let reply_text = load_reply_from(&ctx, "notes-add");

            let reply: ReplyConfig = reply_text.into();
            let transition_result = UserTransitionOutput::new(
                state,
                vec![(Arc::clone(conversation), reply)],
                Some(Box::pin(user_remote_operations::add_note(
                    id,
                    note.to_string(),
                ))),
            );

            Ok(transition_result)
        }
        //----------------------------------------------------Delete Note
        (
            UserState::Notes(NoteState::AwaitingAction { .. }),
            UserAction::NoteAction(NoteAction::Delete(conversation, note_number)),
        ) => {
            let state = UserState::Notes(NoteState::Deleting {
                conversation: Arc::clone(conversation),
                note_number: *note_number,
                expire_on: now.add(Duration::from_secs(20)),
            });
            info!(source, "Set state to {state}");
            let reply_text = load_reply_from(&ctx, "notes-delete");

            let reply: ReplyConfig = reply_text.into();
            let transition_result = UserTransitionOutput::new(
                state,
                vec![(Arc::clone(conversation), reply)],
                Some(Box::pin(user_remote_operations::delete_note(
                    id,
                    *note_number,
                ))),
            );

            Ok(transition_result)
        }
        //----------------------------------------------------Add Or Delete Note Completed
        (
            UserState::Notes(
                NoteState::Adding { conversation, .. } | NoteState::Deleting { conversation, .. },
            ),
            UserAction::NoteAction(NoteAction::Completed),
        ) => {
            let state = UserState::Notes(NoteState::FetchingNotes {
                conversation: Arc::clone(conversation),
                expire_on: now.add(Duration::from_secs(20)),
            });
            info!(source, "Set state to {state}");
            let transition_result = UserTransitionOutput::new(
                state,
                Vec::new(),
                Some(Box::pin(user_remote_operations::get_notes(id))),
            );

            Ok(transition_result)
        }
        //----------------------------------------------------Invalid Note Action
        (UserState::Notes(note_state), UserAction::Invalid(conversation)) => {
            let expire_on = now.add(Duration::from_secs(20));
            let state = UserState::Notes(match note_state {
                NoteState::Adding { note, .. } => NoteState::Adding {
                    conversation: Arc::clone(conversation),
                    note: note.to_string(),
                    expire_on,
                },
                NoteState::Deleting { note_number, .. } => NoteState::Deleting {
                    conversation: Arc::clone(conversation),
                    note_number: *note_number,
                    expire_on,
                },
                NoteState::FetchingNotes { .. } => NoteState::FetchingNotes {
                    conversation: Arc::clone(conversation),
                    expire_on,
                },
                NoteState::AwaitingAction { .. } => NoteState::AwaitingAction {
                    conversation: Arc::clone(conversation),
                    expire_on,
                },
            });
            info!(source, "Set state to {state}");

            let reply_text = load_reply_from(&ctx, "unknown-question");
            let reply: ReplyConfig = reply_text.into();
            let transition_result =
                UserTransitionOutput::new(state, vec![(Arc::clone(conversation), reply)], None);

            Ok(transition_result)
        }
        //----------------------------------------------------Expire Single Query/Notes
        (
            UserState::AnsweringSingleQuery { conversation, .. }
            | UserState::Notes(
                NoteState::FetchingNotes { conversation, .. }
                | NoteState::AwaitingAction { conversation, .. }
                | NoteState::Adding { conversation, .. }
                | NoteState::Deleting { conversation, .. },
            ),
            UserAction::Expire,
        ) => {
            let state = UserState::Default {
                expire_on: now.add(Duration::from_secs(20)),
            };
            info!(source, "Set state to {state}");

            let reply_text = load_reply_from(&ctx, "delay-notice");
            let reply: ReplyConfig = reply_text.into();
            let transition_result =
                UserTransitionOutput::new(state, vec![(Arc::clone(conversation), reply)], None);

            Ok(transition_result)
        }
        _ => Err(anyhow::anyhow!("Action not valid for this state")),
    }
}

fn scheduled_actions(id: &str, user_state: &UserState) -> Vec<Scheduled> {
    let id = id.to_string();
    match user_state {
        UserState::Default { expire_on } => {
            vec![Scheduled::clear_state(*expire_on)]
        }
        UserState::AnsweringSingleQuery { expire_on, .. }
        | UserState::Notes(NoteState::AwaitingAction { expire_on, .. }) => {
            vec![Scheduled::scheduled_action(*expire_on, UserAction::Expire)]
        }
        _ => Vec::new(),
    }
}
