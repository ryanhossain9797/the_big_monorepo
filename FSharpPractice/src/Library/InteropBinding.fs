module InteropBinding

open Interop

let runInterop (text: string) : Async<uint32 * string> =
    async {
        let length = InteropClass.GetLength text
        let textFromRust = InteropClass.GetTextData ()
        return (length, textFromRust)
    }