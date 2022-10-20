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

    public class Silo
    {
        public IHost host;
        IDisposable cancellationTokenRegistration;

        // singleton instance of the silo
        static TaskCompletionSource<Silo> siloPromise = new TaskCompletionSource<Silo>();

        public static Task<Silo> GetSiloAsync()
        {
            return siloPromise.Task;
        }

        public IGrainFactory GrainFactory => host.Services.GetRequiredService<IGrainFactory>();

        internal static void StartOnThreadpool(Dispatcher dispatcher)
        {
            // start on thread pool
            Task.Run(() => new Silo().StartAsync(dispatcher));
        }

        Silo()
        {
        }

        async Task StartAsync(Dispatcher dispatcher)
        {
            this.cancellationTokenRegistration = dispatcher.HostShutdownToken.Register(this.Shutdown);

            try
            {
                
                host = new HostBuilder()
                    .UseOrleans(builder => builder
                        .Configure<ClusterOptions>(options =>
                        {
                            options.ClusterId = "my-first-cluster";
                            options.ServiceId = "MyAwesomeOrleansService";
                        })
                        .ConfigureServices(services =>
                        {
                            services.AddSingletonKeyedService<object, IConnectionFactory>(KeyExports.GetSiloConnectionKey, OrleansExtensions.CreateServerlessConnectionFactory(dispatcher));
                            services.AddSingletonKeyedService<object, IConnectionListenerFactory>(KeyExports.GetSiloConnectionKey, OrleansExtensions.CreateServerlessConnectionListenerFactory(dispatcher));
                            services.AddSingletonKeyedService<object, IConnectionListenerFactory>(KeyExports.GetConnectionListenerKey, OrleansExtensions.CreateServerlessConnectionListenerFactory(dispatcher));
                        })
                        .ConfigureEndpoints(siloPort: 11111, gatewayPort: 0)
                        .UseAzureStorageClustering(options => options.ConfigureTableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage")))
                        .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(Application.HelloGrain).Assembly).WithReferences())
                    )
                    .Build();

                await host.StartAsync();
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
