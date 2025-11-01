use std::str::FromStr;

use super::*;

use futures::stream::StreamExt;
use mongodb::bson::{doc, oid, Bson};
use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize)]
struct Note {
    #[serde(rename = "_id")]
    object_id: Bson,
    id: String,
    note: String,
}

///A single note
/// - `id` is the id in the database
/// - `position` is it's position in the chain
/// - `note` is the actual content
pub struct NoteModel {
    pub id: String,
    pub position: usize,
    pub note: String,
}

impl NoteModel {
    fn new<T: Into<String>>(id: T, position: usize, note: T) -> Self {
        Self {
            id: id.into(),
            position,
            note: note.into(),
        }
    }
}

///Returns all notes for the user.
pub async fn get_by_user(user_id: &str) -> anyhow::Result<Vec<NoteModel>> {
    Ok(
        //Using fold to convert the cursor into a vector of Note objects
        database::mongo::get()
            .ok_or_else(|| anyhow::anyhow!("Couldn't fetch db connection"))?
            //If db connection is successful
            .collection::<Note>("notes")
            .find(
                //Searching the 'notes' collection with the specific id
                doc! {
                    "id": user_id
                },
                None,
            )
            .await?
            //If db search is successful
            .fold(
                (vec![], 1),
                |(mut notes_list, position), note_result| async move {
                    if let Ok(document) = note_result {
                        if let Some(id) = Bson::as_object_id(&document.object_id) {
                            notes_list.push(NoteModel::new(id.to_hex(), position, document.note));
                        }
                    }
                    (notes_list, position + 1)
                },
            )
            .await
            .0, //Only the vector is needed, position not required for result
    )
}

///Adds a new note for the provided note string.
pub async fn add(user_id: &str, note: String) -> anyhow::Result<()> {
    let source = "NOTE_ADD";

    let notes = database::mongo::get()
        .ok_or_else(|| anyhow::anyhow!("Couldn't fetch db connection"))?
        .collection("notes");
    match notes
        .insert_one(doc! {"id":user_id, "note": note.as_str()}, None)
        .await
    {
        Ok(_) => {
            info!(source, "successful insertion");
            Ok(())
        }
        Err(err) => {
            error!(source, "{}", err);
            Err(err.into())
        }
    }
}

///Removes the note for the provided user and the provided note id.
pub async fn delete_note(user_id: &str, note_id: &str) -> anyhow::Result<()> {
    let source = "NOTE_DELETE";

    let notes = database::mongo::get()
        .ok_or_else(|| anyhow::anyhow!("Couldn't fetch db connection"))?
        .collection::<Note>("notes");

    match notes
        .delete_one(
            doc! {"_id": oid::ObjectId::from_str(note_id).map_err(|err| anyhow::anyhow!(format!("Invalid note id {}", err)))?, "id":user_id},
            None,
        )
        .await
    {
        Ok(_) => {
            info!(source, "successful delete");
            Ok(())
        }
        Err(err) => {
            error!(source, "{}", err);
            Err(err.into())
        }
    }
}
