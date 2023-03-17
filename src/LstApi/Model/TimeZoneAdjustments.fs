module LstApi.Model.TimeZoneAdjustments

open System
open Innovative.SolarCalculator
open LstApi.Util
open LstApi.Model

type TimeZoneAdjustmentOptions =
    { Location: Geolocation
      OffsetResolution: OffsetResolution
      AdjustmentEventOffset: TimeSpan
      ExtraOffset: TimeSpan }

type TimeZoneAdjustment =
    { Timestamp: Timestamp
      Offset: TimeSpan }

module private Helpers =
    let getSunriseTimestamp (location: Geolocation) (timestamp: Timestamp) : Timestamp =
        let dt = DateTime(timestamp |> Timestamp.value, DateTimeKind.Utc)
        let solarTimes = SolarTimes(dt, location.Latitude, location.Longitude)
        DateTime.SpecifyKind(solarTimes.Sunrise, DateTimeKind.Utc).Ticks |> Timestamp

    let getExactLstOffset (location: Geolocation) (timestamp: Timestamp) =
        let sunriseUtc = getSunriseTimestamp location timestamp
        timestamp - sunriseUtc

    let calculateOffset
        (location: Geolocation)
        (resolution: OffsetResolution)
        (utcStartOfDayTimestamp: Timestamp)
        : TimeSpan =
        let exactOffset = getExactLstOffset location utcStartOfDayTimestamp
        let resolutionTimeSpan = resolution |> OffsetResolution.AsTimeSpan
        let offsetRemainder = exactOffset.Ticks % resolutionTimeSpan.Ticks
        TimeSpan.FromTicks(exactOffset.Ticks - offsetRemainder)

    let createAdjustment
        (adjustmentEventOffset: TimeSpan)
        (extraOffset: TimeSpan)
        (group: (Timestamp * TimeSpan) seq)
        : TimeZoneAdjustment =
        let utcStartOfDayTimestamp, baseOffset = group |> Seq.toList |> List.pickMiddle
        let offset = baseOffset + extraOffset

        let lstStartOfDayTimestamp = utcStartOfDayTimestamp - offset
        let lstAdjustmentEventTimestamp = lstStartOfDayTimestamp + adjustmentEventOffset

        { Timestamp = lstAdjustmentEventTimestamp
          Offset = offset }

let calculateTimeZoneAdjustments (options: TimeZoneAdjustmentOptions) =
    DateTimeHelpers.daysOfYear 2023
    |> Seq.where DateTimeHelpers.isNotLeapDay
    |> Seq.map (DateTimeHelpers.getUtcStartOfDayTicks >> Timestamp)
    |> Seq.pairWith (Helpers.calculateOffset options.Location options.OffsetResolution)
    |> (Seq.repeat 2 >> Seq.groupUntilChangedBy snd >> Seq.map snd >> Seq.skip 1) // The first group might be incomplete, so skip it after wrapping around
    |> Seq.map (Helpers.createAdjustment options.AdjustmentEventOffset options.ExtraOffset)
    |> Seq.takeUntilHeadRepeatedBy (fun x -> x.Timestamp) // Remove the rest of the wrap-around (from repetition above)
    |> Seq.sortBy (fun x -> x.Timestamp) // Again, because we wrapped around, the order might be out-by-one
    |> Seq.toList
