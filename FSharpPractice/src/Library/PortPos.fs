module Methods.PortPos

open System.Net.Http
open System.Net.Http.Headers
open System.Net.Mime
open System
open Common
open System.Net
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open OptionExtensions
open System.Security.Cryptography
open Models

[<Literal>]
let portPosInvoiceExpirySeconds = 180u

type PortPosConfig = {
    PaymentAccountHead: NonemptyString
    AppKey:             NonemptyString
    SecretKey:          NonemptyString
    RedirectUrl:        NonemptyString
    IpnUrl:             NonemptyString
}

type CustomerAddress = {
    Street:  NonemptyString
    City:    NonemptyString
    State:   NonemptyString
    ZipCode: NonemptyString
    Country: NonemptyString
}

type CustomerInfo = {
    Name:    NonemptyString
    Email:   NonemptyString
    Phone:   NonemptyString
    Address: CustomerAddress
}

type ProductInfo = {
    Name:        NonemptyString
    Description: NonemptyString
}

type PortPosInvoiceInfo = {
    InvoiceId:   NonemptyString
    RedirectUrl: NonemptyString
}

type PortPosInvoiceStatus =
| Pending
| Accepted
| Rejected
| Cancelled
| Expired
| RefundPending
with
    static member fromStatus (status: NonemptyString) =
        match status.Value with
        | "PENDING"        -> Some Pending
        | "ACCEPTED"       -> Some Accepted
        | "REJECTED"       -> Some Rejected
        | "CANCELLED"      -> Some Cancelled
        | "EXPIRED"        -> Some Expired
        | "REFUND_PENDING" -> Some RefundPending
        | _                -> None

let apiEndpoint = "https://api-sandbox.portpos.com/payment/v2" |> NonemptyString.ofLiteral

let portPosSandboxConfig: PortPosConfig= {
    PaymentAccountHead = NonemptyString.ofLiteral "PortPos"
    AppKey             = NonemptyString.ofLiteral "075d3990c1c103d182fd36abf6f3a6de"
    SecretKey          = NonemptyString.ofLiteral "8185dcc6d3d91e796fc89e8e6c651d36"
    RedirectUrl        = NonemptyString.ofLiteral "https://localhost.chaldal.com:4430/PortwalletRedirect"
    IpnUrl             = NonemptyString.ofLiteral "http://localhost:50355/api-v4/Order/SettlePortwalletPaymentFromIpn"
}
// -------------------------------------------------------------------------------------------------------------- COPIED CONTENT END

// -------------------------------------------------------------------------------------------------------------- SHARED CONTENT START// -------------------------------------------------------------------------------------------------------------- SHARED CONTENT START


[<Literal>]
let bdtCurrencyName = "BDT"

type PortPosEndpoint =
| Invoice
| InvoiceRetrieve
| InvoiceRefund
| InvoiceCancel
    member this.toString : NonemptyString =
        match this with
        | Invoice ->         NonemptyString.ofLiteral <| "/invoice"
        | InvoiceRetrieve -> NonemptyString.ofLiteral <| "/invoice"
        | InvoiceRefund ->   NonemptyString.ofLiteral <| "/invoice/refund"
        | InvoiceCancel ->   NonemptyString.ofLiteral <| "/invoice/cancel"

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

let portPosRequest
        (httpClient:    HttpClient)
        (apiEndpoint:   NonemptyString)
        (request:       HttpMethodWithData)
        (endpoint:      PortPosEndpoint)
        (urlParams:     Option<NonemptyString>)
        (authorization: NonemptyString) =
    task {
        let urlParams = urlParams |> NonemptyString.optionToString
        let finalUrl =
            apiEndpoint.Value
                + endpoint.toString.Value
                + urlParams
            |> NonemptyString.ofStringUnsafe

        let req =
            generateRequest
                finalUrl
                request

        req.Headers.Authorization <- AuthenticationHeaderValue ("Bearer", authorization.Value)

        let! response = httpClient.SendAsync req
        let statusCode = response.StatusCode
        let! responseString = response.Content.ReadAsStringAsync()
        return (statusCode, responseString |> NonemptyString.ofStringUnsafe)
    }

let toBytes (text: string) =
    ASCIIEncoding.ASCII.GetBytes(text)

let toMD5Hash (text: string) =
    text
    |> toBytes
    |> MD5.Create().ComputeHash
    |> Convert.ToHexString

