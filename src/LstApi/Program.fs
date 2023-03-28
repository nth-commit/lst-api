open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open LstApi

let routes: HttpHandler =
    choose [ route "/ping" >=> text "pong"; route "/rules" >=> Http.Rules.handler ]

let builder = WebApplication.CreateBuilder()
builder.Services.AddGiraffe() |> ignore

let app = builder.Build()
app.UseGiraffe routes
app.Run()

type Program() =
    class
    end
