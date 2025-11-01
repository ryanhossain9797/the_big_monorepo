mod ast;
mod parse;

trait Pipeable<T, U> {
    fn pipe(self, f: impl FnOnce(T) -> U) -> U;
}

impl<T, U> Pipeable<T, U> for T {
    fn pipe(self, f: impl FnOnce(T) -> U) -> U {
        f(self)
    }
}

fn main() {
    let as_string = 1.pipe(|x| x.to_string());
    println!("{}", as_string);
}
