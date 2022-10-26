// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector.Algorithm
{
    using Azure.Core;
    using Microsoft.Extensions.Logging;
    using OrleansConnector;
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    internal class TimerEvent : DispatcherEvent
    {
        private readonly Random random = new Random();
        private int count = 0;

        public override ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            dispatcher.Logger.LogInformation("{dispatcher} status {information} count={count}", dispatcher, dispatcher.PrintInformation(), count);

            
            MakeContactAsync(dispatcher);

            // remove timed out channel waiters
            dispatcher.OutChannelWaiters = Util.FilterList(
                 dispatcher.OutChannelWaiters,
                 element => !element.TimedOut,
                 element => element.HandleTimeout(dispatcher));

            // remove timed out accept waiters
            dispatcher.AcceptWaiters = Util.FilterQueue(
                 dispatcher.AcceptWaiters,
                 element => !element.TimedOut,
                 element => element.HandleTimeout(dispatcher));

            this.Reschedule(dispatcher, nextBroadcast());

            return default;
        }

        TimeSpan nextBroadcast()
        {
            count++;

            if (count < 5)
            {
                return TimeSpan.FromSeconds(10 * random.NextDouble());
            }
            else if (count % 10 == 5)
            {
                return TimeSpan.FromSeconds(random.Next(30));
            }
            else
            {
                return TimeSpan.FromSeconds(60);
            }
        }

        internal static void MakeContactAsync(Dispatcher dispatcher)
        {
            int knownRemotes = dispatcher.ChannelPools.Count;
            int couponCollectorEx = (int)Math.Round(4 * knownRemotes * Math.Log(knownRemotes + 1));
            int numRequests = Math.Max(couponCollectorEx, 10);

            int numSuccessful = 0;
            for (int i = 0; i < numRequests; i++)
            {
                if (MakeContactAsync(dispatcher, dispatcher.FunctionAddress))
                {
                    numSuccessful++;
                }
            }

            dispatcher.Logger.LogDebug("{dispatcher} sent {numRequests} contact requests", dispatcher, numRequests);
        }

        static bool MakeContactAsync(Dispatcher dispatcher, Uri target)
        {
            Guid channelId = Guid.NewGuid(); // the unique id for this channel

            try
            {
                dispatcher.Logger.LogTrace("{dispatcher} {channelId} sending contact request", dispatcher, channelId);
                // create channel id and add it to URI
                var ub = new UriBuilder(dispatcher.FunctionAddress);
                var uriBuilder = new UriBuilder(dispatcher.FunctionAddress);
                var paramValues = HttpUtility.ParseQueryString(uriBuilder.Query);
                paramValues.Add("channelId", channelId.ToString());
                uriBuilder.Query = paramValues.ToString();
                var uri = uriBuilder.Uri;

                // Wait for the response stream and process it asynchronously
                var responseStream = dispatcher.HttpClient.GetStreamAsync(uri, dispatcher.HostShutdownToken);
                var _ = Task.Run(() => InChannel.ReceiveAsync(channelId, dispatcher, responseStream));
                return true;
            }
            catch (Exception exception)
            {
                dispatcher.Logger.LogWarning("{dispatcher} {channelId} failed to send contact request: {exception}", dispatcher, channelId, exception);
                return false;
            }
        }
    }
}
