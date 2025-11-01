pub mod bkash;

pub use bkash::prepare::*;

use core::ffi::c_char;
use std::collections::HashMap;
use std::ffi::{CStr, CString};
use std::mem;

use interoptopus::util::NamespaceMappings;
use interoptopus::{ffi_function, ffi_type, function, Inventory, InventoryBuilder};
use interoptopus::{Error, Interop};
use interoptopus_backend_csharp::overloads::DotNet;

use crate::bkash::BkashDirectDebitConfig;

/// A simple type in our FFI layer.
#[ffi_type]
#[repr(C)]
pub struct Rectangle {
    pub width: u32,
    pub height: u32,
}

/// Function using the type.
#[ffi_function]
#[no_mangle]
pub extern "C" fn my_function(rect: Rectangle) -> u32 {
    rect.width * rect.height
}

/// # Safety
///
/// This function should not be called before the horsemen are ready.
#[no_mangle]
pub unsafe extern "C" fn get_length(s: *const c_char) -> u32 {
    assert!(!s.is_null());

    let c_str = CStr::from_ptr(s);
    let r_str = c_str.to_str().unwrap().to_string();

    r_str.len() as u32
}

#[no_mangle]
pub extern "C" fn get_text() -> *const c_char {
    let s = CString::new("FFI_LIB_VERSION").unwrap();
    let p = s.as_ptr();
    mem::forget(s);

    p as *const _
}

/// # Safety
///
/// This function should not be called before the horsemen are ready.
#[no_mangle]
pub unsafe extern "C" fn free_text(s: *mut c_char) {
    if s.is_null() {
        return;
    }

    let c_str: &CStr = CStr::from_ptr(s);
    let bytes_len: usize = c_str.to_bytes_with_nul().len();
    let temp_vec: Vec<c_char> = Vec::from_raw_parts(s, bytes_len, bytes_len);
    drop(temp_vec);
}

// This will create a function `my_inventory` which can produce
// an abstract FFI representation (called `Library`) for this crate.
pub fn my_inventory() -> Inventory {
    {
        InventoryBuilder::new()
            .register(function!(my_function))
            .inventory()
    }
}

/// # Safety
///
/// This function should not be called before the horsemen are ready.
#[no_mangle]
pub unsafe extern "C" fn query_bkash_direct_debit_payment_ffi(
    config_serialized: *const c_char,
    payment_request_id: *const c_char,
) -> *const c_char {
    assert!(!config_serialized.is_null());
    assert!(!payment_request_id.is_null());

    let config_serialized = CStr::from_ptr(config_serialized)
        .to_str()
        .expect("config_serialized is not valid")
        .to_string();

    let payment_request_id = CStr::from_ptr(payment_request_id)
        .to_str()
        .expect("payment request id is not valid")
        .to_string();

    let config: BkashDirectDebitConfig =
        serde_json::from_str(&config_serialized).expect("config deserialization failed");

    let result = bkash::query_bkash_direct_debit_payment(&config, &payment_request_id);

    let return_data = CString::new(payment_request_id.clone()).unwrap();
    let return_data_ptr = return_data.as_ptr();
    mem::forget(return_data);

    return_data_ptr as *const _
}
