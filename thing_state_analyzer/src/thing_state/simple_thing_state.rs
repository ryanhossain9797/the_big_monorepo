use std::{collections::HashMap, sync::Arc};

use super::ThingState;
use regex::*;

const SIMPLE_THING_STATE: &str =
    "public\\s+static\\s+readonly\\s+SimpleThingState\\s+(?P<name>[a-zA-Z]+)\\s*=\\s*new\\s+SimpleThingState\\s*\\(\\s*(?P<value>.+)\\s*\\);";

const LONG_SHIFTED_STATE: &str = "(?P<base>1)L?\\s*<<\\s*(?P<shift>\\d+)";
const LONG_REGULAR_STATE: &str = "(?P<base>\\d+)L?";

pub fn find_simple_states(
    content: &str,
    state_collection: &mut HashMap<String, Arc<ThingState>>,
) -> anyhow::Result<()> {
    let simple_state_regex = Regex::new(SIMPLE_THING_STATE)?;
    let regular_state_regex = Regex::new(LONG_REGULAR_STATE)?;
    let shifted_state_regex = Regex::new(LONG_SHIFTED_STATE)?;

    let simple_states = simple_state_regex
        .captures_iter(content)
        .map(|capture| {
            let value_text = capture["value"].to_string();

            let val = if let Some(shifted_value) = shifted_state_regex.captures(&value_text) {
                let base = shifted_value["base"].parse::<u64>()?;
                let shift = shifted_value["shift"].parse::<u64>()?;
                Ok(base << shift)
            } else if let Some(regular_value) = regular_state_regex.captures(&value_text) {
                Ok(regular_value["base"].parse::<u64>()?)
            } else {
                Err(anyhow::anyhow!("state value is invalid"))
            }?;

            Ok(ThingState::SimpleThingState(
                capture["name"].to_string(),
                val,
            ))
        })
        .collect::<anyhow::Result<Vec<ThingState>>>()?;

    for simple_thing_state in simple_states {
        state_collection.insert(
            simple_thing_state.get_name().to_string(),
            Arc::new(simple_thing_state),
        );
    }

    Ok(())
}
