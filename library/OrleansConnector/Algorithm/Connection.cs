// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector.Algorithm
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    /// <summary>
    /// one end of a bidirectional connection
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// A unique identifier for this connection
        /// </summary>
        public Guid ConnectionId { get; internal set; }

        /// <summary>
        /// The incoming stream
        /// </summary>
        public Stream InStream => InChannel.Stream;

        /// <summary>
        /// The outgoing stream
        /// </summary>
        public Stream OutStream => OutChannel.Stream;

        /// <summary>
        /// Whether we are on the server end of the connection (i.e. the place that called accept)
        /// or the client end (i.e. the place that called connect)
        /// </summary>
        public bool IsServerSide { get; internal set; }

        public event Action OnFailure;

        // the channel pair
        internal InChannel InChannel;
        internal OutChannel OutChannel;

        internal void FailureNotify()
        {
            if (this.OnFailure != null)
            {
                Task.Run(() => this.OnFailure());
            }
        }

        internal bool ContainsChannel(Channel channel)
            => channel == this.InChannel || channel == this.OutChannel;
    }
}
