// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest
{
    using global::ConnectionTest.Algorithm;
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

    public class Silo
    {     
        IDisposable cancellationTokenRegistration;
        
        public string Endpoint { get; private set; }
        public IHost Host { get; private set; }

        public IGrainFactory GrainFactory => Host.Services.GetRequiredService<IGrainFactory>();

        public Silo()
        {
        }

        internal async Task StartAsync(string clusterId, IPAddress address, int port, ConnectionFactory connFactory, CancellationToken cancellationToken)
        {
            this.cancellationTokenRegistration = cancellationToken.Register(this.Shutdown);

            Host = new HostBuilder()
                .UseOrleans(builder => builder
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
                    .UseAzureStorageClustering(options => options.ConfigureTableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage")))
                    .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(Application.HelloGrain).Assembly).WithReferences())
                )
                .Build();

            await Host.StartAsync();

            this.Endpoint = Host.Services.GetRequiredService<ILocalSiloDetails>().SiloAddress.Endpoint.ToString();
        }

        void Shutdown()
        {
            this.cancellationTokenRegistration?.Dispose();

            Task.Run(async () =>
            {
                await this.Host.StopAsync();
            });
        }
    }
}
