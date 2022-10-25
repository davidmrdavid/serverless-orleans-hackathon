// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector.Algorithm
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class OutChannel : Channel
    {
        public Action TerminateResponse;

        public override void Dispose()
        {
            TerminateResponse();
        }
    }
}
