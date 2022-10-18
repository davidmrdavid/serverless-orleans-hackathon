// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    internal class TimerEvent : DispatcherEvent
    {
        public override void Process(Dispatcher dispatcher)
        {
            dispatcher.Logger.LogInformation("Processing TimerEvent");


        
            var _ = ScheduleNextPassAsync(dispatcher);
        }

        async Task ScheduleNextPassAsync(Dispatcher dispatcher)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            dispatcher.Worker.Submit(this);
        }
    }
}
