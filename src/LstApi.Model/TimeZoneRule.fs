namespace LstApi.Model

open System
open Innovative.SolarCalculator
open LstApi.Model

type TimeZoneOptions =
    { Location: Geolocation
      OffsetResolution: OffsetResolution
      AdjustmentEventOffset: TimeSpan
      ExtraOffset: TimeSpan }

type TimeZoneRuleBoundary =
    { Month: int
      Day: int
      TimeOfDay: TimeOnly }

type TimeZoneRule =
    { Start: TimeZoneRuleBoundary
      End: TimeZoneRuleBoundary
      Offset: TimeSpan }

module TimeZoneRule =

    module private Helpers =

        type TimeZoneAdjustment =
            { Timestamp: Timestamp
              Offset: TimeSpan }

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
            let resolutionTimeSpan = resolution |> OffsetResolution.asTimeSpan
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

        let sampleTimeZoneAdjustments (options: TimeZoneOptions) : TimeZoneAdjustment seq =
            daysOfYear 2023
            |> Seq.where isNotLeapDay
            |> Seq.map (getUtcStartOfDayTicks >> Timestamp)
            |> Seq.pairWith (calculateOffset options.Location options.OffsetResolution)
            |> (Seq.repeat 2 >> Seq.groupUntilChangedBy snd >> Seq.map snd >> Seq.skip 1) // The first group might be incomplete, so skip it after wrapping around
            |> Seq.map (createAdjustment options.AdjustmentEventOffset options.ExtraOffset)
            |> Seq.takeUntilHeadRepeatedBy (fun x -> x.Timestamp) // Remove the rest of the wrap-around (from repetition above)
            |> Seq.sortBy (fun x -> x.Timestamp) // Again, because we wrapped around, the order might be out-by-one

        let inferRule (adjustment: TimeZoneAdjustment) (nextAdjustment: TimeZoneAdjustment) : TimeZoneRule =
            let tz = TimeZoneInfo.CreateCustomTimeZone("Lst", adjustment.Offset, "LST", "LST")

            let startDt = TimeZoneInfo.ConvertTime(adjustment.Timestamp.ToDateTimeOffset(), tz)

            let endDt =
                TimeZoneInfo
                    .ConvertTime(nextAdjustment.Timestamp.ToDateTimeOffset(), tz)
                    .AddSeconds(-1.0)

            { Start =
                { Month = startDt.Month
                  Day = startDt.Day
                  TimeOfDay = TimeOnly(startDt.Hour, startDt.Minute, startDt.Second) }
              End =
                { Month = endDt.Month
                  Day = endDt.Day
                  TimeOfDay = TimeOnly(endDt.Hour, endDt.Minute, endDt.Second) }
              Offset = adjustment.Offset }

    let calculateRules (options: TimeZoneOptions) : TimeZoneRule list =
        let adjustments = Helpers.sampleTimeZoneAdjustments options

        let rules =
            adjustments
            |> Seq.pairwiseWrapped
            |> Seq.map (fun (adjustment, nextAdjustment) -> Helpers.inferRule adjustment nextAdjustment)
            |> Seq.toList

        rules
