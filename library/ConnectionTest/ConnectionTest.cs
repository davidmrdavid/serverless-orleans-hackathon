// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using System.Net;
    using System.Net.Http;
    using System.Diagnostics;
    using global::OrleansConnector.Algorithm;
    using System.Reflection;
    using System.Threading;
    using OrleansConnector;
    using System.Text;

    public static class ConnectionTest
    {
        // to run these tests first start the dispatchers
        // curl https://functionssb1.azurewebsites.net/orleans?dispatcherTest=true
        // once the dispatchers are running, you can display the available workers with
        // curl https://functionssb1.azurewebsites.net/workers/
        // and then run a connection to any one of these workers with
        // curl https://functionssb1.azurewebsites.net/connect/{seconds}/{worker}

        [FunctionName("ConnectionTest")]
        public static HttpResponseMessage Test(
            [HttpTrigger(AuthorizationLevel.Anonymous, methods: "get", Route = "connect/{seconds}/{worker}")] HttpRequestMessage req,
            int seconds,
            string worker,
            ILogger log)
        {
            HttpResponseMessage httpResponseMessage = new HttpResponseMessage();
            httpResponseMessage.StatusCode = HttpStatusCode.OK;
            httpResponseMessage.RequestMessage = req;
            httpResponseMessage.Content = new PushStreamContent(async (Stream s, HttpContent content, TransportContext context) =>
            {
                var writer = new StreamWriter(s);
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                writer.WriteLine($"accessing dispatcher...");
                await writer.FlushAsync();

                var dispatcher = await Static.GetDispatcherAsync();

                writer.WriteLine($"DispatcherId={dispatcher.DispatcherId} {dispatcher.PrintInformation()}");
                writer.WriteLine();
                writer.WriteLine($"connecting to {worker}...");
                await writer.FlushAsync();

                Connection connection;
                var connectionFactory = new ConnectionFactory(dispatcher);
                try
                {
                    connection = await connectionFactory.ConnectAsync(worker, cancelIfNotAvailableImmediately: true);

                    if (connection != null)
                    {
                        writer.WriteLine($"connected to {worker} after {stopwatch.Elapsed.TotalSeconds:F3}s connectionId={connection.ConnectionId}.");
                        writer.WriteLine();
                        await writer.FlushAsync();
                    }
                    else
                    {
                        writer.WriteLine($"worker {worker} not found in pool.");
                        await writer.FlushAsync();
                        await writer.DisposeAsync();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    writer.WriteLine($"failed to connect to {worker} after {stopwatch.Elapsed.TotalSeconds:F3}s: {ex}");
                    await writer.FlushAsync();
                    await writer.DisposeAsync();
                    return;
                }

                writer.WriteLine($"starting reader loop.");
                await writer.FlushAsync();

                var fromWorker = new StreamReader(connection.InStream);
                Task ReaderLoopTask = ReaderLoopAsync();
                async Task ReaderLoopAsync()
                {
                    string nextLine;
                    do
                    {
                        nextLine = await fromWorker.ReadLineAsync();
                        await writer.WriteLineAsync(nextLine);
                        await writer.FlushAsync();
                    } 
                    while (nextLine != null);
                }

                writer.WriteLine($"starting writer loop.");
                await writer.FlushAsync();

                var toWorker = new StreamWriter(connection.OutStream);
                Task WriterLoopTask = WriterLoopAsync();
                async Task WriterLoopAsync()
                {
                    for (int i = 0; i < seconds / 2; i++)
                    {
                        toWorker.WriteLine($"Hello {i} sent={stopwatch.Elapsed.TotalSeconds:F3}");
                        await toWorker.FlushAsync();
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }


                    await toWorker.DisposeAsync();
                }

                try 
                { 
                    await WriterLoopTask;
                    writer.WriteLine($"writer loop is finished.");
                    await writer.FlushAsync();
                }
                catch (Exception ex)
                {
                    writer.WriteLine($"writer loop failed after {stopwatch.Elapsed.TotalSeconds:F3}s: {ex}");
                    await writer.FlushAsync();
                    await writer.DisposeAsync();
                    return;
                }

                try
                {
                    await ReaderLoopTask;
                    writer.WriteLine($"reader loop is finished.");
                    await writer.FlushAsync();
                }
                catch (Exception ex)
                {
                    writer.WriteLine($"reader loop failed after {stopwatch.Elapsed.TotalSeconds:F3}s: {ex}");
                    await writer.FlushAsync();
                    await writer.DisposeAsync();
                    return;
                }

                await writer.WriteLineAsync($"finishing connection test after {stopwatch.Elapsed.TotalSeconds:F3}s");
                await writer.FlushAsync();
                await writer.DisposeAsync();
            });
            return httpResponseMessage;
        }
    }
}
