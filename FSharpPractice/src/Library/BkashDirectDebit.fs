module Methods.BkashDirectDebit

open System.Net.Http
open System.Net.Mime
open InteropBinding
open System
open System.Collections.Generic
open Common
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.Json.Serialization
open System.IO
open System.Threading.Tasks
open OptionExtensions
open Org.BouncyCastle.Security
open Models

type RawResponse = RawResponse of string

[<RequireQualifiedAccess>]
type BkashFailure =
| UserStatusAbnormal
| AccountRestricted
| AccountInactive
| InsufficientBalance
| ProcessFail
| PaymentOfSameAmountWithinTwoMinutes
| UnknownResultCode of NonemptyString
| UnknownFailure
| NonBkashFailure

type AccountingEventId = AccountingEventId of Id: int


type UserInfo = {
    Id:                NonemptyString
    MaskedPhoneNumber: NonemptyString
}

type InquiredPaymentOrCaptureInfo = {
    PaymentId:     NonemptyString
    PaymentAmount: PositiveDecimal
}

type InquiredRefundInfo = {
    RefundId:     NonemptyString
    RefundAmount: PositiveDecimal
}

type BkashPaymentInfo = Info of PaymentId: NonemptyString

type BkashCapturedInfo = {
    CaptureId: NonemptyString
}

type BkashDirectDebitConfig = {
    PaymentAccountHead: NonemptyString
    MerchantShortCode:  NonemptyString
    AuthClientId:       NonemptyString
    PrivateKey:         NonemptyString
    PublicKey:          NonemptyString
}

module JsonObject =
    let tryFind (key: string) (jsonObject: JsonObject) : Option<JsonNode> =
        match jsonObject.ContainsKey(key) with
        | true -> Some(jsonObject[key])
        | false -> None

module JsonNode =
    let maybeTryFind (key: string) (maybeJsonNode: Option<JsonNode>) : Option<JsonNode> =
        match maybeJsonNode with
        | Some node -> node.AsObject() |> JsonObject.tryFind key
        | None -> None

    let toNonemptyString (maybeJsonNode: Option<JsonNode>) : Option<NonemptyString> =
        match maybeJsonNode with
        | Some node -> node |> string |> NonemptyString.ofString
        | None -> None


let apiEndpoint = "https://sandboxdc.pay.bka.sh/capabilitycore/v2" |> NonemptyString.ofLiteral

let bkashDirectDebitSandboxConfig: BkashDirectDebitConfig = {
    PaymentAccountHead = NonemptyString.ofLiteral "Bkash"
    MerchantShortCode  = NonemptyString.ofLiteral "60044"
    AuthClientId       = NonemptyString.ofLiteral "Test_60044"
    PrivateKey         = NonemptyString.ofLiteral "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"
    PublicKey          = NonemptyString.ofLiteral "MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEA5xpHKQq1ostzYfBDjDeb5cEgIN3yxuewV0uuTRGsjkdZvrmOtIp396DQchFHYu7/a+IT6ZjYNg4dUpsF07F4hHL7GFLwQnRwuNACtOj0xfT3BtkQsClr/3w59QVnWetykEtPjYQNzDMfSts6X4CdwCUuPI1vW7UOe6xKSg/MrLcHTB746qiBH1t0lxLyVwKtM53jx749v9TJz213rYHjJ8GR1NvO1gxQokNIcsStp2igBnghr8Adaslh1637YjPWvM6DYoO+8fN/rVOSgmGv8++mT0eLXZ3zelcoTmmfSkd2tR3QRnm+AnAHQERjZaVLEEPztpFZWlqRrz4+Lj2IMzpTuWt3xcufYWB9sFJTfWfSvc5wWJ2FIrUyJMJD5OlXl+vtmeWiHzXEbJTiWWbesy6O52XOCS4GRTf5giSegjrrnjGZTIhfplu9DJO99ETWfWhJ805ipKr8ZNvn8OnviSQ7S0ySkQt5CLsNR6wASG7sr2ul8eMPNP5TLiY0xbU+XVjPXTILi/7OXKw4EifYcIy240iF7j4U2fwGdkRT/AkrqntzO3kIVo6ij7FKfNKRl0WrKEYVxptQwAco5xW0b4/keuKC4Hy/ne2+rU7woAIxHbzQStgAqUZlWcKzw2ULxiY+ZYKCtQKG+hFxAvCYtkvwvrBKRqVKN/oFwIcZugsCAwEAAQ=="
}
// -------------------------------------------------------------------------------------------------------------- COPIED CONTENT END

// -------------------------------------------------------------------------------------------------------------- SHARED CONTENT START

let SuccessResultStatus = "S"
let InquiryUserSuccessResultStatus = "success" //Some intern at bkash screwed up I guess
let FailResultStatus = "F"
let UnknownResultStatus = "U"
let bkashCheckoutApiTimeoutMillis = 30000
// Bkash checkout api spec. Set their token to 50 minutes expiry time
// let bkashCheckoutTokenExpiryTimeMinutes = 50.0


// Bkash checkout api spec. Set all their api to 30 seconds timeout
[<Literal>]
let private bkashApiTimeoutMillis = 30000

[<Literal>]
let timeStampFormatter = "yyyy-MM-dd HH:mm:ss"

[<Literal>]
let bdtCurrencyName = "BDT"

type BkashEndpoint =
| Prepare
| ApplyToken
| InquiryUserInfo
| CancelToken
| Pay
| Capture
| InquiryPayment
| CancelPayment
| Refund
| InquiryRefund
    member this.toString =
        match this with
        | Prepare         -> "/oauths/prepare"
        | ApplyToken      -> "/oauths/applyToken"
        | InquiryUserInfo -> "/users/inquiryUserInfo"
        | CancelToken     -> "/oauths/cancelToken"
        | Pay             -> "/payments/pay"
        | Capture         -> "/payments/capture"
        | InquiryPayment  -> "/payments/inquiryPayment"
        | CancelPayment   -> "/payments/cancelPayment"
        | Refund          -> "/payments/refund"
        | InquiryRefund   -> "/payments/inquiryRefund"

type Amount = {
    [<JsonPropertyName("currency")>] Currency: string
    [<JsonPropertyName("value")>]    Value:    string
}

type Address = {
    [<JsonPropertyName("region")>] Region: string
    [<JsonPropertyName("city")>]   City:   string
}

