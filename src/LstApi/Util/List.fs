module List

let pickMiddle (l: 'a list) =
    let middleIx = (l.Length / 2) - 1
    l |> Seq.skip middleIx |> Seq.head
