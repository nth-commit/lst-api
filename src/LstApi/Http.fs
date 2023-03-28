module LstApi.Http

open System
open System.ComponentModel.DataAnnotations
open System.Collections.Generic
open FsToolkit.ErrorHandling
open Giraffe
open LstApi.Model
open LstApi.Model.TimeZoneAdjustments
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc

type EndpointResult<'Response> = Result<'Response, ProblemDetails>

module EndpointResult =

    let private validate (request: 'a) : EndpointResult<'a> =
        let validationContext = ValidationContext(request)
        let validationResults = List<ValidationResult>()

        let isValid =
            Validator.TryValidateObject(request, validationContext, validationResults, true)

        if isValid then
            EndpointResult.Ok request
        else
            let detail =
                validationResults |> Seq.map (fun x -> x.ErrorMessage) |> String.concat "\n"

            let problem = ProblemDetails(Status = 400, Title = "Bad Request", Detail = detail)
            EndpointResult.Error problem

    let bindQueryString<'State> (ctx: HttpContext) : EndpointResult<'State> =
        let request = ctx.BindQueryString<'State>()
        validate request

type Endpoint<'Response> = HttpContext -> Async<EndpointResult<'Response>>

module Endpoint =

    let private toHandlerFromResult (endpointResult: EndpointResult<'Response>) : HttpHandler =
        match endpointResult with
        | Ok x -> setStatusCode 200 >=> setContentType "application/json" >=> json x
        | Error problem ->
            setStatusCode problem.Status.Value
            >=> setContentType "application/problem+json"
            >=> json problem

    let toHandler (endpoint: Endpoint<'Response>) : HttpHandler =
        fun next ctx ->
            task {
                let! result = endpoint ctx
                let handler = toHandlerFromResult result
                return! handler next ctx
            }

module Rules =

    [<CLIMutable>]
    type Query =
        { [<Required>]
          latitude: float option
          [<Required>]
          longitude: float option
          offsetResolution: string option }

    type private AbstractRule =
        abstract member _type: string

    type private TimeZoneRuleOnDate =
        { Month: int
          Day: int
          TimeOfDay: TimeOnly
          Offset: TimeSpan }

        interface AbstractRule with
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

    let private ruleToDto (rule: TimeZoneRule) =
        {| _type = "on_date"
           month = rule.Start.Month
           day = rule.Start.Day
           time_of_day = rule.Start.TimeOfDay
           offset = rule.Offset |}

    let handler: HttpHandler =
        Endpoint.toHandler (fun ctx ->
            asyncResult {
                let! query = EndpointResult.bindQueryString<Query> ctx
                let! options = queryToOptions query

                let rules = calculateTimeZoneRules options

                return rules |> Seq.map ruleToDto |> Seq.toArray
            })
