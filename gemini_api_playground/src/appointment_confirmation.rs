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
    Reschedule {
        #[serde(default)]
        requested_time: String,
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
                "enum": ["Reschedule", "Cancel", "Confirmed", "Contact"],
                "description": "The type of decision"
            },
            "requested_time": {
                "type": "string",
                "description": "REQUIRED for Reschedule: the requested new appointment time in format YYYY-MM-DD HH:MM"
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

Based on the patient's communications (if any), determine the appropriate action:

Decision types:
- Confirmed: Patient confirmed the appointment
- Reschedule: Patient wants to reschedule
  * REQUIRED: Include "requested_time" with the new date/time they want (if they said "next week", provide a specific date like "2025-11-22 14:30")
- Cancel: Patient wants to cancel
- Contact: Use this if you cannot infer from patient's communications what they want to do. Do not assume Confirmed by default.
  * REQUIRED: Include "message" with a friendly text message to send to the patient asking them to confirm, reschedule, or cancel

IMPORTANT:
- Do not assume Confirmed, Cancelled or Rescheduled by default. If unsure Contact
- For Reschedule, you MUST include requested_time
- For Contact, you MUST include message
- Infer specific times from vague requests (e.g., "next week" → specific date one week from appointment date)"#,
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
        .with_system_prompt("You are a medical appointment scheduling assistant. Always respond with valid JSON matching the required schema.")
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
            AppointmentDecision::Reschedule { ref requested_time } => {
                println!("\nAppointment RESCHEDULED to: {}", requested_time);
                break;
            }
        }
    }

    Ok(())
}
