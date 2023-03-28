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

type TimeZoneRuleBoundary =
    { Month: int
      Day: int
      TimeOfDay: TimeOnly }

type TimeZoneRule =
    { Start: TimeZoneRuleBoundary
      End: TimeZoneRuleBoundary
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

    let sampleTimeZoneAdjustments (options: TimeZoneAdjustmentOptions) : TimeZoneAdjustment seq =
        DateTimeHelpers.daysOfYear 2023
        |> Seq.where DateTimeHelpers.isNotLeapDay
        |> Seq.map (DateTimeHelpers.getUtcStartOfDayTicks >> Timestamp)
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

    let getNextOccurenceOfRule
        (rule: TimeZoneRule)
        (yearOffset: int)
        (asAt: DateTimeOffset)
        : (DateTimeOffset * TimeSpan) =
        let ruleTimeZone =
            TimeZoneInfo.CreateCustomTimeZone("LST", rule.Offset, "LST", "LST")

        let ruleStart =
            DateTimeOffset(
                DateTime(
                    asAt.Year + yearOffset,
                    rule.Start.Month,
                    rule.Start.Day,
                    rule.Start.TimeOfDay.Hour,
                    rule.Start.TimeOfDay.Minute,
                    rule.Start.TimeOfDay.Second,
                    DateTimeKind.Unspecified
                ),
                ruleTimeZone.BaseUtcOffset
            )

        let ruleEndWrapped =
            DateTimeOffset(
                DateTime(
                    asAt.Year + yearOffset,
                    rule.End.Month,
                    rule.End.Day,
                    rule.End.TimeOfDay.Hour,
                    rule.End.TimeOfDay.Minute,
                    rule.End.TimeOfDay.Second,
                    DateTimeKind.Unspecified
                ),
                ruleTimeZone.BaseUtcOffset
            )

        let ruleEnd =
            if ruleEndWrapped < ruleStart then
                ruleEndWrapped.AddYears(1)
            else
                ruleEndWrapped

        if asAt >= ruleStart && asAt.AddSeconds(-1) <= ruleEnd then
            (ruleStart, TimeSpan.Zero)
        else
            (ruleStart, (ruleStart - asAt))

let calculateTimeZoneRules (options: TimeZoneAdjustmentOptions) : TimeZoneRule list =
    let adjustments = Helpers.sampleTimeZoneAdjustments options

    let rules =
        adjustments
        |> Seq.pairwiseWrapped
        |> Seq.map (fun (adjustment, nextAdjustment) -> Helpers.inferRule adjustment nextAdjustment)
        |> Seq.toList

    rules

let calculateTimeZoneAdjustments (rules: TimeZoneRule list) (asAt: DateTimeOffset) : TimeZoneAdjustment list =
    let adjustments =
        [ -1; 0; 1 ]
        |> Seq.collect (fun yearOffset ->
            rules
            |> Seq.map (fun rule ->
                let (next, ttl) = Helpers.getNextOccurenceOfRule rule yearOffset asAt
                (rule, next, ttl)))
        |> Seq.filter (fun (_, _, ttl) -> ttl >= TimeSpan.Zero && ttl <= TimeSpan.FromDays(365))
        |> Seq.sortBy (fun (_, _, ttl) -> ttl)
        |> Seq.map (fun (rule, next, _) ->
            { Offset = rule.Offset
              Timestamp = next.UtcTicks |> Timestamp })
        |> Seq.toList
        
    adjustments
