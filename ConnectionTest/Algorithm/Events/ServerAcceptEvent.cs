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
        public TaskCompletionSource<Connection> Response;

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

                dispatcher.Logger.LogInformation("{dispatcher} {connectionId:N} connect from {destination} accepted, established", dispatcher, connection.ConnectionId, connection.InChannel.DispatcherId);

                try
                {
                    await Format.SendAsync(
                        connection.OutChannel.Stream,
                        serverConnectEvent.DoClientBroadcast ? Format.Op.AcceptAndSolicit : Format.Op.Accept,
                        connection.ConnectionId);
                }
                catch (Exception exception)
                {
                    dispatcher.Logger.LogWarning("{dispatcher} could not send Accept message: {exception}", dispatcher, exception);

                    // retry this accept
                    dispatcher.Worker.Submit(this);

                    // we have to tear the connection down.
                    dispatcher.Worker.Submit(new ConnectionFailedEvent() { ConnectionId = connection.ConnectionId });
                }

                this.Response.SetResult(connection);
            }
        }
    }
}
