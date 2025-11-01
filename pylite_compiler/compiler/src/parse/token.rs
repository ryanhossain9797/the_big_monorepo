#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum Token {
    Def,
    Return,
    LPar,
    RPar,
    LCur,
    RCur,
    Colon,
    SemiColon,
    Identifier(String),
    Int(i64),
    Unknown(char),
}
