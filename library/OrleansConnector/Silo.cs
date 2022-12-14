// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector
{
    using Algorithm;
    using Microsoft.AspNetCore.Connections;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Orleans;
    using Orleans.Configuration;
    using Orleans.Hosting;
    using Orleans.Runtime.Development;
    using System;
    using System.Threading.Tasks;
    using Orleans.TestingHost.InMemoryTransport;
    using Orleans.Runtime;
    using Orleans.Runtime.Messaging;
    using System.Threading;
    using System.Net;
    using Microsoft.Extensions.Logging;

    public class Silo
    {             
        ILogger logger;
        public string Endpoint { get; private set; }
        public IHost Host { get; private set; }

        public IGrainFactory GrainFactory => Host.Services.GetRequiredService<IGrainFactory>();

        public Silo()
        {
        }

        public class WorkerLoggerProvider : ILoggerProvider
        {
            private ILogger _logger;
            public WorkerLoggerProvider(ILogger logger)
            {
                _logger = logger;
            }

            public ILogger CreateLogger(string categoryName)
            {
                return _logger;
            }

            public void Dispose()
            {
                return;
            }
        }

        static SemaphoreSlim siloBuilderLock = new SemaphoreSlim(1);

        public async Task StartAsync(string clusterId, IPAddress address, int port, Action<ISiloBuilder> configureOrleans, ConnectionFactory connFactory, ILogger logger)
        {
            this.logger = logger;
            ILoggerProvider loggerProvider = new WorkerLoggerProvider(logger);
            
            try
            {
                await siloBuilderLock.WaitAsync();

                Host = new HostBuilder()
                    .UseOrleans(builder =>
                    {
                        builder
                            .Configure<ClusterOptions>(options =>
                            {
                                options.ClusterId = clusterId;
                                options.ServiceId = "MyAwesomeOrleansService";
                            })
                            .Configure<EndpointOptions>(options =>
                            {
                                options.AdvertisedIPAddress = address;
                                options.SiloPort = port;
                                options.GatewayPort = 0;
                            })
                            .ConfigureServices(services =>
                            {
                                services.AddSingletonKeyedService<object, IConnectionFactory>(KeyExports.GetSiloConnectionKey, OrleansExtensions.CreateServerlessConnectionFactory(connFactory));
                                services.AddSingletonKeyedService<object, IConnectionListenerFactory>(KeyExports.GetConnectionListenerKey, OrleansExtensions.CreateServerlessConnectionListenerFactory(connFactory));
                                services.AddSingletonKeyedService<object, IConnectionListenerFactory>(KeyExports.GetGatewayKey, OrleansExtensions.CreateServerlessConnectionListenerFactory(connFactory));
                            })
                            .ConfigureLogging(logBuilder => logBuilder.AddProvider(loggerProvider))
                            .UseAzureStorageClustering(options => options.ConfigureTableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage")));


                        configureOrleans(builder);
                    })
                    .Build();

                await Host.StartAsync();
            }
            finally
            {
                siloBuilderLock.Release();
            }

            this.Endpoint = Host.Services.GetRequiredService<ILocalSiloDetails>().SiloAddress.Endpoint.ToString();
        }
    }
}
