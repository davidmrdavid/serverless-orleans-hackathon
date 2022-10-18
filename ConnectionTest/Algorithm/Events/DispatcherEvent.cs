// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    internal abstract class DispatcherEvent
    {
        public abstract void Process(Dispatcher dispatcher);
    }
}