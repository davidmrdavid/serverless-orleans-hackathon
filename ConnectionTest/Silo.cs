using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace ConnectionTest
{
    internal class Silo
    {
        // the host that is running this silo
        IHost host;

        // singleton instance of the silo
        static TaskCompletionSource<Silo> silo;

        public static Task<Silo> GetSiloAsync()
        {
            if (silo == null)
            {
                // use an interlocked operation to prevent duplicate starts 
                TaskCompletionSource<Silo> promise = new TaskCompletionSource<Silo>();
                if (Interlocked.CompareExchange(ref silo, promise, null) == null)
                {
                    Task.Run(() => new Silo().StartAsync(promise));
                }
            }

            return silo.Task;
        }

        public IGrainFactory GrainFactory => host.Services.GetRequiredService<IGrainFactory>();

        async Task StartAsync(TaskCompletionSource<Silo> promise)
        {
            try
            {

                this.host = new HostBuilder()
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

                await this.host.StartAsync();
                promise.SetResult(this);
            }
            catch (Exception e)
            {
                promise.SetException(e);
            }
        }
    }
}
