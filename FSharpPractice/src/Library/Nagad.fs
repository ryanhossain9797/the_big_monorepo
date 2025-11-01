module Methods.Nagad

open System
open System.Globalization
open System.Net
open System.Net.Http
open System.Net.Mime
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.Json.Serialization
open Models
open Common
open Org.BouncyCastle.Security

type NagadConfig = {
    MerchantId:            NonemptyString
    NagadGatewayPublicKey: NonemptyString
    MerchantPrivateKey:    NonemptyString
    MerchantPublicKey:     NonemptyString
}

type NagadTransactionStatus =
| Success
| OrderInitiated
| Ready
| InProgress
| OtpSent
| OtpVerified
| PinGiven
| Cancelled
| PartialCancelled
| Refunded
| PartialRefunded
| InvalidRequest
| Fraud
| Aborted
| UnknownFailed
| Other of string
with
    static member fromStatus (status: NonemptyString) =
        match status.Value with
        | "Success"               -> Some Success
        | "OrderInitiated"        -> Some OrderInitiated
        | "Ready"                 -> Some Ready
        | "TransactionInProgress"
        | "InProgress"            -> Some InProgress
        | "OtpSent"               -> Some OtpSent
        | "OtpVerified"           -> Some OtpVerified
        | "PinGiven"              -> Some PinGiven
        | "Cancelled"             -> Some Cancelled
        | "PartialCancelled"      -> Some PartialCancelled
        | "Refunded"              -> Some Refunded
        | "PartialRefunded"       -> Some PartialRefunded
        | "InvalidRequest"        -> Some InvalidRequest
        | "Fraud"                 -> Some Fraud
        | "Aborted"               -> Some Aborted
        | "UnKnownFailed"
        | "Failed"                -> Some UnknownFailed
        | _                       -> None

let NagadApiEndpoint = "http://sandbox.mynagad.com:10080/remote-payment-gateway-1.0/api/dfs" |> NonemptyString.ofLiteral //TODO Move to config

