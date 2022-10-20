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
    using global::ConnectionTest.Algorithm;
    using System.Linq;
    using System.Threading;
    using System.Runtime.CompilerServices;
    using System.Web.Http;

    public static class ConnectionTest
    {
        // call this function as follows from command line:
        // curl http://localhost:7195/connectiontest/

        [FunctionName("ConnectionTest")]
        public static HttpResponseMessage RunConnectionTest(
            [HttpTrigger(AuthorizationLevel.Anonymous, methods: "get", Route = "connectiontest")] HttpRequestMessage req,
            CancellationToken cancellationToken,
            ILogger log)
        {

            if (dispatchers == null)
            {
                log.LogWarning($"starting {numDispatchers} dispatchers at {req.RequestUri}");

                dispatchers = Enumerable
                    .Range(0, numDispatchers)
                    .Select(i => new Dispatcher(req.RequestUri, $"{i:D2} {DateTime.UtcNow:o}", log, cancellationToken))
                    .ToArray();

                connectionFactories = Enumerable
                    .Range(0, numDispatchers)
                    .Select(i => new ConnectionFactory(dispatchers[i]))
                    .ToArray();

                foreach (var d in dispatchers)
                {
                    d.StartChannels();
                }
            }

            var dispatcher = GetRoundRobinDispatcher();
            // var dispatcher = GetRandomDispatcher();

            return dispatcher.Dispatch(req);
        }

        [FunctionName("ConnectionTestScenario")]
        public static async Task<IActionResult> ConnectionTestScenario(
           [HttpTrigger(AuthorizationLevel.Anonymous, methods: "get", Route = "connectiontest/{scenario}")] HttpRequest req,
           string scenario,
           ILogger log)
        {
            try
            {
                switch (scenario)
                {
                    case "0-to-1":
                        {
                            var task0 = connectionFactories[0].ConnectAsync("01");
                            var task1 = connectionFactories[1].AcceptAsync();

                            await Task.WhenAll(task0, task1);

                            var connection0 = task0.Result;
                            var connection1 = task1.Result;

                            await Task.WhenAll(SendFrom0To1Async(), SendFrom1To0Async());

                            async Task SendFrom0To1Async()
                            {
                                byte[] buffer0 = new byte[] { 0, 1, 2, 3 };
                                byte[] buffer1 = new byte[1024];
                                await connection0.OutStream.WriteAsync(buffer0);
                                await connection0.OutStream.FlushAsync();
                                var bytesread = await connection1.InStream.ReadAsync(buffer1);
                                Debug.Assert(bytesread == 4);
                                for (int i = 0; i < bytesread; i++)
                                {
                                    Debug.Assert(buffer1[i] == buffer0[i]);
                                }
                            }
                            async Task SendFrom1To0Async()
                            {
                                byte[] buffer1 = new byte[] { 0, 1, 2, 3 };
                                byte[] buffer0 = new byte[1024];
                                await connection1.OutStream.WriteAsync(buffer1);
                                await connection1.OutStream.FlushAsync();
                                var bytesread = await connection0.InStream.ReadAsync(buffer0);
                                Debug.Assert(bytesread == 4);
                                for (int i = 0; i < bytesread; i++)
                                {
                                    Debug.Assert(buffer1[i] == buffer0[i]);
                                }
                            }
                        }
                        break;
                }

                return new OkObjectResult("test completed.\n");
            }
            catch(Exception e)
            {
                return new ObjectResult(e.ToString()) { StatusCode = (int) HttpStatusCode.InternalServerError};
            }
        }


        const int numDispatchers = 2;
        static Dispatcher[] dispatchers;
        static ConnectionFactory[] connectionFactories;
        static int pos;

        static Dispatcher GetRandomDispatcher()
        {
            Random rand = new Random();
            return dispatchers[rand.Next(dispatchers.Length)];
        }

        // for more determinism  during testing
        static Dispatcher GetRoundRobinDispatcher()
        {
            lock (dispatchers)
            {
                var dispatcher = dispatchers[pos];
                pos = (pos + 1) % numDispatchers;
                return dispatcher;
            }
        }
        
    }
}