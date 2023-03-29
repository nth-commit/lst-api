module LstApi.Http

open System
open System.ComponentModel.DataAnnotations
open FsToolkit.ErrorHandling
open Giraffe
open LstApi.Util
open LstApi.Model
open LstApi.Model.TimeZoneAdjustments
open Microsoft.AspNetCore.Mvc


module Rules =

    [<CLIMutable>]
    type Query =
        { [<Required>]
          latitude: float option
          [<Required>]
          longitude: float option
          offsetResolution: string option }

    type private AbstractRuleDto =
        abstract member _type: string

    type private TimeZoneRuleOnDateDto =
        { month: int
          day: int
          timeOfDay: TimeOnly
          offset: TimeSpan }

        interface AbstractRuleDto with
            member this._type = "on_date"

    let private queryToOptions (query: Query) : EndpointResult<TimeZoneAdjustmentOptions> =
        OffsetResolution.fromStringOption query.offsetResolution
        |> Result.map (fun offsetResolution ->
            { Location =
                { Latitude = query.latitude.Value
                  Longitude = query.longitude.Value }
              OffsetResolution = offsetResolution
              AdjustmentEventOffset = TimeSpan.FromHours(-4)
              ExtraOffset = TimeSpan.Zero })
        |> Result.mapError (fun x -> ProblemDetails(Status = 400, Title = "Bad Request", Detail = x))

    let private ruleToDto (rule: TimeZoneRule) : TimeZoneRuleOnDateDto =
        { month = rule.Start.Month
          day = rule.Start.Day
          timeOfDay = rule.Start.TimeOfDay
          offset = rule.Offset }

    let handler: HttpHandler =
        Endpoint.toHandler (fun ctx ->
            asyncResult {
                let! query = EndpointResult.bindQueryString<Query> ctx
                let! options = queryToOptions query

                let rules = calculateTimeZoneRules options

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
          offsetResolution: string option }
        
    type private TimeZoneAdjustmentDto =
        { timestamp: DateTimeOffset
          offset: TimeSpan }

    let private queryToOptions (query: Query) : EndpointResult<TimeZoneAdjustmentOptions> =
        OffsetResolution.fromStringOption query.offsetResolution
        |> Result.map (fun offsetResolution ->
            { Location =
                { Latitude = query.latitude.Value
                  Longitude = query.longitude.Value }
              OffsetResolution = offsetResolution
              AdjustmentEventOffset = TimeSpan.FromHours(-4)
              ExtraOffset = TimeSpan.Zero })
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

                let rules = calculateTimeZoneRules options
                let adjustments = calculateTimeZoneAdjustments rules asAt

                return adjustments |> Seq.map adjustmentToDto |> Seq.toArray
            })
