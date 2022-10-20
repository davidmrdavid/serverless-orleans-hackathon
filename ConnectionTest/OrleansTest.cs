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

    public static class OrleansTest
    {
        // call this function as follows from command line:
        // curl http://localhost:7195/orleanstest/

        [FunctionName("OrleansTest")]
        public static async Task<HttpResponseMessage> RunOrleansTest(
            [HttpTrigger(AuthorizationLevel.Anonymous, methods: "get", Route = "orleanstest")] HttpRequestMessage req,
            CancellationToken cancellationToken,
            ILogger log)
        {

            if (groups == null)
            {
                log.LogWarning($"starting {numSilos} silos at {req.RequestUri}");

                groups = Enumerable
                    .Range(0, numSilos)
                    .Select(i => new Group())
                    .ToArray();

                var tasks = groups
                   .Select((g,i) => g.StartAsync(req, i, log, cancellationToken))
                   .ToList();

                await Task.WhenAll(tasks);
            }

            var dispatcher = GetRoundRobinDispatcher();
            // var dispatcher = GetRandomDispatcher();

            return dispatcher.Dispatch(req);
        }

        const int numSilos = 2;
        static Group[] groups;
        static int pos;

        static Dispatcher GetRandomDispatcher()
        {
            Random rand = new Random();
            return groups[rand.Next(groups.Length)].Dispatcher;
        }

        // for more determinism  during testing
        static Dispatcher GetRoundRobinDispatcher()
        {
            lock (groups)
            {
                var dispatcher = groups[pos].Dispatcher;
                pos = (pos + 1) % numSilos;
                return dispatcher;
            }
        }

        public class Group
        {
            public Silo Silo;
            public Dispatcher Dispatcher;

            public async Task StartAsync(HttpRequestMessage requestMessage, int index, ILogger logger, CancellationToken hostShutdownToken)
            {
                var connectionFactoryPromise = new TaskCompletionSource<ConnectionFactory>();

                this.Silo = new Silo();
                await this.Silo.StartAsync(connectionFactoryPromise.Task, 11111 + index, hostShutdownToken);

                Uri functionAddress = requestMessage.RequestUri;
                string dispatcherId = $"{this.Silo.Endpoint} {DateTime.UtcNow:o}";

                this.Dispatcher = new Dispatcher(functionAddress, dispatcherId, logger, hostShutdownToken);

                this.Dispatcher.StartChannels();

                connectionFactoryPromise.SetResult(new ConnectionFactory(this.Dispatcher));
            }
        }
    }
}