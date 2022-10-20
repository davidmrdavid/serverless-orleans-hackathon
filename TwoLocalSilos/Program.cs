using Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime.Development;
using Orleans.TestingHost.InMemoryTransport;

// To test the membership protocol, we start two silos on this machine, but using different ports


// pretend stream1 is the stream from Host1, and similarly for stream2 and Host2
var stream1 = new MemoryStream();
var stream2 = new MemoryStream();

// the connection manager is a user-managed object (i.e the Function will create and manage it) that
// Orleans uses to request connection streams.
StreamConnectionManager connManager1 = null; //new(stream1, stream2); // 1st argument represents local stream, 2nd is target stream
StreamConnectionManager connManager2 = null; //new(stream2, stream1); 


var transportHub = new InMemoryTransportConnectionHub();
using var host1 = new HostBuilder()
    .UseOrleans(builder => builder
        .Configure<ClusterOptions>(options =>
        {
            options.ClusterId = "my-first-cluster";
            options.ServiceId = "MyAwesomeOrleansService";
        })
        .ConfigureEndpoints(siloPort: 11113, gatewayPort: 0)
        .UseInMemoryConnectionTransport(transportHub, connManager1) // for now, we're ignoring the connectionManager. Just passing it for convenience.
        .UseAzureStorageClustering(options => options.ConfigureTableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage")))
        .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(Application.HelloGrain).Assembly).WithReferences())
    )
    .Build();

using var host2 = new HostBuilder()
    .UseOrleans(builder => {
        builder
            .Configure<ClusterOptions>(options =>
            {
                options.ClusterId = "my-first-cluster";
                options.ServiceId = "MyAwesomeOrleansService";
            })
            .ConfigureEndpoints(siloPort: 11114, gatewayPort: 0)
            .UseInMemoryConnectionTransport(transportHub, connManager2)
            .UseAzureStorageClustering(options => options.ConfigureTableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage")))
            .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(Application.HelloGrain).Assembly).WithReferences());
    }
    )
    .Build();



// Start the host
await Task.WhenAll(host1.StartAsync(), host2.StartAsync());

// Get the grain factory
var grainFactory = host1.Services.GetRequiredService<IGrainFactory>();

// Get a reference to the HelloGrain grain with the key "friend"
var friend = grainFactory.GetGrain<Application.IHelloGrain>("friend");

// Call the grain and print the result to the console
var result = await friend.SayHello("Good morning!");

Console.WriteLine("\n\n{0}\n\n", result);

Console.WriteLine("Orleans is running.\nPress Enter to terminate...");
Console.ReadLine();
Console.WriteLine("Orleans is stopping...");

await Task.WhenAll(host1.StopAsync(), host2.StopAsync());

Console.WriteLine("Orleans has stopped.");