﻿using ConnectionTest.Algorithm;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Networking.Shared;
using Orleans.Runtime.Development;
using Orleans.TestingHost.InMemoryTransport;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConnectionTest
{
    public static class OrleansExtensions
    {
        public static Func<IServiceProvider, object, IConnectionFactory> CreateServerlessConnectionFactory(Task<ConnectionFactory> connFactory)
        {
            return (IServiceProvider sp, object key) =>
            {
                return new ServerlessConnectionManager(connFactory);
            };
        }

        public static Func<IServiceProvider, object, IConnectionListenerFactory> CreateServerlessConnectionListenerFactory(Task<ConnectionFactory> connFactory)
        {
            return (IServiceProvider sp, object key) =>
            {
                return new ServerlessConnectionManager(connFactory);
            };
        }

        public class ServerlessConnectionManager : IConnectionFactory, IConnectionListenerFactory, IConnectionListener
        {
            readonly Task<ConnectionFactory> connFactory;

            public ServerlessConnectionManager(Task<ConnectionFactory> connFactory)
            {
                this.connFactory = connFactory;
            }

            EndPoint IConnectionListener.EndPoint => throw new NotImplementedException();

            async ValueTask<ConnectionContext> IConnectionFactory.ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken)
            {
                return await ServerlessConnection.Create(connFactory, endpoint, cancellationToken);
            }

            async ValueTask<ConnectionContext> IConnectionListener.AcceptAsync(CancellationToken cancellationToken)
            {
                return await ServerlessConnection.Create(connFactory, cancellationToken);
            }

            ValueTask<IConnectionListener> IConnectionListenerFactory.BindAsync(EndPoint endpoint, CancellationToken cancellationToken)
            {
                return new ValueTask<IConnectionListener>(this);
            }

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                // no-op
                return default;
            }

            ValueTask IConnectionListener.UnbindAsync(CancellationToken cancellationToken)
            {
                // no-op
                return default;
            }
        }


        public class ServerlessConnection : TransportConnection
        {
            public Connection myConnection;
            public ConnectionFactory connFactory;

            public async static Task<ServerlessConnection> Create(Task<ConnectionFactory> connFactory, EndPoint endpoint, CancellationToken cancellationToken)
            {
                var targetEndpointStr = endpoint.ToString(); // TODO: ???
                Connection conn = await (await connFactory).ConnectAsync(targetEndpointStr); // TODO: add token

                return new ServerlessConnection(conn);
            }

            public ServerlessConnection(Connection conn)
            {
                myConnection = conn;
                Stream s1 = conn.InStream;
                Stream s2 = conn.OutStream;

                PipeReader pr1 = PipeReader.Create(s1);
                PipeWriter pw1 = PipeWriter.Create(s2);
                var applicationDuplexPipe = new DuplexPipe(pr1, pw1);
                Application = applicationDuplexPipe;

                //todo close connection   
            }

            public async static Task<ServerlessConnection> Create(Task<ConnectionFactory> connFactory, CancellationToken cancellationToken)
            {
                Connection conn = await (await connFactory).AcceptAsync();
                return new ServerlessConnection(conn);
            }
        }
    }
}