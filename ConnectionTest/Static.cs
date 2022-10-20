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

namespace ConnectionTest
{
    internal class Static
    {
        // singleton instance of the dispatcher
        static Dispatcher dispatcher;

        public static HttpResponseMessage Dispatch(HttpRequestMessage requestMessage, ILogger logger, CancellationToken hostShutdownToken, IPEndPoint endpoint)
        {
            // start the dispatcher if we haven't already on this worker
            if (dispatcher == null)
            {
                Uri functionAddress = requestMessage.RequestUri;
                string dispatcherId = $"{endpoint} {DateTime.UtcNow:o}";

                var newDispatcher = new Dispatcher(functionAddress, dispatcherId, logger, hostShutdownToken);

                // use an interlocked operation to prevent two dispatchers being started
                if (Interlocked.CompareExchange(ref dispatcher, newDispatcher, null) == null)
                {
                    // start the dispatcher
                    newDispatcher.StartChannels();

                    // start orleans silo
                    Silo.StartOnThreadpool(newDispatcher);
                }
                else
                {
                    // we lost the race, someone else created the dispatcher
                    newDispatcher.Dispose();
                }
            }

            return dispatcher.Dispatch(requestMessage);
        }
    }
}
