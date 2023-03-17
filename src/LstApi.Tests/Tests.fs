[<VerifyXunit.UsesVerify>]
module Tests

open System
open VerifyXunit
open Xunit
open LstApi.Model
open LstApi.Model.TimeZoneAdjustments

let formatAdjustment (adjustment: TimeZoneAdjustment) =
    let adjustmentEventUtc = adjustment.Timestamp.ToDateTimeOffset().ToString("""s""")

    let lstTimeZoneInfo =
        TimeZoneInfo.CreateCustomTimeZone("Lst", adjustment.Offset, "LST", "LST")

    let adjustmentEventLst =
        TimeZoneInfo
            .ConvertTime(adjustment.Timestamp.ToDateTimeOffset(), lstTimeZoneInfo)
            .ToString("""s""")

    $"Adjustment Event (UTC) = %s{adjustmentEventUtc}, Adjustment Event (LST) = %s{adjustmentEventLst}, UTC Offset = %s{adjustment.Offset.ToString()}"

[<Fact>]
let ``Snapshots`` () =
    let adjustments: string list =
        calculateTimeZoneAdjustments
            { Location = { Latitude = -43; Longitude = 172 }
              OffsetResolution = OffsetResolution.FiveMinutes
              AdjustmentEventOffset = TimeSpan.FromHours(-4)
              ExtraOffset = TimeSpan.Zero }
        |> List.map formatAdjustment

    Verifier.Verify(String.concat "\n" adjustments).ToTask() |> Async.AwaitTask
