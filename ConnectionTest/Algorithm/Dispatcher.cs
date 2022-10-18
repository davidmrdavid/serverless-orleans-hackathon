// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class Dispatcher
    {  
        // singleton instance of the dispatcher
        static Dispatcher dispatcher;

        // state of the dispatcher
        public CancellationToken HostShutdownToken { get; }
        public ILogger Logger { get; }
        public Dispatcher.Processor Worker { get; }
        public string Id { get; }

        public static async Task<HttpResponseMessage> DispatchAsync(HttpRequestMessage requestMessage, CancellationToken hostShutdownToken, ILogger logger)
        {
            // start the dispatcher if we haven't already on this worker
            if (dispatcher == null && requestMessage.Method != HttpMethod.Get)
            {
                Uri serviceAddress;
                try
                {
                    serviceAddress = new Uri(await requestMessage.Content.ReadAsStringAsync());
                }
                catch (Exception)
                {
                    HttpResponseMessage httpResponseMessage = new HttpResponseMessage();
                    httpResponseMessage.StatusCode = HttpStatusCode.BadRequest;
                    httpResponseMessage.RequestMessage = requestMessage;
                    httpResponseMessage.Content = new StringContent("request content must be a valid service address.\n");
                    return httpResponseMessage;
                }

                // use an interlocked operation to prevent two dispatchers being used
                var newDispatcher = new Dispatcher(hostShutdownToken, logger);

                if (Interlocked.CompareExchange(ref dispatcher, newDispatcher, null) == null)
                {
                    newDispatcher.Start(serviceAddress);
                }
            }

            var query = requestMessage.RequestUri.ParseQueryString();
            string from = query["from"];

            if (from == null)
            {
                // this is a request sent from external admin, to start or inquire
                HttpResponseMessage httpResponseMessage = new HttpResponseMessage();
                httpResponseMessage.StatusCode = requestMessage.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.Accepted;
                httpResponseMessage.RequestMessage = requestMessage;
                httpResponseMessage.Content = new StringContent(dispatcher?.PrintInformation() ?? "not started. Need to POST first, with service Url as argument.\n");
                return httpResponseMessage;
            }
            else
            {
                // this is a request sent from some other worker
                var triggerEvent = new RemoteTriggerEvent()
                {
                    Request = requestMessage,
                    Response = new TaskCompletionSource<HttpResponseMessage>(),
                };
                dispatcher.Worker.Submit(triggerEvent);
                return await triggerEvent.Response.Task;
            }
        }

        Dispatcher(CancellationToken hostShutdownToken, ILogger logger)
        {
            this.Logger = logger;
            this.Worker = new Processor(this, hostShutdownToken);
            this.HostShutdownToken = hostShutdownToken;
            this.Id = $"{Environment.MachineName} {DateTime.UtcNow:o}";
        }

        public string PrintInformation()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{this} silostartup={Silo.GetSiloAsync().Status}");
            return sb.ToString();
        }

        public override string ToString()
        {
            return $"{this.Id}";
        }

        public void Start(Uri serviceAddress)
        {
            this.Worker.Submit(new StartEvent() { ServiceAddress = serviceAddress });
            this.Worker.Submit(new TimerEvent());
            this.Worker.Resume();

            // start orleans silo
            Silo.StartOnThreadpool(this);
        }

        public class Processor : BatchWorker<DispatcherEvent>
        {
            readonly Dispatcher dispatcher;

            public Processor(Dispatcher dispatcher, CancellationToken token) : base(true, 1000, token)
            {
                this.dispatcher = dispatcher;
            }

            protected override Task Process(IList<DispatcherEvent> batch)
            {
                foreach (var dispatcherEvent in batch)
                {
                    dispatcherEvent.Process(dispatcher);
                }

                return Task.CompletedTask;
            }
        }
    }
}
