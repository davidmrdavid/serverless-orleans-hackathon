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
        private readonly Random random = new Random();
        private int count = 0;

        public override ValueTask ProcessAsync(Dispatcher dispatcher)
        {
            dispatcher.Logger.LogDebug($"{dispatcher} Processing TimerEvent");
            
            MakeContactAsync(dispatcher);

            TimeSpan nextBroadcast;
            if (count < 5)
            {
                nextBroadcast = TimeSpan.FromSeconds(5); 
            }
            else
            {
                nextBroadcast = TimeSpan.FromSeconds(60 + random.Next(30));
            }
           
            this.Reschedule(dispatcher, nextBroadcast);

            return default;
        }

        internal static void MakeContactAsync(Dispatcher dispatcher)
        {
            int knownRemotes = dispatcher.OutChannels.Count;
            int couponCollectorEx = (int)Math.Round(knownRemotes * Math.Log(knownRemotes + 1));
            int numRequests = Math.Max(couponCollectorEx, 5);

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

        static bool MakeContactAsync(Dispatcher dispatcher, Uri target)
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
