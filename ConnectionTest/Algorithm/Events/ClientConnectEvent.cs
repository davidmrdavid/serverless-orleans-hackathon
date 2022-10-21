// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
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
        public DateTime Issued;
        public TaskCompletionSource<Connection> Response;
        public OutChannel OutChannel;

        public override bool CancelWithConnection(Guid connectionId) => connectionId == this.ConnectionId;

        public override async ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            var key = dispatcher.OutChannels.Keys.LastOrDefault((string s) => s.StartsWith(this.ToMachine));

            if (key == null)
            {
                dispatcher.Logger.LogWarning("{dispatcher} connect to {destination} queued", dispatcher, this.ToMachine);
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

                try
                {
                    await Format.SendAsync(
                        this.OutChannel.Stream,
                        queue.Count() == 0 ? Format.Op.ConnectAndSolicit : Format.Op.Connect,
                        this.ConnectionId);

                    dispatcher.Logger.LogWarning("{dispatcher} connect to {destination} sent", dispatcher, this.ToMachine);
                }
                catch (Exception exception)
                {
                    dispatcher.Logger.LogWarning("{dispatcher} could not send Connect message: {exception}", dispatcher, exception);

                    // we can retry this
                    dispatcher.Worker.Submit(this);
                }
            }
        }

        public override bool TimedOut => DateTime.UtcNow - this.Issued > TimeSpan.FromSeconds(30);

        public override void HandleTimeout(Dispatcher dispatcher)
        {
            this.Response.TrySetException(new TimeoutException($"Could not reach {this.ToMachine} after {DateTime.UtcNow - this.Issued}"));
        }
    }
}