let generateToken (secretKey: string) (timeStamp: DateTimeOffset) =
    (secretKey + timeStamp.ToUnixTimeSeconds().ToString())
    |> toMD5Hash

let base64String (text: string) =
    text
    |> toBytes
    |> Convert.ToBase64String

(*
http://developer.portpos.com/documentation-v2.php

Bearer token has a base64 of appkey and a token adhered by a colon.
The token contains md5 hash of a concatenation of secretkey and current unix timestamp in seconds.
Example:

var appKey: string = ...
var secretKey: string = ...
var timeStamp: string = now.ToUnixTimestampSeconds().ToString()

var token =  md5(secretKey + timeStamp)

var authorization = “Bearer “ + base64_encode(appKey + ”:” + token)

*)
let generateBearer
        (timeStamp: DateTimeOffset)
        (appKey:    string)
        (secretKey: string) =
    let token = generateToken secretKey timeStamp

    $"{appKey}:{token}"
    |> base64String
    |> NonemptyString.ofStringUnsafe


// ---------------------------------------------------------------------- Invoicing

// Invoice Response Body
type ActionResponse = {
    [<JsonPropertyName("type")>]    Type:    string
    [<JsonPropertyName("url")>]     Url:     string
    [<JsonPropertyName("payload")>] Payload: Option<string>
    [<JsonPropertyName("method")>]  Method:  string
}

type OrderResponse = {
    [<JsonPropertyName("status")>] Status: string
}

type DataResponse = {
    [<JsonPropertyName("invoice_id")>] InvoiceId: string
    [<JsonPropertyName("reference")>]  Reference: string
    [<JsonPropertyName("order")>]      Order:     OrderResponse
    [<JsonPropertyName("action")>]     Action:    Option<ActionResponse>
}

type InvoiceResponseBody = {
    [<JsonPropertyName("result")>] Result: string
    [<JsonPropertyName("data")>]   Data:   DataResponse
}

// ------------------------------------- Create Invoice

// Invoice Request Body

type Address = {
    [<JsonPropertyName("street")>]  Street:  string
    [<JsonPropertyName("city")>]    City:    string
    [<JsonPropertyName("state")>]   State:   string
    [<JsonPropertyName("zipcode")>] ZipCode: string
    [<JsonPropertyName("country")>] Country: string
}

type Customer = {
    [<JsonPropertyName("name")>]    Name:    string
    [<JsonPropertyName("email")>]   Email:   string
    [<JsonPropertyName("phone")>]   Phone:   string
    [<JsonPropertyName("address")>] Address: Address
}

type ShippingRequest = {
    [<JsonPropertyName("customer")>] Customer: Customer
}

type BillingRequest = {
    [<JsonPropertyName("customer")>] Customer: Customer
}

type ProductRequest = {
    [<JsonPropertyName("name")>]        Name:        string
    [<JsonPropertyName("description")>] Description: string
}

