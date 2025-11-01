open System
open System.Net
open System.Text
open System.Xml
open Library
open System.Web
open Org.BouncyCastle.Security

let runAsync (args: string []) =
    async {
        match args |> Seq.tryHead with
        | Some "legacybkash" -> return! runLegacyBkash
        | Some "bkash" -> return! runBkash
        | Some "portpos" -> return! runPortPos
        | Some "braintree" -> return! runBraintree
        | Some "nagad" -> return! runNagad
        | Some "interop" ->
            let text = "Foo"
            let! length, rustText = runInterop text
            printfn $"{text}'s length is: {length}"
            printfn $"retrieved text is: {rustText}"
            return ()
        | _ -> failwith "invalid method"
    }

[<EntryPoint>]
let main args =
    runAsync args |> Async.RunSynchronously
    0