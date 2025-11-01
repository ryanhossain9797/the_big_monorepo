module Methods.Braintree

open System
open System.Collections
open Braintree
open Common
open System.Threading.Tasks
open Models

type BraintreeConfig = {
    PaymentAccountHead: NonemptyString
    MerchantId:         NonemptyString
    PublicKey:          NonemptyString
    PrivateKey:         NonemptyString
}

let braintreeSandboxConfig = {
    PaymentAccountHead = NonemptyString.ofLiteral "Braintree"
    MerchantId         = NonemptyString.ofLiteral "22b4ympmx2s4f5ky"
    PublicKey          = NonemptyString.ofLiteral "x8jk9hhkhvgyn3gv"
    PrivateKey         = NonemptyString.ofLiteral "326ef0ba65d935c98e7b6d4b2514be4c"
}

type ProfileId = ProfileId of Guid
type PayerId = PayerId of ProfileId * UserId: Guid

type BraintreePaymentInfo = {
    SkipAdvancedFraudChecking: bool
    OrderId:                   NonemptyString
    OrderDescription:          Option<NonemptyString>
}

[<RequireQualifiedAccess>]
type BraintreePaymentInstrument =
| Paypal     of PayerEmail: NonemptyString
| CreditCard of MaskedCardNumber: NonemptyString
| Unknown

type BraintreePaymentAuthorizationInfo = {
    BraintreeTxId:              NonemptyString
    BraintreePaymentInstrument: BraintreePaymentInstrument
    Nonce:                      NonemptyString
}

// -------------------------------------------------------------------------------------------------------------- SHARED CONTENT START


let getBraintreeEnvironmentFromName (environmentConfig: NonemptyString) =
    match environmentConfig.Value with
    | "SANDBOX" -> Braintree.Environment.SANDBOX |> Ok
    | "PRODUCTION" -> Braintree.Environment.PRODUCTION |> Ok
    | _ -> Error $"Unknown Braintree Environment: {environmentConfig.Value}"


// ---------------------------------------------------------------------- Braintree Payment

// ------------------------------------- Generate Client Token
let internal generateClientToken
        (environment: Braintree.Environment)
        (config:      BraintreeConfig) =

    task {
        let request =
            NonemptyString.ofStringUnsafe $"{environment}, MerchantId: {config.MerchantId.Value}, PublicKey: {config.PublicKey.Value}, PrivateKey: {config.PrivateKey.Value}"

        try
            let gateway =
                Braintree.Configuration (
                    environment,
                    config.MerchantId.Value,
                    config.PublicKey.Value,
                    config.PrivateKey.Value
                )
                |> Braintree.BraintreeGateway

            //The token
            let! maybeToken = gateway.ClientToken.GenerateAsync(new Braintree.ClientTokenRequest())
            let response = maybeToken |> NonemptyString.ofString
            let reqAndResp = { Request = request; Response = response }
            match response with
            | Some token ->
                return Ok (token, reqAndResp)
            | None ->
                return
                    (reqAndResp, "token is empty" |> NonemptyString.ofString)
                    |> DataError
                    |> Error
        with
        | error ->
            return
                (
                    { Request = request; Response = None },
                    error.ToString() |> NonemptyString.ofString
                )
                |> Unknown
                |> Error
    }

// ------------------------------------- Authorize

