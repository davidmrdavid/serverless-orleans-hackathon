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
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    internal class ServerAcceptEvent : DispatcherEvent
    {
        public TaskCompletionSource<Connection> Response { get; set; }

        public override async ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            if (!dispatcher.AcceptWaiters.TryDequeue(out var serverConnectEvent))
            {
                dispatcher.AcceptQueue.Enqueue(this);
            }
            else
            {
                var connection = new Connection()
                {
                    ConnectionId = serverConnectEvent.ConnectionId,
                    InChannel = serverConnectEvent.InChannel,
                    OutChannel = serverConnectEvent.OutChannel,
                    IsServerSide = true,
                };

                dispatcher.InConnections.Add(connection.ConnectionId, connection);

                await Format.SendAsync(connection.OutChannel.Stream, Format.Op.ConnectSuccess, connection.ConnectionId);

                this.Response.SetResult(connection);
            }
        }
    }
}
