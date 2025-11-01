use super::*;
use repositories::notes_repository;
use repositories::notes_repository::NoteModel;

///Returns all notes for the user.
pub async fn get_notes(user_id: &str) -> anyhow::Result<Vec<NoteModel>> {
    notes_repository::get_by_user(user_id)
        .await
        .map(|mut notes| {
            notes.sort_by(|note, next_note| note.id.cmp(&next_note.id));
            notes
        })
}

///Adds a new note for the provided note string.  
///Returns an updated all notes for the user including the new one.
pub async fn add_note(user_id: &str, note: String) -> anyhow::Result<Vec<NoteModel>> {
    notes_repository::add(user_id, note).await?;
    notes_repository::get_by_user(user_id).await
}

///Removes the note for the provided user and the provided note id.  
///Returns an updated all notes for the user excluding the deleted one.
pub async fn delete_note(user_id: &str, note_id: &str) -> anyhow::Result<Vec<NoteModel>> {
    notes_repository::delete_note(user_id, note_id).await?;
    notes_repository::get_by_user(user_id).await
}
