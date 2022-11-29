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

            // print status information
            dispatcher.Logger.LogInformation("{dispatcher} status {information} count={count}", dispatcher, dispatcher.PrintInformation(), count);

            if (!dispatcher.ShutdownImminent)
            {
                // broadcast contact requests
                if (dispatcher.BroadcastFlag.Task.IsCompleted)
                {
                    dispatcher.BroadcastFlag = new TaskCompletionSource<bool>();
                }
                else
                {
                    count++;
                }
                BroadcastContactRequests(dispatcher);


                // schedule next iteration
                var _ = Next();
                async Task Next()
                {
                    Task nextScheduledBroadcast = this.nextBroadcast();
                    await Task.Delay(TimeSpan.FromSeconds(2));  // enforce a minimum delay between broadcasts to avoid storm
                    await Task.WhenAny(nextScheduledBroadcast, dispatcher.BroadcastFlag.Task);
                    dispatcher.Worker.Submit(this);
                };
            }

            return default;
        }

        Task nextBroadcast()
        {
            if (count < 5)
            {
                // For the first ~25 seconds, we issue 5 broadcasts at highly random times
                return Task.Delay(TimeSpan.FromSeconds(10 * random.NextDouble()));
            }
            else if (count % 10 == 5)
            {
                // once every 10 times we re-randomize the timing
                return Task.Delay(TimeSpan.FromSeconds(random.Next(30)));
            }
            else
            {
                // the standard delay between background broadcasts
                return Task.Delay(TimeSpan.FromSeconds(60));
            }
        }

        void BroadcastContactRequests(Dispatcher dispatcher)
        {
            int knownRemotes = dispatcher.ChannelPools.Count;
            int couponCollectorEx = (int)Math.Round(4 * (knownRemotes + 4) * Math.Log(knownRemotes + 4));
            int numRequests = Math.Max(couponCollectorEx, 10);
   
            for (int i = 0; i < numRequests; i++)
            {
                MakeContactAsync(dispatcher);            
            }

            dispatcher.Logger.LogDebug("{dispatcher} sent {numRequests} contact requests", dispatcher, numRequests);
        }

        bool MakeContactAsync(Dispatcher dispatcher)
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
                var responseStream = dispatcher.HttpClient.GetStreamAsync(uri, dispatcher.ShutdownToken);
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
