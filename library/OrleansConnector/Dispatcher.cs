// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector
{
    using Azure;
    using Microsoft.Extensions.Logging;
    using Orleans;
    using OrleansConnector.Algorithm;
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Formatting;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class Dispatcher
    {
        readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        internal CancellationToken ShutdownToken => shutdown.Token;
        internal ILogger Logger { get; }
        internal Processor Worker { get; }
        public string DispatcherId { get; }
        public string ShortId { get; }
        internal byte[] DispatcherIdBytes { get; }
        public Uri FunctionAddress { get; }
        public HttpClient HttpClient { get; }

        // channels
        internal SortedDictionary<string, Queue<OutChannel>> ChannelPools { get; set; }
        internal List<DispatcherEvent> OutChannelWaiters { get; set; }
        internal ConcurrentDictionary<Guid, InChannel> InChannelListeners { get; set; }

        // client
        internal Dictionary<Guid, ClientConnectEvent> ConnectRequests { get; set; }
        internal Dictionary<Guid, Connection> OutConnections { get; set; }

        // server
        internal Dictionary<Guid, Connection> InConnections { get; set; }
        internal Queue<ServerAcceptEvent> AcceptQueue { get; set; }
        internal Queue<ServerConnectEvent> AcceptWaiters { get; set; }

        // filter for incoming. For full pools, contains the oldest entry. 
        internal ConcurrentDictionary<string, DateTime> Filter { get; set; }

        internal TaskCompletionSource<bool> BroadcastFlag { get; set; } = new TaskCompletionSource<bool>();
        public void DoBroadcast() => BroadcastFlag.TrySetResult(true);

        public bool ShutdownImminent { get; set; } // if true, we are no longer initiating new connection. Used to prepare for shutdown.

        public Dispatcher(Uri FunctionAddress, string dispatcherIdPrefix, string dispatcherIdSuffix, ILogger logger)
        {
            Logger = logger;
            Worker = new Processor(this, shutdown.Token);
            this.FunctionAddress = FunctionAddress;
            DispatcherId = $"{dispatcherIdPrefix} {dispatcherIdSuffix}";
            ShortId = dispatcherIdPrefix;
            DispatcherIdBytes = GetBytes(DispatcherId);
            HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.Add("DispatcherId", DispatcherId);
            ChannelPools = new SortedDictionary<string, Queue<OutChannel>>();
            InChannelListeners = new ConcurrentDictionary<Guid, InChannel>();
            OutChannelWaiters = new List<DispatcherEvent>();
            ConnectRequests = new Dictionary<Guid, ClientConnectEvent>();
            OutConnections = new Dictionary<Guid, Connection>();
            InConnections = new Dictionary<Guid, Connection>();
            AcceptQueue = new Queue<ServerAcceptEvent>();
            AcceptWaiters = new Queue<ServerConnectEvent>();
            Filter = new ConcurrentDictionary<string, DateTime>();
        }

        public IEnumerable<string> Remotes =>
            this.ChannelPools.Keys      
            .Union(this.OutConnections.Values.Select(conn => conn.OutChannel.DispatcherId).Distinct())
            .Union(this.ConnectRequests.Values.Select(r => r.ToMachine).Distinct())
            .Union(this.InConnections.Values.Select(conn => conn.OutChannel.DispatcherId).Distinct())
            .Union(this.OutChannelWaiters.SelectMany(w => SingletonEnum(w.WaitsForDispatcher)))
            .OrderBy((dispatcherId) => dispatcherId.Split(' ')[1]);

        static IEnumerable<string> SingletonEnum(string s) => s == null ? Enumerable.Empty<string>() : new[] { s };

        public ValueTask StartAsync()
        {
            Worker.Submit(new TimerEvent());
            return default;
        }

        public ValueTask StopAsync()
        {
            try
            {
                Logger.LogError("Dispatcher {dispatcherId} is shutting down", DispatcherId);

                // todo close all connections etc.

                // cancel ongoing loops
                shutdown.Cancel();

                // todo wait for everything to exit
            }
            catch (Exception exception)
            {
                Logger.LogError("Dispatcher {dispatcherId} failed to shut down cleanly: {exception}", DispatcherId, exception);
            }

            return default;
        }

        public string PrintInformation(IEnumerable<string> remotes, bool printChannelMatrix, bool printConnectionMatrix)
        {
            string inCh;
            string outCh;
            string inConn;
            string outConn;

            if (printChannelMatrix)
            {
                var outList = string.Join(",", remotes
                    .Select(id => ChannelPools.TryGetValue(id, out var q) ? Blank(q.Count) : id == this.DispatcherId ? "X" : " "));
                var inChannels = InChannelListeners.Values.Where(c => c.DispatcherId != null).GroupBy(c => c.DispatcherId).ToDictionary(g => g.Key);
                var inList = string.Join(",", remotes
                    .Select(id => inChannels.TryGetValue(id, out var g) ? Blank(g.Count()) : id == this.DispatcherId ? "X" : " "));
                outCh = $"[{outList}]";
                inCh = $"[{inList}]";
            }
            else
            {
                outCh = Blank(ChannelPools.Sum(q => q.Value.Count));
                inCh = Blank(InChannelListeners.Values.Where(c => c.DispatcherId != null).Count());
            }

            if (printConnectionMatrix)
            {
                var inList = string.Join(",", remotes
                    .Select(id => id == this.DispatcherId ? "X" : Blank(InConnections.Values.Count(c => c.OutChannel?.DispatcherId == id))));
                var outList = string.Join(",", remotes
                    .Select(id => id == this.DispatcherId ? "X" : Blank(OutConnections.Values.Count(c => c.OutChannel?.DispatcherId == id))));
                inConn = $"[{inList}]";
                outConn = $"[{outList}]";
            }
            else
            {
                inConn = Blank(InConnections.Count);
                outConn = Blank(OutConnections.Count);
            }

            var mWaiters = string.Join(",", this.OutChannelWaiters.SelectMany(w => SingletonEnum(w.WaitsForMachine)).Distinct().OrderBy(s => s));

            return $"OutCh={outCh} InCh={inCh} OutConn={outConn} InConn={inConn} "
                + $"ConnReq={Blank(ConnectRequests.Count)} AcceptQ={Blank(AcceptQueue.Count)} AcceptW={Blank(AcceptWaiters.Count)} ChW={Blank(OutChannelWaiters.Count)} {mWaiters}";

            string Blank(int x) => x == 0 ? " " : x.ToString();
        }

        public string PrintInformation()
        {
            return this.PrintInformation(this.Remotes, true, false);
        }

        public override string ToString()
        {
            return $"Dispatcher {ShortId}";
        }

        static byte[] GetBytes(string s)
        {
            var m = new MemoryStream();
            using var b = new BinaryWriter(m);
            b.Write(s);
            b.Flush();
            return m.ToArray();
        }

        public HttpResponseMessage Dispatch(HttpRequestMessage requestMessage)
        {
            HttpResponseMessage httpResponseMessage = new HttpResponseMessage();
            httpResponseMessage.RequestMessage = requestMessage;
            httpResponseMessage.Headers.Add("DispatcherId", DispatcherId);

            try
            {
                string fromDispatcher = null;
                Guid channelId = default;

                if (requestMessage.Headers.TryGetValues("DispatcherId", out var values))
                {
                    var query = requestMessage.RequestUri.ParseQueryString();
                    fromDispatcher = values.FirstOrDefault();
                    string channelIdString = query["channelId"];
                    bool success = Guid.TryParse(channelIdString, out channelId);
                    if (!success)
                    {
                        Logger.LogError("{dispatcher} bad request {request}: cannot parse '{channelIdString}'", this, requestMessage.RequestUri, channelIdString);
                        httpResponseMessage.StatusCode = HttpStatusCode.BadRequest;
                        return httpResponseMessage;
                    }
                }

                if (fromDispatcher == null)
                {
                    // this is a request sent from external admin, to start or inquire
                    httpResponseMessage.StatusCode = requestMessage.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.Accepted;
                    httpResponseMessage.Content = new StringContent($"{this} {PrintInformation()}\n");
                }
                else
                {
                    if (fromDispatcher == DispatcherId)
                    {
                        // this is a request we ended up sending to ourself. So we are done with that.
                        httpResponseMessage.StatusCode = HttpStatusCode.NoContent;                 
                    }
                    else if (this.Filter.TryGetValue(fromDispatcher, out var nextRefresh) && DateTime.UtcNow < nextRefresh)
                    {
                        // this is a request sent from some other worker, for which we already have a full pool.
                        // We don't need to keep this connection going, so we respond with empty content.
                        httpResponseMessage.StatusCode = HttpStatusCode.OK;
                    }
                    else
                    {
                        // this is a request sent from some other worker. We keep the response channel open.
                        Logger.LogTrace("{dispatcher} {channelId} contacted by {destination}", this, channelId, fromDispatcher);
                        bool isFirst = !this.InChannelListeners.Values.Any(channel => channel.DispatcherId == fromDispatcher);
                        httpResponseMessage.StatusCode = HttpStatusCode.Accepted;
                        httpResponseMessage.Content = new PushStreamContent(async (stream, content, context) =>
                        {
                            try
                            {
                                var completionPromise = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                                var outChannel = new OutChannel();
                                outChannel.DispatcherId = fromDispatcher;
                                outChannel.ChannelId = channelId;
                                outChannel.Since = DateTime.UtcNow;

                                await stream.WriteAsync(DispatcherIdBytes);
                                await stream.FlushAsync();

                                outChannel.Stream = new StreamWrapper(stream, this, outChannel);
                                outChannel.TerminateResponse = () => completionPromise.TrySetResult(true);

                                Worker.Submit(new NewChannelEvent()
                                {
                                    OutChannel = outChannel,
                                });

                                await completionPromise.Task;
                                await stream.FlushAsync();
                                await stream.DisposeAsync();

                                Logger.LogTrace("{dispatcher} {channelId} out-channel disposed", this, channelId);
                            }
                            catch (Exception exception)
                            {
                                Logger.LogWarning("{dispatcher} {channelId} error in PushStreamContent: {exception}", this, channelId, exception);
                            }
                        });

                        if (isFirst)
                        {
                            this.DoBroadcast();
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.LogError("Dispatcher {dispatcherId} failed to handle request: {exception}", DispatcherId, exception);
                httpResponseMessage.StatusCode = HttpStatusCode.InternalServerError;
                httpResponseMessage.Content = new StringContent($"Dispatcher {DispatcherId} failed to handle request.");
            }

            return httpResponseMessage;
        }

        internal class Processor : BatchWorker<DispatcherEvent>
        {
            readonly Dispatcher dispatcher;

            public Processor(Dispatcher dispatcher, CancellationToken token) : base(false, 1000, token)
            {
                this.dispatcher = dispatcher;
            }

            protected override async Task Process(IList<DispatcherEvent> batch)
            {
                foreach (var dispatcherEvent in batch)
                {
                    try
                    {
                        await dispatcherEvent.ProcessAsync(dispatcher);
                    }
                    catch (Exception exception)
                    {
                        dispatcher.Logger.LogError("Dispatcher {dispatcherId} encountered exception while processing {event}: {exception}", dispatcher.DispatcherId, dispatcherEvent, exception);
                    }
                }
            }
        }
    }
}