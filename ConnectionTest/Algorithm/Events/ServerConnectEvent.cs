// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Storage.Shared.Protocol;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    internal class ServerConnectEvent : DispatcherEvent
    {
        public Guid ConnectionId { get; set; }

        public InChannel InChannel { get; set; }

        public OutChannel OutChannel { get; set; }

        public override async ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            if (this.OutChannel == null)
            {
                if (!dispatcher.OutChannels.TryGetValue(this.InChannel.DispatcherId, out var queue))
                {
                    dispatcher.OutChannelWaiters.Add(this);
                    return;
                }
                else
                {
                    this.OutChannel = queue.Dequeue();
                    if (queue.Count == 0)
                    {
                        dispatcher.OutChannels.Remove(this.InChannel.DispatcherId);
                    }
                    this.OutChannel.ConnectionId = this.ConnectionId;
                }
            }      

            dispatcher.AcceptWaiters.Enqueue(this);

            // if there is an accept waiting process it now
            if (dispatcher.AcceptQueue.TryDequeue(out ServerAcceptEvent acceptEvent))
            {
                await acceptEvent.ProcessAsync(dispatcher);
            }
        }
    }
}
