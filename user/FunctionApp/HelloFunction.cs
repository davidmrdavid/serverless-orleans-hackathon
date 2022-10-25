// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace FunctionApp
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

    public static class HelloFunction
    {
        [FunctionName("Hello")]
        public static async Task<IActionResult> RunHello(
            [HttpTrigger(AuthorizationLevel.Anonymous, methods: "get", Route = "hello")] HttpRequest req,
            ILogger log)
        {
            try
            {
                string thisMachine = Environment.MachineName;

                var silo = await Static.GetSiloAsync();

                // get a reference (proxy) to the friend grain
                var friend = silo.GrainFactory.GetGrain<Application.IHelloGrain>("friend");

                // call the friend grain through this reference
                var result = await friend.SayHello("Good morning!");

                var message = $"worker '{thisMachine}' called the grain and got: {result}\n";

                return new OkObjectResult(message);
            }
            catch (Exception e)
            {
                return new ObjectResult(e.ToString()) { StatusCode = (int)HttpStatusCode.InternalServerError };
            }
        }
    }
}
