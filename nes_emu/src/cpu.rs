use crate::opcodes::OP_CODES_MAP;
use bitflags::*;

const DEFAULT_PROGRAM_START: usize = 0x8000;
const PROGRAM_START_FROM: u16 = 0xFFFC;

#[derive(Debug)]
#[allow(non_camel_case_types)]
pub enum AddressingMode {
    Immediate,
    ZeroPage,
    ZeroPageX,
    ZeroPageY,
    Absolute,
    AbsoluteX,
    AbsoluteY,
    IndirectX,
    IndirectY,
    NoneAddressing,
}

#[derive(Debug)]
pub enum OpCodeType {
    ADC,
    BRK,
    TAX,
    INX,
    LDA,
    AND,
    ASL,
    BCC,
    STA,
}

pub trait MemOps {
    fn mem_read(&self, addr: u16) -> u8;
    fn mem_write(&mut self, addr: u16, data: u8);

    fn mem_ref(&mut self, addr: u16) -> &mut u8;

    fn mem_read_u16(&self, addr: u16) -> u16 {
        let lo = self.mem_read(addr);
        let hi = self.mem_read(addr + 1);

        u16::from_le_bytes([lo, hi])
    }

    fn mem_write_u16(&mut self, addr: u16, data: u16) {
        let [lo, hi] = data.to_le_bytes();

        self.mem_write(addr, lo);
        self.mem_write(addr + 1, hi);
    }
}

bitflags! {
    /// Represents a set of flags.
    #[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash)]
    pub struct CpuFlags: u32 {
        const C = 0b00000001;
        const Z = 0b00000010;
        const i = 0b00000100;
        const d = 0b00001000;
        const b = 0b00010000;
        const _ = 0b00000000;
        const V = 0b01000000;
        const N = 0b10000000;
    }
}

pub struct CPU {
    pub register_a: u8,
    pub register_x: u8,
    pub register_y: u8,
    pub status: CpuFlags,
    pub program_counter: u16,
    memory: [u8; 0xFFFF],
}

impl MemOps for CPU {
    fn mem_read(&self, addr: u16) -> u8 {
        self.memory[addr as usize]
    }

    fn mem_write(&mut self, addr: u16, data: u8) {
        self.memory[addr as usize] = data;
    }

    fn mem_ref(&mut self, addr: u16) -> &mut u8 {
        &mut self.memory[addr as usize]
    }
}

impl CPU {
    pub fn new() -> Self {
        CPU {
            register_a: 0,
            register_x: 0,
            register_y: 0,
            status: CpuFlags::empty(),
            program_counter: 0,
            memory: [0; 0xFFFF],
        }
    }

    pub fn load(&mut self, program: Vec<u8>) {
        self.memory[DEFAULT_PROGRAM_START..(DEFAULT_PROGRAM_START + program.len())]
            .copy_from_slice(&program[..]);

        self.mem_write_u16(PROGRAM_START_FROM, DEFAULT_PROGRAM_START as u16)
    }

    pub fn reset(&mut self) {
        self.register_a = 0;
        self.register_x = 0;
        self.register_y = 0;
        self.status = CpuFlags::empty();
        self.program_counter = self.mem_read_u16(PROGRAM_START_FROM);
    }

    pub fn load_and_run(&mut self, program: Vec<u8>) {
        self.load(program);
        self.reset();
        self.run();
    }

    fn get_operand_address(&mut self, addressing_mode: &AddressingMode) -> u16 {
        match addressing_mode {
            AddressingMode::Immediate => self.program_counter,
            AddressingMode::ZeroPage => self.mem_read(self.program_counter) as u16,
            AddressingMode::Absolute => self.mem_read_u16(self.program_counter),
            AddressingMode::ZeroPageX => {
                let pos = self.mem_read(self.program_counter);
                let addr = pos.wrapping_add(self.register_x) as u16;
                addr
            }
            AddressingMode::ZeroPageY => {
                let pos = self.mem_read(self.program_counter);
                let addr = pos.wrapping_add(self.register_y) as u16;
                addr
            }
            AddressingMode::AbsoluteX => {
                let base = self.mem_read_u16(self.program_counter);
                let addr = base.wrapping_add(self.register_x as u16);
                addr
            }
            AddressingMode::AbsoluteY => {
                let base = self.mem_read_u16(self.program_counter);
                let addr = base.wrapping_add(self.register_y as u16);
                addr
            }
            AddressingMode::IndirectX => {
                let base = self.mem_read(self.program_counter);

                let ptr: u8 = (base as u8).wrapping_add(self.register_x);
                let lo = self.mem_read(ptr as u16);
                let hi = self.mem_read(ptr.wrapping_add(1) as u16);

                u16::from_le_bytes([lo, hi])
            }
            AddressingMode::IndirectY => {
                let base = self.mem_read(self.program_counter);

                let lo = self.mem_read(base as u16);
                let hi = self.mem_read((base as u8).wrapping_add(1) as u16);
                let deref_base = u16::from_le_bytes([lo, hi]);
                let deref = deref_base.wrapping_add(self.register_y as u16);
                deref
            }
            AddressingMode::NoneAddressing => {
                panic!("mode {:?} is not supported", addressing_mode);
            }
        }
    }

