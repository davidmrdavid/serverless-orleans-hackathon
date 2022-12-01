// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector.Algorithm
{
    using Microsoft.AspNetCore.Mvc.Internal;
    using Microsoft.Extensions.Logging;
    using Orleans.Streams;
    using OrleansConnector;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    internal class ChannelClosedEvent : DispatcherEvent
    {
        public Guid ChannelId;
        public string DispatcherId;
        public Channel Channel;
        public DateTime Issued = DateTime.UtcNow;

        internal ChannelClosedEvent() { }

        public override async ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            if (Channel != null)
            {
                if (this.Channel.ConnectionId != default)
                {
                    // channel is associated with a connection, which may involve
                    // many objects. To clean up everything, we cancel from top-down

                    var evt = new ConnectionClosedEvent()
                    {
                        ConnectionId = this.Channel.ConnectionId
                    };

                    await evt.ProcessAsync(dispatcher);
                }

                this.Channel.Dispose();

                Util.FilterDictionary(  
                    dispatcher.ConnectRequests, 
                    req => req.OutChannel.ChannelId != this.ChannelId,
                    (k,v) => v.Response.TrySetException(new IOException($"Could not reach {v.ToMachine} because connection closed unexpectedly")));
            }
            else
            {
                Util.FilterQueues(dispatcher.ChannelPools, x => x.ChannelId != this.ChannelId, (r,c) => dispatcher.Filter.TryRemove(r, out _));

                if (!dispatcher.ChannelPools.TryGetValue(this.DispatcherId, out var queue))
                {
                    dispatcher.OutChannelWaiters.Add(this);
                }
                else
                {
                    var outChannel = queue.Peek();
                    try
                    {
                        await Format.SendAsync(outChannel.Stream, Format.Op.ChannelClosed, this.ChannelId);

                        dispatcher.Logger.LogTrace("{dispatcher} {channelId} sent ChannelClosed", dispatcher, outChannel.ChannelId);
                    }
                    catch (Exception exception)
                    {
                        dispatcher.Logger.LogWarning("{dispatcher} {channelId} could not send ChannelClosed: {exception}", dispatcher, outChannel.ChannelId, exception);

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
            dispatcher.Logger.LogWarning("{dispatcher} {channelId} ChannelClosed message timed out after {elapsed}", dispatcher, this.ChannelId, elapsed);
        }
    }
}
