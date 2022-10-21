// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class Channel : IDisposable
    {
        public string DispatcherId;
        public StreamWrapper Stream;
        public Guid ConnectionId;
        public bool Disposed;

        public virtual void Dispose() { }
    }
}
