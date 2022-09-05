//cargo build --release

use tokenizers::tokenizer::{ Tokenizer as HFTokenizer };
use std::os::raw::c_char;
use std::ffi::CString;
use std::ffi::CStr;

#[cxx::bridge]
mod ffi {
    extern "Rust" {
        type Tokenizer;
        unsafe fn from_file(text_pointer: *const c_char) -> *mut Tokenizer; 
        unsafe fn from_pretrained(text_pointer: *const c_char) -> *mut Tokenizer;
        unsafe fn encode(tok: *mut Tokenizer, input: *const c_char, idsPtr: *mut u32, length: usize, add_special_tokens: bool) -> u32;
        unsafe fn decode(tok: *mut Tokenizer, idsPtr: *mut u32, length: usize, skip_special_tokens: bool) -> *const c_char;

    }
}

#[no_mangle]
unsafe fn from_file(text_pointer: *const c_char) -> *mut Tokenizer { 
    let text: String = CStr::from_ptr(text_pointer).to_str().expect("Can not read string argument.").to_string();
    std::mem::transmute(Box::new(Tokenizer{tokenizer:HFTokenizer::from_file(text).unwrap()})) 
}

#[no_mangle]
unsafe fn from_pretrained(text_pointer: *const c_char) -> *mut Tokenizer { 
    let text: String = CStr::from_ptr(text_pointer).to_str().expect("Can not read string argument.").to_string();
    std::mem::transmute(Box::new(Tokenizer{tokenizer:HFTokenizer::from_pretrained(text, None).unwrap()})) 
}

#[repr(C)]
struct Tokenizer {
    tokenizer: HFTokenizer,
}


#[no_mangle]
unsafe fn encode(tok: *mut Tokenizer, input: *const c_char, ids_ptr: *mut u32, length: usize, add_special_tokens: bool) -> u32 {
    let text: String = CStr::from_ptr(input).to_str().expect("Can not read string argument.").to_string();

    let enc_res = (*tok).tokenizer.encode(text, add_special_tokens);
    let enc = enc_res.unwrap();

    let u32arr = enc.get_ids();

    let data: &mut [u32] = { std::slice::from_raw_parts_mut(ids_ptr, length) }; 
    
    for i in 0..u32arr.len() {
        data[i] = u32arr[i];
        if i + 1 >= length {
            break;
        }
    }

    u32arr.len().try_into().unwrap()
}

#[no_mangle]
unsafe fn decode(tok: *mut Tokenizer, ids_ptr: *mut u32, length: usize, skip_special_tokens: bool) -> *const c_char {
    let data: Vec<u32> = { Vec::<u32>::from(std::slice::from_raw_parts_mut(ids_ptr, length)) }; 
    let dec_res = (*tok).tokenizer.decode(data, skip_special_tokens);

    let s = CString::new(dec_res.unwrap()).unwrap();
    let p = s.as_ptr();
    std::mem::forget(s);
    p
}