// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector.Algorithm
{
    using Microsoft.Extensions.Logging;
    using OrleansConnector;
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
            var channel = new InChannel();

            try
            {
                var stream = await streamTask;
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

                while (!dispatcher.ShutdownToken.IsCancellationRequested)
                {
                    dispatcher.Logger.LogTrace("{dispatcher} {channelId} waiting for packet", dispatcher, channelId);

                    (Format.Op op, Guid guid) = await Format.ReceiveAsync(stream, dispatcher.ShutdownToken);

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
                            dispatcher.Logger.LogTrace("{dispatcher} {channelId} in-channel closed by sender", dispatcher, channelId);
                            channel.Dispose();
                            return;

                        case Format.Op.ChannelClosed:
                            dispatcher.Worker.Submit(new ChannelClosedEvent()
                            {
                                ChannelId = guid,
                                DispatcherId = channel.DispatcherId,
                            });
                            // we can continue listening on this stream
                            continue;

                        case Format.Op.ConnectionClosed:
                            dispatcher.Worker.Submit(new ConnectionClosedEvent()
                            {
                                ConnectionId = guid,
                            });
                            // we can continue listening on this stream
                            continue;
                    }
                }
            }
            catch(HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                // seems to happen occasionally
                dispatcher.Logger.LogTrace("{dispatcher} {channelId} contact request received 503 ServiceUnavailable", dispatcher, channelId);
            }
            catch (Exception exception)
            {
                if (channel.DispatcherId != null)
                {
                    dispatcher.Worker.Submit(new ChannelClosedEvent()
                    {
                        ChannelId = channelId,
                        DispatcherId = channel.DispatcherId,

                    });
                }
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
                this.Stream?.Dispose();
            }
            catch
            {
            }
        }
    }
}
