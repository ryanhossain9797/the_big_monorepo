use std::sync::Arc;

use crate::{
    handlers::Conversation, second_gen::user_life_cycle::user_action::UserConversationalAction,
};

use super::{user_life_cycle::user_state::UserState, *};

use async_std::sync::RwLockReadGuard;
use user_life_cycle::user_action::{NoteAction, UserAction};

fn parse(context_state: &UserState, processed_text: &str) -> Option<(f32, &'static str)> {
    match context_state {
        &UserState::Default { .. } | &UserState::AnsweringSingleQuery { .. } => {
            if processed_text.contains("hey") {
                Some((1.0, "greet"))
            } else if processed_text.contains("creator") {
                Some((1.0, "creator"))
            } else if processed_text.contains("about") {
                Some((1.0, "about"))
            } else if processed_text.contains("technology") {
                Some((1.0, "technology"))
            } else if processed_text.contains("notes") {
                Some((1.0, "notes"))
            } else {
                None
            }
        }
        UserState::Notes { .. } => {
            if processed_text.starts_with("add") {
                let (confidence_score, intent_name) = (1.0, "add_note");
                Some((confidence_score, intent_name))
            } else if processed_text.starts_with("delete") {
                let (confidence_score, intent_name) = (1.0, "delete_note");
                Some((confidence_score, intent_name))
            } else {
                None
            }
        }
    }
}

///Uses natural understanding to determine intent if no state is found
pub fn detect(
    conversation: &Arc<Box<dyn Conversation>>,
    context_state: &UserState,
    processed_text: &str,
) -> Option<UserAction> {
    let source = "NATURAL_ACTION_PICKER";

    parse(context_state, processed_text).and_then(|(confidence, intent_name)| {
        if confidence > 0.5 {
            info!(source, "intent is {}", intent_name);

            match intent_name {
                "greet" => Some(UserAction::Conversational(
                    Arc::clone(conversation),
                    UserConversationalAction::Greet,
                )),
                "about" => Some(UserAction::Conversational(
                    Arc::clone(conversation),
                    UserConversationalAction::About,
                )),
                "technology" => Some(UserAction::Conversational(
                    Arc::clone(conversation),
                    UserConversationalAction::Technology,
                )),
                "creator" => Some(UserAction::Conversational(
                    Arc::clone(conversation),
                    UserConversationalAction::Creator,
                )),
                "notes" => Some(UserAction::Notes(Arc::clone(conversation))),
                "add_note" => {
                    let note = processed_text.trim_start_matches("add ").trim().to_string();
                    Some(UserAction::NoteAction(NoteAction::Add(
                        Arc::clone(conversation),
                        note,
                    )))
                }
                "delete_note" => processed_text
                    .trim_start_matches("delete ")
                    .trim()
                    .parse::<usize>()
                    .ok()
                    .and_then(|num| match num > 0 {
                        true => Some(num - 1),
                        false => None,
                    })
                    .map(|note_number| {
                        UserAction::NoteAction(NoteAction::Delete(
                            Arc::clone(conversation),
                            note_number,
                        ))
                    }),
                _ => None, //TODO
            }
        } else {
            None
        }
    })
}
