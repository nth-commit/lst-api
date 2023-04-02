namespace Lst.Model

open System

type OffsetResolution =
    | Exact
    | FiveMinutes
    | TenMinutes
    | FifteenMinutes
    | ThirtyMinutes
    | OneHour

module OffsetResolution =
    // val asTimeSpan : OffsetResolution -> TimeSpan
    let asTimeSpan =
        function
        | Exact -> TimeSpan.FromTicks(1)
        | FiveMinutes -> TimeSpan.FromMinutes(5)
        | TenMinutes -> TimeSpan.FromMinutes(10)
        | FifteenMinutes -> TimeSpan.FromMinutes(15)
        | ThirtyMinutes -> TimeSpan.FromMinutes(30)
        | OneHour -> TimeSpan.FromHours(1)

    // val fromStringOption : string option -> OffsetResolution option
    let fromStringOption =
        function
        | None
        | Some "" -> Ok OffsetResolution.FiveMinutes
        | Some "exact" -> Ok OffsetResolution.Exact
        | Some "five_minutes" -> Ok OffsetResolution.FiveMinutes
        | Some "ten_minutes" -> Ok OffsetResolution.TenMinutes
        | Some "fifteen_minutes" -> Ok OffsetResolution.FifteenMinutes
        | Some "thirty_minutes" -> Ok OffsetResolution.ThirtyMinutes
        | Some "one_hour" -> Ok OffsetResolution.OneHour
        | Some x -> Error $"Invalid offset resolution '{x}'"
