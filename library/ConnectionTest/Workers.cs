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
    using Microsoft.AspNetCore.Http.Extensions;
    using Orleans.Runtime.Configuration;
    using System.Web;
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using Microsoft.Azure.WebJobs.Host.Scale;
    using System.Linq;
    using System.Text.RegularExpressions;

    public static class Workers
    {
        // use this function to get a visual of the running workers

        [FunctionName("Workers")]
        public static async Task<IActionResult> Run(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "workers")] HttpRequestMessage requestMessage,
           ILogger log)
        {
            try
            {
                var dispatcher = await Static.GetDispatcherAsync();
                var sb = new StringBuilder();

                var query = requestMessage.RequestUri.ParseQueryString();
                int subquery = 0;
                bool all = false;
                bool printChannelMatrix = true;
                bool printConnectionMatrix = false;
                foreach (String s in query.AllKeys)
                {
                    if (s.ToLower() == "subquery")
                    {
                        subquery = int.Parse(query[s]);
                    }
                    if (s.ToLower() == "all")
                    {
                        all = bool.Parse(query[s]);
                    }
                    if (s.ToLower() == "printchannelmatrix")
                    {
                        printChannelMatrix = bool.Parse(query[s]);
                    }
                    if (s.ToLower() == "printconnectionmatrix")
                    {
                        printConnectionMatrix = bool.Parse(query[s]);
                    }
                }

                switch (subquery)
                {
                    case 0: // --- we receive a request from a client
                        {
                            // first, collect a list of all remotes
                            HashSet<string> remotes = new HashSet<string>();
                            HashSet<string> responders = new HashSet<string>();
                            for (int i = 0; i < 3; i++)
                            {
                                var tasks = Enumerable.Range(0, 20).Select(GetRemotesAsync).ToList();
                                await Task.WhenAll(tasks);
                            }
                            async Task GetRemotesAsync(int iteration)
                            {
                                UriBuilder uriBuilder = new UriBuilder(requestMessage.RequestUri);
                                QueryBuilder queryBuilder = new QueryBuilder();
                                queryBuilder.Add("subquery", "1");
                                queryBuilder.Add("all", all.ToString());
                                uriBuilder.Query = queryBuilder.ToString();
                                var response = await httpClient.GetAsync(uriBuilder.Uri);
                                if (response.IsSuccessStatusCode)
                                {
                                    var content = await response.Content.ReadAsStringAsync();
                                    var result = JsonConvert.DeserializeObject<List<string>>(content);
                                    lock (remotes)
                                    {
                                        responders.Add(result[^1]);
                                        foreach (var r in result)
                                        {
                                            remotes.Add(r);
                                        }
                                    }
                                }
                            }

                            // sort the remotes based on response(first) and id (second)
                            var orderedRemotes = remotes.OrderBy(r => (responders.Contains(r), r)).ToList();

                            // then, collect info from all
                            Dictionary<string, string> infos = new Dictionary<string, string>();
                            for (int i = 0; i < 3; i++)
                            {
                                var tasks = Enumerable.Range(0, 20).Select(GetInfoAsync).ToList();
                                await Task.WhenAll(tasks);
                            }
                            async Task GetInfoAsync(int iteration)
                            {
                                UriBuilder uriBuilder = new UriBuilder(requestMessage.RequestUri);
                                QueryBuilder queryBuilder = new QueryBuilder();
                                queryBuilder.Add("subquery", "2");
                                queryBuilder.Add("printChannelMatrix", printChannelMatrix.ToString());
                                queryBuilder.Add("printConnectionMatrix", printConnectionMatrix.ToString());
                                uriBuilder.Query = queryBuilder.ToString();
                                HttpContent postContent = new StringContent(JsonConvert.SerializeObject(orderedRemotes));
                                var response = await httpClient.PostAsync(uriBuilder.Uri, postContent);
                                if (response.IsSuccessStatusCode)
                                {
                                    var content = await response.Content.ReadAsStringAsync();
                                    var result = JsonConvert.DeserializeObject<(string, string)>(content);
                                    lock (remotes)
                                        infos[result.Item1] = result.Item2;
                                }
                            }

                            // finally, format the responses
                            foreach (var r in orderedRemotes)
                            {
                                sb.Append($"{r,50} | ");
                                if (infos.TryGetValue(r, out var info))
                                {
                                    sb.AppendLine(info);
                                }
                                else
                                {
                                    string diag = string.Join(' ', orderedRemotes.Select(r2 => r == r2 ? "X" : " "));
                                    string outCh = printChannelMatrix ? $"[{diag}]" : " ";
                                    string inCh = printChannelMatrix ? $"[{diag}]" : " ";
                                    string outConn = printConnectionMatrix ? $"[{diag}]" : " ";
                                    string inConn = printConnectionMatrix ? $"[{diag}]" : " ";

                                    sb.AppendLine($"OutCh={outCh} InCh={inCh} OutConn={outConn} InConn={inConn} ConnReq=  AcceptQ=  AcceptW=  ChW=");
                                }
                            }
                        }
                        break;

                    case 1: // --- this is a subrequests for returning a list of remotes
                        {
                            var response = all ? dispatcher.Remotes.ToList() : new List<string>();
                            response.Add(dispatcher.DispatcherId);
                            sb.Append(JsonConvert.SerializeObject(response));
                        }
                        break;

                    case 2:// --- this is a subrequests for returning the info for this dispatcher
                        {
                            var content = await requestMessage.Content.ReadAsStringAsync();
                            var workers = JsonConvert.DeserializeObject<List<string>>(content);
                            var information = dispatcher.PrintInformation(workers, printChannelMatrix, printConnectionMatrix);
                            var response = (dispatcher.DispatcherId, information);
                            sb.Append(JsonConvert.SerializeObject(response));
                        }
                        break;
                }

                return new OkObjectResult(sb.ToString());
            }
            catch(Exception e)
            {
                return new ObjectResult($"exception in Workers: {e}\n") { StatusCode = (int)HttpStatusCode.InternalServerError };
            }
        }

        static HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
    }
}
