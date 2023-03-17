module LstApi.Tests.ApiTests

open System.Net
open Microsoft.AspNetCore.Mvc.Testing
open Program
open Xunit

let factory = new WebApplicationFactory<Program>()

[<Fact>]
let ``/ping should return "pong"`` () =
    task {
        use client = factory.CreateClient()

        let! response = client.GetAsync "/ping"
        let! responseContent = response.Content.ReadAsStringAsync()

        Assert.Equal("pong", responseContent)
    }

[<Fact>]
let ``/time-zone-adjustments should 400 with no query params`` () =
    task {
        use client = factory.CreateClient()

        let! response = client.GetAsync "/time-zone-adjustments"

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
    }

[<Fact>]
let ``/time-zone-adjustments should 200 with sufficient query params`` () =
    task {
        use client = factory.CreateClient()

        let! response = client.GetAsync "/time-zone-adjustments?latitude=-43&longitude=172"

        Assert.Equal(HttpStatusCode.OK, response.StatusCode)
    }