let nagadSandboxConfig = {
    MerchantId            = NonemptyString.ofLiteral "683002007104225"
    NagadGatewayPublicKey = NonemptyString.ofLiteral "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAjBH1pFNSSRKPuMcNxmU5jZ1x8K9LPFM4XSu11m7uCfLUSE4SEjL30w3ockFvwAcuJffCUwtSpbjr34cSTD7EFG1Jqk9Gg0fQCKvPaU54jjMJoP2toR9fGmQV7y9fz31UVxSk97AqWZZLJBT2lmv76AgpVV0k0xtb/0VIv8pd/j6TIz9SFfsTQOugHkhyRzzhvZisiKzOAAWNX8RMpG+iqQi4p9W9VrmmiCfFDmLFnMrwhncnMsvlXB8QSJCq2irrx3HG0SJJCbS5+atz+E1iqO8QaPJ05snxv82Mf4NlZ4gZK0Pq/VvJ20lSkR+0nk+s/v3BgIyle78wjZP1vWLU4wIDAQAB"
    MerchantPrivateKey    = NonemptyString.ofLiteral "MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQCJakyLqojWTDAVUdNJLvuXhROV+LXymqnukBrmiWwTYnJYm9r5cKHj1hYQRhU5eiy6NmFVJqJtwpxyyDSCWSoSmIQMoO2KjYyB5cDajRF45v1GmSeyiIn0hl55qM8ohJGjXQVPfXiqEB5c5REJ8Toy83gzGE3ApmLipoegnwMkewsTNDbe5xZdxN1qfKiRiCL720FtQfIwPDp9ZqbG2OQbdyZUB8I08irKJ0x/psM4SjXasglHBK5G1DX7BmwcB/PRbC0cHYy3pXDmLI8pZl1NehLzbav0Y4fP4MdnpQnfzZJdpaGVE0oI15lq+KZ0tbllNcS+/4MSwW+afvOw9bazAgMBAAECggEAIkenUsw3GKam9BqWh9I1p0Xmbeo+kYftznqai1pK4McVWW9//+wOJsU4edTR5KXK1KVOQKzDpnf/CU9SchYGPd9YScI3n/HR1HHZW2wHqM6O7na0hYA0UhDXLqhjDWuM3WEOOxdE67/bozbtujo4V4+PM8fjVaTsVDhQ60vfv9CnJJ7dLnhqcoovidOwZTHwG+pQtAwbX0ICgKSrc0elv8ZtfwlEvgIrtSiLAO1/CAf+uReUXyBCZhS4Xl7LroKZGiZ80/JE5mc67V/yImVKHBe0aZwgDHgtHh63/50/cAyuUfKyreAH0VLEwy54UCGramPQqYlIReMEbi6U4GC5AQKBgQDfDnHCH1rBvBWfkxPivl/yNKmENBkVikGWBwHNA3wVQ+xZ1Oqmjw3zuHY0xOH0GtK8l3Jy5dRL4DYlwB1qgd/Cxh0mmOv7/C3SviRk7W6FKqdpJLyaE/bqI9AmRCZBpX2PMje6Mm8QHp6+1QpPnN/SenOvoQg/WWYM1DNXUJsfMwKBgQCdtddE7A5IBvgZX2o9vTLZY/3KVuHgJm9dQNbfvtXw+IQfwssPqjrvoU6hPBWHbCZl6FCl2tRh/QfYR/N7H2PvRFfbbeWHw9+xwFP1pdgMug4cTAt4rkRJRLjEnZCNvSMVHrri+fAgpv296nOhwmY/qw5Smi9rMkRY6BoNCiEKgQKBgAaRnFQFLF0MNu7OHAXPaW/ukRdtmVeDDM9oQWtSMPNHXsx+crKY/+YvhnujWKwhphcbtqkfj5L0dWPDNpqOXJKV1wHt+vUexhKwus2mGF0flnKIPG2lLN5UU6rs0tuYDgyLhAyds5ub6zzfdUBG9Gh0ZrfDXETRUyoJjcGChC71AoGAfmSciL0SWQFU1qjUcXRvCzCK1h25WrYS7E6pppm/xia1ZOrtaLmKEEBbzvZjXqv7PhLoh3OQYJO0NM69QMCQi9JfAxnZKWx+m2tDHozyUIjQBDehve8UBRBRcCnDDwU015lQN9YNb23Fz+3VDB/LaF1D1kmBlUys3//r2OV0Q4ECgYBnpo6ZFmrHvV9IMIGjP7XIlVa1uiMCt41FVyINB9SJnamGGauW/pyENvEVh+ueuthSg37e/l0Xu0nm/XGqyKCqkAfBbL2Uj/j5FyDFrpF27PkANDo99CdqL5A4NQzZ69QRlCQ4wnNCq6GsYy2WEJyU2D+K8EBSQcwLsrI7QL7fvQ=="
    MerchantPublicKey     = NonemptyString.ofLiteral "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAiWpMi6qI1kwwFVHTSS77l4UTlfi18pqp7pAa5olsE2JyWJva+XCh49YWEEYVOXosujZhVSaibcKccsg0glkqEpiEDKDtio2MgeXA2o0ReOb9RpknsoiJ9IZeeajPKISRo10FT314qhAeXOURCfE6MvN4MxhNwKZi4qaHoJ8DJHsLEzQ23ucWXcTdanyokYgi+9tBbUHyMDw6fWamxtjkG3cmVAfCNPIqyidMf6bDOEo12rIJRwSuRtQ1+wZsHAfz0WwtHB2Mt6Vw5iyPKWZdTXoS822r9GOHz+DHZ6UJ382SXaWhlRNKCNeZavimdLW5ZTXEvv+DEsFvmn7zsPW2swIDAQAB"
}

// -------------------------------------------------------------------------------------------------------------- SHARED CONTENT START
[<Literal>]
let private timeStampFormatter = "yyyyMMddHHmmss"
[<Literal>]
let private dateFormatter = "yyyyMMdd"

type NagadApiError =
| InvalidMerchant
| InactiveMerchant
| EncryptionError
| DecryptionError
| SignatureVerificationFailed
| InvalidApiRequest
| InvalidOrderId
| DuplicateOrderId
| InvalidPurchaseInformation
| InvalidReferenceId
| EncodingError
| InvalidCurrency
| ServerError

type NagadEndpoint =
| Initialize
| Create
| Verify
| Cancel
    member this.toString =
        match this with
        | Initialize -> "/check-out/initialize/"
        | Create     -> "/check-out/complete/"
        | Verify     -> "/verify/payment/"
        | Cancel     -> "/purchase/cancel"


