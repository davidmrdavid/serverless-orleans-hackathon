// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    internal class StartEvent : DispatcherEvent
    {
        public Uri ServiceAddress;

        public override void Process(Dispatcher dispatcher)
        {
            dispatcher.Logger.LogInformation($"Processing StartEvent ServiceAddress={ServiceAddress}");
        }
    }

}