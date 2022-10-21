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
            if (started == 0)
            {
                // use an interlocked operation to prevent two dispatchers being started
                if (Interlocked.CompareExchange(ref started, 1, 0) == 0)
                {
                    var address = IPAddress.Parse($"0.0.0.0"); // todo we need this host's address
                    var _ = Task.Run(() => StartSiloAndDispatcher(requestMessage, address, 1, logger, hostShutdownToken));
                }
            }

            var dispatcher = await DispatcherPromise.Task.ConfigureAwait(false);
            return dispatcher.Dispatch(requestMessage);
        }

        public static async Task StartSiloAndDispatcher(HttpRequestMessage requestMessage, IPAddress address, int port, ILogger logger, CancellationToken hostShutdownToken)
        {
            try
            {
                Uri functionAddress = requestMessage.RequestUri;
                string siloEndpoint = $"{address}:{port}";
                string dispatcherId = $"{siloEndpoint} {DateTime.UtcNow:o}";

                var newDispatcher = new Dispatcher(functionAddress, dispatcherId, logger, hostShutdownToken);
                newDispatcher.StartChannels();
                DispatcherPromise.SetResult(newDispatcher);

                var connectionFactory = new ConnectionFactory(newDispatcher);
                var silo = new Silo();
                await silo.StartAsync(address, port, connectionFactory, hostShutdownToken);
                SiloPromise.SetResult(silo);
            }
            catch (Exception e)
            {
                DispatcherPromise.TrySetException(e);
                SiloPromise.TrySetException(e);
            }
        }
    }
}
