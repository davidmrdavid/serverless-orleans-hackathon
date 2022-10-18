// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


namespace ConnectionTest.Algorithm
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Orleans;
    using Orleans.Configuration;
    using Orleans.Hosting;
    using System;
    using System.Threading.Tasks;

    public class Silo
    {
        Dispatcher dispatcher;
        IHost host;
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
            this.dispatcher = dispatcher;
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
            this.cancellationTokenRegistration.Dispose();
            Task.Run(async () =>
            {
                await siloPromise.Task;
                await this.host.StopAsync();
            });
        }
    }
}
