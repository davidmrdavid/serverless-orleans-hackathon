// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.AspNetCore.Mvc.Internal;
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
    using System.Runtime.InteropServices;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    internal class ChannelFailedEvent : DispatcherEvent
    {
        public Channel Channel;
        
        public override async ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            if (this.Channel.ConnectionId != default)
            {
                // channel is associated with a connection, which may involve
                // many objects. To clean up everything, we cancel from top-down

                var evt = new ConnectionFailedEvent()
                {
                    ConnectionId = this.Channel.ConnectionId
                };

                await evt.ProcessAsync(dispatcher);
            }
            else
            {       
                Util.FilterQueues(dispatcher.ChannelPools, x => x != this.Channel);
                this.Channel.Dispose();
            }
        }
    }
}