let private encryptMessage (key: string) (plainText: string) : string =
    let cipherProvider = CipherUtilities.GetCipher("RSA/NONE/PKCS1Padding")
    cipherProvider.Init (true, key |> Convert.FromBase64String |> PublicKeyFactory.CreateKey)
    cipherProvider.DoFinal (plainText |> Encoding.UTF8.GetBytes) |> Convert.ToBase64String

let private decryptMessage (key: string) (encryptedText: string) : string =
    let cipherProvider = CipherUtilities.GetCipher("RSA/NONE/PKCS1Padding")
    cipherProvider.Init (false, key |> Convert.FromBase64String |> PrivateKeyFactory.CreateKey)
    cipherProvider.DoFinal (encryptedText |> Convert.FromBase64String) |> Encoding.UTF8.GetString

let private generateSignature (key: string) (plainText: string) : string =
    let dataBytes = plainText |> Encoding.UTF8.GetBytes
    let signerProvider = SignerUtilities.GetSigner("SHA-256withRSA")
    signerProvider.Init (true, key |> Convert.FromBase64String |> PrivateKeyFactory.CreateKey)
    signerProvider.BlockUpdate (dataBytes, 0, dataBytes.Length)
    signerProvider.GenerateSignature() |> Convert.ToBase64String

let private isValidSignature (key: string) (decryptedText: string) (signature: string) : bool =
    let dataBytes = decryptedText |> Encoding.UTF8.GetBytes
    let signerProvider = SignerUtilities.GetSigner("SHA-256withRSA")
    signerProvider.Init(false, key |> Convert.FromBase64String |> PublicKeyFactory.CreateKey)
    signerProvider.BlockUpdate(dataBytes, 0, dataBytes.Length)
    signature |> Convert.FromBase64String |> signerProvider.VerifySignature

let private generateRequest
        (url:     NonemptyString)
        (request: HttpMethodWithData) =
    let mutable httpRequest =
        new HttpRequestMessage(
            request.toMethod,
            url.Value)

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

let nagadRequest
        (httpClient:  HttpClient)
        (apiEndpoint: NonemptyString)
        (endpoint:    NagadEndpoint)
        (instrument:  PaymentInstrument)
        (urlParams:   Option<NonemptyString>)
        (queryParams: Option<NonemptyString>)
        (merchantId:  NonemptyString)
        (request:     HttpMethodWithData) =
    task {
        let urlParams =
            urlParams
            |> Option.map (fun urlParams -> $"{urlParams.Value}" |> NonemptyString.ofLiteral)
            |> NonemptyString.optionToString

        let queryParams =
            queryParams
            |> NonemptyString.optionToString

        let url = apiEndpoint.Value + endpoint.toString + urlParams + queryParams |> NonemptyString.ofStringUnsafe

        let (PaymentInstrument (_, platform)) = instrument

        let clientType =
            match platform with
            | Android
            | Ios ->
                "MOBILE_APP"
            | _ ->
                "PC_WEB"

        let req =
            generateRequest
                url
                request

        req.Headers.Add ("X-KM-Api-Version", "v-0.2.0")
        req.Headers.Add ("X-KM-MC-Id",       merchantId.Value)
        req.Headers.Add ("X-KM-Client-Type", clientType)
        req.Headers.Add ("X-KM-IP-V4",       "118.179.93.246") //TODO IP Address

        let! response = httpClient.SendAsync req
        let! responseString = response.Content.ReadAsStringAsync()
        return (response.StatusCode, responseString |> NonemptyString.ofString)
    }


type EncryptedInitializeRequestBody = {
    [<JsonPropertyName("dateTime")>]      DateTime:      string
    [<JsonPropertyName("sensitiveData")>] SensitiveData: string
    [<JsonPropertyName("signature")>]     Signature:     string
}

type InitializeSensitiveData = {
    [<JsonPropertyName("merchantId")>] MerchantId: string
    [<JsonPropertyName("datetime")>]   DateTime:   string
    [<JsonPropertyName("orderId")>]    OrderId:    string
    [<JsonPropertyName("challenge")>]  Challenge:  string
}

type EncryptedCreateOrderBody = {
    [<JsonPropertyName("merchantCallbackURL")>] MerchantCallbackURL: string
    [<JsonPropertyName("sensitiveData")>]       SensitiveData:       string
    [<JsonPropertyName("signature")>]           Signature:           string
}

