namespace Lst

open System.ComponentModel.DataAnnotations
open System.Collections.Generic
open FsToolkit.ErrorHandling
open Giraffe
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