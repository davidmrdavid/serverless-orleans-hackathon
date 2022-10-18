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

    public static class OrleansTrigger
    {
        [FunctionName("Orleans")]
        public static Task<HttpResponseMessage> Orleans(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "get", Route = "orleans")] HttpRequestMessage req,
            CancellationToken hostShutdownToken,
            ILogger log)
        {
            return Algorithm.Dispatcher.DispatchAsync(req, hostShutdownToken, log);
        }
    }
}