type CreateOrderSensitiveData = {
    [<JsonPropertyName("merchantId")>]   MerchantId:   string
    [<JsonPropertyName("currencyCode")>] CurrencyCode: string
    [<JsonPropertyName("orderId")>]      OrderId:      string
    [<JsonPropertyName("amount")>]       Amount:       string
    [<JsonPropertyName("challenge")>]    Challenge:    string
}

type EncryptedRefundRequestBody = {
    [<JsonPropertyName("sensitiveDataCancelRequest")>] SensitiveDataCancelRequest: string
    [<JsonPropertyName("signature")>]                  Signature:                  string
}

type RefundSensitiveData = {
    [<JsonPropertyName("merchantId")>]          MerchantId:          string
    [<JsonPropertyName("originalRequestDate")>] OriginalRequestDate: string
    [<JsonPropertyName("originalAmount")>]      OriginalAmount:      string
    [<JsonPropertyName("cancelAmount")>]        CancelAmount:        string
    [<JsonPropertyName("referenceNo")>]         ReferenceNo:         string
    [<JsonPropertyName("referenceMessage")>]    ReferenceMessage:    string
}

type InitializePaymentResponse = {
    [<JsonPropertyName("paymentReferenceId")>]  PaymentReferenceId: string
    [<JsonPropertyName("challenge")>]           Challenge:          string
    [<JsonPropertyName("acceptDateTime")>]      AcceptDateTime:     string
}

type VerifyPaymentResponse = {
    [<JsonPropertyName("amount")>]     Amount:     string
    [<JsonPropertyName("status")>]     Status:     string
    [<JsonPropertyName("statusCode")>] StatusCode: string
}

type RefundPaymentResponse = {
    [<JsonPropertyName("status")>]         Status:         string
    [<JsonPropertyName("code")>]           Code:           string
    [<JsonPropertyName("originalAmount")>] OriginalAmount: string
    [<JsonPropertyName("cancelAmount")>]   CancelAmount:   string
    [<JsonPropertyName("cancelTrxId")>]    CancelTrxId:    string
}

