module Seq

let repeat (loops: int) (a: 'a seq) : 'a seq =
    let rec loop (n: int) (a: 'a seq) : 'a seq =
        if n = 0 then
            Seq.empty
        else
            a |> Seq.append (loop (n - 1) a)

    loop loops a

let pairWith (f: 'a -> 'b) (a: 'a seq) : ('a * 'b) seq = a |> Seq.map (fun x -> (x, f x))

type IncompleteGroup<'key, 'value> =
    { CurrentKey: 'key
      CurrentGroup: 'value seq }

type CompleteGroup<'key, 'value> =
    { NextKey: 'key
      CurrentKey: 'key
      Group: 'value seq }

type GroupProgress<'key, 'value> =
    | None of unit
    | Incomplete of IncompleteGroup<'key, 'value>
    | Complete of CompleteGroup<'key, 'value>

let groupUntilChangedBy (f: 'a -> 'b) (a: 'a seq) : ('b * 'a seq) seq =
    let scanItem (progress: GroupProgress<'b, 'a>) (item: 'a) : GroupProgress<'b, 'a> =
        match progress with
        | None _ ->
            let key = f item

            Incomplete
                { CurrentKey = key
                  CurrentGroup = item |> Seq.singleton }
        | Incomplete { CurrentKey = key
                       CurrentGroup = group } ->
            let newKey = f item

            if newKey = key then
                Incomplete
                    { CurrentKey = key
                      CurrentGroup = group |> Seq.append (item |> Seq.singleton) }
            else
                Complete
                    { NextKey = newKey
                      CurrentKey = key
                      Group = group }
        | Complete { NextKey = key } ->
            Incomplete
                { CurrentKey = key
                  CurrentGroup = item |> Seq.singleton }

    a
    |> Seq.scan scanItem (GroupProgress<'b, 'a>.None ())
    |> Seq.choose (function
        | None _ -> Option.None
        | Incomplete _ -> Option.None
        | Complete { Group = group; CurrentKey = key } -> (key, group) |> Option.Some)

let takeUntilHeadRepeatedBy (f: 'a -> 'b) (a: 'a seq) : 'a seq =
    let rec loop (a: 'a seq) (head: 'b) : 'a seq =
        match a |> Seq.tryHead with
        | Option.None -> Seq.empty
        | Option.Some x ->
            if f x = head then
                Seq.empty
            else
                Seq.append (x |> Seq.singleton) (loop (a |> Seq.tail) head)

    match a |> Seq.tryHead with
    | Option.None -> Seq.empty
    | Option.Some x -> Seq.append (x |> Seq.singleton) (loop (a |> Seq.tail) (f x))
