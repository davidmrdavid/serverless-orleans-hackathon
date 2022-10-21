// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    public class InChannel : Channel
    {
        internal static async Task ReceiveAsync(Guid channelId, Dispatcher dispatcher, Task<Stream> streamTask)
        {
            try
            {
                var stream = await streamTask;
                var channel = new InChannel();
                channel.ChannelId = channelId;

                dispatcher.InChannelListeners.TryAdd(channelId, channel);

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

                while (!dispatcher.HostShutdownToken.IsCancellationRequested)
                {
                    (Format.Op op, Guid guid) = await Format.ReceiveAsync(stream, dispatcher.HostShutdownToken);

                    switch (op)
                    {
                        case Format.Op.Connect:
                        case Format.Op.ConnectAndSolicit:
                            channel.ConnectionId = guid;
                            channel.Stream = new StreamWrapper(stream, dispatcher, channel);
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
                            channel.ConnectionId = guid;
                            channel.Stream = new StreamWrapper(stream, dispatcher, channel);
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
                            dispatcher.Worker.Submit(new ChannelFailedEvent()
                            {
                                ChannelId = guid,
                                DispatcherId = channel.DispatcherId,
                            });
                            // we can continue listening on this stream
                            break;

                        case Format.Op.ConnectionFailed:
                            dispatcher.Worker.Submit(new ConnectionFailedEvent()
                            {
                                ConnectionId = guid,
                            });
                            // we can continue listening on this stream
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                dispatcher.Worker.Submit(new ChannelFailedEvent()
                {
                    ChannelId = channelId,
                    DispatcherId = dispatcher.DispatcherId,

                });
                dispatcher.Logger.LogWarning("Dispatcher {dispatcherId} encountered exception in ListenAsync: {exception}", dispatcher.DispatcherId, exception);
            }
            finally
            {
                dispatcher.InChannelListeners.TryRemove(channelId, out _);
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
