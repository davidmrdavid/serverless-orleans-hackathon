// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector.Algorithm
{
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging;
    using OrleansConnector;
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

                dispatcher.Logger.LogInformation("{dispatcher} {connectionId} connect from {destination} accepted, established", dispatcher, connection.ConnectionId, connection.InChannel.DispatcherId);

                try
                {
                    await Format.SendAsync(
                        connection.OutChannel.Stream,
                        serverConnectEvent.DoClientBroadcast ? Format.Op.AcceptAndSolicit : Format.Op.Accept,
                        connection.ConnectionId);

                    dispatcher.Logger.LogTrace("{dispatcher} {channelId} {connectionId} sent Accept", dispatcher, connection.OutChannel.ChannelId, connection.ConnectionId);
                }
                catch (Exception exception)
                {
                    dispatcher.Logger.LogWarning("{dispatcher} {connectionId} could not send Accept: {exception}", dispatcher, connection.ConnectionId, exception);

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
