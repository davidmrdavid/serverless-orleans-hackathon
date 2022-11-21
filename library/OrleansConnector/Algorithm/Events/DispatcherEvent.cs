// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace OrleansConnector.Algorithm
{
    internal abstract class DispatcherEvent
    {
        public abstract ValueTask ProcessAsync(Dispatcher dispatcher);

        public virtual bool CancelWithConnection(Guid connectionId) => false;

        public virtual bool TimedOut => false;

        public virtual void HandleTimeout(Dispatcher dispatcher) 
        {
        }
    }
}