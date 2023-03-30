namespace LstApi.Model

open System
open LstApi.Model

type TimeZoneAdjustment =
    { Timestamp: Timestamp
      Offset: TimeSpan }

module TimeZoneAdjustment =

    module private Helpers =

        let getRuleBoundary (ruleBoundary: TimeZoneRuleBoundary) (ruleOffset: TimeSpan) (year: int) : DateTimeOffset =
            DateTimeOffset(
                DateTime(
                    year,
                    ruleBoundary.Month,
                    ruleBoundary.Day,
                    ruleBoundary.TimeOfDay.Hour,
                    ruleBoundary.TimeOfDay.Minute,
                    ruleBoundary.TimeOfDay.Second,
                    DateTimeKind.Unspecified
                ),
                ruleOffset
            )

        let getRuleStart (rule: TimeZoneRule) (yearOffset: int) (asAt: DateTimeOffset) : DateTimeOffset =
            getRuleBoundary rule.Start rule.Offset (asAt.Year + yearOffset)

        let getRuleEnd
            (rule: TimeZoneRule)
            (yearOffset: int)
            (asAt: DateTimeOffset)
            (ruleStart: DateTimeOffset)
            : DateTimeOffset =
            let ruleEndWrapped = getRuleBoundary rule.End rule.Offset (asAt.Year + yearOffset)

            if ruleEndWrapped < ruleStart then
                ruleEndWrapped.AddYears(1)
            else
                ruleEndWrapped

        let getNextOccurenceOfRule
            (rule: TimeZoneRule)
            (yearOffset: int)
            (asAt: DateTimeOffset)
            : (DateTimeOffset * TimeSpan) =

            let ruleStart = getRuleStart rule yearOffset asAt
            let ruleEnd = getRuleEnd rule yearOffset asAt ruleStart

            if asAt >= ruleStart && asAt.AddSeconds(-1) <= ruleEnd then
                (ruleStart, TimeSpan.Zero)
            else
                (ruleStart, (ruleStart - asAt))

    let calculateAdjustments (rules: TimeZoneRule list) (asAt: DateTimeOffset) : TimeZoneAdjustment list =
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
