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
    using global::OrleansConnector.Algorithm;
    using System.Reflection;
    using System.Threading;

    public static class HttpTriggers
    {
        internal readonly static HttpClient Client = new HttpClient();

        // call this function as follows from command line:
        // curl http://localhost:7195/test/10
        // curl https://functionssb1.azurewebsites.net/test/5000

        [FunctionName("Test")]
        public static HttpResponseMessage Test(
            [HttpTrigger(AuthorizationLevel.Anonymous, methods: "get", Route = "test/{iterations}")] HttpRequestMessage req,
            int iterations,
            ILogger log)
        {
            log.LogWarning($"starting callee");
            HttpResponseMessage httpResponseMessage = new HttpResponseMessage();
            httpResponseMessage.StatusCode = HttpStatusCode.OK;
            httpResponseMessage.RequestMessage = req;
            httpResponseMessage.Content = new PushStreamContent(async (Stream s, HttpContent content, TransportContext context) =>
            {
                var writer = new StreamWriter(s);
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                for (int i = 0; i < iterations; i++)
                {
                    writer.WriteLine($"Hello {i} {stopwatch.Elapsed}");
                    log.LogWarning($"writing to stream, iteration {i}, elapsed={stopwatch.Elapsed}");
                    await writer.FlushAsync();
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                log.LogWarning($"closing stream");
                await writer.DisposeAsync();
            });
            log.LogWarning($"finished callee");
            return httpResponseMessage;
        }

        [FunctionName("CallTest")]
        public static async Task<HttpResponseMessage> CallTest(
           [HttpTrigger(AuthorizationLevel.Anonymous, methods: "post", Route = "calltest")] HttpRequestMessage req,
           ILogger log)
        {
            log.LogWarning($"starting caller");
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            int linesReceived = 0;

            string url = await req.Content.ReadAsStringAsync();

            var stream = await Client.GetStreamAsync(url);

            using (var reader = new StreamReader(stream))
            {
                while (true)
                {
                    var nextLine = await reader.ReadLineAsync();
                    if (nextLine == null)
                    {
                        break;
                    }
                    log.LogWarning($"received: {nextLine}");
                    linesReceived++;
                }
                log.LogWarning($"received end of stream");
            }

            log.LogWarning($"finished caller");

            HttpResponseMessage httpResponseMessage = new HttpResponseMessage();
            httpResponseMessage.StatusCode = HttpStatusCode.OK;
            httpResponseMessage.RequestMessage = req;
            httpResponseMessage.Content = new StringContent($"caller completed successfully after receiving {linesReceived} lines in {stopwatch.Elapsed}.\n");
            return httpResponseMessage;
        }

        [FunctionName("TestShutdown")]
        public static async Task TestShutdown(
           [HttpTrigger(AuthorizationLevel.Anonymous, methods: "get", Route = "testshutdown")] HttpRequestMessage req,
           CancellationToken token,
           ILogger log)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(300), token);
            }
            catch (Exception e)
            {
                log.LogError("canceled {e}", e);
            }
        }
    }
}
