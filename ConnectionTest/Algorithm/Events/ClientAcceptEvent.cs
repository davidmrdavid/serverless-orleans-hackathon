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
    using System.Threading.Tasks;

    internal class ClientAcceptEvent : DispatcherEvent
    {
        public Guid ConnectionId;

        public InChannel InChannel;

        public bool Success;

        public override ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            if (dispatcher.ConnectRequests.TryGetValue(this.ConnectionId, out var request))
            {
                if (this.Success)
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
                }
                else
                {
                    request.Response.SetException(new Exception("connection failed"));
                }

                dispatcher.ConnectRequests.Remove(this.ConnectionId);
            }

            return default;
        }
    }
}
