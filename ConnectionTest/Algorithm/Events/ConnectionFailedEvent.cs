// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.AspNetCore.Mvc.Internal;
    using Microsoft.Azure.WebJobs.Host.Executors;
    using Microsoft.Extensions.Logging;
    using Orleans.Streams;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    internal class ConnectionFailedEvent : DispatcherEvent
    {
        public Guid ConnectionId;

        public string ToSend;

        public DateTime Issued = DateTime.UtcNow;

        public override async ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            Util.FilterDictionary(dispatcher.ConnectRequests, x => !x.CancelWithConnection(this.ConnectionId));
            dispatcher.AcceptWaiters = Util.FilterQueue(dispatcher.AcceptWaiters, x => !x.CancelWithConnection(this.ConnectionId));
            dispatcher.OutChannelWaiters = Util.FilterList(dispatcher.OutChannelWaiters, x => !x.CancelWithConnection(this.ConnectionId));

            Connection connection = null;

            if (dispatcher.InConnections.TryGetValue(this.ConnectionId, out connection))
            {
                dispatcher.InConnections.Remove(this.ConnectionId);
            }
            else if (dispatcher.OutConnections.TryGetValue(this.ConnectionId, out connection))
            {
                dispatcher.OutConnections.Remove(this.ConnectionId);
            }

            if (connection != null)
            {
                if (connection.InChannel != null)
                {
                    connection.InChannel.Dispose();
                }
                if (connection.OutChannel != null)
                {
                    ToSend = connection.OutChannel.DispatcherId;
                    connection.OutChannel.Dispose();
                }

                connection.FailureNotify();
            }

            if (ToSend != null)
            {
                if (!dispatcher.ChannelPools.TryGetValue(ToSend, out var queue))
                {
                    dispatcher.OutChannelWaiters.Add(this);
                }
                else
                {
                    try
                    {
                        await Format.SendAsync(queue.Peek().Stream, Format.Op.ConnectionFailed, this.ConnectionId);
                    }
                    catch(Exception exception)
                    {
                        dispatcher.Logger.LogWarning("{dispatcher} could not send ConnectionFailed message: {exception}", dispatcher, exception);

                        // we can retry this
                        dispatcher.Worker.Submit(this);
                    }
                }
            }
        }

        public override bool TimedOut => DateTime.UtcNow - this.Issued > TimeSpan.FromSeconds(30);

        public override void HandleTimeout(Dispatcher dispatcher)
        {
            TimeSpan elapsed = DateTime.UtcNow - this.Issued;
            dispatcher.Logger.LogWarning("{dispatcher} {connectionId:N} ConnectionFailed message timed out after {elapsed}", dispatcher, this.ConnectionId, elapsed);
        }
    }
}