    fn update_zero_flag(&mut self, result: u8) {
        if result == 0 {
            self.status.insert(CpuFlags::Z)
        } else {
            self.status.remove(CpuFlags::Z)
        }
    }

    fn update_negative_flag(&mut self, result: u8) {
        if result & 0b1000_0000 == 0 {
            self.status.remove(CpuFlags::N)
        } else {
            self.status.insert(CpuFlags::N)
        }
    }

    fn update_zero_and_negative_flags(&mut self, result: u8) {
        self.update_zero_flag(result);
        self.update_negative_flag(result);
    }

    fn set_register_a(&mut self, data: u8) {
        self.register_a = data;
        self.update_zero_and_negative_flags(data);
    }

    fn tax(&mut self) {
        self.register_x = self.register_a;
        self.update_zero_and_negative_flags(self.register_x);
    }

    fn inx(&mut self) {
        self.register_x = self.register_x.wrapping_add(1);
        self.update_zero_and_negative_flags(self.register_x);
    }

    fn add_to_register_a_with_carry(&mut self, value: u8) {
        let sum = (self.register_a as u16)
            + (value as u16)
            + match self.status.contains(CpuFlags::C) {
                true => 1,
                false => 0,
            };

        match sum > 0xff {
            true => self.status.insert(CpuFlags::C),
            false => self.status.remove(CpuFlags::C),
        }

        let sum = sum as u8;

        match (value ^ sum) & (self.register_a ^ sum) & 0x80 == 0 {
            true => self.status.remove(CpuFlags::V),
            false => self.status.insert(CpuFlags::V),
        }

        self.set_register_a(sum);
    }

    fn adc(&mut self, addressing_mode: &AddressingMode) {
        let addr = self.get_operand_address(addressing_mode);
        let value = self.mem_read(addr);

        self.add_to_register_a_with_carry(value);
    }

    fn and(&mut self, addressing_mode: &AddressingMode) {
        let addr = self.get_operand_address(addressing_mode);
        let value = self.mem_read(addr);

        let and = self.register_a & value;

        self.set_register_a(and);
    }

    fn asl(&mut self, addressing_mode: &AddressingMode) {
        let value = match addressing_mode {
            AddressingMode::NoneAddressing => &mut self.register_a,
            _ => {
                let addr = self.get_operand_address(addressing_mode);
                self.mem_ref(addr)
            }
        };

        let msb = *value & 0b1000_0000;
        let shifted = *value << 0b01;
        *value = shifted;

        if msb > 0 {
            self.status.insert(CpuFlags::C);
        } else {
            self.status.remove(CpuFlags::C);
        }

        self.update_zero_flag(self.register_a);
        self.update_negative_flag(shifted);
    }

    fn bcc(&mut self) {
        let jmp = self.mem_read(self.program_counter) as i8;

        let jmp_addr = self
            .program_counter
            .wrapping_add(1)
            .wrapping_add(jmp as u16);

        self.program_counter = jmp_addr;
    }

    fn lda(&mut self, addressing_mode: &AddressingMode) {
        let addr = self.get_operand_address(addressing_mode);
        let value = self.mem_read(addr);

        self.set_register_a(value);
    }

    fn sta(&mut self, addressing_mode: &AddressingMode) {
        let addr = self.get_operand_address(addressing_mode);
        self.mem_write(addr, self.register_a);
    }

    pub fn run(&mut self) {
        loop {
            let op_code = self.mem_read(self.program_counter);
            self.program_counter += 1;

            let program_counter_state: u16 = self.program_counter;

            let op_code = OP_CODES_MAP
                .get(&op_code)
                .expect(&format!("OpCode {op_code} is invalid"));

            match op_code.op_code_type {
                OpCodeType::BRK => return,
                OpCodeType::TAX => self.tax(),
                OpCodeType::INX => self.inx(),
                OpCodeType::ADC => self.adc(&op_code.addressing_mode),
                OpCodeType::AND => self.and(&op_code.addressing_mode),
                OpCodeType::ASL => self.asl(&op_code.addressing_mode),
                OpCodeType::BCC => self.bcc(),
                OpCodeType::LDA => self.lda(&op_code.addressing_mode),
                OpCodeType::STA => self.sta(&op_code.addressing_mode),
            }

            //Branch/Jump instructions may change the program counter explicitely,
            //Which negates the need for advancing the counter normally.
            let program_counter_unchanged = program_counter_state == self.program_counter;

            if program_counter_unchanged {
                // -1 because we advance the program counter once already,
                // Right after reading the op_code
                self.program_counter += (op_code.len - 1) as u16;
            }
        }
    }
}
