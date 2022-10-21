// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Azure;
    using global::ConnectionTest;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class Dispatcher : IDisposable, IAsyncDisposable
    {
        internal CancellationToken HostShutdownToken { get; }
        internal ILogger Logger { get; }
        internal Dispatcher.Processor Worker { get; }
        public string DispatcherId { get; }
        public string ShortId { get; }
        internal byte[] DispatcherIdBytes { get; }
        public Uri FunctionAddress { get; }
        public HttpClient HttpClient { get; }

        IDisposable cancellationTokenRegistration;

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

        public Dispatcher(Uri FunctionAddress, string dispatcherIdPrefix, string dispatcherIdSuffix, ILogger logger, CancellationToken hostShutdownToken)
        {
            this.Logger = logger;
            this.Worker = new Processor(this, hostShutdownToken);
            this.HostShutdownToken = hostShutdownToken;
            this.FunctionAddress = FunctionAddress;
            this.DispatcherId = $"{dispatcherIdPrefix} {dispatcherIdSuffix}";
            this.ShortId = dispatcherIdPrefix;
            this.DispatcherIdBytes = GetBytes(this.DispatcherId);
            this.HttpClient = new HttpClient();
            this.HttpClient.DefaultRequestHeaders.Add("DispatcherId", this.DispatcherId);
            this.ChannelPools = new SortedDictionary<string, Queue<OutChannel>>();
            this.InChannelListeners = new ConcurrentDictionary<Guid, InChannel>();
            this.OutChannelWaiters = new List<DispatcherEvent>();
            this.ConnectRequests = new Dictionary<Guid, ClientConnectEvent>();
            this.OutConnections = new Dictionary<Guid, Connection>();
            this.InConnections = new Dictionary<Guid, Connection>();
            this.AcceptQueue = new Queue<ServerAcceptEvent>();
            this.AcceptWaiters = new Queue<ServerConnectEvent>();
            this.cancellationTokenRegistration = this.HostShutdownToken.Register(this.Dispose);
        }

        public void StartChannels()
        {
            this.Worker.Submit(new TimerEvent());
        }

        public string PrintInformation(bool includeIdentity = false)
        {
            var poolSizes = string.Join(",", this.ChannelPools.Values.Select(q => q.Count.ToString()));
            var chListeners = string.Join(",", this.InChannelListeners.Values.Where(c => c.DispatcherId != null).GroupBy(c => c.DispatcherId).Select(g => g.Count()));
            return $"{(includeIdentity? this.ToString() + " " : "")}ChOut=[{poolSizes}] ChIn=[{chListeners}] ChW={this.OutChannelWaiters.Count} ConnReq={this.ConnectRequests.Count} "
                + $"acceptQ={this.AcceptQueue.Count} acceptW={this.AcceptWaiters.Count} outConn={this.OutConnections.Count} inConn={this.InConnections.Count}";
        }

        public override string ToString()
        {
            return $"Dispatcher {this.ShortId}";
        }

        public void Dispose()
        {
            Task.Run(() => this.DisposeAsync());
        }

        public ValueTask DisposeAsync()
        {
            this.cancellationTokenRegistration?.Dispose();
            return this.ShutdownAsync();
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
            httpResponseMessage.Headers.Add("DispatcherId", this.DispatcherId);

            //var query = requestMessage.RequestUri.ParseQueryString();
            //string from = query["from"];

            try
            {
                string from = null;
                Guid channelId = default;

                if (requestMessage.Headers.TryGetValues("DispatcherId", out var values))
                {
                    from = values.FirstOrDefault();
                    var query = requestMessage.RequestUri.ParseQueryString();
                    string channelIdString = query["channelId"];
                    channelId = Guid.Parse(channelIdString);
                }

                if (from == null)
                {
                    // this is a request sent from external admin, to start or inquire
                    httpResponseMessage.StatusCode = requestMessage.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.Accepted;
                    httpResponseMessage.Content = new StringContent(this?.PrintInformation(true) ?? "not started. Need to POST first, with service Url as argument.\n");
                }
                else
                {
                    if (from == this.DispatcherId)
                    {
                        // this is a request we ended up sending to ourself. So we are done with that.
                        httpResponseMessage.StatusCode = HttpStatusCode.OK;
                    }
                    else
                    {
                        // this is a request sent from some other worker. We keep the response channel open.
                        httpResponseMessage.StatusCode = HttpStatusCode.Accepted;
                        httpResponseMessage.Content = new PushStreamContent(async (Stream stream, HttpContent content, TransportContext context) =>
                        {
                            var completionPromise = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                            var outChannel = new OutChannel();
                            outChannel.DispatcherId = from;
                            outChannel.ChannelId = channelId;

                            await stream.WriteAsync(this.DispatcherIdBytes);
                            await stream.FlushAsync();

                            outChannel.Stream = new StreamWrapper(stream, this, outChannel);
                            outChannel.TerminateResponse = () => completionPromise.TrySetResult(true);
                            
                            this.Worker.Submit(new NewChannelEvent()
                            {
                                OutChannel = outChannel,
                            });

                            await completionPromise.Task;
                            await stream.DisposeAsync();
                        });
                    }

                }
            }
            catch (Exception exception)
            {
                this.Logger.LogError("Dispatcher {dispatcherId} failed to handle request: {exception}", this.DispatcherId, exception);
                httpResponseMessage.StatusCode = HttpStatusCode.InternalServerError;
                httpResponseMessage.Content = new StringContent($"Dispatcher {this.DispatcherId} failed to handle request.");
            }

            return httpResponseMessage;
        }

        ValueTask ShutdownAsync()
        {
            try
            {
                //TODO close and dispose everything
            }
            catch (Exception exception)
            {
                this.Logger.LogError("Dispatcher {dispatcherId} failed to shut down cleanly: {exception}", this.DispatcherId, exception);
            }

            return default;
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
                try
                {
                    foreach (var dispatcherEvent in batch)
                    {
                        await dispatcherEvent.ProcessAsync(dispatcher);
                    }
                }
                catch (Exception exception)
                {
                    this.dispatcher.Logger.LogError("Dispatcher {dispatcherId} encountered exception in processor: {exception}", this.dispatcher.DispatcherId, exception);
                }
            }
        }
    }
}