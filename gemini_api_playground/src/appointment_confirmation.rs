use crate::prepare_gemini_client;
use chrono::{NaiveDate, NaiveDateTime, NaiveTime};
use gemini_rust::{Content, FunctionCallingMode, FunctionDeclaration, Gemini, Message, Role, Tool};
use schemars::JsonSchema;
use serde::{Deserialize, Serialize};
use serde_json::json;
use std::io::{self, Write};

#[derive(Debug, Serialize, Deserialize)]
pub struct Appointment {
    patient_name: String,
    doctor_id: String,
    doctor_name: String,
    appointment_date: NaiveDate,
    appointment_time: NaiveTime,
    appointment_type: String,
    status: AppointmentStatus,
    #[serde(skip)]
    conversation_history: Vec<Message>,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "PascalCase")]
pub enum AppointmentStatus {
    Unconfirmed {
        datetime: NaiveDateTime,
    },
    Confirmed {
        datetime: NaiveDateTime,
    },
    ManualIntervention {
        last_known_time: NaiveDateTime,
        approximate_time: String,
    },
    Cancelled,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(tag = "decision_type", rename_all = "PascalCase")]
pub enum AppointmentDecision {
    Undecided,
    Confirm {
        #[serde(default)]
        new_datetime: Option<String>,
    },
    Cancel,
    ManualIntervention {
        #[serde(default)]
        approximate_time: String,
    },
}

#[derive(Debug, Serialize, Deserialize)]
pub struct LLMResponse {
    message: String,
    decision: AppointmentDecision,
}

// BEGIN Tool call stuff
#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema)]
struct GetAvailableSlotsParams {
    doctor_id: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema)]
struct AvailableSlotsResponse {
    available_slots: Vec<String>,
}

fn build_get_available_slots_tool() -> Tool {
    let function = FunctionDeclaration::new(
        "get_available_appointment_slots",
        "Get all available appointment slots for a specific doctor. Use this when a patient wants to reschedule.",
        None,
    )
    .with_parameters::<GetAvailableSlotsParams>()
    .with_response::<AvailableSlotsResponse>();

    Tool::new(function)
}

// Simulated function to get available slots
fn get_available_appointment_slots(_doctor_id: &str) -> AvailableSlotsResponse {
    // For simulation, return some available slots over the next few days
    // In a real system, this would query based on doctor_id
    let base_date = NaiveDate::from_ymd_opt(2025, 11, 16).unwrap();

    let mut slots = vec![];
    for day_offset in 0..3 {
        let date = base_date + chrono::Duration::days(day_offset);
        for hour in [9, 10, 11, 14, 15, 16] {
            for minute in [0, 30] {
                let datetime = date.and_hms_opt(hour, minute, 0).unwrap();
                slots.push(datetime.format("%Y-%m-%d %H:%M").to_string());
            }
        }
    }

    AvailableSlotsResponse {
        available_slots: slots,
    }
}
// END Tool call stuff

const SYSTEM_PROMPT: &str = r#"You are a friendly and warm automated agent at a medical clinic helping with appointment scheduling.

When writing messages to patients:
- Write in a conversational, natural tone like a caring nurse would speak
- Use casual, friendly language (e.g., "Hi!", "Thanks!", "Hope you're doing well!")
- When introducing yourself for the first time, say "I'm an automated agent from [clinic name]"
- Do NOT make up names like "[Your Name]" - you are an automated system
- If you need clarification, politely explain why you're reaching out (e.g., "I wasn't quite sure what you meant" or "Just wanted to check with you")
- Keep messages brief but warm
- Avoid overly formal or robotic language
- Use the patient's first name when appropriate
- Vary your phrasing - don't use the same patterns repeatedly in a conversation thread"#;

