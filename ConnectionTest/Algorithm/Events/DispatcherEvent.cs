// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace ConnectionTest.Algorithm
{
    internal abstract class DispatcherEvent
    {
        public abstract ValueTask ProcessAsync(Dispatcher dispatcher);

        public void Reschedule(Dispatcher dispatcher, TimeSpan delay)
        {
            var _ = ScheduleNextPassAsync();
            async Task ScheduleNextPassAsync()
            {
                await Task.Delay(delay);
                dispatcher.Worker.Submit(this);
            }
        }

        public virtual bool CancelWithConnection(Guid connectionId) => false;

        public virtual bool TimedOut => false;

        public virtual void HandleTimeout(Dispatcher dispatcher) 
        {
        }

    }
}