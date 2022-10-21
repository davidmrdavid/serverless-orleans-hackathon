﻿// Copyright (c) Microsoft Corporation.
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
    using System.Threading.Tasks;

    internal class ClientAcceptEvent : DispatcherEvent
    {
        public Guid ConnectionId;
        public InChannel InChannel;
        public bool DoClientBroadcast;

        public override ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            if (dispatcher.ConnectRequests.TryGetValue(this.ConnectionId, out var request))
            {
                var connection = new Connection()
                {
                    ConnectionId = this.ConnectionId,
                    InChannel = this.InChannel,
                    OutChannel = request.OutChannel,
                    IsServerSide = false,
                };

                dispatcher.OutConnections.Add(this.ConnectionId, connection);

                request.Response.SetResult(connection);

                dispatcher.ConnectRequests.Remove(this.ConnectionId);
            }

            if (this.DoClientBroadcast)
            {
                TimerEvent.MakeContactAsync(dispatcher);
            }

            return default;
        }
    }
}
