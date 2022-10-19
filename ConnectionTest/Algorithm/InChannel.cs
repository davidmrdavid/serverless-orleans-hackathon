// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class InChannel
    {
        public string DispatcherId;

        public Stream Stream;

        public Guid ConnectionId;

        internal static async Task ReceiveAsync(Dispatcher dispatcher, Task<Stream> streamTask)
        {
            try
            {
                var channel = new InChannel();
                channel.Stream = await streamTask;

                if (channel.Stream.GetType().Name == "EmptyReadStream")
                {
                    // avoid exception throwing path in common case
                    return;
                }

                try
                {
                    var reader = new BinaryReader(channel.Stream);
                    channel.DispatcherId = reader.ReadString();
                }
                catch (System.IO.EndOfStreamException)
                {
                    return;
                }

                while (!dispatcher.HostShutdownToken.IsCancellationRequested)
                {
                    (Format.Op op, channel.ConnectionId) = await Format.ReceiveAsync(channel.Stream, dispatcher.HostShutdownToken);

                    switch (op)
                    {
                        case Format.Op.TryConnect:

                            dispatcher.Worker.Submit(new ServerConnectEvent()
                            {
                                ConnectionId = channel.ConnectionId,
                                InChannel = channel,
                            });
                            // now streaming data from client to server
                            return;

                        case Format.Op.ConnectFail:
                            dispatcher.Worker.Submit(new ClientAcceptEvent()
                            {
                                ConnectionId = channel.ConnectionId,
                                InChannel = channel,
                                Success = false,
                            });
                            // we continue listening since the channel is not "used up" after sending this message
                            break;

                        case Format.Op.ConnectSuccess:
                            dispatcher.Worker.Submit(new ClientAcceptEvent()
                            {
                                ConnectionId = channel.ConnectionId,
                                InChannel = channel,
                                Success = true,
                            });
                            // now streaming data from server to client
                            return;

                        case Format.Op.Closed:
                            // now closed (without ever being used)
                            return;
                    }
                }
            }
            catch (Exception exception)
            {
                dispatcher.Logger.LogError("Dispatcher {dispatcherId} encountered exception in ListenAsync: {exception}", dispatcher.DispatcherId, exception);
            }
        }
    }
}
