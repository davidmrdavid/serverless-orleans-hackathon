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

    public class Silo
    {
        public IHost host;
        IDisposable cancellationTokenRegistration;
        public string Endpoint;

        // singleton instance of the silo
        static TaskCompletionSource<Silo> siloPromise = new TaskCompletionSource<Silo>();

        public static Task<Silo> GetSiloAsync()
        {
            return siloPromise.Task;
        }

        public IGrainFactory GrainFactory => host.Services.GetRequiredService<IGrainFactory>();

        internal Silo()
        {
        }

        internal async Task StartAsync(Task<ConnectionFactory> connFactory, int port, CancellationToken cancellationToken)
        {
            try
            {
                this.cancellationTokenRegistration = cancellationToken.Register(this.Shutdown);

                host = new HostBuilder()
                    .UseOrleans(builder => builder
                        .Configure<ClusterOptions>(options =>
                        {
                            options.ClusterId = "my-first-cluster";
                            options.ServiceId = "MyAwesomeOrleansService";
                        })
                        .ConfigureServices(services =>
                        {
                            services.AddSingletonKeyedService<object, IConnectionFactory>(KeyExports.GetSiloConnectionKey, OrleansExtensions.CreateServerlessConnectionFactory(connFactory));
                            services.AddSingletonKeyedService<object, IConnectionListenerFactory>(KeyExports.GetSiloConnectionKey, OrleansExtensions.CreateServerlessConnectionListenerFactory(connFactory));
                            services.AddSingletonKeyedService<object, IConnectionListenerFactory>(KeyExports.GetConnectionListenerKey, OrleansExtensions.CreateServerlessConnectionListenerFactory(connFactory));
                        })
                        .ConfigureEndpoints(siloPort: port, gatewayPort: 0)
                        .UseAzureStorageClustering(options => options.ConfigureTableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage")))
                        .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(Application.HelloGrain).Assembly).WithReferences())
                    )
                    .Build();

                await host.StartAsync();

                this.Endpoint = host.Services.GetRequiredService<ILocalSiloDetails>().SiloAddress.Endpoint.ToString();
                siloPromise.SetResult(this);
            }
            catch (Exception e)
            {
                siloPromise.SetException(e);
            }
        }

        void Shutdown()
        {
            this.cancellationTokenRegistration?.Dispose();

            Task.Run(async () =>
            {
                await siloPromise.Task;
                await this.host.StopAsync();
            });
        }
    }
}
