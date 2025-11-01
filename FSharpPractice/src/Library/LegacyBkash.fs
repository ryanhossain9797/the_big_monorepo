module Methods.LegacyBkash

open System
open System.Net.Http
open System.Net.Mime
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.Json.Serialization
open System.Threading.Tasks
open Models
open Common

let legacyBkashPaymentExpiryDays = 7u

let apiEndpoint = "https://www.bkashcluster.com:9081/dreamwave" |> NonemptyString.ofLiteral

type LegacyBkashConfig = {
    PaymentAccountHead: NonemptyString
    Username:           NonemptyString
    Password:           NonemptyString
    Msisdn:             NonemptyString
}

type LegacyBkashFailure =
| DuplicateRequestWithinFiveMinutes
| TrxIdTooOld
| TransactionPending
| TransactionReversed
| TransactionFailed
| InvalidMSISDN
| InvalidTransactionId
| InvalidUsernameOrPassword
| TransactionIdNotValidForUserName
| AccessDeniedToModule
| AccessDeniedUserDateTimeExceedsLimit
| MissingFields
| UnknownResultCode of int
| TxIdAlreadyUsed
| CouldNotProcessRequest
| UnknownError
| NonLegacyBkashFailure
// "COOKUPSTECLRM42744"
// 01887454024
let legacyBkashSandboxConfig = {
    PaymentAccountHead = NonemptyString.ofLiteral "LegacyBkash"
    Username           = NonemptyString.ofLiteral "COOKUPSTECLRM42744"
    Password           = NonemptyString.ofLiteral "raY@6cHLpq"
    Msisdn             = NonemptyString.ofLiteral "01887454024"
}

// -------------------------------------------------------------------------------------------------------------- SHARED CONTENT START

type LegacyBkashEndpoint =
| TrxCheck
    member this.toString =
        match this with
        | TrxCheck        -> "/merchant/trxcheck/sendmsg"
        |> NonemptyString.ofLiteral

let generateRequest
        (uri:     Uri)
        (request: HttpMethodWithData) =
    let mutable httpRequest =
        new HttpRequestMessage(
            request.toMethod,
            uri)

    match request with
    | Post (Some requestBody) ->
        let content =
            new StringContent(
                requestBody.Value,
                encoding = Encoding.UTF8,
                mediaType = MediaTypeNames.Application.Json)

        httpRequest.Content <- content
    | _ -> ()

    httpRequest

type TrxIdApiRequest = {
    [<JsonPropertyName("user")>]   User:   string
    [<JsonPropertyName("pass")>]   Pass:   string
    [<JsonPropertyName("msisdn")>] Msisdn: string
    [<JsonPropertyName("trxid")>]  TrxId:  string
}

let legacyBkashRequest
        (httpClient:  HttpClient)
        (apiEndpoint: NonemptyString)
        (endpoint:    LegacyBkashEndpoint)
        (request:     HttpMethodWithData) =

    task {
        let builder = UriBuilder($"{apiEndpoint.Value}{endpoint.toString.Value}")

        let req =
            generateRequest
                builder.Uri
                request

        let! response = httpClient.SendAsync req
        let! responseString = response.Content.ReadAsStringAsync()

        return (responseString |> NonemptyString.ofStringUnsafe)
    }