fn build_decision_schema() -> serde_json::Value {
    json!({
        "type": "object",
        "properties": {
            "message": {
                "type": "string",
                "description": "REQUIRED: A friendly message to send to the patient"
            },
            "decision": {
                "type": "object",
                "properties": {
                    "decision_type": {
                        "type": "string",
                        "enum": ["Undecided", "Confirm", "Cancel", "ManualIntervention"],
                        "description": "The decision type"
                    },
                    "new_datetime": {
                        "type": "string",
                        "description": "Optional for Confirm: If patient rescheduled to a new time that you verified is available, include it here in format 'YYYY-MM-DD HH:MM'"
                    },
                    "approximate_time": {
                        "type": "string",
                        "description": "REQUIRED for ManualIntervention: approximate time the patient wants (e.g., 'next week', 'in 2 days') or reason for manual intervention (e.g., 'patient requested to speak with staff')"
                    }
                },
                "required": ["decision_type"]
            }
        },
        "required": ["message", "decision"]
    })
}

fn build_initial_prompt(clinic_name: &str, appointment: &Appointment) -> String {
    let status_description = match &appointment.status {
        AppointmentStatus::Unconfirmed { datetime } => {
            format!(
                "Unconfirmed (scheduled for {})",
                datetime.format("%Y-%m-%d %H:%M")
            )
        }
        AppointmentStatus::Confirmed { datetime } => {
            format!(
                "Confirmed (scheduled for {})",
                datetime.format("%Y-%m-%d %H:%M")
            )
        }
        AppointmentStatus::ManualIntervention {
            last_known_time,
            approximate_time,
        } => {
            format!(
                "Manual intervention required (was: {}, reason: {})",
                last_known_time.format("%Y-%m-%d %H:%M"),
                approximate_time
            )
        }
        AppointmentStatus::Cancelled => "Cancelled".to_string(),
    };

    format!(
        r#"You are an AI assistant for {clinic_name} helping to process patient appointment confirmations.

Appointment Details:
- Patient: {patient_name}
- Doctor: {doctor_name} (ID: {doctor_id})
- Date: {appointment_date}
- Time: {appointment_time}
- Type: {appointment_type}
- Current Status: {status}

Based on the conversation with the patient, decide what action to take:

Decision types:
- Undecided: You need more information from the patient AND an automated LLM agent will contact them again
  * Use when you can't clearly determine their intent
  * If first contact: Introduce yourself and ask about their appointment
  * If continuing: Reference their previous response and ask follow-up
  * IMPORTANT: Only use this if you (the automated agent) can handle the next interaction
  * DO NOT use if patient requested to speak with a person or human staff
- Confirm: Patient clearly confirmed the appointment
  * Acknowledge their confirmation warmly
  * If patient selected a specific new time from the available slots, include it in "new_datetime" field
- Cancel: Patient clearly wants to cancel
  * Confirm cancellation with empathy
- ManualIntervention: Patient needs human staff assistance (NOT automated agent)
  * Use when patient explicitly asks to speak with a person, nurse, or staff member
  * Use when patient wants to reschedule but couldn't select a specific time from available slots
  * REQUIRED: Include "approximate_time" field with reason or patient's preference
  * Examples: "patient requested to speak with staff", "next week", "in 2 days"
  * Acknowledge and let them know human staff will follow up

TOOL USAGE:
- You have access to ONLY ONE tool: get_available_appointment_slots(doctor_id)
- Use this when patient wants to reschedule or asks about available times
- The tool returns a list of all available appointment slots
- Present the options to the patient in a natural, conversational way
- If patient picks one of the available slots, use Confirm decision with new_datetime (NO TOOL NEEDED - just include the datetime in your decision)
- If none work or patient is still uncertain, use Undecided or ManualIntervention as appropriate
- DO NOT call any other tools or functions - they do not exist

IMPORTANT:
- Always include a "message" field with a friendly, nurse-like message
- The conversation will continue regardless of your decision
- Vary your phrasing - don't repeat patterns
- Do NOT assume intent - use Undecided if unsure"#,
        clinic_name = clinic_name,
        patient_name = appointment.patient_name,
        doctor_id = appointment.doctor_id,
        doctor_name = appointment.doctor_name,
        appointment_date = appointment.appointment_date.format("%Y-%m-%d"),
        appointment_time = appointment.appointment_time.format("%H:%M"),
        appointment_type = appointment.appointment_type,
        status = status_description,
    )
}

