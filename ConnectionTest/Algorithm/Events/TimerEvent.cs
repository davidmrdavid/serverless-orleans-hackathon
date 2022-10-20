// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Azure.Core;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    internal class TimerEvent : DispatcherEvent
    {
        public override ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            dispatcher.Logger.LogDebug($"{dispatcher} Processing TimerEvent");

            this.MakeContactAsync(dispatcher, 2); // temporary, for testing

            this.Reschedule(dispatcher, TimeSpan.FromSeconds(100000));

            return default;
        }

        void MakeContactAsync(Dispatcher dispatcher, int numRequests)
        {
            //var ub = new UriBuilder(dispatcher.FunctionAddress);
            //var uriBuilder = new UriBuilder(dispatcher.FunctionAddress);
            //var paramValues = HttpUtility.ParseQueryString(uriBuilder.Query);
            //paramValues.Add("from", dispatcher.Id);
            //uriBuilder.Query = paramValues.ToString();
            //var uri = uriBuilder.Uri;

            int numSuccessful = 0;
            for (int i = 0; i < numRequests; i++)
            {
                if (MakeContactAsync(dispatcher, dispatcher.FunctionAddress))
                {
                    numSuccessful++;
                }
            }
             
            dispatcher.Logger.LogDebug($"{dispatcher} sent {numRequests} contact requests");
        }

        bool MakeContactAsync(Dispatcher dispatcher, Uri target)
        {
            try
            {
                // Wait for the response stream and process it asynchronously
                var responseStream = dispatcher.HttpClient.GetStreamAsync(dispatcher.FunctionAddress, dispatcher.HostShutdownToken);
                var _ = Task.Run(() => InChannel.ReceiveAsync(dispatcher, responseStream));
                return true;
            }
            catch (Exception exception)
            {
                dispatcher.Logger.LogDebug($"{dispatcher} failed to send contact request: {exception}");
                return false;
            }
        }
    }
}