let internal authorizeBraintreePayment
        (environment: Braintree.Environment)
        (config:      BraintreeConfig)
        (payerId:     PayerId)
        (paymentInfo: BraintreePaymentInfo)
        (nonce:       NonemptyString)
        (amount:      PositiveDecimal)
        : Task<Result<BraintreePaymentAuthorizationInfo * RequestAndResponse, ApiError>> =
    task {
        let (PayerId (_, payerGuid)) = payerId

        let request =
            Braintree.TransactionRequest (
                Amount = amount.Value,
                PaymentMethodNonce = nonce.Value,
                Options =
                    Braintree.TransactionOptionsRequest(
                        SubmitForSettlement       = Nullable(false),
                        SkipAdvancedFraudChecking = Nullable(true)
                    ),
                OrderId = paymentInfo.OrderId.Value
                // CustomFields =
                //     Generic.Dictionary<string, string>(
                //         Map.empty
                //             .Add("customerGuid", payerGuid.ToString())
                //             .Add("description", paymentInfo.OrderDescription |> NonemptyString.optionToString)
                //     )

            )
        let gateway =
            Braintree.Configuration (
                environment,
                config.MerchantId.Value,
                config.PublicKey.Value,
                config.PrivateKey.Value
            )
            |> Braintree.BraintreeGateway

        try
            let! result = gateway.Transaction.SaleAsync(request)

            return
                match result.IsSuccess() with
                | true ->
                    let braintreeAuthorization: BraintreePaymentAuthorizationInfo =
                        {
                            BraintreeTxId = result.Target.Id |> NonemptyString.ofStringUnsafe
                            BraintreePaymentInstrument =
                                match result.Target.PaymentInstrumentType with
                                | Braintree.PaymentInstrumentType.PAYPAL_ACCOUNT ->
                                    BraintreePaymentInstrument.Paypal (
                                        result.Target.PayPalDetails.PayerEmail
                                        |> NonemptyString.ofString
                                        |> Option.defaultWith (fun _ -> "unknown@unknown" |> NonemptyString.ofLiteral)
                                    )

                                | Braintree.PaymentInstrumentType.CREDIT_CARD ->
                                    BraintreePaymentInstrument.CreditCard (
                                        result.Target.CreditCard.MaskedNumber
                                        |> NonemptyString.ofString
                                        |> Option.defaultWith (fun _ -> "UNKNOWN" |> NonemptyString.ofLiteral)
                                    )

                                | _ ->
                                    BraintreePaymentInstrument.Unknown

                            Nonce = nonce
                        }

                    let reqAndResp: RequestAndResponse = {
                        Request = request.ToString() |> NonemptyString.ofLiteral
                        Response = result.ToString() |> NonemptyString.ofString
                    }

                    Ok (braintreeAuthorization, reqAndResp)
                | false ->
                    let reqAndResp: RequestAndResponse = {
                        Request = request.ToString() |> NonemptyString.ofLiteral
                        Response = None
                    }

                    DataError (reqAndResp, $"Failed to authorize braintree payment, nonce: {nonce.Value}, customerGuid: {payerGuid.ToString()}, error: {result.Errors}" |> NonemptyString.ofString)
                    |> Error
        with
        | exn ->
            let reqAndResp: RequestAndResponse = {
                Request = request.ToString() |> NonemptyString.ofLiteral
                Response = None
            }
            return
                Unknown (reqAndResp, $"Failed to authorize braintree payment, nonce: {nonce.Value}, customerGuid: {payerGuid.ToString()}, error: {exn.Message}" |> NonemptyString.ofString)
                |> Error
    }

// -------------------------------------------------------------------------------------------------------------- SHARED CONTENT END


// ------------------------------------- QQQ
let runBraintree : Async<unit> =
    async {

        let braintreeEnvironment =
            match ("SANDBOX" |> NonemptyString.ofLiteral |> getBraintreeEnvironmentFromName) with
            | Ok env -> env
            | Error _ -> failwith "Invalid Env Name"

        let profileGuid = Guid.Parse "7ce4208d-a699-45b1-9fbf-6abd6b9fca64"
        let payerGuid = Guid.Parse "20b0b7e2-727a-485f-b0c3-d728f5ba0b09"
        let payerId = PayerId (ProfileId profileGuid, payerGuid)

        let paymentInfo: BraintreePaymentInfo = {
            SkipAdvancedFraudChecking = true
            OrderId = NonemptyString.ofLiteral "OrderId"
            OrderDescription = NonemptyString.ofString "Order Description"
        }

        let nonce = NonemptyString.ofLiteral "fake-valid-nonce"

        // -----------------------------------------------Generate Braintree Client Token
        // let! tokenResult = generateClientToken braintreeEnvironment braintreeSandboxConfig |> Async.AwaitTask
        // printfn $"%A{tokenResult}"

        // -----------------------------------------------Authorize Braintree Payment
        let! authorizeResult = authorizeBraintreePayment braintreeEnvironment braintreeSandboxConfig payerId paymentInfo nonce (2m |> PositiveDecimal.ofDecimalUnsafe) |> Async.AwaitTask
        printfn $"%A{authorizeResult}"



        return ()
    }