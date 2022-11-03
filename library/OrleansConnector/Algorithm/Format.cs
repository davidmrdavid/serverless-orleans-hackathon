// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector.Algorithm
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Orleans;
    using Orleans.Configuration;
    using Orleans.Hosting;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection.PortableExecutable;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Threading;
    using System.Threading.Tasks;

    public static class Format
    {
        public enum Op
        {
            None,
            Connect,
            ConnectAndSolicit,
            Accept,
            AcceptAndSolicit,
            ConnectionClosed,
            ChannelClosed,
            Closed,
        }

        const int packetSize = 17;

        public static async ValueTask SendAsync(StreamWrapper s, Op op, Guid connectionId)
        {
            var buffer = new byte[packetSize];
            buffer[0] = (byte)op;
            connectionId.TryWriteBytes(new Span<byte>(buffer, 1, 16));
            await s.WriteAsync(buffer);
            await s.FlushAsync();
        }

        public static async Task<(Op op, Guid connectionId)> ReceiveAsync(Stream stream, CancellationToken token)
        {
            var buffer = new byte[packetSize];
            int pos = 0;

            while (pos < packetSize)
            {
                int numRead = await stream.ReadAsync(buffer, pos, packetSize - pos, token);
                if (numRead == 0)
                {
                    return (Op.Closed, default);
                }
                pos += numRead;
            }

            Op op = (Op)buffer[0];
            Guid connectionId = new Guid(new ReadOnlySpan<byte>(buffer, 1, 16));

            return (op, connectionId);
        }
    }
}
