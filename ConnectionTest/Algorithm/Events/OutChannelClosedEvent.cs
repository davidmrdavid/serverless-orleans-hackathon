// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    internal class OutChannelClosedEvent : DispatcherEvent
    {
        public OutChannel OutChannel;

        public override ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            // TODO remove from channels and connections
            return default;
        }
    }
}
