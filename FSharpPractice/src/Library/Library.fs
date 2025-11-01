module Library

open Methods.LegacyBkash
open Methods.BkashDirectDebit
open Methods.PortPos
open Methods.Braintree
open Methods.Nagad
open InteropBinding

let runInterop (text: string): Async<uint32 * string> =
    async {
        return! runInterop text
    }

let runLegacyBkash : Async<unit> =
    async {
        return! runLegacyBkash
    }

let runBkash : Async<unit> =
    async {
        return! runBkash
    }

let runPortPos : Async<unit> =
    async {
        return! runPortPos
    }

let runBraintree : Async<unit> =
    async {
        return! runBraintree
    }

let runNagad : Async<unit> =
    async {
        return! runNagad
    }