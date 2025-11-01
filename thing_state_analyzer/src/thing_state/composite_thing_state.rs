use std::collections::HashMap;
use std::sync::Arc;

use super::ThingState;
use regex::*;

const COMPOSITE_THING_STATE: &str =
    "public\\s+static\\s+readonly\\s+ThingState\\s+(?P<name>[a-zA-Z]+)\\s*=\\s*(?P<composing_states>[a-zA-Z]+(\\s*\\|\\s*[a-zA-Z]+)*);";

const COMPOSING_STATE_SPLITTER: &str = "\\s*\\|\\s*";

pub fn find_composite_states(
    content: &str,
    state_collection: &mut HashMap<String, Arc<ThingState>>,
) -> anyhow::Result<()> {
    let composite_state_regex = Regex::new(COMPOSITE_THING_STATE)?;

    composite_state_regex
        .captures_iter(content)
        .try_for_each(|capture| {
            let state_names = get_state_names(&capture["composing_states"])?;

            let composing_states = state_names
                .into_iter()
                .map(|name| {
                    let composing_state = state_collection
                        .get(&name)
                        .ok_or_else(|| anyhow::anyhow!("not found"))?;

                    Ok(Arc::clone(composing_state))
                })
                .collect::<anyhow::Result<Vec<Arc<ThingState>>>>()?;

            state_collection.insert(
                capture["name"].to_string(),
                Arc::new(ThingState::CompositeThingState(
                    capture["name"].to_string(),
                    composing_states,
                )),
            );
            anyhow::Result::<()>::Ok(())
        })?;

    Ok(())
}

pub fn get_state_names(composing_states_string: &str) -> anyhow::Result<Vec<String>> {
    let splitter_regex = Regex::new(COMPOSING_STATE_SPLITTER)?;

    Ok(splitter_regex
        .replace_all(composing_states_string, ",")
        .split(',')
        .map(|name| ToString::to_string(&name))
        .collect())
}
