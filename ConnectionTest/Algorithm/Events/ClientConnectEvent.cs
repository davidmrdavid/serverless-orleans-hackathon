// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    internal class ClientConnectEvent : DispatcherEvent
    {
        public Guid ConnectionId;

        public string ToMachine;

        public TaskCompletionSource<Connection> Response;

        public OutChannel OutChannel;

        public override async ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            var key = dispatcher.OutChannels.Keys.LastOrDefault((string s) => s.StartsWith(this.ToMachine));

            if (key == null)
            {
                dispatcher.Logger.LogWarning($"{dispatcher} connect to {this.ToMachine} queued");
                dispatcher.OutChannelWaiters.Add(this);
            }
            else
            {
                var queue = dispatcher.OutChannels[key];
                this.OutChannel = queue.Dequeue();
                if (queue.Count == 0)
                {
                    dispatcher.OutChannels.Remove(key);
                }

                this.OutChannel.ConnectionId = this.ConnectionId;
       
                dispatcher.ConnectRequests.Add(this.ConnectionId, this);

                await Format.SendAsync(this.OutChannel.Stream, Format.Op.TryConnect, this.ConnectionId);

                dispatcher.Logger.LogWarning($"{dispatcher} connect to {this.ToMachine} sent");
            }
        }
    }
}
