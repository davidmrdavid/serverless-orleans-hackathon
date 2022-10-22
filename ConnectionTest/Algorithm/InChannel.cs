﻿// Copyright (c) Microsoft Corporation.
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
                    dispatcher.Logger.LogTrace("{dispatcher} {channelId} empty content", dispatcher, channelId);
                    return;
                }

                try
                {
                    var reader = new BinaryReader(stream);
                    channel.DispatcherId = reader.ReadString();
                }
                catch (System.IO.EndOfStreamException e)
                {
                    dispatcher.Logger.LogTrace("{dispatcher} {channelId} empty content: {message}", dispatcher, channelId, e.Message);
                    return;
                }

                while (!dispatcher.HostShutdownToken.IsCancellationRequested)
                {
                    dispatcher.Logger.LogTrace("{dispatcher} {channelId} waiting for packet", dispatcher, channelId);

                    (Format.Op op, Guid guid) = await Format.ReceiveAsync(stream, dispatcher.HostShutdownToken);

                    dispatcher.Logger.LogTrace("{dispatcher} {channelId} received packet {op} {guid}", dispatcher, channelId, op, guid);

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
                            dispatcher.Logger.LogTrace("{dispatcher} {channelId} channel closed by sender", dispatcher, channelId);
                            return;

                        case Format.Op.ChannelFailed:
                            dispatcher.Worker.Submit(new ChannelFailedEvent()
                            {
                                ChannelId = guid,
                                DispatcherId = channel.DispatcherId,
                            });
                            // we can continue listening on this stream
                            continue;

                        case Format.Op.ConnectionFailed:
                            dispatcher.Worker.Submit(new ConnectionFailedEvent()
                            {
                                ConnectionId = guid,
                            });
                            // we can continue listening on this stream
                            continue;
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
                dispatcher.Logger.LogWarning("{dispatcher} {channelId} error in ListenAsync: {exception}", dispatcher, channelId, exception);
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
