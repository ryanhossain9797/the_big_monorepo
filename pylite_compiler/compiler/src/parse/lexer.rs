use crate::ast::Span;
use crate::parse::token::Token;

use std::{iter::Peekable, str::Chars};

pub struct Lexer<'c> {
    chars: Peekable<Chars<'c>>,
    position: usize,
}

impl<'c> Lexer<'c> {
    pub fn new(input: &'c str) -> Self {
        Self {
            chars: input.chars().peekable(),
            position: 0,
        }
    }

    fn next_char_if(&mut self, f: impl FnOnce(char) -> bool) -> Option<char> {
        self.chars
            .next_if(|&c| f(c))
            .inspect(|c| self.position += c.len_utf8() as usize)
    }

    fn next_char(&mut self) -> Option<char> {
        self.next_char_if(|_| true)
    }

    fn emit_token(&mut self, start: usize, token: Token) -> (Token, Span) {
        let end = self.position;
        (token, Span::new(start, end))
    }

    fn next_token(&mut self) -> Option<(Token, Span)> {
        let start_pos = self.position;

        match self.next_char()? {
            '(' => Some(self.emit_token(start_pos, Token::LPar)),
            ')' => Some(self.emit_token(start_pos, Token::RPar)),
            '{' => Some(self.emit_token(start_pos, Token::LCur)),
            '}' => Some(self.emit_token(start_pos, Token::RCur)),
            ':' => Some(self.emit_token(start_pos, Token::Colon)),
            ';' => Some(self.emit_token(start_pos, Token::SemiColon)),
            c if c.is_numeric() => {
                let mut int_repr = String::from(c);
                while let Some(c) = self.next_char_if(|c| c.is_numeric()) {
                    int_repr.push(c);
                }
                let int = int_repr.parse().unwrap();
                Some(self.emit_token(start_pos, Token::Int(int)))
            }
            c if c.is_alphabetic() => {
                let mut ident = String::from(c);
                while let Some(c) = self.next_char_if(|c| c.is_alphanumeric()) {
                    ident.push(c);
                }
                match ident.as_str() {
                    "def" => Some(self.emit_token(start_pos, Token::Def)),
                    "return" => Some(self.emit_token(start_pos, Token::Return)),
                    _ => Some(self.emit_token(start_pos, Token::Identifier(ident))),
                }
            }
            c if c.is_whitespace() => {
                while self.next_char_if(|c| c.is_whitespace()).is_some() {}
                self.next_token()
            }
            c => Some(self.emit_token(start_pos, Token::Unknown(c))),
        }
    }
}

impl Iterator for Lexer<'_> {
    type Item = (Token, Span);

    fn next(&mut self) -> Option<Self::Item> {
        self.next_token()
    }
}

#[cfg(test)]
mod test {
    use super::*;

    #[test]
    fn tokenize_return_const() {
        let input = r##"
def main(){
    return 34;
}
"##;

        let expected = vec![
            ("def", Token::Def),
            ("main", Token::Identifier("main".to_string())),
            ("(", Token::LPar),
            (")", Token::RPar),
            ("{", Token::LCur),
            ("return", Token::Return),
            ("34", Token::Int(34)),
            (";", Token::SemiColon),
            ("}", Token::RCur),
        ];

        test_lexer(input, expected);
    }

    fn test_lexer(input: &str, expected: Vec<(&str, Token)>) {
        let rendered: Vec<(&str, Token)> = Lexer::new(input)
            .map(|(token, span)| {
                let rendered = &input[span.start as usize..span.end as usize];
                (rendered, token)
            })
            .collect();

        assert_eq!(expected, rendered);
    }
}
