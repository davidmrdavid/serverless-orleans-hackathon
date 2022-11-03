﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector
{
    using Algorithm;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Net;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Orleans.Runtime;
    using System.Transactions;
    using Orleans.Runtime.Configuration;
    using Microsoft.AspNetCore.Http.Extensions;
    using Orleans.Hosting;

    /// <summary>
    /// This class is used for starting a single dispatcher and silo on each host
    /// </summary>
    public class Static
    {
        static int started = 0;
        static int stopped = 0;

        static TaskCompletionSource<Dispatcher> DispatcherPromise = new TaskCompletionSource<Dispatcher>();
        static TaskCompletionSource<Silo> SiloPromise = new TaskCompletionSource<Silo>();

        static volatile TaskCompletionSource<bool> stalled = new TaskCompletionSource<bool>();

        public static Task<Silo> GetSiloAsync() => SiloPromise.Task;

        public static async Task<HttpResponseMessage> DispatchAsync(HttpRequestMessage requestMessage, Action<ISiloBuilder> configureOrleans, ILogger logger, CancellationToken hostShutdownToken)
        {
            // start the dispatcher if we haven't already on this worker
            if (Interlocked.CompareExchange(ref started, 1, 0) == 0)
            {
                var _ = Task.Run(() => StartSiloAndDispatcher(requestMessage, configureOrleans, logger));
            }

            var dispatcher = await DispatcherPromise.Task.ConfigureAwait(false);
            var response = dispatcher.Dispatch(requestMessage);

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                // this is a response form us to ourselves, so it is not useful for building connections.
                // but it is useful for another purpose: we can stall it right here, to detect host shutdown.
                var tcs = new TaskCompletionSource<bool>();

                using var registration = hostShutdownToken.Register(() =>
                {
                    logger.LogInformation("{dispatcher} Host shutdown detected", dispatcher);
                    Task.Run(StopSiloAndDispatcherAsync);
                    tcs.TrySetResult(true);
                });

                // we want to stall only the latest one of these self-responses.
                var previous = Interlocked.Exchange(ref stalled, tcs);
                previous.TrySetResult(false);
                await tcs.Task;
            }

            return response;
        }



        public static async Task StartSiloAndDispatcher(HttpRequestMessage requestMessage, Action<ISiloBuilder> configureOrleans, ILogger logger)
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


                var newDispatcher = new Dispatcher(functionAddress, dispatcherIdPrefix, dispatcherIdSuffix, logger);
                logger.LogDebug("{dispatcher} starting dispatcher", newDispatcher);
                await newDispatcher.StartAsync();
                DispatcherPromise.SetResult(newDispatcher);

                if (dispatcherOnly)
                {
                    SiloPromise.SetResult(null);
                }
                else
                {
                    logger.LogDebug("{dispatcher} starting silo", newDispatcher);

                    var connectionFactory = new ConnectionFactory(newDispatcher);
                    var silo = new Silo();
                    await silo.StartAsync(
                        clusterIdValue ?? "my-first-cluster",
                        address,
                        port,
                        configureOrleans,
                        connectionFactory,
                        logger);
                    SiloPromise.SetResult(silo);
                }
                logger.LogInformation("{dispatcher} started", newDispatcher);
            }
            catch (Exception e)
            {
                DispatcherPromise.TrySetException(e);
                SiloPromise.TrySetException(e);
            }
        }

        public static async Task StopSiloAndDispatcherAsync()
        {
            if (Interlocked.CompareExchange(ref stopped, 1, 0) == 0)
            {
                var dispatcher = await DispatcherPromise.Task;
                var silo = await SiloPromise.Task;

                if (silo != null)
                {
                    try
                    {
                        dispatcher.Logger.LogDebug("{dispatcher} stopping silo", dispatcher);
                        await silo.Host.StopAsync();
                    }
                    catch (Exception exception)
                    {
                        dispatcher.Logger.LogError("{dispatcher} failed to stop silo cleanly: {exception}", dispatcher, exception);
                    }
                }

                try
                {
                    dispatcher.Logger.LogDebug("{dispatcher} stopping dispatcher", dispatcher);
                    await dispatcher.StopAsync();
                }
                catch (Exception exception)
                {
                    dispatcher.Logger.LogError("{dispatcher} failed to stop dispatcher cleanly: {exception}", dispatcher, exception);
                }

                dispatcher.Logger.LogInformation("{dispatcher} stopped", dispatcher);
            }
        }
    }
}