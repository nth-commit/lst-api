namespace LstApi.Model

open System

type Timestamp =
    | Timestamp of int64

    static member value(Timestamp x) = x

    static member (-)(x: Timestamp, y: Timestamp) =
        TimeSpan.FromTicks((x |> Timestamp.value) - (y |> Timestamp.value))

    static member (-)(x: Timestamp, y: TimeSpan) =
        Timestamp((x |> Timestamp.value) - y.Ticks)

    static member (+)(x: Timestamp, y: TimeSpan) =
        Timestamp((x |> Timestamp.value) + y.Ticks)

    member this.ToDateTimeOffset() =
        DateTimeOffset(DateTime(this |> Timestamp.value, DateTimeKind.Utc), TimeSpan.Zero)

    member this.ToDateTimeOffset(offset: TimeSpan) =
        DateTimeOffset(DateTime(this |> Timestamp.value), offset)

    override this.ToString() =
        DateTime(this |> Timestamp.value, DateTimeKind.Utc).ToString("s") + "Z"
