// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.Extensions.Logging;
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
            if (!dispatcher.OutChannels.TryGetValue(this.OutChannel.DispatcherId, out var queue))
            {
                dispatcher.OutChannels.Add(this.OutChannel.DispatcherId, queue = new Queue<OutChannel>());
            }

            queue.Enqueue(this.OutChannel);

            if (queue.Count == 1)
            {
                dispatcher.Logger.LogInformation($"{dispatcher} added new channel to {this.OutChannel.DispatcherId}");

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
            }
            else if (queue.Count > maxPool)
            {
                var obsoleteChannel = queue.Dequeue();
                obsoleteChannel.Dispose();
            }
        }
    }
}
