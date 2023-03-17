module LstApi.Http

open System
open System.ComponentModel.DataAnnotations
open System.Collections.Generic
open Giraffe
open LstApi.Model
open LstApi.Model.TimeZoneAdjustments
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc

type MiddlewareResult<'State> = Result<'State, ProblemDetails>

type Middleware<'State> = HttpContext -> MiddlewareResult<'State>

module Middleware =

    let bind<'State, 'NextState>
        (f: 'State -> Middleware<'NextState>)
        (m: Middleware<'State>)
        : Middleware<'NextState> =
        fun ctx ->
            match m ctx with
            | Ok x -> f x ctx
            | Error x -> Error x

    let bindResult<'State, 'NextState>
        (f: 'State -> MiddlewareResult<'NextState>)
        (m: Middleware<'State>)
        : Middleware<'NextState> =
        let f': 'State -> Middleware<'NextState> =
            fun x ->
                let r = f x
                fun _ -> r

        bind f' m

    let toHandler (success: 'State -> HttpHandler) (m: Middleware<'State>) : HttpHandler =
        fun next ctx ->
            match m ctx with
            | Ok x -> success x next ctx
            | Error problem ->
                let f =
                    setStatusCode problem.Status.Value
                    >=> setContentType "application/problem+json"
                    >=> json problem

                f next ctx

    let private validate (request: 'a) : MiddlewareResult<'a> =
        let validationContext = ValidationContext(request)
        let validationResults = List<ValidationResult>()

        let isValid =
            Validator.TryValidateObject(request, validationContext, validationResults, true)

        if isValid then
            MiddlewareResult.Ok request
        else
            let detail =
                validationResults |> Seq.map (fun x -> x.ErrorMessage) |> String.concat "\n"

            let problem = ProblemDetails(Status = 400, Title = "Bad Request", Detail = detail)
            MiddlewareResult.Error problem

    let queryStringToState<'State> : Middleware<'State> =
        fun ctx ->
            let request = ctx.BindQueryString<'State>()
            validate request

module TimeZoneAdjustments =

    [<CLIMutable>]
    type TimeZoneAdjustmentsQuery =
        { [<Required>]
          latitude: float option
          [<Required>]
          longitude: float option
          offsetResolution: string option }

    let private parseOffsetResolution (s: string option) : Result<OffsetResolution, string> =
        match s with
        | None
        | Some "" -> Ok OffsetResolution.FiveMinutes
        | Some "exact" -> Ok OffsetResolution.Exact
        | Some "five_minutes" -> Ok OffsetResolution.FiveMinutes
        | Some "ten_minutes" -> Ok OffsetResolution.TenMinutes
        | Some "fifteen_minutes" -> Ok OffsetResolution.FifteenMinutes
        | Some "thirty_minutes" -> Ok OffsetResolution.ThirtyMinutes
        | Some "one_hour" -> Ok OffsetResolution.OneHour
        | Some x -> Error $"Invalid offset resolution '{x}'"

    let private queryToOptions (query: TimeZoneAdjustmentsQuery) : MiddlewareResult<TimeZoneAdjustmentOptions> =
        parseOffsetResolution query.offsetResolution
        |> Result.map (fun offsetResolution ->
            { Location =
                { Latitude = query.latitude.Value
                  Longitude = query.longitude.Value }
              OffsetResolution = offsetResolution
              AdjustmentEventOffset = TimeSpan.FromHours(-4)
              ExtraOffset = TimeSpan.Zero })
        |> Result.mapError (fun x -> ProblemDetails(Status = 400, Title = "Bad Request", Detail = x))

    let private adjustmentToDto (adjustment: TimeZoneAdjustment) =
        {| Timestamp = adjustment.Timestamp.ToDateTimeOffset()
           Offset = adjustment.Offset |}

    let handler: HttpHandler =
        Middleware.queryStringToState<TimeZoneAdjustmentsQuery>
        |> Middleware.bindResult<TimeZoneAdjustmentsQuery, TimeZoneAdjustmentOptions> queryToOptions
        |> Middleware.toHandler (fun options ->
            calculateTimeZoneAdjustments options
            |> Seq.map adjustmentToDto
            |> Seq.toArray
            |> json)
