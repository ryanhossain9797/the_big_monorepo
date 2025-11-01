#[cfg(test)]
mod test {
    #[test]
    fn u16_from_le() {
        let lo: u8 = 0x00;
        let hi: u8 = 0x80;

        let val = u16::from_le_bytes([lo, hi]);
        assert_eq!(val, 0x8000)
    }

    #[test]
    fn le_from_u16() {
        let val: u16 = 0x8000;
        let le_bytes = val.to_le_bytes();

        assert_eq!(le_bytes, [0x00, 0x80])
    }

    #[test]
    fn carry_flag() {
        let status: u16 = 0b1010_0110;
        let carry = (status >> 7) & 0b01;

        assert_eq!(carry, 1);

        let status: u16 = 0b0010_0110;
        let carry = (status >> 7) & 0b01;

        assert_eq!(carry, 0);
    }
}
