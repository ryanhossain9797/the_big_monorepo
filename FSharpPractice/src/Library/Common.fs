module Common

open System.Net
open System.Text.Json.Nodes
open Models

let BkashBindRoute = NonemptyString.ofLiteral "/BkashBind/"

module JsonObject =
    let tryFind (key: string) (jsonObject: JsonObject) : Option<JsonNode> =
        match jsonObject.ContainsKey(key) with
        | true -> Some(jsonObject[key])
        | false -> None

module JsonNode =
    let tryFind (key: string) (jsonNode: JsonNode) : Option<JsonNode> =
        jsonNode.AsObject() |> JsonObject.tryFind key

    let maybeTryFind (key: string) (maybeJsonNode: Option<JsonNode>) : Option<JsonNode> =
        match maybeJsonNode with
        | Some node -> node.AsObject() |> JsonObject.tryFind key
        | None -> None

    let toNonemptyString (maybeJsonNode: Option<JsonNode>) : Option<NonemptyString> =
        match maybeJsonNode with
        | Some node -> node |> string |> NonemptyString.ofString
        | None -> None

let handleApiException (request: NonemptyString) (unknownErrorMessage: string) (exn: exn) =

    let requestAndResponse = { Request = request; Response = None }

    let getErrorMessageOrDefault message =
        message
        |> NonemptyString.ofString
        |> Option.map (fun msg -> msg.Value)
        |> Option.defaultValue "NO ERROR MESSAGE"

    try
        match exn with
        | :? WebException as err ->
            match err.Status with
            | WebExceptionStatus.Timeout -> requestAndResponse |> Timeout
            | _ ->
                (
                    requestAndResponse,
                    $"{unknownErrorMessage} - {err.Message |> getErrorMessageOrDefault}" |> NonemptyString.ofString
                )
                |> WebError

        | _ ->
            (
                requestAndResponse,
                $"{unknownErrorMessage} - {exn.Message |> getErrorMessageOrDefault}" |> NonemptyString.ofString
            )
            |> Unknown
    with
    | error ->
        (
            requestAndResponse,
            $"{unknownErrorMessage} - Unknown error occured {exn.ToString()}" |> NonemptyString.ofString
        )
        |> Unknown