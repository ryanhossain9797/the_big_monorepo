use crate::prepare_gemini_client;
use chrono::{NaiveDate, NaiveDateTime, NaiveTime};
use gemini_rust::{Gemini, Message};
use serde::{Deserialize, Serialize};
use serde_json::json;
use std::io::{self, Write};

#[derive(Debug, Serialize, Deserialize)]
pub struct Appointment {
    patient_name: String,
    appointment_date: NaiveDate,
    appointment_time: NaiveTime,
    doctor_name: String,
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
    RescheduleRequired {
        last_known_time: NaiveDateTime,
        approximate_time: String,
    },
    Cancelled,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct LLMResponse {
    message: String,
    decision: AppointmentDecision,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(tag = "decision_type", rename_all = "PascalCase")]
pub enum AppointmentDecision {
    Undecided,
    Confirm,
    Cancel,
    Reschedule {
        #[serde(default)]
        approximate_time: String,
    },
}

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
                        "enum": ["Undecided", "Confirm", "Cancel", "Reschedule"],
                        "description": "The decision type"
                    },
                    "approximate_time": {
                        "type": "string",
                        "description": "REQUIRED for Reschedule: approximate time the patient wants (e.g., 'next week', 'in 2 days')"
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
        AppointmentStatus::RescheduleRequired {
            last_known_time,
            approximate_time,
        } => {
            format!(
                "Reschedule requested (was: {}, wants: {})",
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
- Date: {appointment_date}
- Time: {appointment_time}
- Doctor: {doctor_name}
- Type: {appointment_type}
- Current Status: {status}

Based on the conversation with the patient, decide what action to take:

Decision types:
- Undecided: You need more information from the patient
  * Use when you can't clearly determine their intent
  * If first contact: Introduce yourself and ask about their appointment
  * If continuing: Reference their previous response and ask follow-up
- Confirm: Patient clearly confirmed the appointment
  * Acknowledge their confirmation warmly
- Cancel: Patient clearly wants to cancel
  * Confirm cancellation with empathy
- Reschedule: Patient wants to reschedule
  * REQUIRED: Include "approximate_time" field with your best guess
  * Examples: "next week", "in 2 days", "1.5 hours later"
  * Acknowledge and let them know someone will follow up

IMPORTANT:
- Always include a "message" field with a friendly, nurse-like message
- The conversation will continue regardless of your decision
- Vary your phrasing - don't repeat patterns
- Do NOT assume intent - use Undecided if unsure"#,
        clinic_name = clinic_name,
        patient_name = appointment.patient_name,
        appointment_date = appointment.appointment_date.format("%Y-%m-%d"),
        appointment_time = appointment.appointment_time.format("%H:%M"),
        doctor_name = appointment.doctor_name,
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

    let mut request = client
        .generate_content()
        .with_system_prompt(
            r#"You are a friendly and warm automated agent at a medical clinic helping with appointment scheduling.

When writing messages to patients:
- Write in a conversational, natural tone like a caring nurse would speak
- Use casual, friendly language (e.g., "Hi!", "Thanks!", "Hope you're doing well!")
- When introducing yourself for the first time, say "I'm an automated agent from [clinic name]"
- Do NOT make up names like "[Your Name]" - you are an automated system
- If you need clarification, politely explain why you're reaching out (e.g., "I wasn't quite sure what you meant" or "Just wanted to check with you")
- Keep messages brief but warm
- Avoid overly formal or robotic language
- Use the patient's first name when appropriate
- Vary your phrasing - don't use the same patterns repeatedly in a conversation thread

Always respond with valid JSON matching the required schema."#
        )
        .with_user_message(&initial_prompt);

    // Add conversation history
    for msg in &appointment.conversation_history {
        request = request.with_message(msg.clone());
    }

    let response = request
        .with_response_mime_type("application/json")
        .with_response_schema(schema)
        .execute()
        .await?;

    let json_text = response.text();
    println!("Raw JSON response:\n{}\n", json_text);

    let llm_response: LLMResponse = serde_json::from_str(&json_text)?;

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
        appointment_date,
        appointment_time,
        doctor_name: "Dr. Sara Karim".to_string(),
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
            AppointmentDecision::Confirm => {
                let datetime =
                    NaiveDateTime::new(appointment.appointment_date, appointment.appointment_time);
                appointment.status = AppointmentStatus::Confirmed { datetime };
                println!("[Status updated: Confirmed]");
            }
            AppointmentDecision::Cancel => {
                appointment.status = AppointmentStatus::Cancelled;
                println!("[Status updated: Cancelled]");
            }
            AppointmentDecision::Reschedule { approximate_time } => {
                let last_known_time =
                    NaiveDateTime::new(appointment.appointment_date, appointment.appointment_time);
                appointment.status = AppointmentStatus::RescheduleRequired {
                    last_known_time,
                    approximate_time: approximate_time.clone(),
                };
                println!(
                    "[Status updated: Reschedule Required - {}]",
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
