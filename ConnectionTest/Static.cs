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

namespace ConnectionTest
{
    /// <summary>
    /// This class is used for starting a single dispatcher and silo on each host
    /// </summary>
    internal class Static
    {
        // singleton instance of the dispatcher
        internal static TaskCompletionSource<Dispatcher> DispatcherPromise = null;

        public static async ValueTask<HttpResponseMessage> DispatchAsync(HttpRequestMessage requestMessage, ILogger logger, CancellationToken hostShutdownToken)
        {
            // start the dispatcher if we haven't already on this worker
            if (DispatcherPromise == null)
            {
                var dispatcherPromise = new TaskCompletionSource<Dispatcher>();

                // use an interlocked operation to prevent two dispatchers being started
                if (Interlocked.CompareExchange(ref DispatcherPromise, dispatcherPromise, null) == null)
                {
                    var _ = Task.Run(() => StartSiloAndDispatcher(requestMessage, logger, hostShutdownToken));
                }
            }

            var dispatcher = await DispatcherPromise.Task;
            return dispatcher.Dispatch(requestMessage);
        }

        public static async Task StartSiloAndDispatcher(HttpRequestMessage requestMessage, ILogger logger, CancellationToken hostShutdownToken)
        {
            try
            {
                var connectionFactoryPromise = new TaskCompletionSource<ConnectionFactory>();

                var silo = new Silo();
                await silo.StartAsync(connectionFactoryPromise.Task, 11111, hostShutdownToken);

                Uri functionAddress = requestMessage.RequestUri;
                string dispatcherId = $"{silo.Endpoint} {DateTime.UtcNow:o}";

                var newDispatcher = new Dispatcher(functionAddress, dispatcherId, logger, hostShutdownToken);

                newDispatcher.StartChannels();

                connectionFactoryPromise.SetResult(new ConnectionFactory(newDispatcher));

                DispatcherPromise.SetResult(newDispatcher);
            }
            catch (Exception e)
            {
                DispatcherPromise.SetException(e);
            }
        }
    }
}
