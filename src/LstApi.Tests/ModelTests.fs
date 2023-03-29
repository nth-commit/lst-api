[<VerifyXunit.UsesVerify>]
module Tests

open System
open VerifyXunit
open Xunit
open LstApi.Model

let formatAdjustment (adjustment: TimeZoneAdjustment) =
    let adjustmentEventUtc =
        adjustment.Timestamp.ToDateTimeOffset(adjustment.Offset).ToString("""s""")

    let lstTimeZoneInfo =
        TimeZoneInfo.CreateCustomTimeZone("Lst", adjustment.Offset, "LST", "LST")

    let adjustmentEventLst =
        TimeZoneInfo
            .ConvertTime(adjustment.Timestamp.ToDateTimeOffset(), lstTimeZoneInfo)
            .ToString("""s""")

    $"Adjustment Event (UTC) = %s{adjustmentEventUtc}, Adjustment Event (LST) = %s{adjustmentEventLst}, UTC Offset = %s{adjustment.Offset.ToString()}"

let formatRule (rule: TimeZoneRule) : string =
    let timeOfDayFormat = "h:mm:ss tt"

    $"Start = %02i{rule.Start.Day}/%02i{rule.End.Month} - {rule.Start.TimeOfDay.ToString(timeOfDayFormat)}, "
    + $"End = %02i{rule.End.Day}/%02i{rule.End.Month} - {rule.End.TimeOfDay.ToString(timeOfDayFormat)}, "
    + $"UTC Offset = {rule.Offset}"

[<Fact>]
let ``Snapshot Rules`` () =
    let rules: string list =
        TimeZoneRule.calculateRules
            { Location = { Latitude = -43; Longitude = 172 }
              OffsetResolution = OffsetResolution.FiveMinutes
              AdjustmentEventOffset = TimeSpan.FromHours(-4)
              ExtraOffset = TimeSpan.Zero }
        |> List.map formatRule

    Verifier.Verify(String.concat "\n" rules).AutoVerify().ToTask()
    |> Async.AwaitTask

// [<Fact>]
// let ``Snapshot Current Rules`` () =
//     let rules =
//         calculateTimeZoneRules
//             { Location = { Latitude = -43; Longitude = 172 }
//               OffsetResolution = OffsetResolution.FiveMinutes
//               AdjustmentEventOffset = TimeSpan.FromHours(-4)
//               ExtraOffset = TimeSpan.Zero }
//
//     let calculateCurrentRule (from: DateTimeOffset) = calculateTimeZoneAdjustments rules from
//
//     let currentRules: string list =
//         [ 0 .. (7 * 24) ]
//         |> Seq.map (fun hours -> DateTimeOffset(DateTime(2023, 1, 1), TimeSpan.Zero).AddHours(hours))
//         |> Seq.pairWith calculateCurrentRule
//         |> Seq.map (fun x -> x |> snd |> formatAdjustment)
//         |> Seq.toList
//
//     Verifier.Verify(String.concat "\n" currentRules).AutoVerify().ToTask()
//     |> Async.AwaitTask
