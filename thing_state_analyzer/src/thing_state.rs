use colored::*;
use std::collections::HashMap;
use std::sync::Arc;

mod composite_thing_state;
mod simple_thing_state;

pub enum ThingState {
    SimpleThingState(String, u64),
    CompositeThingState(String, Vec<Arc<ThingState>>),
}

impl ThingState {
    fn get_name(&self) -> &str {
        match self {
            ThingState::SimpleThingState(name, _) | ThingState::CompositeThingState(name, _) => {
                name
            }
        }
    }

    pub fn print_indented(&self, indent: usize) {
        let indent_text = match indent {
            0 => "".to_string(),
            1 => "├───".to_string(),
            n => {
                format!("{}{}", "│   ".repeat(n - 1), "├───")
            }
        }
        .green();

        match self {
            ThingState::SimpleThingState(name, value) => {
                println!(
                    "{}{} {} {}",
                    indent_text,
                    name.purple().bold(),
                    "=>".green(),
                    value.to_string().bright_cyan().bold()
                );
            }
            ThingState::CompositeThingState(name, composing_states) => {
                println!("{}{}", indent_text, name.blue().bold());
                for composing_state in composing_states {
                    composing_state.print_indented(indent + 1);
                }
            }
        }
    }
}

pub fn find_all_states(content: &str) -> anyhow::Result<HashMap<String, Arc<ThingState>>> {
    let mut thing_states = HashMap::new();

    simple_thing_state::find_simple_states(content, &mut thing_states)?;
    composite_thing_state::find_composite_states(content, &mut thing_states)?;

    Ok(thing_states)
}
