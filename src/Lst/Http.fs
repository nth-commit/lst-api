module Lst.Http

open System
open System.ComponentModel.DataAnnotations
open FsToolkit.ErrorHandling
open Giraffe
open Lst.Model
open Microsoft.AspNetCore.Mvc

module Rules =

    [<CLIMutable>]
    type Query =
        { [<Required>]
          [<Range(-90, 90)>]
          latitude: float option
          [<Required>]
          [<Range(-180, 180)>]
          longitude: float option
          offsetResolution: string option
          adjustmentEventOffsetMinutes: int option
          extraOffsetMinutes: int option }


    let private queryToOptions (query: Query) : EndpointResult<TimeZoneOptions> =
        OffsetResolution.fromStringOption query.offsetResolution
        |> Result.map (fun offsetResolution ->
            { Location =
                { Latitude = query.latitude.Value
                  Longitude = query.longitude.Value }
              OffsetResolution = offsetResolution
              AdjustmentEventOffset =
                query.adjustmentEventOffsetMinutes
                |> Option.map (fun minutes -> TimeSpan.FromMinutes(minutes))
                |> Option.defaultWith (fun _ -> TimeSpan.FromHours(-4))
              ExtraOffset =
                query.extraOffsetMinutes
                |> Option.map (fun minutes -> TimeSpan.FromMinutes(minutes))
                |> Option.defaultWith (fun _ -> TimeSpan.Zero) })
        |> Result.mapError (fun x -> ProblemDetails(Status = 400, Title = "Bad Request", Detail = x))

    let private ruleBoundaryToDto (ruleBoundary: TimeZoneRuleBoundary) =
        {| month = ruleBoundary.Month
           day = ruleBoundary.Day
           timeOfDay = ruleBoundary.TimeOfDay |}

    let private ruleToDto (rule: TimeZoneRule) =
        {| start = ruleBoundaryToDto rule.Start
           ``end`` = ruleBoundaryToDto rule.End
           offset = rule.Offset |}

    let handler: HttpHandler =
        Endpoint.toHandler (fun ctx ->
            asyncResult {
                let! query = EndpointResult.bindQueryString<Query> ctx
                let! options = queryToOptions query

                let rules = TimeZoneRule.calculateRules options

                return rules |> Seq.map ruleToDto |> Seq.toArray
            })

module Adjustments =

    [<CLIMutable>]
    type Query =
        { asAt: DateTimeOffset option
          [<Required>]
          latitude: float option
          [<Required>]
          longitude: float option
          offsetResolution: string option
          adjustmentEventOffsetMinutes: int option
          extraOffsetMinutes: int option }

    let private queryToOptions (query: Query) : EndpointResult<TimeZoneOptions> =
        OffsetResolution.fromStringOption query.offsetResolution
        |> Result.map (fun offsetResolution ->
            { Location =
                { Latitude = query.latitude.Value
                  Longitude = query.longitude.Value }
              OffsetResolution = offsetResolution
              AdjustmentEventOffset =
                query.adjustmentEventOffsetMinutes
                |> Option.map (fun minutes -> TimeSpan.FromMinutes(minutes))
                |> Option.defaultWith (fun _ -> TimeSpan.FromHours(-4))
              ExtraOffset =
                query.extraOffsetMinutes
                |> Option.map (fun minutes -> TimeSpan.FromMinutes(minutes))
                |> Option.defaultWith (fun _ -> TimeSpan.Zero) })
        |> Result.mapError (fun x -> ProblemDetails(Status = 400, Title = "Bad Request", Detail = x))

    let private queryToAsAt (query: Query) : EndpointResult<DateTimeOffset> =
        match query.asAt with
        | Some x -> EndpointResult.Ok x
        | None -> EndpointResult.Ok DateTimeOffset.UtcNow

    let private adjustmentToDto (adjustment: TimeZoneAdjustment) =
        {| timestamp = adjustment.Timestamp.ToDateTimeOffset()
           offset = adjustment.Offset |}

    let handler: HttpHandler =
        Endpoint.toHandler (fun ctx ->
            asyncResult {
                let! query = EndpointResult.bindQueryString<Query> ctx

                let! options = queryToOptions query
                let! asAt = queryToAsAt query

                let rules = TimeZoneRule.calculateRules options
                let adjustments = TimeZoneAdjustment.calculateAdjustments rules asAt

                return adjustments |> Seq.map adjustmentToDto |> Seq.toArray
            })
