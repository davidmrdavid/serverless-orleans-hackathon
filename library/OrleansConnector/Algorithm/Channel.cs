// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector.Algorithm
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    public abstract class Channel : IDisposable
    {
        public string DispatcherId;
        public Guid ChannelId;
        public StreamWrapper Stream;
        public Guid ConnectionId;
        public bool Disposed;
        public DateTime Since;

        public abstract void Dispose();
    }
}
