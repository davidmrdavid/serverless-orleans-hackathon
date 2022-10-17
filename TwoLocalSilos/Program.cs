using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;


// To test the membership protocol, we start two silos on this machine, but using different ports

using var host1 = new HostBuilder()
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

using var host2 = new HostBuilder()
    .UseOrleans(builder => builder
        .Configure<ClusterOptions>(options =>
        {
            options.ClusterId = "my-first-cluster";
            options.ServiceId = "MyAwesomeOrleansService";
        })
        .ConfigureEndpoints(siloPort: 11112, gatewayPort: 0)
        .UseAzureStorageClustering(options => options.ConfigureTableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage")))
        .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(Application.HelloGrain).Assembly).WithReferences())
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