use crate::prepare_gemini_client;
use gemini_rust::Gemini;
use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use std::io::{self, Write};

#[derive(Debug, Serialize, Deserialize)]
pub struct AppointmentDetails {
    patient_name: String,
    appointment_date: String,
    appointment_time: String,
    doctor_name: String,
    appointment_type: String,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct PatientCommunication {
    message_type: String, // "email", "sms", "call", "contact_attempt"
    content: String,
    timestamp: String,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(tag = "decision_type", rename_all = "PascalCase")]
pub enum AppointmentDecision {
    WantsToReschedule {
        #[serde(default)]
        approximate_time: String,
    },
    Cancel,
    Confirmed,
    Contact {
        #[serde(default)]
        message: String,
    },
}

fn build_decision_schema() -> Value {
    json!({
        "type": "object",
        "properties": {
            "decision_type": {
                "type": "string",
                "enum": ["WantsToReschedule", "Cancel", "Confirmed", "Contact"],
                "description": "The type of decision"
            },
            "approximate_time": {
                "type": "string",
                "description": "REQUIRED for WantsToReschedule: approximate or best-guess time the patient wants (e.g., 'next week', 'in 2 days', '1 hour later')"
            },
            "message": {
                "type": "string",
                "description": "REQUIRED for Contact: the message to send to the patient"
            }
        },
        "required": ["decision_type"]
    })
}

fn build_prompt(
    clinic_name: &str,
    appointment: &AppointmentDetails,
    previous_communications: &[PatientCommunication],
) -> String {
    let comm_context = if previous_communications.is_empty() {
        "No previous communications.".to_string()
    } else {
        previous_communications
            .iter()
            .map(|c| format!("[{}] {}: {}", c.timestamp, c.message_type, c.content))
            .collect::<Vec<_>>()
            .join("\n")
    };

    format!(
        r#"You are an AI assistant for {clinic_name} helping to process patient appointment confirmations.

Appointment Details:
- Patient: {patient_name}
- Date: {appointment_date}
- Time: {appointment_time}
- Doctor: {doctor_name}
- Type: {appointment_type}

Previous Communications:
{comm_context}

IMPORTANT CONTEXT:
- Communications marked as "contact_attempt" are part of an ongoing SMS conversation thread
- Each contact_attempt shows: your previous message and the patient's response
- If there are contact_attempts, you are CONTINUING an existing conversation - do NOT repeat yourself
- Read the full conversation history to understand what has already been discussed
- Avoid repeating questions or information already covered in previous contact_attempts
- Build on the conversation naturally

Based on the patient's communications (if any), determine the appropriate action:

Decision types:
- Confirmed: Patient clearly confirmed the appointment
- WantsToReschedule: Patient clearly wants to reschedule (will be handled by staff)
  * REQUIRED: Include "approximate_time" with your best guess of when they want to reschedule
  * The time does NOT need to be precise or accurate - approximate is fine
  * Examples: "next week", "in a few days", "1-2 hours later", "Friday afternoon"
  * If patient says "delay by 1 or 2 hours", putting "1 hour later" or "1.5 hours later" is perfectly acceptable
- Cancel: Patient clearly wants to cancel
- Contact: Use this if you cannot clearly determine what the patient wants
  * REQUIRED: Include "message" with a warm, conversational text message
  * If this is the FIRST contact (no contact_attempts in history): Introduce yourself and ask about their appointment preference
  * If CONTINUING a conversation (there are contact_attempts): Reference their previous response and ask a follow-up question. Do NOT repeat the same greeting or question patterns
  * Briefly explain WHY you need more info (e.g., "I wasn't sure what you meant by...", "Just to clarify...")

IMPORTANT:
- Do not assume Confirmed, Cancelled or WantsToReschedule by default. If unsure, choose Contact
- For Contact messages, ALWAYS explain why clarification is needed
- Write Contact messages in a friendly, natural nurse-like tone
- When continuing a conversation (contact_attempts exist), be contextually aware - don't repeat yourself
- Vary your language - if you already asked "what would you like to do?", try "could you let me know..." or similar
- For WantsToReschedule, you MUST include approximate_time (doesn't need to be exact!)
- For Contact, you MUST include message"#,
        clinic_name = clinic_name,
        patient_name = appointment.patient_name,
        appointment_date = appointment.appointment_date,
        appointment_time = appointment.appointment_time,
        doctor_name = appointment.doctor_name,
        appointment_type = appointment.appointment_type,
        comm_context = comm_context
    )
}

async fn generate_appointment_confirmation(
    client: &Gemini,
    clinic_name: &str,
    appointment: &AppointmentDetails,
    previous_communications: &[PatientCommunication],
) -> Result<AppointmentDecision, Box<dyn std::error::Error>> {
    let schema = build_decision_schema();
    let prompt = build_prompt(clinic_name, appointment, previous_communications);

    let response = client
        .generate_content()
        .with_system_prompt(
            r#"You are a friendly and warm nurse at a medical clinic helping with appointment scheduling.

When writing messages to patients:
- Write in a conversational, natural tone like a caring nurse would speak
- Use casual, friendly language (e.g., "Hi!", "Thanks!", "Hope you're doing well!")
- If you need clarification, politely explain why you're reaching out (e.g., "I wasn't quite sure what you meant" or "Just wanted to check with you")
- Keep messages brief but warm
- Avoid overly formal or robotic language
- Use the patient's first name when appropriate
- IMPORTANT: If you're continuing a conversation (previous contact_attempts exist), acknowledge their response and build on it naturally
- Vary your phrasing - don't use the same patterns repeatedly in a conversation thread

Always respond with valid JSON matching the required schema."#
        )
        .with_user_message(&prompt)
        .with_response_mime_type("application/json")
        .with_response_schema(schema)
        .execute()
        .await?;

    let json_text = response.text();
    println!("Raw JSON response:\n{}\n", json_text);

    let decision: AppointmentDecision = serde_json::from_str(&json_text)?;

    Ok(decision)
}

fn get_patient_response() -> Result<String, Box<dyn std::error::Error>> {
    print!("Patient response: ");
    io::stdout().flush()?;

    let mut input = String::new();
    io::stdin().read_line(&mut input)?;
    Ok(input.trim().to_string())
}

fn get_timestamp() -> String {
    use std::time::{SystemTime, UNIX_EPOCH};
    let now = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_secs();
    format!("2025-11-06 {:02}:{:02}", (now / 60) % 24, now % 60)
}

pub async fn do_main() -> Result<(), Box<dyn std::error::Error>> {
    let client = prepare_gemini_client()?;

    let appointment = AppointmentDetails {
        patient_name: "Raiyan Hossain".to_string(),
        appointment_date: "2025-11-15".to_string(),
        appointment_time: "14:30".to_string(),
        doctor_name: "Dr. Sara Karim".to_string(),
        appointment_type: "Annual Checkup".to_string(),
    };

    let mut communications: Vec<PatientCommunication> = vec![];

    loop {
        let decision = generate_appointment_confirmation(
            &client,
            "Labaid Hospital",
            &appointment,
            &communications,
        )
        .await?;

        match decision {
            AppointmentDecision::Contact { ref message } => {
                println!("\n>>> Clinic: {}\n", message);

                let response = get_patient_response()?;

                // Record both message and response as a single contact attempt
                communications.push(PatientCommunication {
                    message_type: "contact_attempt".to_string(),
                    content: format!(
                        "message_to_patient: {message}\npatient_response: {response}"
                    ),
                    timestamp: get_timestamp(),
                });
            }
            AppointmentDecision::Confirmed => {
                println!("\nAppointment CONFIRMED");
                break;
            }
            AppointmentDecision::Cancel => {
                println!("\nAppointment CANCELLED");
                break;
            }
            AppointmentDecision::WantsToReschedule { ref approximate_time } => {
                println!("\nPatient WANTS TO RESCHEDULE (approx: {})", approximate_time);
                println!("→ A staff member will follow up to finalize the new appointment time");
                break;
            }
        }
    }

    Ok(())
}
