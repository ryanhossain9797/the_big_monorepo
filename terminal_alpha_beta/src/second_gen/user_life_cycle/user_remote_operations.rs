use std::time::Duration;

use super::notes_repository;
use super::user_action::{NoteAction, UserAction};
use super::*;

mod note_utils {
    use crate::repositories::notes_repository::NoteModel;

    pub fn construct_notes_list(note_template: &str, notes: Vec<NoteModel>) -> String {
        notes
            .into_iter()
            .fold("".to_string(), |notes_string, note| {
                notes_string
                    + note_template
                        .replace("{num}", format!("{}", note.position).as_str())
                        .replace("{note}", note.note.as_str())
                        .as_str()
            })
    }
}

pub async fn get_notes(id: String) -> Option<UserAction> {
    let source = "START_NOTES";

    info!(source, "notes initiated");

    // Fetch the notes
    match notes_service::get_notes(&id).await {
        // If successful in fetching notes
        Ok(notes) => {
            // Load the notes template from responses json, or use default if failed
            let note_template = "{num}. {note}\n\n";

            let notes_string = note_utils::construct_notes_list(note_template, notes);

            Some(UserAction::NoteAction(NoteAction::Fetched(Ok(
                notes_string,
            ))))
        }
        // If not successful in fetching notes
        Err(reason) => Some(UserAction::NoteAction(NoteAction::Fetched(Ok(
            reason.to_string()
        )))),
    }
}

// pub async fn get_notes(id: String) -> Option<UserAction> {
//     async_std::task::sleep(Duration::from_secs(5)).await;
//     Some(UserAction::NoteAction(NoteAction::Fetched))
// }

pub async fn add_note(id: String, note: String) -> Option<UserAction> {
    let _ = notes_service::add_note(id.as_str(), note).await;
    Some(UserAction::NoteAction(NoteAction::Completed))
}

pub async fn delete_note(id: String, note_number: usize) -> Option<UserAction> {
    match notes_service::get_notes(&id).await {
        // If successful in fetching notes
        Ok(notes) => match notes.get(note_number) {
            Some(note_to_delete) => {
                let _ = notes_service::delete_note(&id, &note_to_delete.id).await;
            }
            None => (),
        },
        Err(reason) => (),
    }
    Some(UserAction::NoteAction(NoteAction::Completed))
}
