// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class Connection
    {
        public Guid ConnectionId { get; internal set; }

        public Stream InStream => InChannel.Stream;

        public Stream OutStream => OutChannel.Stream;

        public bool IsServerSide { get; internal set; }

        internal InChannel InChannel { get; set; }

        internal OutChannel OutChannel { get; set; }
    }
}