type OrderRequest = {
    [<JsonPropertyName("amount")>]       Amount:          decimal
    [<JsonPropertyName("currency")>]     Currency:        string
    [<JsonPropertyName("redirect_url")>] RedirectUrl:     string
    [<JsonPropertyName("ipn_url")>]      IpnUrl:          string

    [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
    [<JsonPropertyName("reference")>]    Reference:   Option<string>
    // [<JsonPropertyName("validity")>]     Validity:    string //Optional expiry time
}

type InvoiceRequestBody = {
    [<JsonPropertyName("order")>]    Order:    OrderRequest
    [<JsonPropertyName("product")>]  Product:  ProductRequest
    [<JsonPropertyName("billing")>]  Billing:  BillingRequest

    [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
    [<JsonPropertyName("shipping")>] Shipping: Option<ShippingRequest>
}

// Invoice Helper Functions
let prepareCustomerForRequest (customerInfo: CustomerInfo) =
    {
        Name    = customerInfo.Name.Value
        Email   = customerInfo.Email.Value
        Phone   = customerInfo.Phone.Value
        Address = {
            Street  = customerInfo.Address.Street.Value
            City    = customerInfo.Address.City.Value
            State   = customerInfo.Address.State.Value
            ZipCode = customerInfo.Address.ZipCode.Value
            Country = customerInfo.Address.Country.Value
        }
    }


let prepareInvoiceBody
        (redirectUrl:          NonemptyString)
        (ipnUrl:               NonemptyString)
        (billingCustomerInfo:  CustomerInfo)
        (shippingCustomerInfo: Option<CustomerInfo>)
        (productInfo:          ProductInfo)
        (amount:               PositiveDecimal)
        (reference:            Option<NonemptyString>)
        : InvoiceRequestBody =

    let billingCustomer  = prepareCustomerForRequest billingCustomerInfo
    let maybeShippingCustomer = shippingCustomerInfo |> Option.map prepareCustomerForRequest

    let billing:  BillingRequest  = { Customer = billingCustomer }
    let shipping: Option<ShippingRequest> =
        maybeShippingCustomer
        |> Option.map (fun shippingCustomer -> { Customer = shippingCustomer })

    let product: ProductRequest = {
        Name        = productInfo.Name.Value
        Description = productInfo.Description.Value
    }

    let order: OrderRequest = {
        Amount          = amount.Value
        Currency        = bdtCurrencyName
        RedirectUrl     = redirectUrl.Value
        IpnUrl          = ipnUrl.Value
        Reference       = reference |> Option.map NonemptyString.value
    }

    let invoiceBody: InvoiceRequestBody = {
         Order    = order
         Product  = product
         Billing  = billing
         Shipping = shipping
    }

    invoiceBody



// Invoice Request
let internal createPortPosInvoice
        (httpClient:           HttpClient)
        (apiEndpoint:          NonemptyString)
        (redirectUrl:          NonemptyString)
        (ipnUrl:               NonemptyString)
        (config:               PortPosConfig)
        (billingCustomerInfo:  CustomerInfo)
        (shippingCustomerInfo: Option<CustomerInfo>)
        (productInfo:          ProductInfo)
        (amount:               PositiveDecimal)
        (reference:            Option<NonemptyString>)
        : Task<Result<PortPosInvoiceInfo * RequestAndResponse, ApiError>> =

    task {
        let timeStamp = DateTimeOffset.Now |> toBdt
        let authorization = generateBearer timeStamp config.AppKey.Value config.SecretKey.Value
        let endpoint = PortPosEndpoint.Invoice

        let invoiceBody =
            prepareInvoiceBody
                redirectUrl
                ipnUrl
                billingCustomerInfo
                shippingCustomerInfo
                productInfo
                amount
                reference

        let request =
            invoiceBody
            |> JsonSerializer.Serialize<InvoiceRequestBody>
            |> NonemptyString.ofStringUnsafe

        let requestInfo =
            $"{endpoint.toString} - {request}" |> NonemptyString.ofStringUnsafe

        try
            let! statusCode, invoiceResponseJson = portPosRequest httpClient apiEndpoint (Some request |> HttpMethodWithData.Post) endpoint None authorization

            return
                match statusCode with
                | HttpStatusCode.Created ->
                    let invoiceResponse = JsonSerializer.Deserialize<InvoiceResponseBody>(invoiceResponseJson.Value)
                    match invoiceResponse.Result with
                    | "success" ->
                        match (invoiceResponse.Data.InvoiceId |> NonemptyString.ofString, invoiceResponse.Data.Action |> Option.bind (fun action -> action.Url |> NonemptyString.ofString)) with

                        | Some invoiceId, Some redirectUrl ->
                            let invoiceInfo: PortPosInvoiceInfo = {
                                InvoiceId   = invoiceId
                                RedirectUrl = redirectUrl
                            }
                            (invoiceInfo, { Request = requestInfo; Response = Some invoiceResponseJson }) |> Ok

                        | _ ->
                            (
                                { Request = requestInfo; Response = Some invoiceResponseJson },
                                "Response missing InvoiceId or RedirectUrl" |> NonemptyString.ofString
                            )
                            |> DataError
                            |> Error

                    | error ->
                        (
                            { Request = requestInfo; Response = Some invoiceResponseJson },
                            $"Invoice creation error: {error}" |> NonemptyString.ofString
                        )
                        |> ErrorCode
                        |> Error

                | _ ->
                    (
                        { Request = requestInfo; Response = Some invoiceResponseJson },
                        $"HttpStatusCode: {statusCode}" |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error
        with
        | error ->
            return
                error
                |> handleApiException requestInfo "Unexpected error while creating PortPos invoice"
                |> Error
    }

// ------------------------------------- Retrieve Invoice


// Invoice Retrieve
let internal retrievePortPosInvoice
        (httpClient:  HttpClient)
        (apiEndpoint: NonemptyString)
        (config:      PortPosConfig)
        (invoiceId:   NonemptyString)
        : Task<Result<PortPosInvoiceStatus * RequestAndResponse, ApiError>> =

    task {
        let timeStamp = DateTimeOffset.Now |> toBdt
        let authorization = generateBearer timeStamp config.AppKey.Value config.SecretKey.Value
        let urlParams = $"/{invoiceId.Value}" |> NonemptyString.ofString
        let endpoint = PortPosEndpoint.InvoiceRetrieve

        let requestInfo = $"{endpoint.toString}{urlParams.ToDisplayString}" |> NonemptyString.ofLiteral

        try
            let! statusCode, invoiceResponseJson = portPosRequest httpClient apiEndpoint HttpMethodWithData.Get endpoint urlParams authorization

            return
                match statusCode with
                | HttpStatusCode.OK ->
                    let invoiceResponse = JsonSerializer.Deserialize<InvoiceResponseBody>(invoiceResponseJson.Value)
                    match invoiceResponse.Result with

                    | "success" ->
                        match invoiceResponse.Data.Order.Status |> NonemptyString.ofString |> Option.bind PortPosInvoiceStatus.fromStatus with
                        | Some status ->
                            Ok (status, { Request = requestInfo; Response = Some invoiceResponseJson })

                        | None ->
                            (
                                { Request = requestInfo; Response = Some invoiceResponseJson },
                                $"Response status is invalid {invoiceResponse.Data.Order.Status}" |> NonemptyString.ofString
                            )
                            |> DataError
                            |> Error

                    | error ->
                        (
                            { Request = requestInfo; Response = Some invoiceResponseJson },
                            $"Invoice retrieve result is {error}" |> NonemptyString.ofString
                        )
                        |> ErrorCode
                        |> Error

                | _ ->
                    (
                        { Request = requestInfo; Response = Some invoiceResponseJson },
                        $"HttpStatusCode: {statusCode}" |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error
        with
        | error ->
            return
                error
                |> handleApiException requestInfo "Unexpected error while retrieving port pos invoice"
                |> Error
    }


// ------------------------------------- Cancel Invoice


// Invoice Retrieve
let internal checkAndCancelPortPosInvoice
        (httpClient:       HttpClient)
        (apiEndpoint:      NonemptyString)
        (config:           PortPosConfig)
        (portPosInvoiceId: NonemptyString)
        : Task<Result<PortPosInvoiceStatus * RequestAndResponse, ApiError>> =

    task {
        let timeStamp = DateTimeOffset.Now |> toBdt
        let authorization = generateBearer timeStamp config.AppKey.Value config.SecretKey.Value
        let urlParams = $"/{portPosInvoiceId.Value}" |> NonemptyString.ofString
        let endpoint = PortPosEndpoint.InvoiceCancel

        let! invoiceStatusResult = retrievePortPosInvoice httpClient apiEndpoint config portPosInvoiceId

        let requestInfo = $"{endpoint.toString}{urlParams.ToDisplayString}" |> NonemptyString.ofLiteral

        match invoiceStatusResult with
        | Ok (invoiceStatus, reqAndResp) ->
            match invoiceStatus with
            | Accepted
            | Rejected
            | Expired
            | RefundPending
            | Cancelled -> return Ok (invoiceStatus, reqAndResp)
            | Pending ->
                try
                    let! statusCode, invoiceResponseJson = portPosRequest httpClient apiEndpoint (HttpMethodWithData.Post None) endpoint urlParams authorization

                    return
                        match statusCode with
                        | HttpStatusCode.Accepted ->
                            Ok (Cancelled, { Request = requestInfo; Response = Some invoiceResponseJson })

                        | _ ->
                            (
                                { Request = requestInfo; Response = Some invoiceResponseJson },
                                $"HttpStatusCode: {statusCode}" |> NonemptyString.ofString
                            )
                            |> ErrorCode
                            |> Error
                with
                | error ->
                    return
                        error
                        |> handleApiException requestInfo "Unexpected error while cancelling portpos invoice"
                        |> Error
        | Error error ->
            return Error error
    }

// ------------------------------------- Refund Invoice

type RefundRequest = {
    [<JsonPropertyName("amount")>]   Amount:   Decimal
    [<JsonPropertyName("currency")>] Currency: String
}

type InvoiceRefundRequestBody = {
    [<JsonPropertyName("refund")>] Refund: RefundRequest
}

let prepareRefundBody
        (amount: PositiveDecimal)
        : InvoiceRefundRequestBody =
    {
        Refund = {
            Amount   = amount.Value
            Currency = bdtCurrencyName
        }
    }


let internal refundPortPosInvoice
        (httpClient:       HttpClient)
        (apiEndpoint:      NonemptyString)
        (config:           PortPosConfig)
        (portPosInvoiceId: NonemptyString)
        (refundAmount:     PositiveDecimal)
        : Task<Result<PortPosInvoiceStatus * RequestAndResponse, ApiError>> =

    task {
        let timeStamp = DateTimeOffset.Now |> toBdt
        let authorization = generateBearer timeStamp config.AppKey.Value config.SecretKey.Value
        let urlParams = $"/{portPosInvoiceId.Value}" |> NonemptyString.ofString
        let endpoint = PortPosEndpoint.InvoiceRefund

        let invoiceBody =
            prepareRefundBody
                refundAmount

        let request =
            invoiceBody
            |> JsonSerializer.Serialize<InvoiceRefundRequestBody>
            |> NonemptyString.ofStringUnsafe

        let requestInfo =
            $"{endpoint.toString} - {request}" |> NonemptyString.ofStringUnsafe

        try
            let! statusCode, invoiceResponseJson = portPosRequest httpClient apiEndpoint (HttpMethodWithData.Post (Some request)) endpoint urlParams authorization

            return
                match statusCode with
                | HttpStatusCode.Accepted ->
                    Ok (RefundPending, { Request = requestInfo; Response = Some invoiceResponseJson })

                | _ ->
                    (
                        { Request = requestInfo; Response = Some invoiceResponseJson},
                        $"HttpStatusCode: {statusCode}" |> NonemptyString.ofString
                    )
                    |> ErrorCode
                    |> Error

        with
        | error ->
            return
                error
                |> handleApiException requestInfo "Unexpected error while refunding portpos invoice"
                |> Error
    }

// -------------------------------------------------------------------------------------------------------------- SHARED CONTENT END

let runPortPos : Async<unit> =
    async {
        let httpClient = new HttpClient()
        let config = portPosSandboxConfig
        let amount = PositiveDecimal.ofDecimalUnsafe 12.0m
        let reference = NonemptyString.ofString "REFERENCE2" //TODO

        let testAddress: CustomerAddress = {
            Street  = NonemptyString.ofLiteral <| "Malibagh"
            City    = NonemptyString.ofLiteral <| "Dhaka"
            State   = NonemptyString.ofLiteral <| "Dhaka"
            ZipCode = NonemptyString.ofLiteral <| "1219"
            Country = NonemptyString.ofLiteral <| "BD"
        }

        let testCustomer: CustomerInfo = {
            Name            = NonemptyString.ofLiteral <| "It's a me, Mario"
            Email           = NonemptyString.ofLiteral <| "admin@test.com"
            Phone           = NonemptyString.ofLiteral <| "+8801701546984"
            Address         = testAddress
        }

        let testProduct: ProductInfo = {
            Name            = NonemptyString.ofLiteral <| "Noctua Black Fan"
            Description     = NonemptyString.ofLiteral <| "Beri khul"
        }

        let lastInvoiceId =   "863EB751A4207B81" |> NonemptyString.ofStringUnsafe
        let lastRedirectUrl = "https://payment-sandbox.portwallet.com/payment/?invoice=863EB751A4207B81" |> NonemptyString.ofStringUnsafe

        // ----------------------------------------------- Prepare Invoice
        // let! invoice = createPortPosInvoice httpClient apiEndpoint ("https://cookups.app" |> NonemptyString.ofLiteral) ("https://cookups.app" |> NonemptyString.ofLiteral) config testCustomer None testProduct amount reference |> Async.AwaitTask
        // printfn $"%A{invoice}"

        // ----------------------------------------------- Retrieve Invoice
        let! invoice = retrievePortPosInvoice httpClient apiEndpoint config lastInvoiceId |> Async.AwaitTask
        printfn $"%A{invoice}"

        // ----------------------------------------------- Cancel Invoice
        // let! invoice = cancelPortPosInvoice httpClient config lastInvoiceId |> Async.AwaitTask
        // printfn $"%A{invoice}"

        // ----------------------------------------------- Refund Invoice
        // let! invoice = refundPortPosInvoice httpClient config lastInvoiceId (5m |> PositiveDecimal.ofDecimalUnsafe) |> Async.AwaitTask
        // printfn $"%A{invoice}"

        return ()
    }