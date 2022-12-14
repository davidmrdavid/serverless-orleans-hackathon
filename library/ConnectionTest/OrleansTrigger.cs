// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest
{
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using System.Net.Http;
    using System.Threading;
    using Orleans.Runtime;
    using Microsoft.Extensions.DependencyInjection;
    using Orleans.Runtime.Configuration;
    using System.Net;
    using System;
    using OrleansConnector;
    using Orleans.Hosting;
    using Orleans;

    public static class OrleansTrigger
    {
        /// <summary>
        /// This single Orleans function binding serves as 
        /// 1. the single endpoint that Orleans uses to connect to its workers everywhere (via load balancer)
        /// 2. the way to start Orleans after the function app has started (it cannot start on its own)
        /// 3. a way to query the state of the distributed deployment, returning some status info
        /// </summary>
        /// <param name="req">The http request. The request Uri is used to reach all workers via load balancer.</param>
        /// <param name="shutDownToken">A cancellation token that will shut down this Orleans worker permanently.</param>
        /// <param name="logger">A logger for displaying log messages.</param>
        /// <returns></returns>
        [FunctionName("Orleans")]
        public static async Task<HttpResponseMessage> Orleans(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orleans")] HttpRequestMessage req,
            CancellationToken shutDownToken,
            ILogger logger)
        {
            return await Static.DispatchAsync(req, ConfigureOrleans, logger, shutDownToken);
        }

        static void ConfigureOrleans(ISiloBuilder builder)
        {
            builder
                .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(Application.HelloGrain).Assembly).WithReferences());
        }
    }
}
