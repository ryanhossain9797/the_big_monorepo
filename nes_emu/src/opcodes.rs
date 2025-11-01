use once_cell::sync::Lazy;

use crate::cpu::{AddressingMode, OpCodeType};
use std::collections::HashMap;

pub struct OpCode {
    pub code: u8,
    pub op_code_type: &'static OpCodeType,
    pub len: u8,
    pub cycles: u8,
    pub addressing_mode: AddressingMode,
}

impl OpCode {
    fn new(
        code: u8,
        mnemonic: &'static OpCodeType,
        len: u8,
        cycles: u8,
        mode: AddressingMode,
    ) -> Self {
        OpCode {
            code: code,
            op_code_type: mnemonic,
            len: len,
            cycles: cycles,
            addressing_mode: mode,
        }
    }
}

static OP_CODES: Lazy<Vec<OpCode>> = Lazy::new(|| {
    vec![
        //BRK
        OpCode::new(0x00, &OpCodeType::BRK, 1, 7, AddressingMode::NoneAddressing),
        //TAX
        OpCode::new(0xaa, &OpCodeType::TAX, 1, 2, AddressingMode::NoneAddressing),
        //INX
        OpCode::new(0xe8, &OpCodeType::INX, 1, 2, AddressingMode::NoneAddressing),
        //ADC
        OpCode::new(0x69, &OpCodeType::ADC, 2, 2, AddressingMode::Immediate),
        OpCode::new(0x65, &OpCodeType::ADC, 2, 3, AddressingMode::ZeroPage),
        OpCode::new(0x75, &OpCodeType::ADC, 2, 4, AddressingMode::ZeroPageX),
        OpCode::new(0x6d, &OpCodeType::ADC, 3, 4, AddressingMode::Absolute),
        OpCode::new(0x7d, &OpCodeType::ADC, 3, 5, AddressingMode::AbsoluteX),
        OpCode::new(0x79, &OpCodeType::ADC, 3, 5, AddressingMode::AbsoluteY),
        OpCode::new(0x61, &OpCodeType::ADC, 2, 6, AddressingMode::IndirectX),
        OpCode::new(0x71, &OpCodeType::ADC, 2, 5, AddressingMode::IndirectY),
        //AND
        OpCode::new(0x29, &OpCodeType::AND, 2, 2, AddressingMode::Immediate),
        OpCode::new(0x25, &OpCodeType::AND, 2, 3, AddressingMode::ZeroPage),
        OpCode::new(0x35, &OpCodeType::AND, 2, 4, AddressingMode::ZeroPageX),
        OpCode::new(0x2d, &OpCodeType::AND, 3, 4, AddressingMode::Absolute),
        OpCode::new(0x3d, &OpCodeType::AND, 3, 4, AddressingMode::AbsoluteX),
        OpCode::new(0x39, &OpCodeType::AND, 3, 4, AddressingMode::AbsoluteY),
        OpCode::new(0x21, &OpCodeType::AND, 2, 6, AddressingMode::IndirectX),
        OpCode::new(0x31, &OpCodeType::AND, 2, 5, AddressingMode::IndirectY),
        //ASL
        OpCode::new(0x0a, &OpCodeType::ASL, 1, 2, AddressingMode::NoneAddressing),
        OpCode::new(0x06, &OpCodeType::ASL, 2, 3, AddressingMode::ZeroPage),
        OpCode::new(0x16, &OpCodeType::ASL, 2, 4, AddressingMode::ZeroPageX),
        OpCode::new(0x0e, &OpCodeType::ASL, 3, 4, AddressingMode::Absolute),
        OpCode::new(0x1e, &OpCodeType::ASL, 3, 4, AddressingMode::AbsoluteX),
        //BEQ
        OpCode::new(0x90, &OpCodeType::BCC, 2, 2, AddressingMode::NoneAddressing),
        //LDA
        OpCode::new(0xa9, &OpCodeType::LDA, 2, 2, AddressingMode::Immediate),
        OpCode::new(0xa5, &OpCodeType::LDA, 2, 3, AddressingMode::ZeroPage),
        OpCode::new(0xb5, &OpCodeType::LDA, 2, 4, AddressingMode::ZeroPageX),
        OpCode::new(0xad, &OpCodeType::LDA, 3, 4, AddressingMode::Absolute),
        OpCode::new(
            0xbd,
            &OpCodeType::LDA,
            3,
            4, /*+1 if page crossed*/
            AddressingMode::AbsoluteX,
        ),
        OpCode::new(
            0xb9,
            &OpCodeType::LDA,
            3,
            4, /*+1 if page crossed*/
            AddressingMode::AbsoluteY,
        ),
        OpCode::new(0xa1, &OpCodeType::LDA, 2, 6, AddressingMode::IndirectX),
        OpCode::new(
            0xb1,
            &OpCodeType::LDA,
            2,
            5, /*+1 if page crossed*/
            AddressingMode::IndirectY,
        ),
        //STA
        OpCode::new(0x85, &OpCodeType::STA, 2, 3, AddressingMode::ZeroPage),
        OpCode::new(0x95, &OpCodeType::STA, 2, 4, AddressingMode::ZeroPageX),
        OpCode::new(0x8d, &OpCodeType::STA, 3, 4, AddressingMode::Absolute),
        OpCode::new(0x9d, &OpCodeType::STA, 3, 5, AddressingMode::AbsoluteX),
        OpCode::new(0x99, &OpCodeType::STA, 3, 5, AddressingMode::AbsoluteY),
        OpCode::new(0x81, &OpCodeType::STA, 2, 6, AddressingMode::IndirectX),
        OpCode::new(0x91, &OpCodeType::STA, 2, 6, AddressingMode::IndirectY),
    ]
});

pub static OP_CODES_MAP: Lazy<HashMap<u8, &'static OpCode>> = Lazy::new(|| {
    let mut map = HashMap::new();
    for cpuop in &*OP_CODES {
        map.insert(cpuop.code, cpuop);
    }
    map
});