type PaymentFactor = {
    [<JsonPropertyName("isAgreementPayment")>]     IsAgreementPayment:     bool
    [<JsonPropertyName("isAgreementPay")>]         IsAgreementPay:         bool

    [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
    [<JsonPropertyName("isAuthorizationPayment")>] IsAuthorizationPayment: Option<bool>
}
let authorizationPaymentFactor = {
    IsAgreementPayment     = true
    IsAgreementPay         = true
    IsAuthorizationPayment = Some true
}
let directPaymentFactor = {
    IsAgreementPayment     = true
    IsAgreementPay         = true
    IsAuthorizationPayment = None
}

type DirectDebitRequestHead = {
    [<JsonPropertyName("merchantShortCode")>] MerchantShortCode: string
    [<JsonPropertyName("version")>]           Version:           string
    [<JsonPropertyName("timestamp")>]         TimeStamp:         string

    [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
    [<JsonPropertyName("signature")>]         Signature:         Option<string>
}
with member this.withSignature (signature: string) =
        { this with
            Signature = Some signature
        }

let private generateRequestHead (timeStamp: DateTimeOffset) (shortCode: string) (signature: Option<string>) : DirectDebitRequestHead =
    {
        Version           = "1.0.0"
        TimeStamp         = timeStamp.ToString(timeStampFormatter)
        MerchantShortCode = shortCode
        Signature         = signature
    }

let private generateDataForSignature (header: string) (body: string) : string =
    "params=" + body
    + "&&verify=" + header

let private generateSignature (key: string) (plainText: string) : string =
    let dataBytes = plainText |> Encoding.UTF8.GetBytes
    let signerProvider = SignerUtilities.GetSigner("SHA-256withRSA")
    signerProvider.Init (true, key |> Convert.FromBase64String |> PrivateKeyFactory.CreateKey)
    signerProvider.BlockUpdate (dataBytes, 0, dataBytes.Length)
    signerProvider.GenerateSignature() |> Convert.ToBase64String


let generateRequest
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

let bkashRequest
    (httpClient:  HttpClient)
    (apiEndpoint: NonemptyString)
    (endpoint:    BkashEndpoint)
    (timeStamp:   DateTimeOffset)
    (signature:   string)
    (request:     HttpMethodWithData) =
    task {
        let url = apiEndpoint.Value + endpoint.toString |> NonemptyString.ofStringUnsafe

        let req =
            generateRequest
                url
                request

        req.Headers.Add ("request-time", timeStamp.ToString(timeStampFormatter))
        req.Headers.Add ("signature",    "signature=" + signature)
        req.Headers.Add ("client-id",    "Default")

        let! response = httpClient.SendAsync req
        let! responseString = response.Content.ReadAsStringAsync()
        return (responseString |> NonemptyString.ofStringUnsafe)
    }
let getBkashError
        (jsonData: JsonObject)
        (request: NonemptyString)
        (bkashResponse: NonemptyString)
        (errorMessageSubtext: string) =
    match
        jsonData
        |> JsonObject.tryFind "result"
        |> JsonNode.maybeTryFind "resultStatus"
        |> Option.map string
        = Some FailResultStatus
    with
    | true ->
        let failureResultCode =
            match
                jsonData
                |> JsonObject.tryFind "result"
                |> JsonNode.maybeTryFind "resultCode"
                |> Option.bind (string >> NonemptyString.ofString)
            with
            | Some resultCode ->
                match resultCode.Value with
                | "USER_STATUS_ABNORMAL" -> BkashFailure.UserStatusAbnormal
                | "ACCOUNT_RESTRICTED" -> BkashFailure.AccountRestricted
                | "ACCOUNT_INACTIVE" -> BkashFailure.AccountInactive
                | "USER_BALANCE_NOT_ENOUGH" -> BkashFailure.InsufficientBalance
                | "PROCESS_FAIL" -> BkashFailure.ProcessFail
                | _ -> BkashFailure.UnknownResultCode resultCode
            | None -> BkashFailure.NonBkashFailure

        let error = $"Failure during {errorMessageSubtext}"

        (
            failureResultCode,
            (

                { Request = request; Response = Some bkashResponse },
                error |> NonemptyString.ofString
            )
            |> ErrorCode
        )

    | false ->
        (*
            {
                "result": {
                    "resultStatus":"U",
                    "resultCode":"UNKNOWN_EXCEPTION",
                    "resultMessage":"Duplicate for All Transactions",
                    "bKashResultCode":"ETC70052",
                    "bKashResultMessage":"Duplicate for All Transactions"
                }
            }
        *)
        let maybeResult = jsonData |> JsonObject.tryFind "result"
        match
            (maybeResult
            |> JsonNode.maybeTryFind "resultStatus"
            |> Option.map string
            = Some UnknownResultStatus)
            &&
            (maybeResult
            |> JsonNode.maybeTryFind "resultCode"
            |> Option.map string
            = Some "UNKNOWN_EXCEPTION")
            &&
            (maybeResult
            |> JsonNode.maybeTryFind "bKashResultCode"
            |> Option.map string
            = Some "ETC70052")
        with
        | true ->
            let error = $"Failure during {errorMessageSubtext}"
            let failureResultCode = BkashFailure.PaymentOfSameAmountWithinTwoMinutes
            (
                failureResultCode,
                (

                    { Request = request; Response = Some bkashResponse },
                    error |> NonemptyString.ofString
                )
                |> ErrorCode
            )

        | false ->
            let error = $"Unexpected status during {errorMessageSubtext}"

            (
                BkashFailure.UnknownFailure,
                (
                    { Request = request; Response = Some bkashResponse },
                    error |> NonemptyString.ofString
                )
                |> ErrorCode
            )
// ---------------------------------------------------------------------- Direct Debit Binding

// ------------------------------------- Prepare Bkash Direct Debit Binding

type PrepareBody = {
    [<JsonPropertyName("customerBelongsTo")>] CustomerBelongsTo:    string
    [<JsonPropertyName("authClientId")>]      AuthClientId:         string
    [<JsonPropertyName("scopes")>]            Scopes:               List<string>
    [<JsonPropertyName("authRedirectUrl")>]   AuthRedirectUrl:      string
    [<JsonPropertyName("authState")>]         AuthState:            string
}

type PrepareSignature = {
    [<JsonPropertyName("customerBelongsTo")>] CustomerBelongsTo:    string
    [<JsonPropertyName("authClientId")>]      AuthClientId:         string
    [<JsonPropertyName("scopes")>]            Scopes:               List<string>
    [<JsonPropertyName("authRedirectUrl")>]   AuthRedirectUrl:      string
}

type PrepareApiRequest = {
    [<JsonPropertyName("verify")>] Verify: DirectDebitRequestHead
    [<JsonPropertyName("params")>] Params: PrepareBody
}

let prepareBindingBodyAndSignature
        (config:      BkashDirectDebitConfig)
        (redirectUrl: NonemptyString)
        (authState:   string)
        : PrepareBody * PrepareSignature =
    let requestBody: PrepareBody = {
        CustomerBelongsTo     = "CHALDAL"
        AuthClientId          = config.AuthClientId.Value
        AuthRedirectUrl       = redirectUrl.Value
        Scopes                = [ "AGREEMENT_PAY"; "USER_INFO" ] |> List
        AuthState             = authState
    }

    let signatureBody: PrepareSignature = {
        CustomerBelongsTo     = requestBody.CustomerBelongsTo
        AuthClientId          = requestBody.AuthClientId
        AuthRedirectUrl       = requestBody.AuthRedirectUrl
        Scopes                = requestBody.Scopes
    }

    (requestBody, signatureBody)

let prepareBkashDirectDebitBinding
        (httpClient:  HttpClient)
        (apiEndpoint: NonemptyString)
        (config:      BkashDirectDebitConfig)
        (redirectUrl: NonemptyString)
        : Task<Result<NonemptyString * RequestAndResponse, ApiError>> =
    task {
        let timeStamp = DateTimeOffset.Now

        let requestHeadForSignature = generateRequestHead timeStamp config.MerchantShortCode.Value None

        let requestBody, signatureBody =
            prepareBindingBodyAndSignature
                config
                redirectUrl
                (Guid.NewGuid().ToString())

        let requestHeadSignatureSerialized = requestHeadForSignature |> JsonSerializer.Serialize<DirectDebitRequestHead>
        let requestBodySignatureSerialized = signatureBody |> JsonSerializer.Serialize<PrepareSignature>

        let signature =
            generateDataForSignature
                requestHeadSignatureSerialized
                requestBodySignatureSerialized
            |> generateSignature config.PrivateKey.Value

        let request =
            {
                Verify = requestHeadForSignature.withSignature signature
                Params = requestBody
            }
            |> JsonSerializer.Serialize<PrepareApiRequest>
            |> NonemptyString.ofStringUnsafe

        try
            let! prepareBindingResponse =
                bkashRequest
                    httpClient
                    apiEndpoint
                    BkashEndpoint.Prepare
                    timeStamp
                    signature
                    (Some request |> HttpMethodWithData.Post)

            let jsonData = JsonSerializer.Deserialize<JsonObject>(prepareBindingResponse.Value)

            return
                match
                    jsonData
                    |> JsonObject.tryFind "result"
                    |> JsonNode.maybeTryFind "resultStatus"
                    |> Option.map(string)
                    = Some SuccessResultStatus
                with
                | true ->
                    jsonData
                    |> JsonObject.tryFind "authUrl"
                    |> JsonNode.toNonemptyString
                    |> Option.map (fun authUrl -> (authUrl, { Request = request; Response = Some prepareBindingResponse }))
                    |> Option.getAsResult (
                        (
                            { Request = request; Response = Some prepareBindingResponse },
                            "authUrl in response is not valid" |> NonemptyString.ofString
                        )
                        |> DataError)

                | false ->
                    let error = "Unexpected result status while initiating bkash direct charge prepare API"
                    ({ Request = request; Response = Some prepareBindingResponse }, error |> NonemptyString.ofString) |> ErrorCode |> Error
        with
        | error ->
            return
                error
                |> handleApiException request "Unexpected error while initiating bkash direct charge prepare API"
                |> Error
    }

// ------------------------------------- Bkash Direct Debit Apply Token

type ApplyTokenBody = {
    [<JsonPropertyName("authCode")>]  AuthCode: string
    [<JsonPropertyName("grantType")>] GrantType: string
}

type ApplyTokenSignature = {
    [<JsonPropertyName("authCode")>]  AuthCode: string
}

type BkashDirectDebitUserInfoRequest = {
    [<JsonPropertyName("accessToken")>] AccessToken: string
}

type ApplyTokenApiRequest = {
    [<JsonPropertyName("verify")>] Verify: DirectDebitRequestHead
    [<JsonPropertyName("params")>] Params: ApplyTokenBody
}

let prepareApplyTokenBodyAndSignature
        (authCode: NonemptyString)
        : ApplyTokenBody * ApplyTokenSignature =

    let requestBody = {
        AuthCode = authCode.Value
        GrantType = "AUTHORIZATION_CODE"
    }

    let signatureBody = {
        AuthCode = requestBody.AuthCode
    }

    (requestBody, signatureBody)

let internal applyBkashDirectDebitToken //Bkash calls this the ApplyToken but we use it to fetch a newly generated token ¯\_(ツ)_/¯
        (httpClient:  HttpClient)
        (apiEndpoint: NonemptyString)
        (config:      BkashDirectDebitConfig)
        (authCode:    NonemptyString)
    : Task<Result<NonemptyString * Option<UserInfo> * RequestAndResponse, ApiError>> =
    task {

        let timeStamp = DateTimeOffset.Now

        let requestHeadForSignature = generateRequestHead timeStamp config.MerchantShortCode.Value None

        let requestBody, signatureBody = prepareApplyTokenBodyAndSignature authCode

        let requestHeadSignatureSerialized = requestHeadForSignature |> JsonSerializer.Serialize<DirectDebitRequestHead>
        let requestBodySignatureSerialized = signatureBody |> JsonSerializer.Serialize<ApplyTokenSignature>

        let signature =
            generateDataForSignature
                requestHeadSignatureSerialized
                requestBodySignatureSerialized
            |> generateSignature config.PrivateKey.Value

        let request =
            {
                Verify = requestHeadForSignature.withSignature signature
                Params = requestBody
            }
            |> JsonSerializer.Serialize<ApplyTokenApiRequest>
            |> NonemptyString.ofStringUnsafe

        try
            let! applyTokenResponse =
                bkashRequest
                    httpClient
                    apiEndpoint
                    BkashEndpoint.ApplyToken
                    timeStamp
                    signature
                    (Some request |> HttpMethodWithData.Post)

            (*let lines =
                [|
                    $"ApplyToken response data for %s{request.Value} is %A{applyTokenResponse.Value}"
                |]
            printfn $"%A{applyTokenResponse.Value}"
            File.WriteAllLines($"AccessTokenResponseForAuthCode.txt", lines)*)

            let jsonData = JsonSerializer.Deserialize<JsonObject>(applyTokenResponse.Value)

            return
                match jsonData
                    |> JsonObject.tryFind "result"
                    |> JsonNode.maybeTryFind "resultStatus"
                    |> Option.map(string)
                    = Some SuccessResultStatus
                with
                | true ->
                    match jsonData
                        |> JsonObject.tryFind "accessToken"
                        |> JsonNode.toNonemptyString
                    with
                    | Some token ->
                        (token, None, { Request = request; Response = Some applyTokenResponse })
                        |> Ok

                    | None ->
                        (
                            { Request = request; Response = Some applyTokenResponse },
                            "AccessToken is empty" |> NonemptyString.ofString
                        )
                        |> DataError
                        |> Error

                | false ->
                    let error = "Unexpected result status while applying bkash direct charge token"
                    ({ Request = request; Response = Some applyTokenResponse }, error |> NonemptyString.ofString) |> ErrorCode |> Error

        with
        | error ->
            return
                error
                |> handleApiException request "Unexpected error while applying bkash direct charge token"
                |> Error
    }

// ------------------------------------- Bkash Direct Debit User Info

type UserInfoRequestBody = {
    [<JsonPropertyName("accessToken")>] AccessToken: string
}

type UserInfoRequestSignature = {
    [<JsonPropertyName("accessToken")>] AccessToken: string
}

type UserInfoRequest = {
    [<JsonPropertyName("verify")>] Verify: DirectDebitRequestHead
    [<JsonPropertyName("params")>] Params: UserInfoRequestBody
}

let prepareUserInfoBodyAndSignature
        (accessToken: NonemptyString):
        UserInfoRequestBody * UserInfoRequestSignature =
    let requestBody: UserInfoRequestBody = {
        AccessToken = accessToken.Value
    }

    let signatureBody = {
        AccessToken = requestBody.AccessToken
    }

    (requestBody, signatureBody)

let public queryBkashDirectDebitUserInfo
        (httpClient:  HttpClient)
        (apiEndpoint: NonemptyString)
        (config:      BkashDirectDebitConfig)
        (accessToken: NonemptyString)
    : Task<Result<UserInfo, ApiError>> =
    task {
        let timeStamp = DateTimeOffset.Now

        let requestHeadForSignature = generateRequestHead timeStamp config.MerchantShortCode.Value None

        let requestBody, signatureBody = prepareUserInfoBodyAndSignature accessToken

        let requestHeadSignatureSerialized = requestHeadForSignature |> JsonSerializer.Serialize<DirectDebitRequestHead>
        let requestBodySignatureSerialized = signatureBody |> JsonSerializer.Serialize<UserInfoRequestSignature>

        let signature =
            generateDataForSignature
                requestHeadSignatureSerialized
                requestBodySignatureSerialized
            |> generateSignature config.PrivateKey.Value

        let request =
            {
                Verify = { requestHeadForSignature with Signature = Some(signature) }
                Params = requestBody
            }
            |> JsonSerializer.Serialize<UserInfoRequest>
            |> NonemptyString.ofStringUnsafe

        try
            let! queryUserResponse =
                bkashRequest
                    httpClient
                    apiEndpoint
                    BkashEndpoint.InquiryUserInfo
                    timeStamp
                    signature
                    (Some request |> HttpMethodWithData.Post)

            let jsonData = JsonSerializer.Deserialize<JsonObject>(queryUserResponse.Value)

            return
                match jsonData
                    |> JsonObject.tryFind "result"
                    |> JsonNode.maybeTryFind "resultStatus"
                    |> Option.map(string)
                    = Some InquiryUserSuccessResultStatus
                with
                | true ->
                    match
                        (
                            (jsonData |> JsonObject.tryFind "userInfo" |> JsonNode.maybeTryFind "userLoginId" |> JsonNode.toNonemptyString),
                            (jsonData |> JsonObject.tryFind "userInfo" |> JsonNode.maybeTryFind "userId" |> JsonNode.toNonemptyString)
                        )
                    with
                    | Some(maskedPhoneNumber), Some(userId) ->
                        let userInfo: UserInfo = {
                            MaskedPhoneNumber = maskedPhoneNumber
                            Id = userId
                        }

                        Ok userInfo

                    | _ ->
                        (
                            { Request = request; Response = Some queryUserResponse },
                            "MaskedBkashPhoneNumber or BkashUserId is not valid" |> NonemptyString.ofString
                        )
                        |> DataError
                        |> Error

                | false ->
                    let error =  "Unexpected result status while inquiring direct charge user API"
                    (
                        { Request = request; Response = Some queryUserResponse },
                        error |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error
        with
        | error ->
            return
                error
                |> handleApiException request "Unexpected error while inquiring direct charge user API"
                |> Error
    }

// ------------------------------------- Cancel Direct Debit Binding

type CancelTokenBody = {
    [<JsonPropertyName("accessToken")>] AccessToken: string
}

type CancelTokenSignature = {
    [<JsonPropertyName("accessToken")>] AccessToken: string
}

type CancelTokenApiRequest = {
    [<JsonPropertyName("verify")>] Verify: DirectDebitRequestHead
    [<JsonPropertyName("params")>] Params: CancelTokenBody
}

let prepareCancelTokenBodyAndSignature
        (accessToken: NonemptyString):
        CancelTokenBody * CancelTokenSignature =
    let requestBody: CancelTokenBody = {
        AccessToken = accessToken.Value
    }

    let signatureBody: CancelTokenSignature = {
        AccessToken = requestBody.AccessToken
    }

    (requestBody, signatureBody)

let internal cancelBkashDirectDebitToken
        (httpClient:  HttpClient)
        (apiEndpoint: NonemptyString)
        (config:      BkashDirectDebitConfig)
        (accessToken: NonemptyString)
        : Task<Result<RequestAndResponse, ApiError>> =
    task {
        let timeStamp = DateTimeOffset.Now

        let requestHeadForSignature = generateRequestHead timeStamp config.MerchantShortCode.Value None

        let requestBody, signatureBody = prepareCancelTokenBodyAndSignature accessToken

        let requestHeadSignatureSerialized = requestHeadForSignature |> JsonSerializer.Serialize<DirectDebitRequestHead>
        let requestBodySignatureSerialized = signatureBody |> JsonSerializer.Serialize<CancelTokenSignature>

        let signature =
            generateDataForSignature
                requestHeadSignatureSerialized
                requestBodySignatureSerialized
            |> generateSignature config.PrivateKey.Value

        let request =
            {
                Verify = { requestHeadForSignature with Signature = Some(signature) }
                Params = requestBody
            }
            |> JsonSerializer.Serialize<CancelTokenApiRequest>
            |> NonemptyString.ofStringUnsafe

        try
            let! bkashResponse =
                bkashRequest
                    httpClient
                    apiEndpoint
                    BkashEndpoint.CancelToken
                    timeStamp
                    signature
                    (Some request |> HttpMethodWithData.Post)

            let jsonData = JsonSerializer.Deserialize<JsonObject>(bkashResponse.Value)

            return!
                match jsonData
                    |> JsonObject.tryFind "result"
                    |> JsonNode.maybeTryFind "resultStatus"
                    |> Option.map(string)
                    = Some SuccessResultStatus
                with
                | true ->
                    Ok { Request = request; Response = Some bkashResponse } |> Task.FromResult
                | false ->
                    task {
                        let! queryResult = queryBkashDirectDebitUserInfo httpClient apiEndpoint config accessToken

                        return
                            match queryResult with
                            | Error(ErrorCode _) -> Ok { Request = request; Response = Some bkashResponse }
                            | Ok _ ->
                                (
                                    { Request = request; Response = Some bkashResponse },
                                    "Binding still exists" |> NonemptyString.ofString
                                )
                                |> DataError
                                |> Error

                            | Error err -> Error err
                    }
        with
        | error ->
            return
                error
                |> handleApiException request "Unexpected error while canceling bkash account token"
                |> Error
    }

// ---------------------------------------------------------------------- Direct Debit Authorize And Capture

// ------------------------------------- Inquiry

type InquiryBody = {
    [<JsonPropertyName("paymentRequestId")>] PaymentRequestId:     string
}

type InquirySignature = {
    [<JsonPropertyName("paymentRequestId")>] PaymentRequestId:     string
}

type InquiryApiRequest = {
    [<JsonPropertyName("verify")>] Verify: DirectDebitRequestHead
    [<JsonPropertyName("params")>] Params: InquiryBody
}

let prepareInquiryPaymentBodyAndSignature
        (paymentOrCaptureRequestId: NonemptyString)
        : InquiryBody * InquirySignature =
    let requestBody: InquiryBody = {
        PaymentRequestId = paymentOrCaptureRequestId.Value
    }

    let signatureBody: InquirySignature = {
        PaymentRequestId = requestBody.PaymentRequestId
    }

    (requestBody, signatureBody)

let internal queryBkashDirectDebitPayment
        (httpClient:                HttpClient)
        (apiEndpoint:               NonemptyString)
        (config:                    BkashDirectDebitConfig)
        (paymentOrCaptureRequestId: NonemptyString)
        (errorMessageSubtext:       string)
        : Task<Result<InquiredPaymentOrCaptureInfo * RequestAndResponse, BkashFailure * ApiError>> =
    task {
        let timeStamp = DateTimeOffset.Now

        let requestHeadForSignature = generateRequestHead timeStamp config.MerchantShortCode.Value None

        let requestBody, signatureBody =
            prepareInquiryPaymentBodyAndSignature
                paymentOrCaptureRequestId

        let requestHeadSignatureSerialized = requestHeadForSignature |> JsonSerializer.Serialize<DirectDebitRequestHead>
        let requestBodySignatureSerialized = signatureBody |> JsonSerializer.Serialize<InquirySignature>

        let signature =
            generateDataForSignature
                requestHeadSignatureSerialized
                requestBodySignatureSerialized
            |> generateSignature config.PrivateKey.Value

        let request =
            {
                Verify = { requestHeadForSignature with Signature = Some(signature) }
                Params = requestBody
            }
            |> JsonSerializer.Serialize<InquiryApiRequest>
            |> NonemptyString.ofStringUnsafe

        try
            let! bkashResponse =
                bkashRequest
                    httpClient
                    apiEndpoint
                    BkashEndpoint.InquiryPayment
                    timeStamp
                    signature
                    (Some request |> HttpMethodWithData.Post)

            let jsonData = JsonSerializer.Deserialize<JsonObject>(bkashResponse.Value)

            return
                match jsonData
                    |> JsonObject.tryFind "paymentResult"
                    |> JsonNode.maybeTryFind "resultStatus"
                    |> Option.map(string)
                    = Some SuccessResultStatus
                with
                | true ->
                    match jsonData
                        |> JsonObject.tryFind "paymentId"
                        |> JsonNode.toNonemptyString
                    with
                    | Some(paymentId) ->
                        match
                            jsonData
                            |> JsonObject.tryFind "paymentAmount"
                            |> JsonNode.maybeTryFind "value"
                            |> JsonNode.toNonemptyString
                            |> Option.bind (fun amountString -> match Decimal.TryParse amountString.Value with | true, n -> n |> PositiveDecimal.ofDecimal | _ -> None) with
                        | Some amount ->
                            Ok ({ PaymentId = paymentId; PaymentAmount = amount }, { Request = request; Response = Some bkashResponse })
                        | None ->
                            (
                                BkashFailure.UnknownFailure,
                                (
                                    { Request = request; Response = Some bkashResponse },
                                    $"Payment amount is invalid for {errorMessageSubtext}" |> NonemptyString.ofString
                                )
                                |> DataError
                            )
                            |> Error

                    | None ->
                        (BkashFailure.UnknownFailure, ({ Request = request; Response = Some bkashResponse }, $"PaymentId is empty for {errorMessageSubtext}" |> NonemptyString.ofString) |> DataError) |> Error
                | false ->
                    getBkashError jsonData request bkashResponse errorMessageSubtext
                    |> Error
        with
        | error ->
            return
                (BkashFailure.UnknownFailure, error |> handleApiException request $"Unexpected error during {errorMessageSubtext}")
                |> Error
    }


// ------------------------------------- Authorize

type Store = {
    [<JsonPropertyName("referenceStoreId")>] ReferenceStoreId: string
    [<JsonPropertyName("storeName")>]        StoreName:        string
    [<JsonPropertyName("storeMCC")>]         StoreMCC:         string
}

type Merchant = {
    [<JsonPropertyName("referenceMerchantId")>] ReferenceMerchantId: string
    [<JsonPropertyName("merchantMCC")>]         MerchantMCC:         string
    [<JsonPropertyName("merchantName")>]        MerchantName:        string
    [<JsonPropertyName("merchantAddress")>]     MerchantAddress:     Address
    [<JsonPropertyName("store")>]               Store:               Store
}
let defaultMerchant: Merchant = {
    ReferenceMerchantId = "M00000000001"
    MerchantMCC         = "1405"
    MerchantName        = "UGG"

    MerchantAddress = {
        Region = "JP"
        City = "xxx"
    }

    Store = {
        ReferenceStoreId = "S0000000001n"
        StoreName = "UGG-2"
        StoreMCC = "1405"
    }
}
let defaultOrderAmount = {
    Currency = "JPY"
    Value    = "100.00"
}

type Order = { //
    [<JsonPropertyName("referenceOrderId")>] ReferenceOrderId: string
    [<JsonPropertyName("orderDescription")>] OrderDescription: string
    [<JsonPropertyName("orderAmount")>]      OrderAmount:      Amount
    [<JsonPropertyName("merchant")>]         Merchant:         Merchant
}

type PaymentMethod = {
    [<JsonPropertyName("paymentMethodType")>] PaymentMethodType: string
    [<JsonPropertyName("paymentMethodId")>]   PaymentMethodId:   string
}

type AuthorizeBody = {
    [<JsonPropertyName("order")>]            Order:            Order
    [<JsonPropertyName("paymentRequestId")>] PaymentRequestId: string
    [<JsonPropertyName("paymentAmount")>]    PaymentAmount:    Amount
    [<JsonPropertyName("paymentMethod")>]    PaymentMethod:    PaymentMethod
    [<JsonPropertyName("paymentFactor")>]    PaymentFactor:    PaymentFactor
}

type AuthorizeSignature = {
    [<JsonPropertyName("paymentRequestId")>] PaymentRequestId: string
    [<JsonPropertyName("orderDescription")>] OrderDescription: string
    [<JsonPropertyName("paymentAmount")>]    PaymentAmount:    Amount
    [<JsonPropertyName("merchantName")>]     MerchantName:     string
    [<JsonPropertyName("referenceOrderId")>] ReferenceOrderId: string
    [<JsonPropertyName("orderAmount")>]      OrderAmount:      Amount
    [<JsonPropertyName("merchantMCC")>]      MerchantMCC:      string
}

type AuthorizeApiRequest = {
    [<JsonPropertyName("verify")>] Verify: DirectDebitRequestHead
    [<JsonPropertyName("params")>] Params: AuthorizeBody
}

let prepareAuthorizeDirectDebitBodyAndSignature
        (token:            NonemptyString)
        (amount:           PositiveDecimal)
        (paymentRequestId: NonemptyString)
        (referenceOrderId: NonemptyString) :
        AuthorizeBody * AuthorizeSignature =

    let paymentAmount = {
        Currency = bdtCurrencyName
        Value    = amount.Value.ToString("F")
    }

    let requestBody: AuthorizeBody = {
        Order = {
            ReferenceOrderId = referenceOrderId.Value
            OrderDescription = "SHOES" //Bkash Provided Default?
            Merchant         = defaultMerchant
            OrderAmount      = defaultOrderAmount
        }
        PaymentRequestId = paymentRequestId.Value
        PaymentAmount = paymentAmount
        PaymentMethod = {
            PaymentMethodType = "CONNECT_WALLET"
            PaymentMethodId   = token.Value
        }
        PaymentFactor = authorizationPaymentFactor
    }

    let signatureBody: AuthorizeSignature =
        {
            PaymentRequestId = requestBody.PaymentRequestId
            OrderDescription = requestBody.Order.OrderDescription
            PaymentAmount = requestBody.PaymentAmount
            MerchantName = requestBody.Order.Merchant.MerchantName
            ReferenceOrderId = requestBody.Order.ReferenceOrderId
            OrderAmount = requestBody.Order.OrderAmount
            MerchantMCC = requestBody.Order.Merchant.MerchantMCC
        }

    (requestBody, signatureBody)


let internal authorizeBkashDirectDebitPayment
        (httpClient:       HttpClient)
        (apiEndpoint:      NonemptyString)
        (config:           BkashDirectDebitConfig)
        (paymentRequestId: NonemptyString)
        (referenceOrderId: NonemptyString)
        (token:            NonemptyString)
        (amount:           PositiveDecimal)
        : Task<Result<NonemptyString * RequestAndResponse, BkashFailure * ApiError>> =
    task {
        let timeStamp = DateTimeOffset.Now

        let requestHeadForSignature = generateRequestHead timeStamp config.MerchantShortCode.Value None

        let requestBody, signatureBody =
            prepareAuthorizeDirectDebitBodyAndSignature
                token
                amount
                paymentRequestId
                referenceOrderId

        let requestHeadSignatureSerialized = requestHeadForSignature |> JsonSerializer.Serialize<DirectDebitRequestHead>
        let requestBodySignatureSerialized = signatureBody |> JsonSerializer.Serialize<AuthorizeSignature>

        let signature =
            generateDataForSignature
                requestHeadSignatureSerialized
                requestBodySignatureSerialized
            |> generateSignature config.PrivateKey.Value

        let request =
            {
                Verify = { requestHeadForSignature with Signature = Some(signature) }
                Params = requestBody
            }
            |> JsonSerializer.Serialize<AuthorizeApiRequest>
            |> NonemptyString.ofStringUnsafe

        try
            let! bkashResponse =
                bkashRequest
                    httpClient
                    apiEndpoint
                    BkashEndpoint.Pay
                    timeStamp
                    signature
                    (Some request |> HttpMethodWithData.Post)

            let jsonData = JsonSerializer.Deserialize<JsonObject>(bkashResponse.Value)

            return
                match
                    jsonData
                    |> JsonObject.tryFind "result"
                    |> JsonNode.maybeTryFind "resultStatus"
                    |> Option.map string
                    = Some SuccessResultStatus
                with
                | true ->
                    match
                        jsonData
                        |> JsonObject.tryFind "paymentId"
                        |> JsonNode.toNonemptyString
                    with
                    | Some paymentId ->
                        Ok (paymentId, { Request = request; Response = Some bkashResponse })

                    | None ->
                        (
                            BkashFailure.UnknownFailure,
                            (
                                { Request = request; Response = Some bkashResponse },
                                "PaymentId is empty" |> NonemptyString.ofString
                            )
                            |> DataError
                        )
                        |> Error

                | false ->
                    getBkashError jsonData request bkashResponse $"Authorize of {paymentRequestId}"
                    |> Error
        with
        | error ->
            //inquiry time
            let! inquiryResult = queryBkashDirectDebitPayment httpClient apiEndpoint config paymentRequestId $"Authorize payment {paymentRequestId}"

            return
                match inquiryResult with
                | Ok (inquiryInfo, reqAndResp) ->
                    Ok (inquiryInfo.PaymentId, reqAndResp)
                | Error (_, inquiryError) ->
                    (
                        BkashFailure.NonBkashFailure,
                        error |> handleApiException request $"Unexpected error while authorizing bkash payment, InquiryResult: {inquiryError.ToString()}"
                    )
                    |> Error

    }

// ------------------------------------- Capture

type CaptureBody = {
    [<JsonPropertyName("paymentRequestId")>] PaymentRequestId:     string
    [<JsonPropertyName("captureRequestId")>] CaptureRequestId:     string
    [<JsonPropertyName("paymentAmount")>]    PaymentAmount:        Amount
}

type CaptureSignature = {
    [<JsonPropertyName("paymentRequestId")>] PaymentRequestId:     string
    [<JsonPropertyName("captureRequestId")>] CaptureRequestId:     string
    [<JsonPropertyName("paymentAmount")>]    PaymentAmount:        Amount
}

type CaptureApiRequest = {
    [<JsonPropertyName("verify")>] Verify: DirectDebitRequestHead
    [<JsonPropertyName("params")>] Params: CaptureBody
}

let prepareCaptureBodyAndSignature
        (paymentRequestId: NonemptyString)
        (captureRequestId: NonemptyString)
        (paymentAmount:    Amount)
        : CaptureBody * CaptureSignature =
    let requestBody: CaptureBody = {
        PaymentRequestId = paymentRequestId.Value
        CaptureRequestId = captureRequestId.Value
        PaymentAmount    = paymentAmount
    }

    let signatureBody: CaptureSignature = {
        PaymentRequestId = requestBody.PaymentRequestId
        CaptureRequestId = requestBody.CaptureRequestId
        PaymentAmount    = requestBody.PaymentAmount
    }

    (requestBody, signatureBody)

let internal captureBkashDirectDebitPayment
        (httpClient:       HttpClient)
        (apiEndpoint: NonemptyString)
        (config:           BkashDirectDebitConfig)
        (paymentRequestId: NonemptyString)
        (captureRequestId: NonemptyString)
        (amount:           PositiveDecimal)
        : Task<Result<NonemptyString * RequestAndResponse, ApiError>> =
    task {
        let timeStamp = DateTimeOffset.Now

        let requestHeadForSignature = generateRequestHead timeStamp config.MerchantShortCode.Value None

        let paymentAmount = {
            Value = amount.Value.ToString("F")
            Currency = bdtCurrencyName
        }

        let requestBody, signatureBody =
            prepareCaptureBodyAndSignature
                paymentRequestId
                captureRequestId
                paymentAmount

        let requestHeadSignatureSerialized = requestHeadForSignature |> JsonSerializer.Serialize<DirectDebitRequestHead>
        let requestBodySignatureSerialized = signatureBody |> JsonSerializer.Serialize<CaptureSignature>

        let signature =
            generateDataForSignature
                requestHeadSignatureSerialized
                requestBodySignatureSerialized
            |> generateSignature config.PrivateKey.Value

        let request =
            {
                Verify = { requestHeadForSignature with Signature = Some(signature) }
                Params = requestBody
            }
            |> JsonSerializer.Serialize<CaptureApiRequest>
            |> NonemptyString.ofStringUnsafe
        try
            let! bkashResponse =
                bkashRequest
                    httpClient
                    apiEndpoint
                    BkashEndpoint.Capture
                    timeStamp
                    signature
                    (Some request |> HttpMethodWithData.Post)

            let jsonData = JsonSerializer.Deserialize<JsonObject>(bkashResponse.Value)

            return
                match jsonData
                    |> JsonObject.tryFind "result"
                    |> JsonNode.maybeTryFind "resultStatus"
                    |> Option.map(string)
                    = Some SuccessResultStatus
                with

                | true ->
                    jsonData
                    |> JsonObject.tryFind "captureId"
                    |> JsonNode.toNonemptyString
                    |> Option.map (fun captureId -> (captureId, { Request = request; Response = Some bkashResponse }))
                    |> Option.getAsResult (
                        (
                            { Request = request; Response = Some bkashResponse },
                            "captureId is not valid" |> NonemptyString.ofString
                        )
                        |> DataError
                    )

                | false ->
                    let error = "Unexpected status while trying to capture from bkash account"
                    (
                        { Request = request; Response = Some bkashResponse },
                        error |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error
        with
        | error ->
            //inquiry time
            let! inquiryResult = queryBkashDirectDebitPayment httpClient apiEndpoint config captureRequestId $"Capture payment: {paymentRequestId}, capture: {captureRequestId}"

            return
                match inquiryResult with
                | Ok (inquiryInfo, reqAndResp) ->
                    Ok (inquiryInfo.PaymentId, reqAndResp)
                | Error (_, inquiryError) ->
                    error |> handleApiException request $"Unexpected error while capturing bkash payment, InquiryResult: {inquiryError.ToString()}"
                    |> Error
    }

// ------------------------------------- Release / Cancel

type CancelBody = {
    [<JsonPropertyName("paymentRequestId")>] PaymentRequestId:     string
}

type CancelSignature = {
    [<JsonPropertyName("paymentRequestId")>] PaymentRequestId:     string
}

type CancelApiRequest = {
    [<JsonPropertyName("verify")>] Verify: DirectDebitRequestHead
    [<JsonPropertyName("params")>] Params: CancelBody
}

let prepareCancelPaymentBodyAndSignature
        (paymentRequestId: NonemptyString)
        : CancelBody * CancelSignature =
    let requestBody: CancelBody = {
        PaymentRequestId = paymentRequestId.Value
    }

    let signatureBody: CancelSignature = {
        PaymentRequestId = requestBody.PaymentRequestId
    }

    (requestBody, signatureBody)

let internal cancelBkashDirectDebitPayment
        (httpClient:       HttpClient)
        (apiEndpoint:      NonemptyString)
        (config:           BkashDirectDebitConfig)
        (paymentRequestId: NonemptyString)
        : Task<Result<RequestAndResponse, ApiError>> =
    task {
        let timeStamp = DateTimeOffset.Now

        let requestHeadForSignature = generateRequestHead timeStamp config.MerchantShortCode.Value None

        let requestBody, signatureBody =
            prepareCancelPaymentBodyAndSignature
                paymentRequestId

        let requestHeadSignatureSerialized = requestHeadForSignature |> JsonSerializer.Serialize<DirectDebitRequestHead>
        let requestBodySignatureSerialized = signatureBody |> JsonSerializer.Serialize<CancelSignature>

        let signature =
            generateDataForSignature
                requestHeadSignatureSerialized
                requestBodySignatureSerialized
            |> generateSignature config.PrivateKey.Value

        let request =
            {
                Verify = { requestHeadForSignature with Signature = Some(signature) }
                Params = requestBody
            }
            |> JsonSerializer.Serialize<CancelApiRequest>
            |> NonemptyString.ofStringUnsafe

        try
            let! bkashResponse =
                bkashRequest
                    httpClient
                    apiEndpoint
                    BkashEndpoint.CancelPayment
                    timeStamp
                    signature
                    (Some request |> HttpMethodWithData.Post)

            let jsonData = JsonSerializer.Deserialize<JsonObject>(bkashResponse.Value)

            return
                match jsonData
                    |> JsonObject.tryFind "result"
                    |> JsonNode.maybeTryFind "resultStatus"
                    |> Option.map(string)
                    = Some SuccessResultStatus
                with

                | true ->
                    Ok { Request = request; Response = Some bkashResponse }

                | false ->
                    let error = "Unexpected status while trying to cancel payment"
                    (
                        { Request = request; Response = Some bkashResponse },
                        error |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error

        with
        | error ->
            return
                error
                |> handleApiException request "Unexpected error while trying to cancel payment"
                |> Error
    }

// ------------------------------------- Inquiry Refund

type InquiryRefundBody = {
    [<JsonPropertyName("refundRequestId")>] RefundRequestId: string
}

type InquiryRefundSignature = {
    [<JsonPropertyName("refundRequestId")>] RefundRequestId: string
}

type InquiryRefundApiRequest = {
    [<JsonPropertyName("verify")>] Verify: DirectDebitRequestHead
    [<JsonPropertyName("params")>] Params: InquiryRefundBody
}

let prepareInquiryRefundedPaymentBodyAndSignature
        (refundRequestId: NonemptyString)
        : InquiryRefundBody * InquiryRefundSignature =
    let requestBody: InquiryRefundBody = {
        RefundRequestId = refundRequestId.Value
    }

    let signatureBody: InquiryRefundSignature = {
        RefundRequestId = requestBody.RefundRequestId
    }

    (requestBody, signatureBody)

let internal queryRefundedBkashDirectDebitPayment
        (httpClient:      HttpClient)
        (apiEndpoint:     NonemptyString)
        (config:          BkashDirectDebitConfig)
        (refundRequestId: NonemptyString)
        : Task<Result<InquiredRefundInfo * RequestAndResponse, ApiError>> =
    task {
        let timeStamp = DateTimeOffset.Now

        let requestHeadForSignature = generateRequestHead timeStamp config.MerchantShortCode.Value None

        let requestBody, signatureBody =
            prepareInquiryRefundedPaymentBodyAndSignature
                refundRequestId

        let requestHeadSignatureSerialized = requestHeadForSignature |> JsonSerializer.Serialize<DirectDebitRequestHead>
        let requestBodySignatureSerialized = signatureBody |> JsonSerializer.Serialize<InquiryRefundSignature>

        let signature =
            generateDataForSignature
                requestHeadSignatureSerialized
                requestBodySignatureSerialized
            |> generateSignature config.PrivateKey.Value

        let request =
            {
                Verify = { requestHeadForSignature with Signature = Some(signature) }
                Params = requestBody
            }
            |> JsonSerializer.Serialize<InquiryRefundApiRequest>
            |> NonemptyString.ofStringUnsafe

        try
            let! bkashResponse =
                bkashRequest
                    httpClient
                    apiEndpoint
                    BkashEndpoint.InquiryRefund
                    timeStamp
                    signature
                    (Some request |> HttpMethodWithData.Post)

            let jsonData = JsonSerializer.Deserialize<JsonObject>(bkashResponse.Value)

            return
                match jsonData
                    |> JsonObject.tryFind "result"
                    |> JsonNode.maybeTryFind "resultStatus"
                    |> Option.map(string)
                    = Some SuccessResultStatus
                with
                | true ->
                    match jsonData|> JsonObject.tryFind "refundId" |> JsonNode.toNonemptyString with
                    | Some(refundId) ->
                        match
                            jsonData
                            |> JsonObject.tryFind "refundAmount"
                            |> JsonNode.maybeTryFind "value"
                            |> JsonNode.toNonemptyString
                            |> Option.bind (fun amountString -> match Decimal.TryParse amountString.Value with | true, n -> n |> PositiveDecimal.ofDecimal | _ -> None)
                        with
                        | Some amount ->
                            Ok ({ RefundId = refundId; RefundAmount = amount }, { Request = request; Response = Some bkashResponse })

                        | None ->
                            (
                                { Request = request; Response = Some bkashResponse },
                                "Refund amount is invalid" |> NonemptyString.ofString
                            )
                            |> DataError
                            |> Error

                    | None ->
                        (
                            { Request = request; Response = Some bkashResponse },
                            "RefundId is empty" |> NonemptyString.ofString
                        )
                        |> DataError
                        |> Error

                | false ->
                    let error = $"Unexpected status while trying to inquire about refund: {refundRequestId.Value}"
                    (
                        { Request = request; Response = Some bkashResponse },
                        error |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error
        with
        | error ->
            return
                error
                |> handleApiException request $"Unexpected error while trying to inquire about refund: {refundRequestId.Value} bkash account"
                |> Error
    }


// ------------------------------------- Refund

type RefundBody = {
    [<JsonPropertyName("paymentRequestId")>] PaymentRequestId: string
    [<JsonPropertyName("refundRequestId")>]  RefundRequestId:  string
    [<JsonPropertyName("refundAmount")>]     RefundAmount:     Amount
}

type RefundSignature = {
    [<JsonPropertyName("paymentRequestId")>] PaymentRequestId: string
    [<JsonPropertyName("refundRequestId")>]  RefundRequestId:  string
    [<JsonPropertyName("refundAmount")>]     RefundAmount:     Amount
}

type RefundApiRequest = {
    [<JsonPropertyName("verify")>] Verify: DirectDebitRequestHead
    [<JsonPropertyName("params")>] Params: RefundBody
}

let prepareRefundPaymentBodyAndSignature
        (paymentOrCaptureRequestId: NonemptyString)
        (refundRequestId:           NonemptyString)
        (refundAmount:              Amount)
        : RefundBody * RefundSignature =
    let requestBody: RefundBody = {
        PaymentRequestId = paymentOrCaptureRequestId.Value
        RefundRequestId  = refundRequestId.Value
        RefundAmount     = refundAmount
    }

    let signatureBody: RefundSignature = {
        PaymentRequestId = requestBody.PaymentRequestId
        RefundRequestId  = requestBody.RefundRequestId
        RefundAmount     = requestBody.RefundAmount
    }

    (requestBody, signatureBody)

let internal refundBkashDirectDebitPayment
        (httpClient:                HttpClient)
        (apiEndpoint:               NonemptyString)
        (config:                    BkashDirectDebitConfig)
        (paymentOrCaptureRequestId: NonemptyString)
        (refundRequestId:           NonemptyString)
        (amount:                    PositiveDecimal)
        : Task<Result<NonemptyString * RequestAndResponse, ApiError>> =
    task {
        let timeStamp = DateTimeOffset.Now

        let requestHeadForSignature = generateRequestHead timeStamp config.MerchantShortCode.Value None

        let refundAmount = {
            Value = amount.Value.ToString("F")
            Currency = bdtCurrencyName
        }

        let requestBody, signatureBody =
            prepareRefundPaymentBodyAndSignature
                paymentOrCaptureRequestId
                refundRequestId
                refundAmount

        let requestHeadSignatureSerialized = requestHeadForSignature |> JsonSerializer.Serialize<DirectDebitRequestHead>
        let requestBodySignatureSerialized = signatureBody |> JsonSerializer.Serialize<RefundSignature>

        let signature =
            generateDataForSignature
                requestHeadSignatureSerialized
                requestBodySignatureSerialized
            |> generateSignature config.PrivateKey.Value

        let request =
            {
                Verify = { requestHeadForSignature with Signature = Some(signature) }
                Params = requestBody
            }
            |> JsonSerializer.Serialize<RefundApiRequest>
            |> NonemptyString.ofStringUnsafe

        try
            let! bkashResponse =
                bkashRequest
                    httpClient
                    apiEndpoint
                    BkashEndpoint.Refund
                    timeStamp
                    signature
                    (Some request |> HttpMethodWithData.Post)

            let jsonData = JsonSerializer.Deserialize<JsonObject>(bkashResponse.Value)

            return
                match jsonData
                    |> JsonObject.tryFind "result"
                    |> JsonNode.maybeTryFind "resultStatus"
                    |> Option.map(string)
                    = Some SuccessResultStatus
                with
                | true ->
                    jsonData
                    |> JsonObject.tryFind "refundId"
                    |> JsonNode.toNonemptyString
                    |> Option.map (fun refundId -> (refundId, { Request = request; Response = Some bkashResponse }))
                    |> Option.getAsResult (
                        (
                            { Request = request; Response = Some bkashResponse },
                            "refundId is not valid" |> NonemptyString.ofString
                        )
                        |> DataError)
                | false ->
                    let error = "Unexpected status while trying to refund payment"
                    (
                        { Request = request; Response = Some bkashResponse },
                        error |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error

        with
        | error ->
            //inquiry time
            let! inquiryRefundResult = queryRefundedBkashDirectDebitPayment httpClient apiEndpoint config refundRequestId
            return
                match inquiryRefundResult with
                | Ok (inquiryInfo, reqAndResp) ->
                    Ok (inquiryInfo.RefundId, reqAndResp)
                | Error inquiryError ->
                    error |> handleApiException request $"Unexpected error while refunding bkash payment, InquiryResult: {inquiryError.ToString()}"
                    |> Error
    }


// -------------------------------------------------------------------------------------------------------------- SHARED CONTENT END

// ------------------------------------- QQQ
let runBkash : Async<unit> =
    async {
        // 01619777283 - Chaldal
        // 12121  - Pin
        // 123456 - OTP
        // Default20221124dd5a6398a14f467a
        let httpClient = new HttpClient()
        let token = "Default20230208500c3d07a15ccd3c" |> NonemptyString.ofLiteral
        let paymentRequestId = "60044431243562874561237828" |> NonemptyString.ofLiteral
        let paymentId = "AA560A41IS" |> NonemptyString.ofLiteral
        let captureRequestId = $"{paymentRequestId.Value}CAP" |> NonemptyString.ofLiteral
        let captureId = $"CTAA200A3HRS" |> NonemptyString.ofLiteral
        let refundRequestId = $"{paymentRequestId}REF" |> NonemptyString.ofLiteral
        let refundId = "AA260A3EFO" |> NonemptyString.ofLiteral

        //{"result":{"resultStatus":"U","resultCode":"UNKNOWN_EXCEPTION","resultMessage":"Duplicate for All Transactions","bKashResultCode":"ETC70052","bKashResultMessage":"Duplicate for All Transactions"}}

        // ----------------------------------------------- Query User Binding
        // let! userInfo = queryBkashDirectDebitUserInfo httpClient apiEndpoint bkashDirectDebitSandboxConfig token |> Async.AwaitTask
        // printfn $"%A{userInfo}"

        // ----------------------------------------------- Cancel Binding
        // let! cancelResult = cancelBkashDirectDebitToken httpClient apiEndpoint bkashDirectDebitSandboxConfig token |> Async.AwaitTask
        // printfn $"%A{cancelResult}"

        // ----------------------------------------------- Prepare And Apply Token For Binding
        // let! bindingResult = prepareBkashDirectDebitBinding httpClient apiEndpoint bkashDirectDebitSandboxConfig ("https://cookups.app/" |> NonemptyString.ofLiteral) |> Async.AwaitTask
        //
        // printfn $"Binding Preparation Result %A{bindingResult}"
        //
        // printf "Enter AuthCode: "
        // let authCode = Console.ReadLine() |> NonemptyString.ofLiteral
        //
        // let! applyTokenResult = applyBkashDirectDebitToken httpClient apiEndpoint bkashDirectDebitSandboxConfig authCode |> Async.AwaitTask
        //
        // printfn $"Binding Preparation Result %A{applyTokenResult}"




        // ----------------------------------------------- Authorize Payment For Binding Auth Capture
        // let! authorizedPayment =
        //     authorizeBkashDirectDebitPayment
        //         httpClient
        //         apiEndpoint
        //         bkashDirectDebitSandboxConfig
        //         paymentRequestId
        //         ("INV21545" |> NonemptyString.ofLiteral)
        //         token
        //         (200m |> PositiveDecimal.ofDecimalUnsafe)
        //     |> Async.AwaitTask
        //
        // printfn $"%A{authorizedPayment}"

        // ----------------------------------------------- Cancel Authorized Payment
        // let! cancelPayment =
        //     cancelBkashDirectDebitPayment httpClient apiEndpoint bkashDirectDebitSandboxConfig paymentRequestId |> Async.AwaitTask
        // printfn $"%A{cancelPayment}"

        // ----------------------------------------------- Capture Authorized Payment
        // let! capturedPayment =
        //     captureBkashDirectDebitPayment
        //         httpClient
        //         apiEndpoint
        //         bkashDirectDebitSandboxConfig
        //         paymentRequestId
        //         captureRequestId
        //         (5m |> PositiveDecimal.ofDecimalUnsafe)
        //     |> Async.AwaitTask
        //
        // printfn $"%A{capturedPayment}"

        // ----------------------------------------------- Query Authorized or Captured Payment
        // let! queriedPayment =
        //     queryBkashDirectDebitPayment httpClient apiEndpoint bkashDirectDebitSandboxConfig captureRequestId "FSharpPractice Query"|> Async.AwaitTask
        // printfn $"%A{queriedPayment}"

        // ----------------------------------------------- Refund Captured Payment
        // let! refundPayment =
        //     refundBkashDirectDebitPayment
        //         httpClient
        //         apiEndpoint
        //         bkashDirectDebitSandboxConfig
        //         captureRequestId
        //         refundRequestId
        //         (5m |> PositiveDecimal.ofDecimalUnsafe)
        //     |> Async.AwaitTask
        // printfn $"%A{refundPayment}"

        // ----------------------------------------------- Query Refunded Payment
        // let! queriedRefundedPayment =
        //     queryRefundedBkashDirectDebitPayment httpClient apiEndpoint bkashDirectDebitSandboxConfig ("600444312452378461237828REF" |> NonemptyString.ofLiteral) |> Async.AwaitTask
        // printfn $"%A{queriedRefundedPayment}"

        // let lines =
        //     [|
        //         $"ApplyToken response data for %s{request.Value} is %A{applyTokenResponse.Value}"
        //     |]
        // printfn $"%A{applyTokenResponse.Value}"
        // File.WriteAllLines($"DATA AccessTokenResponseForAuthCode.txt", lines)

        // let lines =
        //     [|
        //         $"AccessToken for %s{request} is %s{jsonData.AccessToken}"
        //     |]
        // printfn $"%s{jsonData.AccessToken}"
        // File.WriteAllLines($"TOKEN AccessTokenForAuthCode.txt", lines)




        // let rect = Rectangle(3u, 5u)
        //
        // let foo = InteropClass.my_function rect
        //
        // printfn $"{foo}"

        return ()
    }