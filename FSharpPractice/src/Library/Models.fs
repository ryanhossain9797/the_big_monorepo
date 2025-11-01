module Models

open System.Net.Http

type RequestAndResponse = {
    Request:  NonemptyString
    Response: Option<NonemptyString>
}

type ApiError =
| ErrorCode of RequestAndResponse * Error: Option<NonemptyString>
| Timeout   of RequestAndResponse
| WebError  of RequestAndResponse * Error: Option<NonemptyString>
| Unknown   of RequestAndResponse * Error: Option<NonemptyString>
| DataError of RequestAndResponse * Error: Option<NonemptyString>

type HttpMethodWithData =
| Get
| Post of MaybeBody: Option<NonemptyString>
with
    member this.toMethod =
        match this with
        | Get    -> HttpMethod.Get
        | Post _ -> HttpMethod.Post

type Platform =
| Ios
| Android
| Windows
| Linux
| MacOs
| DesktopWeb
| MobileWeb

type PaymentInstrument = PaymentInstrument of Instrument: NonemptyString * Platform