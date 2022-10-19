﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Orleans;
    using Orleans.Configuration;
    using Orleans.Hosting;
    using System;
    using System.Reflection.PortableExecutable;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    public class ConnectionFactory
    {
        Dispatcher dispatcher;

        public ConnectionFactory(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        public Task<Connection> ConnectAsync(string machine)
        {
            var connectEvent = new ClientConnectEvent()
            {
                ConnectionId = Guid.NewGuid(),
                ToMachine = machine,
                Response = new TaskCompletionSource<Connection>(),
            };
            this.dispatcher.Worker.Submit(connectEvent);
            return connectEvent.Response.Task;
        }

        public Task<Connection> AcceptAsync()
        {
            var acceptEvent = new ServerAcceptEvent()
            {
                Response = new TaskCompletionSource<Connection>(),
            };
            this.dispatcher.Worker.Submit(acceptEvent);
            return acceptEvent.Response.Task;
        }
    }
}