let private initializeNagadPayment
        (httpClient:  HttpClient)
        (apiEndpoint: NonemptyString)
        (config:      NagadConfig)
        (instrument:  PaymentInstrument)
        (orderId:     NonemptyString) (*Implement alphanumeric string*) =
    task {
        let dateTime = (DateTimeOffset.Now |> toBdt).ToString timeStampFormatter

        let sensitiveData: InitializeSensitiveData = {
            MerchantId = config.MerchantId.Value
            DateTime   = dateTime
            OrderId    = orderId.Value
            Challenge  = Guid.NewGuid().ToString()
        }

        let plainTextSensitiveData = JsonSerializer.Serialize<InitializeSensitiveData>(sensitiveData)

        let encryptedBase64Message = encryptMessage config.NagadGatewayPublicKey.Value plainTextSensitiveData
        let encryptedBase64Signature = generateSignature config.MerchantPrivateKey.Value plainTextSensitiveData

        let request =
            {
                DateTime      = dateTime
                SensitiveData = encryptedBase64Message
                Signature     = encryptedBase64Signature
            }
            |> JsonSerializer.Serialize<EncryptedInitializeRequestBody>
            |> NonemptyString.ofStringUnsafe

        try
            let! statusCode, maybeNagadResponse =
                nagadRequest
                    httpClient
                    apiEndpoint
                    NagadEndpoint.Initialize
                    instrument
                    ($"{config.MerchantId.Value}/{orderId.Value}" |> NonemptyString.ofString)
                    None
                    config.MerchantId
                    (Some request |> HttpMethodWithData.Post)

            match statusCode, maybeNagadResponse with
            | HttpStatusCode.OK, Some nagadResponse ->
                let jsonData = JsonSerializer.Deserialize<JsonObject>(nagadResponse.Value)

                let maybeSensitiveData = jsonData |> JsonObject.tryFind "sensitiveData" |> JsonNode.toNonemptyString
                let maybeSignature = jsonData |> JsonObject.tryFind "signature" |> JsonNode.toNonemptyString

                match
                    maybeSensitiveData
                    |> Option.bind (
                        fun sensitiveData ->
                            decryptMessage config.MerchantPrivateKey.Value sensitiveData.Value
                            |> NonemptyString.ofString
                            |> Option.bind (
                                fun decryptedData ->
                                    maybeSignature
                                    |> Option.bind (
                                        fun signature ->
                                            match isValidSignature config.NagadGatewayPublicKey.Value decryptedData.Value signature.Value with
                                            | true ->
                                                Some (JsonSerializer.Deserialize<InitializePaymentResponse>(decryptedData.Value))

                                            | false ->
                                                None
                                    )

                            )

                    )
                with
                | Some responseData ->
                    match
                        responseData.PaymentReferenceId |> NonemptyString.ofString,
                        responseData.Challenge |> NonemptyString.ofString,
                        responseData.AcceptDateTime |> NonemptyString.ofString
                    with
                    | Some paymentReferenceId, Some challenge, Some acceptDateTimeString ->
                        let acceptDateTime =
                            (DateTimeOffset.ParseExact (acceptDateTimeString.Value, timeStampFormatter, CultureInfo.InvariantCulture.DateTimeFormat))
                            |> toBdt

                        return
                            (
                                paymentReferenceId,
                                challenge,
                                acceptDateTime,
                                { Request = request ; Response = Some nagadResponse }
                            )
                            |> Ok

                    | _ ->
                        return
                            (
                                { Request = request; Response = Some nagadResponse},
                                $"HttpStatusCode: {statusCode}, PaymentReferenceId: {responseData.PaymentReferenceId}, Challenge: {responseData.Challenge} or AcceptDateTime: {responseData.AcceptDateTime} is invalid" |> NonemptyString.ofString
                            )
                            |> DataError
                            |> Error

                | None ->
                    return
                        (
                            { Request = request; Response = Some nagadResponse},
                            $"HttpStatusCode: {statusCode}, Invalid response data" |> NonemptyString.ofString
                        )
                        |> DataError
                        |> Error

            | HttpStatusCode.OK, None ->
                //Ok response but no body
                return
                    (
                        { Request = request; Response = None},
                        $"HttpStatusCode: {statusCode}, Response is invalid: {maybeNagadResponse}" |> NonemptyString.ofString
                    )
                    |> DataError
                    |> Error

            | _, Some nagadErrorResponse ->
                //Error with a body
                return
                    (
                        { Request = request; Response = Some nagadErrorResponse},
                        $"HttpStatusCode: {statusCode}, Error: {nagadErrorResponse.Value}" |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error

            | _ ->
                //Error but missing error response body
                return
                    (
                        { Request = request; Response = None },
                        $"HttpStatusCode: {statusCode}, Invalid error response: {maybeNagadResponse}" |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error

        with
        | error ->
            return
                error
                |> handleApiException request "Unexpected error while initializing nagad payment"
                |> Error
    }

let private createNagadPayment
        (httpClient:         HttpClient)
        (apiEndpoint:        NonemptyString)
        (config:             NagadConfig)
        (instrument:         PaymentInstrument)
        (redirectUrl:        NonemptyString)
        (amount:             PositiveDecimal) (*Implement alphanumeric string*)
        (orderId:            NonemptyString)
        (paymentReferenceId: NonemptyString)
        (challenge:          NonemptyString) =

    task {
        let sensitiveData: CreateOrderSensitiveData = {
            Amount       = amount.Value.ToString("F")
            CurrencyCode = "050" // for BDT
            OrderId      = orderId.Value
            MerchantId   = config.MerchantId.Value
            Challenge    = challenge.Value
        }

        let plainTextSensitiveData = JsonSerializer.Serialize<CreateOrderSensitiveData>(sensitiveData)

        let encryptedBase64Message = encryptMessage config.NagadGatewayPublicKey.Value plainTextSensitiveData
        let encryptedBase64Signature = generateSignature config.MerchantPrivateKey.Value plainTextSensitiveData

        let request =
            {
                MerchantCallbackURL = redirectUrl.Value
                SensitiveData       = encryptedBase64Message
                Signature           = encryptedBase64Signature
            }
            |> JsonSerializer.Serialize<EncryptedCreateOrderBody>
            |> NonemptyString.ofStringUnsafe



        try
            let! statusCode, maybeNagadResponse =
                nagadRequest
                    httpClient
                    apiEndpoint
                    NagadEndpoint.Create
                    instrument
                    ($"{paymentReferenceId.Value}" |> NonemptyString.ofString)
                    None
                    config.MerchantId
                    (Some request |> HttpMethodWithData.Post)

            match statusCode, maybeNagadResponse with
            | HttpStatusCode.OK, Some nagadResponse ->
                let jsonData = JsonSerializer.Deserialize<JsonObject>(nagadResponse.Value)

                match
                    jsonData |> JsonObject.tryFind "status" |> JsonNode.toNonemptyString |> Option.bind NagadTransactionStatus.fromStatus,
                    jsonData |> JsonObject.tryFind "callBackUrl" |> JsonNode.toNonemptyString
                with
                | Some Success, Some callbackUrl ->
                    return
                        (
                            callbackUrl,
                            { Request = request ; Response = Some nagadResponse }
                        )
                        |> Ok
                | maybeStatus, maybeCallbackUrl ->
                    return
                        (
                            { Request = request; Response = Some nagadResponse},
                            $"HttpStatusCode: {statusCode}, Status is not successful: {maybeStatus}, MaybeCallbackUrl: {maybeCallbackUrl}" |> NonemptyString.ofString
                        )
                        |> ErrorCode
                        |> Error

            | HttpStatusCode.OK, None ->
                //Ok response but no body
                return
                    (
                        { Request = request; Response = None},
                        $"HttpStatusCode: {statusCode}, Response is invalid: {maybeNagadResponse}" |> NonemptyString.ofString
                    )
                    |> DataError
                    |> Error

            | _, Some nagadErrorResponse ->
                //Error with a body
                return
                    (
                        { Request = request; Response = Some nagadErrorResponse},
                        $"HttpStatusCode: {statusCode}, Error: {nagadErrorResponse.Value}" |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error

            | _ ->
                //Error but missing error response body
                return
                    (
                        { Request = request; Response = None },
                        $"HttpStatusCode: {statusCode}, Invalid error response: {maybeNagadResponse}" |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error
        with
        | error ->
            return
                error
                |> handleApiException request "Unexpected error creating nagad payment after initialization"
                |> Error
    }

let private verifyNagadPayment
        (httpClient:         HttpClient)
        (apiEndpoint:        NonemptyString)
        (config:             NagadConfig)
        (instrument:         PaymentInstrument)
        (paymentReferenceId: NonemptyString) =

    task {
        let requestInfo = apiEndpoint.Value + NagadEndpoint.Create.toString + $"/{paymentReferenceId.Value}" |> NonemptyString.ofStringUnsafe

        try
            let! statusCode, maybeNagadResponse =
                nagadRequest
                    httpClient
                    apiEndpoint
                    NagadEndpoint.Verify
                    instrument
                    ($"{paymentReferenceId.Value}" |> NonemptyString.ofString)
                    None
                    config.MerchantId
                    HttpMethodWithData.Get


            match statusCode, maybeNagadResponse with
            | HttpStatusCode.OK, Some nagadResponse ->
                let jsonData = JsonSerializer.Deserialize<JsonObject>(nagadResponse.Value)

                let nagadTransactionStatus = jsonData |> JsonObject.tryFind "status" |> JsonNode.toNonemptyString |> Option.bind NagadTransactionStatus.fromStatus
                match nagadTransactionStatus with
                | Some Success ->
                    match jsonData |> JsonObject.tryFind "issuerPaymentRefNo" |> JsonNode.toNonemptyString with
                    | Some issuerPaymentRef ->
                        return
                            (issuerPaymentRef |> Choice1Of2, { Request = requestInfo ; Response = Some nagadResponse })
                            |> Ok
                    | None ->
                        return
                            (
                                { Request = requestInfo; Response = None},
                                $"HttpStatusCode: {statusCode}, Response missing issuer payment ref: {maybeNagadResponse}" |> NonemptyString.ofString
                            )
                            |> DataError
                            |> Error

                | Some OrderInitiated
                | Some Ready
                | Some InProgress
                | Some OtpSent
                | Some OtpVerified
                | Some PinGiven ->
                    return
                        (nagadTransactionStatus.Value |> Choice2Of2, { Request = requestInfo ; Response = Some nagadResponse })
                        |> Ok
                | maybeStatus ->
                    return
                        (
                            { Request = requestInfo; Response = Some nagadResponse},
                            $"HttpStatusCode: {statusCode}, Status is not successful: {maybeStatus}" |> NonemptyString.ofString
                        )
                        |> ErrorCode
                        |> Error

            | HttpStatusCode.OK, None ->
                //Ok response but no body
                return
                    (
                        { Request = requestInfo; Response = None},
                        $"HttpStatusCode: {statusCode}, Response is invalid: {maybeNagadResponse}" |> NonemptyString.ofString
                    )
                    |> DataError
                    |> Error

            | _, Some nagadErrorResponse ->
                //Error with a body
                return
                    (
                        { Request = requestInfo; Response = Some nagadErrorResponse},
                        $"HttpStatusCode: {statusCode}, Error: {nagadErrorResponse.Value}" |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error

            | _ ->
                //Error but missing error response body
                return
                    (
                        { Request = requestInfo; Response = None },
                        $"HttpStatusCode: {statusCode}, Invalid error response: {maybeNagadResponse}" |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error
        with
        | error ->
            return
                error
                |> handleApiException requestInfo "Unexpected error creating nagad payment after initialization"
                |> Error
    }

let private refundNagadPayment
        (httpClient:         HttpClient)
        (apiEndpoint:        NonemptyString)
        (config:             NagadConfig)
        (instrument:         PaymentInstrument)
        (originalAmount:     PositiveDecimal)
        (refundAmount:       PositiveDecimal)
        (paymentReferenceId: NonemptyString)
        (paymentCreatedOn:   DateTimeOffset)
        (refundGuid:         Guid)=

    task {
        let sensitiveData: RefundSensitiveData = {
            MerchantId          = config.MerchantId.Value
            OriginalRequestDate = (paymentCreatedOn |> toBdt).ToString dateFormatter
            OriginalAmount      = originalAmount.Value.ToString("F")
            CancelAmount        = refundAmount.Value.ToString("F")
            ReferenceNo         = refundGuid.ToString()
            ReferenceMessage    = "Refund Requested"
        }

        let plainTextSensitiveData = JsonSerializer.Serialize<RefundSensitiveData>(sensitiveData)

        let encryptedBase64Message = encryptMessage config.NagadGatewayPublicKey.Value plainTextSensitiveData
        let encryptedBase64Signature = generateSignature config.MerchantPrivateKey.Value plainTextSensitiveData

        let request =
            {
                SensitiveDataCancelRequest = encryptedBase64Message
                Signature     = encryptedBase64Signature
            }
            |> JsonSerializer.Serialize<EncryptedRefundRequestBody>
            |> NonemptyString.ofStringUnsafe

        try
            let! statusCode, maybeNagadResponse =
                nagadRequest
                    httpClient
                    apiEndpoint
                    NagadEndpoint.Cancel
                    instrument
                    None
                    ($"?paymentRefId={paymentReferenceId.Value}" |> NonemptyString.ofString)
                    config.MerchantId
                    (Some request |> HttpMethodWithData.Post)

            match statusCode, maybeNagadResponse with
            | HttpStatusCode.OK, Some nagadResponse ->
                let jsonData = JsonSerializer.Deserialize<JsonObject>(nagadResponse.Value)

                let maybeSensitiveData = jsonData |> JsonObject.tryFind "sensitiveData" |> JsonNode.toNonemptyString
                let maybeSignature = jsonData |> JsonObject.tryFind "signature" |> JsonNode.toNonemptyString

                match
                    maybeSensitiveData
                    |> Option.bind (
                        fun sensitiveData ->
                            decryptMessage config.MerchantPrivateKey.Value sensitiveData.Value
                            |> NonemptyString.ofString
                            |> Option.bind (
                                fun decryptedData ->
                                    maybeSignature
                                    |> Option.bind (
                                        fun signature ->
                                            match isValidSignature config.NagadGatewayPublicKey.Value decryptedData.Value signature.Value with
                                            | true ->
                                                Some (JsonSerializer.Deserialize<RefundPaymentResponse>(decryptedData.Value))

                                            | false ->
                                                None
                                    )

                            )

                    )
                with
                | Some responseData ->
                    let nagadTransactionStatus = responseData.Status |> NonemptyString.ofString |> Option.bind NagadTransactionStatus.fromStatus
                    match nagadTransactionStatus with
                    | Some NagadTransactionStatus.Refunded
                    | Some NagadTransactionStatus.PartialRefunded
                    | Some NagadTransactionStatus.Cancelled
                    | Some NagadTransactionStatus.PartialCancelled ->
                        match responseData.CancelTrxId |> NonemptyString.ofString with
                        | Some cancelTrxId ->
                            return
                                (cancelTrxId, { Request = request ; Response = Some nagadResponse })
                                |> Ok
                        | None ->
                            return
                                (
                                    { Request = request; Response = None},
                                    $"HttpStatusCode: {statusCode}, Response missing cancel transaction id: {maybeNagadResponse}" |> NonemptyString.ofString
                                )
                                |> DataError
                                |> Error

                    | maybeStatus ->
                        return
                            (
                                { Request = request; Response = Some nagadResponse},
                                $"HttpStatusCode: {statusCode}, Status is not successful: {maybeStatus}" |> NonemptyString.ofString
                            )
                            |> ErrorCode
                            |> Error
                | None ->
                    return
                        (
                            { Request = request; Response = Some nagadResponse},
                            $"HttpStatusCode: {statusCode}, Invalid response data" |> NonemptyString.ofString
                        )
                        |> DataError
                        |> Error
            | HttpStatusCode.OK, None ->
                //Ok response but no body
                return
                    (
                        { Request = request; Response = None},
                        $"HttpStatusCode: {statusCode}, Response is invalid: {maybeNagadResponse}" |> NonemptyString.ofString
                    )
                    |> DataError
                    |> Error
            | _, Some nagadErrorResponse ->
                //Error with a body
                return
                    (
                        { Request = request; Response = Some nagadErrorResponse},
                        $"HttpStatusCode: {statusCode}, Error: {nagadErrorResponse.Value}" |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error

            | _ ->
                //Error and missing error response body
                return
                    (
                        { Request = request; Response = None },
                        $"HttpStatusCode: {statusCode}, Invalid error response: {maybeNagadResponse}" |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error
        with
        | error ->
            return
                error
                |> handleApiException request "Unexpected error creating nagad payment after initialization"
                |> Error
    }

// -------------------------------------------------------------------------------------------------------------- SHARED CONTENT END


// ------------------------------------- QQQ
let runNagad : Async<unit> =
    async {
        let httpClient = new HttpClient()
        let redirectUrl = "http://localhost:12345" |> NonemptyString.ofLiteral
        let amount = 6m |> PositiveDecimal.ofDecimalUnsafe
        let orderId = ("23426733323" |> NonemptyString.ofLiteral)
        let lastPaymentReferenceId = "MDIxNTE4MTE1MDM4NC42ODMwMDIwMDcxMDQyMjUuMjM0MjY3MzMzMjMuMWUzOTk4MjdlY2QzNjVjYTI2NTU=" |> NonemptyString.ofLiteral
        let paymentInstrument =
            PaymentInstrument (
                "Funny browser" |> NonemptyString.ofLiteral,
                Platform.DesktopWeb
            )

        // ----------------------------------------------- Initialize And Create Nagad Payment
        // let! initializationInfo = initializeNagadPayment httpClient NagadApiEndpoint nagadSandboxConfig paymentInstrument orderId |> Async.AwaitTask
        // printfn $"%A{initializationInfo}"
        //
        // let paymentReferenceId, challenge, _, _ = initializationInfo |> Result.defaultWith (fun _ -> failwith "Initialization failed")
        //
        // let! creationInfo = createNagadPayment httpClient NagadApiEndpoint nagadSandboxConfig paymentInstrument redirectUrl amount orderId paymentReferenceId challenge |> Async.AwaitTask
        // printfn $"%A{creationInfo}"

        // Redirect Query Parameters
        //
        // merchant=683002007104225
        // order_id=23467632
        // payment_ref_id=MDIxMzExMDMzNDkxOC42ODMwMDIwMDcxMDQyMjUuMjM0Njc2MzIuMmU4MGNjMjQyNjVhYWY0ZGVlNzI=
        // status=Success
        // status_code=00_0000_000
        // message=Successful%20Transaction
        // payment_dt=20230213110541
        // issuer_payment_ref=000177YT

        // ----------------------------------------------- Verify Nagad Payment
        // let! verificationInfo = verifyNagadPayment httpClient NagadApiEndpoint nagadSandboxConfig paymentInstrument lastPaymentReferenceId |> Async.AwaitTask
        // printfn $"%A{verificationInfo}"

        // ----------------------------------------------- Refund Nagad Payment
        // let! refundInfo = refundNagadPayment httpClient NagadApiEndpoint nagadSandboxConfig paymentInstrument amount (amount.Value/2m |> PositiveDecimal.ofDecimalUnsafe) lastPaymentReferenceId DateTimeOffset.Now (Guid.NewGuid()) |> Async.AwaitTask
        // printfn $"%A{refundInfo}"

        return ()
    }