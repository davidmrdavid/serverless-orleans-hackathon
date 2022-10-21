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
    using System.Transactions;
    using Orleans.Runtime.Configuration;

    public static class SiloTest
    {
        // call this function as follows from command line:
        // curl http://localhost:7071/startsilos/2
        // curl http://localhost:7071/testsilos/hello


        [FunctionName("startsilos")]
        public static async Task<HttpResponseMessage> StartSilos(
            [HttpTrigger(AuthorizationLevel.Anonymous, methods: "get", Route = "startsilos/{numsilos}")] HttpRequestMessage req,
            int numSilos,
            CancellationToken cancellationToken,
            ILogger log)
        {

            IPAddress ip = await ConfigUtilities.ResolveIPAddress(null, null, System.Net.Sockets.AddressFamily.InterNetwork);
            Console.WriteLine(ip.ToString());
            log.LogWarning($"You have reached IP = {ip}");

            if (started == 0)
            {
                if (Interlocked.CompareExchange(ref started, 1, 0) == 0)
                {
                    log.LogWarning($"starting {numSilos} silos at {req.RequestUri}");

                    groups = Enumerable
                        .Range(0, numSilos)
                        .Select(i => new Group())
                        .ToArray();

                    startupPromise.SetResult(true);

                    string clusterId = $"my-cluster-{Guid.NewGuid()}";

                    var tasks = groups
                       .Select((g, i) => g.StartAsync(req, clusterId, i, log, cancellationToken))
                       .ToList();
                }
            }

            var dispatcher = await (await GetRoundRobinGroupAsync()).GetDispatcherAsync();
            //var dispatcher = (await GetRandomGroupAsync()).GetDispatcherAsync();

            return dispatcher.Dispatch(req);
        }

        [FunctionName("testsilos")]
        public static async Task<IActionResult> ConnectionTestScenario(
            [HttpTrigger(AuthorizationLevel.Anonymous, methods: "get", Route = "testsilos/{scenario}")] HttpRequest req,
            string scenario,
            ILogger log)
        {
            try
            {
                log.LogWarning($"starting scenario {scenario}");
                switch (scenario)
                {
                    case "hello":
                        var silo = await (await GetRoundRobinGroupAsync()).GetSiloAsync();
                        var grainFactory = silo.GrainFactory;
                        var friend = grainFactory.GetGrain<Application.IHelloGrain>("friend");
                        log.LogWarning($"calling grain via silo {silo.Endpoint}");
                        var result = await friend.SayHello("Good morning!");
                        log.LogWarning($"grain replied: {result}");
                        break;
                }
                log.LogWarning($"finished scenario {scenario}");
                return new OkObjectResult("test completed.\n");
            }
            catch (Exception e)
            {
                log.LogWarning($"failed scenario {scenario}");
                return new ObjectResult(e.ToString()) { StatusCode = (int)HttpStatusCode.InternalServerError };
            }
        }

        static int started = 0;
        static TaskCompletionSource<bool> startupPromise = new TaskCompletionSource<bool>();
        static Group[] groups;
        static int pos;

        static async Task<Group> GetRandomGroupAsync()
        {
            await startupPromise.Task;
            Random rand = new Random();
            return groups[rand.Next(groups.Length)];
        }

        // for more determinism  during testing
        static async Task<Group> GetRoundRobinGroupAsync()
        {
            await startupPromise.Task;
            lock (groups)
            {
                var result = groups[pos];
                pos = (pos + 1) % groups.Length;
                return result;
            }
        }

        public class Group
        {
            readonly TaskCompletionSource<Dispatcher> dispatcherPromise = new TaskCompletionSource<Dispatcher>();
            readonly TaskCompletionSource<Silo> siloPromise = new TaskCompletionSource<Silo>();

            public Task<Dispatcher> GetDispatcherAsync() => dispatcherPromise.Task;
            public Task<Silo> GetSiloAsync() => siloPromise.Task;

            public async Task StartAsync(HttpRequestMessage requestMessage, string clusterId, int index, ILogger logger, CancellationToken hostShutdownToken)
            {

                Uri functionAddress = requestMessage.RequestUri;
                var address = IPAddress.Parse($"{index + 1}.{index + 1}.{index + 1}.{index + 1}");
                int port = (new Random()).Next(99999) + 1;
                string siloEndpoint = $"{address}:{port}";
                string dispatcherIdPrefix = siloEndpoint;
                string dispatcherIdSuffix = DateTime.UtcNow.ToString("O");

                var newDispatcher = new Dispatcher(functionAddress, dispatcherIdPrefix, dispatcherIdSuffix, logger, hostShutdownToken);
                newDispatcher.StartChannels();
                dispatcherPromise.SetResult(newDispatcher);

                var connectionFactory = new ConnectionFactory(newDispatcher);
                var silo = new Silo();
                logger.LogWarning($"starting silo {index} on {newDispatcher}");
                await silo.StartAsync(clusterId, address, port, connectionFactory, hostShutdownToken, logger);
                logger.LogWarning($"Silo {index} started successfully {newDispatcher.DispatcherId}");
                siloPromise.SetResult(silo);
            }
        }
    }
}