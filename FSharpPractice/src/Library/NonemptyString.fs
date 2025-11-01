[<Microsoft.FSharp.Core.AutoOpen>]
module NonemptyString

type NonemptyString =
    private | NonemptyString of string

    // NOTE this method is here to make the "%O" syntax of sprintf work nicely
    override this.ToString () : string =
        this.Value

    member this.Value : string =
        match this with
        | NonemptyString value -> value

type NonemptyLowerCaseString =
    private | NonemptyLowerCaseString of string

    // NOTE this method is here to make the "%O" syntax of sprintf work nicely
    override this.ToString () : string =
        this.Value

    member this.Value : string =
        match this with
        | NonemptyLowerCaseString value -> value

module NonemptyString =
    let value (ns: NonemptyString) : string =
        ns.Value

    let optionToString (candidate: Option<NonemptyString>) : string =
        match candidate with
        | Some (NonemptyString value) -> value
        | None                        -> ""

    let ofString (candidate: string) : Option<NonemptyString> =
        match candidate with
        | null
        | ""    -> None
        | value -> Some (NonemptyString value)

    let ofStringUnsafe (candidate: string) : NonemptyString =
        ofString candidate
        |> Option.get

    let ofStringWithDefault (defaultValue: string) (candidate: string) : NonemptyString =
        ofString candidate
        |> Option.defaultWith (fun _ -> ofStringUnsafe defaultValue)

    let ofLiteral (literal: string) : NonemptyString =
        ofStringUnsafe literal

let (|NonemptyString|) (candidate: NonemptyString) =
    match candidate with
    | NonemptyString.NonemptyString s -> s

module NonemptyLowerCaseString =
    let value (ns: NonemptyLowerCaseString) : string =
        ns.Value

    let optionToString (candidate: Option<NonemptyLowerCaseString>) : string =
        match candidate with
        | Some (NonemptyLowerCaseString value) -> value
        | None                        -> ""

    /// Converts empty strings to None but fails for null string, i.e. ofString and optionToString make an isomorphism
    let ofString (candidate: string) : Option<NonemptyLowerCaseString> =
        match candidate with
        | null  ->
            failwith "null string is not isomorphic to Option<NonemptyLowerCaseString>"
        | ""    -> None
        | value -> Some (NonemptyLowerCaseString (value.ToLowerInvariant()))


    let ofStringUnsafe (candidate: string) : NonemptyLowerCaseString =
        ofString candidate
        |> Option.get

    let ofLiteral (literal: string) : NonemptyLowerCaseString =
        ofStringUnsafe literal

    let ofNonemptyString (nonEmptyString: NonemptyString) : NonemptyLowerCaseString =
        nonEmptyString.Value.ToLowerInvariant()
        |> ofStringUnsafe

    let toNonemptyString (nonEmptyLowerCaseString: NonemptyLowerCaseString) : NonemptyString =
        nonEmptyLowerCaseString.Value |> NonemptyString

let (|NonemptyLowerCaseString|) (candidate: NonemptyLowerCaseString) =
    match candidate with
    | NonemptyLowerCaseString.NonemptyLowerCaseString s -> s