// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector.Algorithm
{
    using Microsoft.Extensions.Logging;
    using OrleansConnector;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    internal class NewChannelEvent : DispatcherEvent
    {
        public OutChannel OutChannel;

        const int maxPool = 2;

        public override async ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            if (!dispatcher.ChannelPools.TryGetValue(this.OutChannel.DispatcherId, out var queue))
            {
                dispatcher.ChannelPools.Add(this.OutChannel.DispatcherId, queue = new Queue<OutChannel>());
            }

            queue.Enqueue(this.OutChannel);

            dispatcher.Logger.LogTrace("{dispatcher} {channelId} added out-channel to {destination}", dispatcher, this.OutChannel.ChannelId, this.OutChannel.DispatcherId);

            if (queue.Count == 1)
            {
                if (dispatcher.OutChannelWaiters.Count > 0)
                {
                    // we may have unblocked a channel waiter. Rerun them all.
                    var waiters = dispatcher.OutChannelWaiters;
                    dispatcher.OutChannelWaiters = new List<DispatcherEvent>();
                    foreach (var waiter in waiters)
                    {
                        await waiter.ProcessAsync(dispatcher);
                    }
                }

                // we just discovered a new node. Means we should be broadcasting.
                dispatcher.DoBroadcast();
            }
            
            if (queue.Count >= maxPool)
            {
                if (queue.Count > maxPool)
                {
                    var excessChannel = queue.Dequeue();
                    dispatcher.Logger.LogTrace("{dispatcher} {channelId} removed excess out-channel to {destination}", dispatcher, this.OutChannel.ChannelId, excessChannel.DispatcherId);
                    excessChannel.Dispose();
                }

                // since the pool is full, filter incoming requests
                var nextRefresh = queue.Peek().Since + TimeSpan.FromMinutes(8);
                dispatcher.Filter[this.OutChannel.DispatcherId] = nextRefresh;
            }
        }
    }
}
