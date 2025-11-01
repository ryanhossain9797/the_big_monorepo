use std::io::Cursor;

use image::ImageOutputFormat;

pub fn to_b_and_w(image_bytes: Vec<u8>) -> Result<Vec<u8>, String> {
    let img = image::load_from_memory(&image_bytes)
        .map_err(|err| err.to_string())?
        .grayscale()
        .into_luma8();

    let img = image::imageops::colorops::contrast(&img, 30.0);

    let mut cursor = Cursor::new(Vec::new());
    img.write_to(&mut cursor, ImageOutputFormat::Png)
        .map_err(|err| err.to_string())?;

    Ok(cursor.into_inner())
}

pub fn get_text_from_image(image_bytes: Vec<u8>) -> Result<String, String> {
    let mut lt = leptess::LepTess::new(None, "eng").map_err(|err| err.to_string())?;

    lt.set_image_from_mem(&image_bytes)
        .map_err(|err| err.to_string())?;

    match lt.get_utf8_text() {
        Ok(ocr_text) => Ok(ocr_text),
        Err(utf_8_error) => Err(format!("{utf_8_error}")),
    }
}
