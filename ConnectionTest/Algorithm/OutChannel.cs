// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class OutChannel
    {
        public string DispatcherId { get; set; }

        internal HttpResponseMessage HttpResponseMessage { get; set; }

        public Stream Stream { get; set; }

        public Action TerminateResponse { get; set; }

        public Guid ConnectionId { get; set; }
    }
}
