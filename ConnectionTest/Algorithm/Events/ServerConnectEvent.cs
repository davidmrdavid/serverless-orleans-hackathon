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
        public Guid ConnectionId;
        public InChannel InChannel;
        public OutChannel OutChannel;
        public bool DoServerBroadcast;
        public bool DoClientBroadcast;
        public DateTime Issued;

        public override bool CancelWithConnection(Guid connectionId) => connectionId == this.ConnectionId;

        public override async ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            if (this.OutChannel == null)
            {
                if (!dispatcher.ChannelPools.TryGetValue(this.InChannel.DispatcherId, out var queue))
                {
                    dispatcher.OutChannelWaiters.Add(this);
                    dispatcher.Logger.LogDebug("{dispatcher} {connectionId:N} connect from {destination} queued for channel", dispatcher, this.ConnectionId, this.InChannel.DispatcherId);
                    return;
                }
                else
                {
                    this.OutChannel = queue.Dequeue();
                    if (queue.Count == 0)
                    {
                        dispatcher.ChannelPools.Remove(this.InChannel.DispatcherId);
                        DoClientBroadcast = true;
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
            else
            {
                dispatcher.Logger.LogDebug("{dispatcher} {connectionId:N} connect from {destination} queued for accept", dispatcher, this.ConnectionId, this.InChannel.DispatcherId);
            }

            if (this.DoServerBroadcast)
            {
                TimerEvent.MakeContactAsync(dispatcher);
            }
        }

        public override bool TimedOut => DateTime.UtcNow - this.Issued > TimeSpan.FromSeconds(30);

        public override void HandleTimeout(Dispatcher dispatcher)
        {
            TimeSpan elapsed = DateTime.UtcNow - this.Issued;
            dispatcher.Logger.LogWarning("{dispatcher} {connectionId:N} server connect timed out after {elapsed}", dispatcher, this.ConnectionId, elapsed);
            dispatcher.Worker.Submit(new ConnectionFailedEvent()
            {
                ConnectionId = this.ConnectionId, 
            });
        }
    }
}
