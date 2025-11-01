[<AutoOpen>]
module OptionExtensions

type Option<'T> with
    member this.SideEffect (f: 'T -> unit) : unit = Option.iter f this

    member this.ToDisplayString : string =
        match this with
        | Some value -> value.ToString()
        | None       -> "N/A"

// For some reason, without this alias, we get the following error:
// error FS0534: A module abbreviation must be a simple name, not a path
module Option = Microsoft.FSharp.Core.Option
module Option =
    let tap (o: 'O option) (f: 'O -> 'T -> 'T) (x: 'T) =
        match o with
        | Some o -> f o x
        | None -> x

    let getOrElse<'T> (elseValue: 'T) (o: Option<'T>) : 'T =
        Option.defaultValue elseValue o

    let getOrElseLazy<'T> (elseValueThunk: unit -> 'T) (o: Option<'T>) : 'T =
        Option.defaultWith elseValueThunk o

    let getOrElseRaise<'T, 'E when 'E :> System.Exception> (theException: 'E) (o: Option<'T>) : 'T =
        getOrElseLazy (fun () -> raise theException) o

    let mapOrElse<'T, 'U> (elseValue: 'U) (mapper: 'T -> 'U) (source: Option<'T>) : 'U =
        source |> Option.map mapper |> getOrElse elseValue

    let rec all<'T> (os: List<Option<'T>>) : Option<List<'T>> =
        match os with
        | [] -> Some []
        | None :: _ -> None
        | (Some t) :: rest ->
            match all rest with
            | None -> None
            | Some restUnwrapped -> Some (t :: restUnwrapped)

    let getAsResult<'T, 'Error> (none: 'Error) (o: Option<'T>) : Result<'T, 'Error> =
        match o with
        | Some t -> Ok t
        | None   -> Error none

    let getAsResultLazy<'T, 'Error> (noneThunk: unit -> 'Error) (o: Option<'T>) : Result<'T, 'Error> =
        match o with
        | Some t -> Ok t
        | None   -> Error (noneThunk ())

    let sideEffect<'T> (f: 'T -> unit) (o: Option<'T>) : unit = Option.iter f o

    let flatMap<'T, 'U> (f: 'T -> Option<'U>) (o: Option<'T>) : Option<'U> = Option.bind f o

    let flattenSeq (opts: seq<Option<'T>>) : List<'T> =
        (opts, List.empty)
        ||> Seq.foldBack
            (fun opt acc ->
                match opt with
                | Some t -> t :: acc
                | None   -> acc
            )

    let flattenList<'T> (os: List<Option<'T>>): List<'T> =
        let rec loop (acc: List<'T>) = function
        | []             -> List.rev acc
        | None :: os     -> loop acc os
        | (Some o) :: os -> loop (o :: acc) os

        loop [] os

    let mapYield<'T, 'U> (mapper: 'T -> 'U) (o: Option<'T>) : seq<'U> =
        match o with
        | Some v -> mapper v |> Seq.singleton
        | None -> Seq.empty

type OptionBuilder() =
    member _.Bind(x, f) =
        match x with
        | None -> None
        | Some value -> f value

    member _.Return(value) =
        Some value

    member _.ReturnFrom(wrappedValue) =
        wrappedValue

    member _.Zero() =
        None

let optional = OptionBuilder()