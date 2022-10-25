// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using System.Net;
    using System.Net.Http;
    using System.Diagnostics;
    using OrleansConnector;

    public static class Hello
    {
        [FunctionName("Hello")]
        public static async Task<IActionResult> RunHello(
            [HttpTrigger(AuthorizationLevel.Anonymous, methods: "get", Route = "hello")] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogWarning($"getting silo");

                var silo = await Static.GetSiloAsync();

                var friend = silo.GrainFactory.GetGrain<Application.IHelloGrain>("friend");

                log.LogWarning($"calling grain");

                var result = await friend.SayHello("Good morning!");

                log.LogWarning($"grain replied: {result}");

                return new OkObjectResult(result);
            }
            catch(Exception e)
            {
                return new ObjectResult(e.ToString()) { StatusCode = (int)HttpStatusCode.InternalServerError };
            }
        }
    }
}
