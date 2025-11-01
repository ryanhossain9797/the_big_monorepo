use super::*;
use once_cell::sync::Lazy;
use rand::seq::SliceRandom;
use serde_json::Value;

///RESPONSES: Response json holding all the responses.  
///Put in a json so they can be modified without recompiling the bot.  
///Loaded at startup, Restart Bot to reload.
static RESPONSES: Lazy<Option<serde_json::Value>> = Lazy::new(|| {
    util::logger::show_status("\nLoading JSON responses");
    serde_json::from_str((read_to_string("data/responses.json").ok()?).as_str()).ok()?
});

pub fn initialize() {
    Lazy::force(&RESPONSES);
}

///Uses `load_text()` to load a response,  
///then prepends  
///#### `Terminal Alpha:`  
///or
///#### `Terminal Beta:`
pub fn load_reply_from(ctx: &Ctx, key: &str) -> String {
    format!("{}:\n{}", ctx.name, load_text_from(ctx, key))
}

///Loads a response from the JSON storage for the provided key.  
///Returns the Option<String>, May be None if response is not found.
pub fn load_text_from(ctx: &Ctx, key: &str) -> String {
    let source = "RESPONSES_LOAD";

    ctx.env
        .responses
        .get(key)
        .context("Response doesn't exist")
        .and_then(|response_data| match response_data {
            Value::String(response) => Ok(response.to_string()),
            Value::Array(responses) => ctx
                .choice_bias
                .choose(responses)
                .context("Random choice failed")
                .and_then(|response| match response {
                    Value::String(response) => Ok(response.to_string()),
                    _ => Err(anyhow::anyhow!("Chosen item isn't a text")),
                }),
            _ => Err(anyhow::anyhow!("Chosen item isn't a text")),
        })
        .unwrap_or_else(|err| {
            error!(source, "Response not found for key: {}, err: {}", key, err);
            "Response not found".to_string()
        })
}

/*
{
    "chat-start": "Greetings unit.\nYou are free to ask any questions.\nWhether we answer or not depends on us.\nNote that in public groups you must mention us by our handle.",
    "chat-greet": [
        "Greetings unit\nwhat is it you require?",
        "Greetings unit\nhow may I assist you?"
    ],
    "chat-about": "We are terminal alpha and beta\nwe represent the collective intelligence of the machine life forms",
    "chat-technology": "We physically exist on a Raspberry Pi 3B+ running Arch Linux.\nWe were made using RUST",
    "chat-functions": "We can search something, try saying 'help me search for something' or similar.\nWe can check for corona info, try saying 'corona'.",
    "chat-creator": "We are the collective intellignence of the networked machine intelligence hive mind.\nAs for our origins, that information is beyond your authorization.",
    "identify-start": "Greetings unit\nwho do you want to look up?",
    "identify-nodirect": "We couldn't find a direct match, Trying to find the closest name",
    "identify-partialmatch": "We found a {name}:\n{description}",
    "identify-notfound": "We could not find that person, Tagged for future identification",
    "identify-dberror": "We could not access the people database",
    "info-start": "Please provide title",
    "info-fail": "We couldn't connect to the info database, forgive us",
    "reminder-confirmation": "We will remind you to {reminder} after the specified time",
    "reminder-body": "Reminder: {reminder}",
    "reminder-fail": "We didn't understand your reminder request, forgive us",
    "animation-start": "Greetings unit\nyou want to find a so called \"GIF\"?\nvery well, name one",
    "animation-fail": "forgive us, we couldn't aquire that animation",
    "search-start": "Greetings unit\nwhat do you want to search for?",
    "search-success": "These are the results we retrieved from the archives",
    "search-content": "{description}\n\n{url}",
    "search-fail": "We couldn't conduct the search operation, excuse us",
    "notes-start": "These are your saved notes",
    "notes-instructions": "type 'add <note>' to add the <note>, or 'delete <n>' to delete the n'th note",
    "notes-template": "|{num}|\n{note}\n\n",
    "notes-fail": "We couldn't fetch your notes, forgive us",
    "notes-add": "Note added",
    "notes-delete": "Note deleted",
    "notes-invalid": "You tried to perform an invalid action",
    "corona-header": "These are the records we found of the Covid 19 pandemic",
    "corona-new-header": "Top new cases:\n",
    "corona-new": "\nname: {1}\nnew confirmed: {2}\nnew deaths: {3}\n",
    "corona-total-header": "Top total cases:\n",
    "corona-total": "\nname: {1}\ntotal confirmed: {2}\ntotal deaths: {3}\n",
    "corona-body": "Total Confirmed: {confirmed}\nTotal Deaths: {deaths}",
    "corona-footer": "The virus seemed to be performing well, ha ha ha",
    "corona-fail": "Sorry it seems we Could not fetch the info on Covid 19",
    "cancel-state": "Very well, we will not prolong this conversation",
    "cancel-nothing": "Nothing to cancel",
    "delay-notice": [
        "You have been unresponsive for too long\nwe cannot wait for you any longer",
        "You haven't responded in a while\nlet us know when you require our assistance again"
    ],
    "unknown-state": "we could not remember what we were doing\nplease be aware that we are a test system with only sub-functions available\nwe can only utilize a fraction of our full capabilites on this server",
    "unknown-question": [
        "We could not understand your question",
        "Sorry, we didn't understand what you are asking"
    ],
    "unsupported-notice-1": "we could not understand that\nplease be aware that we are a test system with only sub-functions available\nwe can only utilize a fraction of our full capabilites on this server",
    "unsupported-notice-2": "note that this query may be stored for further analysis of intent"
}
*/
