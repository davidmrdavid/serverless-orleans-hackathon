// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    static class WorkGeneration
    {
        // allows us to generate work in the form of http calls and check how many workers are taking the calls.

        // example invocations:
        //    curl "http://localhost:7195/work?heavy=true&width=10&seconds=10" -d http://localhost:7195
        //    curl "https://functionssb1.azurewebsites.net/work?heavy=false&width=100&seconds=100" -d "https://functionssb1.azurewebsites.net"

        [FunctionName(nameof(Work))]
        public static async Task<IActionResult> Work(
            [HttpTrigger(AuthorizationLevel.Anonymous, methods: "post", Route = "work")] HttpRequest req,
            ILogger log)
        {
            log.LogWarning($"starting busy background work");
            var stopwatch = new Stopwatch();
            int numCalls = 0;
            stopwatch.Start();

            var parameters = req.GetQueryParameterDictionary();

            int width = 10;
            int seconds = 30;
            bool heavy = false;

            if (parameters.TryGetValue("width", out string widthString))
            {
               int.TryParse(widthString, out width);
            }
            if (parameters.TryGetValue("heavy", out string heavyString))
            {
                bool.TryParse(heavyString, out heavy);
            }
            if (parameters.TryGetValue("seconds", out string secondsString))
            {
                int.TryParse(secondsString, out seconds);
            }

            string serviceUrl = await req.ReadAsStringAsync();
            string workFunctionUrl = $"{serviceUrl}/{(heavy ? "heavyworkfunction" : "workfunction")}";

            var tasks = new List<Task<List<string>>>();
            for(int i = 0; i < width; i++)
            {
                tasks.Add(CallLoop());
            }

            async Task<List<string>> CallLoop()
            {
                var hosts = new List<string>();
                while (stopwatch.Elapsed < TimeSpan.FromSeconds(seconds))
                {
                    Interlocked.Increment(ref numCalls);
                    var response = await HttpTriggers.Client.GetAsync(workFunctionUrl);
                    string host = await response.Content.ReadAsStringAsync();
                    hosts.Add(host);
                }
                return hosts;
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // count how many invocations on each host
            var invocationCount = new Dictionary<string, int>();
            foreach (string host in tasks.SelectMany(t => t.Result))
            {
                if (invocationCount.TryGetValue(host, out int value))
                {
                    invocationCount[host] = value + 1;
                }
                else
                {
                    invocationCount[host] = 1;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"----------------------");
            sb.AppendLine($"Made a total of {numCalls} calls on {invocationCount.Count} workers in {stopwatch.Elapsed}");
            sb.AppendLine();
            sb.AppendLine($"count       worker");
            sb.AppendLine($"----------  ------------");
            foreach (var kvp in invocationCount)
            {
                sb.AppendLine($"{kvp.Value,10}  {kvp.Key}");
            }

            return new OkObjectResult(sb.ToString());
        }

        [FunctionName(nameof(WorkFunction))]
        public static async Task<IActionResult> WorkFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, methods: "get", Route = "workfunction")] HttpRequest req,
            ILogger logger)
        {
            await Task.Delay(5);
            return new OkObjectResult(Environment.MachineName);
        }

        [FunctionName(nameof(HeavyWorkFunction))]
        public static IActionResult HeavyWorkFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, methods: "get", Route = "heavyworkfunction")] HttpRequest req,
            ILogger logger)
        {
            long count = 1000000000;
            Stopwatch s = new Stopwatch();
            int numConflicts = 0;

            for (long i = 0; i < count; i++)
            {
                if (i.GetHashCode() == 0)
                    numConflicts++;
            }

            return new OkObjectResult(Environment.MachineName);
        }
    }
}
