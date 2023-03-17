namespace LstApi.Model

open System

type OffsetResolution =
    | Exact
    | FiveMinutes
    | TenMinutes
    | FifteenMinutes
    | ThirtyMinutes
    | OneHour

    static member AsTimeSpan =
        function
        | Exact -> TimeSpan.FromTicks(1)
        | FiveMinutes -> TimeSpan.FromMinutes(5)
        | TenMinutes -> TimeSpan.FromMinutes(10)
        | FifteenMinutes -> TimeSpan.FromMinutes(15)
        | ThirtyMinutes -> TimeSpan.FromMinutes(30)
        | OneHour -> TimeSpan.FromHours(1)
