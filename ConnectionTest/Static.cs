using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConnectionTest.Algorithm;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Runtime;
using System.Transactions;
using Orleans.Runtime.Configuration;
using Microsoft.AspNetCore.Http.Extensions;

namespace ConnectionTest
{
    /// <summary>
    /// This class is used for starting a single dispatcher and silo on each host
    /// </summary>
    internal class Static
    {
        static int started = 0;
 
        static TaskCompletionSource<Dispatcher> DispatcherPromise = new TaskCompletionSource<Dispatcher>();
        static TaskCompletionSource<Silo> SiloPromise = new TaskCompletionSource<Silo>();

        public static Task<Silo> GetSiloAsync() => SiloPromise.Task;

        public static async Task<HttpResponseMessage> DispatchAsync(HttpRequestMessage requestMessage, ILogger logger, CancellationToken hostShutdownToken)
        {
            // start the dispatcher if we haven't already on this worker
            if (Interlocked.CompareExchange(ref started, 1, 0) == 0)
            {
                var _ = Task.Run(() => StartSiloAndDispatcher(requestMessage, logger, hostShutdownToken));
            }

            var dispatcher = await DispatcherPromise.Task.ConfigureAwait(false);
            return dispatcher.Dispatch(requestMessage);
        }

        public static async Task StartSiloAndDispatcher(HttpRequestMessage requestMessage, ILogger logger, CancellationToken hostShutdownToken)
        {
            try
            {
                var query = requestMessage.RequestUri.ParseQueryString();
                string dispatcherOnlyValue = query["dispatcherOnly"];
                bool.TryParse(dispatcherOnlyValue, out bool dispatcherOnly);
                string clusterIdValue = query["clusterId"];

                // to construct the generic entry point, we have to remove the channel ID from the query
                UriBuilder builder = new UriBuilder(requestMessage.RequestUri);
                QueryBuilder queryBuilder = new QueryBuilder();
                foreach (String s in query.AllKeys)
                    if (s != "channelId")
                        queryBuilder.Add(s, query[s]);
                builder.Query = queryBuilder.ToString();
                var functionAddress = builder.Uri;

                IPAddress address = await ConfigUtilities.ResolveIPAddress(null, null, System.Net.Sockets.AddressFamily.InterNetwork);
                int port = new Random().Next(9999) + 1;

                string siloEndpoint = $"{address}:{port}";
                string dispatcherIdPrefix = $"{siloEndpoint}";
                string dispatcherIdSuffix = $"{DateTime.UtcNow:o}";

                var newDispatcher = new Dispatcher(functionAddress, dispatcherIdPrefix, dispatcherIdSuffix, logger, hostShutdownToken);
                newDispatcher.StartChannels();
                DispatcherPromise.SetResult(newDispatcher);

                if (!dispatcherOnly)
                {
                    var connectionFactory = new ConnectionFactory(newDispatcher);
                    var silo = new Silo();
                    await silo.StartAsync(clusterIdValue ?? "my-first-cluster", address, port, connectionFactory, hostShutdownToken, logger);
                    SiloPromise.SetResult(silo);
                } 
            }
            catch (Exception e)
            {
                DispatcherPromise.TrySetException(e);
                SiloPromise.TrySetException(e);
            }
        }
    }
}
