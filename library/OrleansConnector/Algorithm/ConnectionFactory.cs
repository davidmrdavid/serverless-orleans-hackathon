// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector.Algorithm
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Orleans;
    using Orleans.Configuration;
    using Orleans.Hosting;
    using OrleansConnector;
    using System;
    using System.Reflection.PortableExecutable;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    public class ConnectionFactory
    {
        readonly Dispatcher dispatcher;

        public ConnectionFactory(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        public Task<Connection> ConnectAsync(string machine, bool cancelIfNotAvailableImmediately = false)
        {

            var connectEvent = new ClientConnectEvent()
            {
                ConnectionId = Guid.NewGuid(),
                ToMachine = machine,
                Issued = DateTime.UtcNow,
                Response = new TaskCompletionSource<Connection>(),
                DontQueue = cancelIfNotAvailableImmediately,
            };
            dispatcher.Logger.LogDebug("{dispatcher} {connectionId:N} connect to {destination} called", dispatcher, connectEvent.ConnectionId, connectEvent.ToMachine);
            this.dispatcher.Worker.Submit(connectEvent);
            return connectEvent.Response.Task;
        }

        public Task<Connection> AcceptAsync()
        {
            var acceptEvent = new ServerAcceptEvent()
            {
                Response = new TaskCompletionSource<Connection>(),
            };
            dispatcher.Logger.LogDebug("{dispatcher} accept called", dispatcher);
            this.dispatcher.Worker.Submit(acceptEvent);
            return acceptEvent.Response.Task;
        }
    }
}
