#[cfg(test)]
mod test {
    use crate::cpu::{CpuFlags, MemOps, CPU};

    #[test]
    fn test_0xa9_lda_immediate_load_data() {
        let mut cpu: CPU = CPU::new();
        cpu.load_and_run(vec![0xa9, 0x05, 0x00]);
        assert_eq!(cpu.register_a, 0x05);
        assert!(!cpu.status.contains(CpuFlags::Z));
        assert!(!cpu.status.contains(CpuFlags::N));
    }

    #[test]
    fn test_0xa9_lda_zero_flag() {
        let mut cpu = CPU::new();
        cpu.load_and_run(vec![0xa9, 0x00, 0x00]);
        assert!(cpu.status.contains(CpuFlags::Z));
    }

    #[test]
    fn test_0xa9_lda_negative_flag() {
        let mut cpu = CPU::new();
        cpu.load_and_run(vec![0xa9, 0xa8, 0x00]);
        assert!(cpu.status.contains(CpuFlags::N));
    }

    #[test]
    fn test_lda_from_memory() {
        let mut cpu = CPU::new();
        cpu.mem_write(0x10, 0x55);

        cpu.load_and_run(vec![0xa5, 0x10, 0x00]);

        assert_eq!(cpu.register_a, 0x55);
    }

    #[test]
    fn test_0xaa_tax_move_a_to_x() {
        let mut cpu = CPU::new();
        cpu.load_and_run(vec![0xa9, 0xa8, 0xaa, 0x00]);

        assert_eq!(cpu.register_x, 0xa8)
    }

    #[test]
    fn test_0xaa_inx_increments_x() {
        let mut cpu = CPU::new();
        cpu.load_and_run(vec![0xa9, 0xa8, 0xaa, 0xe8, 0x00]);

        assert_eq!(cpu.register_x, 0xa9)
    }

    #[test]
    fn test_inx_overflow() {
        let mut cpu = CPU::new();
        cpu.load(vec![0xe8, 0xe8, 0x00]);
        cpu.reset();
        cpu.register_x = 0xff;
        cpu.run();

        assert_eq!(cpu.register_x, 1)
    }

    #[test]
    fn test_and() {
        let mut cpu = CPU::new();
        cpu.load_and_run(vec![0xa9, 0x05, 0x29, 0xa9, 0x00]);
        assert_eq!(cpu.register_a, 0x01);
        assert!(!cpu.status.contains(CpuFlags::N));
        assert!(!cpu.status.contains(CpuFlags::Z));
    }

    #[test]
    fn test_asl() {
        let mut cpu = CPU::new();
        cpu.load_and_run(vec![0xa9, 0xd1, 0x0a, 0x00]);
        assert_eq!(cpu.register_a, 0xa2);
        assert!(cpu.status.contains(CpuFlags::N));
        assert!(!cpu.status.contains(CpuFlags::Z));
        assert!(cpu.status.contains(CpuFlags::C));
    }

    #[test]
    fn test_and_negative_flag() {
        let mut cpu = CPU::new();
        cpu.load_and_run(vec![0xa9, 0x05, 0x29, 0xa8, 0x00]);
        assert_eq!(cpu.register_a, 0x00);
        assert!(!cpu.status.contains(CpuFlags::N));
        assert!(cpu.status.contains(CpuFlags::Z));
    }

    #[test]
    fn test_and_zero_flag() {
        let mut cpu = CPU::new();
        cpu.load_and_run(vec![0xa9, 0xa8, 0x29, 0xa0, 0x00]);
        assert_eq!(cpu.register_a, 0xa0);
        assert!(cpu.status.contains(CpuFlags::N));
        assert!(!cpu.status.contains(CpuFlags::Z));
    }
}