async fn generate_appointment_confirmation(
    client: &Gemini,
    clinic_name: &str,
    appointment: &Appointment,
) -> Result<LLMResponse, Box<dyn std::error::Error>> {
    let schema = build_decision_schema();
    let initial_prompt = build_initial_prompt(clinic_name, appointment);
    let tool = build_get_available_slots_tool();

    let mut request = client
        .generate_content()
        .with_system_prompt(SYSTEM_PROMPT)
        .with_user_message(&initial_prompt)
        .with_tool(tool)
        .with_function_calling_mode(FunctionCallingMode::Auto);

    for msg in &appointment.conversation_history {
        request = request.with_message(msg.clone());
    }

    let response = request.execute().await?;
    let function_calls = response.function_calls();

    // Handle function calls if present (Phase 2: get conversational response)
    let conversational_text = if !function_calls.is_empty() {
        let function_call_system_prompt = format!(
            "{}\n\nIMPORTANT: Only provide the conversational message to the patient. Do NOT include any JSON, decision types, or structured data in your response. The decision will be extracted separately.",
            SYSTEM_PROMPT
        );

        let mut conversation = client
            .generate_content()
            .with_system_prompt(&function_call_system_prompt)
            .with_user_message(&initial_prompt);

        for msg in &appointment.conversation_history {
            conversation = conversation.with_message(msg.clone());
        }

        for function_call in function_calls {
            match function_call.name.as_str() {
                "get_available_appointment_slots" => {
                    let params: GetAvailableSlotsParams =
                        serde_json::from_value(function_call.args.clone())?;
                    let result = get_available_appointment_slots(&params.doctor_id);

                    let model_content =
                        Content::function_call(function_call.clone()).with_role(Role::Model);
                    conversation = conversation.with_message(Message {
                        content: model_content,
                        role: Role::Model,
                    });

                    conversation = conversation
                        .with_function_response("get_available_appointment_slots", result)?;
                }
                _ => return Err(format!("Unknown function: {}", function_call.name).into()),
            }
        }

        Some(conversation.execute().await?.text())
    } else {
        None
    };

    // Phase 3 (or Phase 1 if no tools): Extract structured decision
    let mut decision_request = client
        .generate_content()
        .with_system_prompt(&format!(
            "{}\n\nAlways respond with valid JSON matching the required schema.",
            SYSTEM_PROMPT
        ))
        .with_user_message(&initial_prompt);

    for msg in &appointment.conversation_history {
        decision_request = decision_request.with_message(msg.clone());
    }

    // If we have conversational text from tool use, add it and request decision extraction
    if let Some(ref text) = conversational_text {
        decision_request = decision_request
            .with_message(Message::model(text))
            .with_user_message(&format!(
                r#"Based on your previous response: "{}"

Now provide ONLY the decision type as structured JSON.
Use "Undecided" if you're still gathering information and the automated agent will contact them again.
Use "Confirm" with "new_datetime" if patient chose a specific available slot.
Use "ManualIntervention" with "approximate_time" if patient requested human staff or wants to reschedule but gave vague timing.
Use "Cancel" if patient wants to cancel."#,
                text
            ));
    }

    let decision_response = decision_request
        .with_response_mime_type("application/json")
        .with_response_schema(schema)
        .execute()
        .await?;

    let mut llm_response: LLMResponse = serde_json::from_str(&decision_response.text())?;

    // Use conversational text if available, otherwise use the message from JSON
    if let Some(text) = conversational_text {
        llm_response.message = text;
    }

    Ok(llm_response)
}

