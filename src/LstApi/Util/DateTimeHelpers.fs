module LstApi.Util.DateTimeHelpers

open System

let isLeapDay (date: DateOnly) : bool = (date.Month = 2 && date.Day = 29)

let isNotLeapDay = isLeapDay >> not

let daysOfYear (year: int) : DateOnly seq =
    let start = DateOnly(year, 1, 1)

    let nextDay (date: DateOnly) =
        let date0 = date.AddDays(1)
        (date0, date0) |> Some

    let isInYear (date: DateOnly) : bool = date.Year = year

    Seq.unfold nextDay start |> Seq.takeWhile isInYear

let getUtcStartOfDayTicks (date: DateOnly) : int64 =
    let dt = DateTimeOffset(DateTime(date.Year, date.Month, date.Day), TimeSpan.Zero)
    dt.UtcDateTime.Ticks
