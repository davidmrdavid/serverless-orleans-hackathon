// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;

    internal class RemoteTriggerEvent : DispatcherEvent
    {
        public HttpRequestMessage Request;
        public TaskCompletionSource<HttpResponseMessage> Response;

        public override void Process(Dispatcher dispatcher)
        {
            dispatcher.Logger.LogInformation($"Processing RemoteTriggerEvent");

        }
    }
}
