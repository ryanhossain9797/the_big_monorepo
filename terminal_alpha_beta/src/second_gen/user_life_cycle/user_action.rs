use super::{user_state::UserState, *};
use std::fmt;

#[derive(Clone)]
pub enum UserConversationalAction {
    Greet,
    About,
    Technology,
    Creator,
}

#[derive(Clone)]
pub enum NoteAction {
    Fetched(Result<String, String>),
    Add(Arc<Box<dyn Conversation>>, String),
    Delete(Arc<Box<dyn Conversation>>, usize),
    Completed,
}

///A user state record holds an individual user's state.
#[derive(Clone)]
pub enum UserAction {
    Conversational(Arc<Box<dyn Conversation>>, UserConversationalAction),
    Notes(Arc<Box<dyn Conversation>>),
    NoteAction(NoteAction),
    Invalid(Arc<Box<dyn Conversation>>),
    Expire,
}
impl fmt::Display for UserAction {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        write!(
            f,
            "{}",
            match *self {
                UserAction::Conversational(..) => "Conversational",
                UserAction::Notes(..) => "Notes",
                UserAction::NoteAction(_) => "NoteAction",
                UserAction::Expire => "Expire",
                UserAction::Invalid(..) => "Invalid",
            }
        )
    }
}
