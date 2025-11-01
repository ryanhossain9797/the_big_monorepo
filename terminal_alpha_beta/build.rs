use std::process::Command;
use std::str;
static CARGOENV: &str = "cargo:rustc-env=";
fn main() {
    let time_c = Command::new("date").args(&["+%d-%m-%Y"]).output();
    match time_c {
        Ok(t) => {
            let time;
            unsafe {
                time = str::from_utf8_unchecked(&t.stdout);
            }
            println!("{}COMPILED_AT={}", CARGOENV, time);
        }
        Err(_) => {
            println!("{}COMPILED_AT=Date time fetch failed", CARGOENV);
        }
    }
}
