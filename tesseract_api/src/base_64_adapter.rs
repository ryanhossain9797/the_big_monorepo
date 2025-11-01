use base64::{engine::general_purpose, Engine};

pub fn get_text_from_image_b64(image_base_64: String) -> Result<String, String> {
    let image_bytes = general_purpose::STANDARD
        .decode(image_base_64)
        .map_err(|err| err.to_string())?;

    let image_bytes = crate::core::to_b_and_w(image_bytes)?;

    crate::core::get_text_from_image(image_bytes)
}

pub fn get_b_and_w_from_image_b64(image_base_64: String) -> Result<String, String> {
    let image_bytes = general_purpose::STANDARD
        .decode(image_base_64)
        .map_err(|err| err.to_string())?;

    let image_bytes = crate::core::to_b_and_w(image_bytes)?;

    Ok(general_purpose::STANDARD.encode(&image_bytes))
}