let public queryLegacyBkashTrxStatus
        (httpClient:  HttpClient)
        (apiEndpoint: NonemptyString)
        (config:      LegacyBkashConfig)
        (trxId:       NonemptyString)
    : Task<Result<PositiveDecimal * RequestAndResponse, LegacyBkashFailure * ApiError>> =
    task {
        let nowBdTz = DateTimeOffset.Now |> toBdt

        let endpoint = LegacyBkashEndpoint.TrxCheck

        let request =
            {
                User   = config.Username.Value
                Pass   = config.Password.Value
                Msisdn = config.Msisdn.Value
                TrxId  = trxId.Value
            }
            |> JsonSerializer.Serialize<TrxIdApiRequest>
            |> NonemptyString.ofStringUnsafe

        try
            // {
            //    "transaction":{
            //       "trx_id":"ACS1UOU1C9",
            //       "amount":"1999.5",
            //       "counter":"1",
            //       "currency":"BDT",
            //       "datetime":"2023-03-28T13:35:35+06:00",
            //       "receiver":"01818181818",
            //       "reference":"abc",
            //       "sender":"01717171717",
            //       "service":"Payment",
            //       "trx_status":"0000",
            //       "reversed":"0"
            //    }
            // }
            let! legacyBkashResponse =
                legacyBkashRequest
                    httpClient
                    apiEndpoint
                    endpoint
                    (HttpMethodWithData.Post (Some request))

            let jsonData = JsonSerializer.Deserialize<JsonObject>(legacyBkashResponse.Value)

            let maybeStatusWithAmountResult =
                match jsonData |> JsonObject.tryFind "transaction" with
                | Some transaction ->
                    match
                        (transaction |> JsonNode.tryFind "trxId" |> Option.map string |> Option.contains trxId.Value),
                        (transaction |> JsonNode.tryFind "trxStatus" |> Option.map (string >> Int32.TryParse))
                    with
                    | true, Some (true, status) ->
                        let maybeAmount =
                            match (transaction |> JsonNode.tryFind "amount" |> Option.map (string >> Decimal.TryParse)) with
                            | Some (true, amount) -> amount |> PositiveDecimal.ofDecimal
                            | _ -> None

                        let maybeTimeStamp =
                            match (transaction |> JsonNode.tryFind "trxTimestamp" |> Option.map (string >> DateTimeOffset.TryParse)) with
                            | Some (true, trxTimeStamp) -> Some trxTimeStamp
                            | _ -> None

                        (Ok (status, maybeAmount, maybeTimeStamp))

                    | _ ->
                        (Error "trx id or trx status inconsistent")

                | None ->
                    (Error "No transaction in response")

            return
                match maybeStatusWithAmountResult with
                | Ok statusAndAmountWithTimeStamp ->
                    // Code       | Message                                                                   | Interpretation
                    // 0000       | trxID is valid and transaction is successful.                             | Transaction Successful
                    // 0010, 0011 | trxID is valid but transaction is in pending state.                       | Transaction Pending
                    // 0100       | trxID is valid but transaction has been reversed.                         | Transaction Reversed
                    // 0111       | trxID is valid but transaction has failed.                                | Transaction Failure
                    // 1001       | Invalid MSISDN input. Try with correct mobile no.                         | Format Error
                    // 1002       | Invalid trxID, it does not exist.                                         | Invalid Reference
                    // 1003       | Access denied. Username or Password is incorrect.                         | Authorization Error
                    // 1004       | Access denied. trxID is not related to this username.                     | Authorization Error
                    // 2000       | Access denied. User does not have access to this module.                  | Authorization Error
                    // 2001       | Access denied. User date time request is exceeded of the defined limit.   | Date time limit Error
                    // 3000       | Missing required mandatory fields for this module.                        | Missing fields Error
                    // 4001       | Duplicate request. Consecutive hit for the same request within 5 minutes. | Duplicate Error
                    // 9999       | Could not process request.                                                | System Error
                    match statusAndAmountWithTimeStamp with
                    | 0, Some amount, Some timeStamp when (nowBdTz < timeStamp.AddDays(legacyBkashPaymentExpiryDays |> float)) ->
                        (amount, { Request = request; Response = legacyBkashResponse |> Some }) |> Ok
                    | 0, Some _, Some timeStamp ->
                        (LegacyBkashFailure.TrxIdTooOld, ({ Request = request; Response = Some legacyBkashResponse }, $"trx id is too old {timeStamp}" |> NonemptyString.ofString) |> DataError) |> Error
                    | 0, _, None ->
                        (LegacyBkashFailure.UnknownError, ({ Request = request; Response = Some legacyBkashResponse }, "trx timestamp not available on a successful payment" |> NonemptyString.ofString) |> DataError) |> Error
                    | 0, None, _ ->
                        (LegacyBkashFailure.UnknownError, ({ Request = request; Response = Some legacyBkashResponse }, "amount not available on a successful payment" |> NonemptyString.ofString) |> DataError) |> Error
                    //Duplicate request. Consecutive hit for the same request within 5 minutes
                    | 4001, _, _ ->
                        //Sort of a hack, but makes sense too? ApiError.Unknown will trigger a retry, which we want in this case
                        (LegacyBkashFailure.UnknownError, ({ Request = request; Response = Some legacyBkashResponse }, "duplicate request within five minutes" |> NonemptyString.ofString) |> Unknown) |> Error
                    | status, _, _ ->
                        let errorDefinition =
                            match status with
                            | 10 | 11 -> LegacyBkashFailure.TransactionPending
                            | 100 -> LegacyBkashFailure.TransactionReversed
                            | 111 -> LegacyBkashFailure.TransactionFailed
                            | 1001 -> LegacyBkashFailure.InvalidMSISDN
                            | 1002 -> LegacyBkashFailure.InvalidTransactionId
                            | 1003 -> LegacyBkashFailure.InvalidUsernameOrPassword
                            | 1004 -> LegacyBkashFailure.TransactionIdNotValidForUserName
                            | 2000 -> LegacyBkashFailure.AccessDeniedToModule
                            | 2001 -> LegacyBkashFailure.AccessDeniedUserDateTimeExceedsLimit
                            | 3000 -> LegacyBkashFailure.MissingFields
                            | 9999 -> LegacyBkashFailure.CouldNotProcessRequest
                            | _ -> LegacyBkashFailure.UnknownResultCode status

                        (errorDefinition, ({ Request = request; Response = Some legacyBkashResponse }, $"Status is: {status}" |> NonemptyString.ofString) |> ErrorCode) |> Error

                | Error error ->
                    (LegacyBkashFailure.UnknownError, ({ Request = request; Response = Some legacyBkashResponse }, error |> NonemptyString.ofString) |> DataError) |> Error

        with
        | error ->
            return
                (LegacyBkashFailure.NonLegacyBkashFailure, error |> handleApiException request "Unexpected error while initiating bkash direct charge prepare API")
                |> Error
    }

// -------------------------------------------------------------------------------------------------------------- SHARED CONTENT END

// ------------------------------------- QQQ

let runLegacyBkash : Async<unit> =
    async {
        let httpClient = new HttpClient()
        let config = legacyBkashSandboxConfig

        // ----------------------------------------------- Verify Legacy Bkash
        printf "Enter TrxId: "
        let trxId = Console.ReadLine() |> NonemptyString.ofLiteral
        let! verificationResult = queryLegacyBkashTrxStatus httpClient apiEndpoint config trxId |> Async.AwaitTask
        printfn $"%A{verificationResult}"


        return ()
    }