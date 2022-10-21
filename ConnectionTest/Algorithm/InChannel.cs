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

    public class InChannel : Channel
    {
        internal static async Task ReceiveAsync(Dispatcher dispatcher, Task<Stream> streamTask)
        {
            try
            {
                var stream = await streamTask;
                var channel = new InChannel();

                if (stream.GetType().Name == "EmptyReadStream")
                {
                    // avoid exception throwing path in common case
                    return;
                }

                try
                {
                    var reader = new BinaryReader(stream);
                    channel.DispatcherId = reader.ReadString();
                }
                catch (System.IO.EndOfStreamException)
                {
                    return;
                }

                channel.Stream = new StreamWrapper(stream, dispatcher, channel);

                while (!dispatcher.HostShutdownToken.IsCancellationRequested)
                {
                    (Format.Op op, Guid connectionId) = await Format.ReceiveAsync(channel.Stream, dispatcher.HostShutdownToken);

                    switch (op)
                    {
                        case Format.Op.Connect:
                        case Format.Op.ConnectAndSolicit:
                            channel.ConnectionId = connectionId;
                            dispatcher.Worker.Submit(new ServerConnectEvent()
                            {
                                ConnectionId = channel.ConnectionId,
                                InChannel = channel,
                                DoServerBroadcast = (op == Format.Op.ConnectAndSolicit),
                                Issued = DateTime.UtcNow,
                            });
                            // now streaming data from client to server
                            return;

                        case Format.Op.Accept:
                        case Format.Op.AcceptAndSolicit:
                            channel.ConnectionId = connectionId;
                            dispatcher.Worker.Submit(new ClientAcceptEvent()
                            {
                                ConnectionId = channel.ConnectionId,
                                InChannel = channel,
                                DoClientBroadcast = (op == Format.Op.AcceptAndSolicit),
                            });
                            // now streaming data from server to client
                            return;

                        case Format.Op.Closed:
                            // now closed (without ever being used)
                            return;

                        case Format.Op.ChannelFailed:
                            dispatcher.Worker.Submit(new ConnectionFailedEvent()
                            {
                                ConnectionId = connectionId,
                            });
                            // we can continue listening on this stream
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                dispatcher.Logger.LogError("Dispatcher {dispatcherId} encountered exception in ListenAsync: {exception}", dispatcher.DispatcherId, exception);
            }
        }

        public override void Dispose()
        {
            try
            {
                this.Stream.Dispose();
            }
            catch
            {
            }
        }
    }
}