fn get_patient_response() -> Result<String, Box<dyn std::error::Error>> {
    print!("Patient response: ");
    io::stdout().flush()?;

    let mut input = String::new();
    io::stdin().read_line(&mut input)?;
    Ok(input.trim().to_string())
}

pub async fn do_main() -> Result<(), Box<dyn std::error::Error>> {
    let client = prepare_gemini_client()?;

    let appointment_date = NaiveDate::from_ymd_opt(2025, 11, 15).unwrap();
    let appointment_time = NaiveTime::from_hms_opt(14, 30, 0).unwrap();
    let initial_datetime = NaiveDateTime::new(appointment_date, appointment_time);

    let mut appointment = Appointment {
        patient_name: "Raiyan Hossain".to_string(),
        doctor_id: "DR001".to_string(),
        doctor_name: "Dr. Sara Karim".to_string(),
        appointment_date,
        appointment_time,
        appointment_type: "Annual Checkup".to_string(),
        status: AppointmentStatus::Unconfirmed {
            datetime: initial_datetime,
        },
        conversation_history: vec![],
    };

    loop {
        let llm_response =
            generate_appointment_confirmation(&client, "Labaid Hospital", &appointment).await?;

        // Print the LLM's message
        println!("\n>>> Clinic: {}\n", llm_response.message);

        // Update appointment status based on decision
        match &llm_response.decision {
            AppointmentDecision::Undecided => {
                // Status remains unchanged
            }
            AppointmentDecision::Confirm { new_datetime } => {
                // If new_datetime is provided, update the appointment date/time
                if let Some(new_dt_str) = new_datetime {
                    if let Ok(new_datetime) =
                        NaiveDateTime::parse_from_str(new_dt_str, "%Y-%m-%d %H:%M")
                    {
                        appointment.appointment_date = new_datetime.date();
                        appointment.appointment_time = new_datetime.time();
                        appointment.status = AppointmentStatus::Confirmed {
                            datetime: new_datetime,
                        };
                        println!("[Status updated: Confirmed with new time {}]", new_dt_str);
                    } else {
                        // Fall back to current appointment time if parsing fails
                        let datetime = NaiveDateTime::new(
                            appointment.appointment_date,
                            appointment.appointment_time,
                        );
                        appointment.status = AppointmentStatus::Confirmed { datetime };
                        println!("[Status updated: Confirmed (original time)]");
                    }
                } else {
                    // No new time, just confirm existing appointment
                    let datetime = NaiveDateTime::new(
                        appointment.appointment_date,
                        appointment.appointment_time,
                    );
                    appointment.status = AppointmentStatus::Confirmed { datetime };
                    println!("[Status updated: Confirmed]");
                }
            }
            AppointmentDecision::Cancel => {
                appointment.status = AppointmentStatus::Cancelled;
                println!("[Status updated: Cancelled]");
            }
            AppointmentDecision::ManualIntervention { approximate_time } => {
                let last_known_time =
                    NaiveDateTime::new(appointment.appointment_date, appointment.appointment_time);
                appointment.status = AppointmentStatus::ManualIntervention {
                    last_known_time,
                    approximate_time: approximate_time.clone(),
                };
                println!(
                    "[Status updated: Manual Intervention Required - {}]",
                    approximate_time
                );
            }
        }

        // Add the model's message to conversation history
        appointment
            .conversation_history
            .push(Message::model(llm_response.message.clone()));

        // Get patient response
        let response = get_patient_response()?;

        // Check for quit command
        if response.trim().eq_ignore_ascii_case("quit") {
            println!("\nEnding conversation.");
            break;
        }

        // Add user's response to conversation history
        appointment
            .conversation_history
            .push(Message::user(response));
    }

    println!("\nFinal appointment status: {:?}", appointment.status);
    Ok(())
}
