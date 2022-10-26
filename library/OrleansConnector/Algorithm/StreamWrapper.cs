// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector.Algorithm
{

    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using OrleansConnector;

    public class StreamWrapper : Stream
    {
        readonly Stream stream;
        readonly Dispatcher dispatcher;
        readonly Channel channel;

        public bool Closed => this.closed == 1;

        volatile int closed = 0;

        public StreamWrapper(Stream stream, Dispatcher dispatcher, Channel channel)
        {
            this.stream = stream;
            this.dispatcher = dispatcher;
            this.channel = channel;
        }

        void OnFailed(Exception e)
        {
            if (Interlocked.CompareExchange(ref closed, 1, 0) == 0)
            {
                dispatcher.Logger.LogDebug("{dispatcher} {channelId} channel failure: {message}", dispatcher, channel.ChannelId, e.Message);

                dispatcher.Worker.Submit(new ChannelClosedEvent()
                {
                    ChannelId = channel.ChannelId,
                    DispatcherId = channel.DispatcherId,
                    Channel = channel,
                });
            }      
        }

        void OnClosed()
        {
            if (Interlocked.CompareExchange(ref closed, 1, 0) == 0)
            {
                dispatcher.Logger.LogDebug("{dispatcher} {channelId} channel closed", dispatcher, channel.ChannelId);

                dispatcher.Worker.Submit(new ChannelClosedEvent()
                {
                    ChannelId = channel.ChannelId,
                    DispatcherId = channel.DispatcherId,
                    Channel = channel,
                });
            }      
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                return stream.Read(buffer, offset, count);
            }
            catch(ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            try
            {
                stream.Write(buffer, offset, count);
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            try
            {
                await this.stream.CopyToAsync(destination, bufferSize, cancellationToken);
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try
            {
                return await this.stream.ReadAsync(buffer, cancellationToken);
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            try
            {
                await this.stream.FlushAsync(cancellationToken);
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                 return await this.stream.ReadAsync(buffer, offset, count, cancellationToken);
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                await this.stream.WriteAsync(buffer, offset, count, cancellationToken);
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try
            {
                await this.stream.WriteAsync(buffer, cancellationToken);
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override async ValueTask DisposeAsync()
        {
            try
            {
                await this.FlushAsync();
                this.OnClosed();
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            try
            {
                this.stream.CopyTo(destination, bufferSize);
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override void Close()
        {
            try
            {
                this.Flush();
                this.OnClosed();
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override int Read(Span<byte> buffer)
        {
            try
            {
                return this.stream.Read(buffer);
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override int ReadByte()
        {
            try
            {
                return this.stream.ReadByte();
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            try
            {
                this.stream.Write(buffer);
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override void WriteByte(byte value)
        {
            try
            {
                this.stream.WriteByte(value);
            }
            catch (ObjectDisposedException e)
            {
                this.OnFailed(e);
                throw;
            }
            catch (IOException e)
            {
                this.OnFailed(e);
                throw;
            }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            // TODO hoping Orleans does not call these
            var e = new NotImplementedException("BeginRead");
            this.OnFailed(e);
            throw e;
            //return this.stream.BeginRead(buffer, offset, count, callback, state); 
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            // TODO hoping Orleans does not call these
            var e = new NotImplementedException("BeginWrite");
            this.OnFailed(e);
            throw e;
            //return this.stream.BeginWrite(buffer, offset, count, callback, state); 
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            // TODO hoping Orleans does not call these
            var e = new NotImplementedException("EndRead");
            this.OnFailed(e);
            throw e;
            //return this.stream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            // TODO hoping Orleans does not call these
            var e = new NotImplementedException("EndWrite");
            this.OnFailed(e);
            throw e;
            //this.stream.EndWrite(asyncResult);
        }

        public override bool CanTimeout => this.stream.CanTimeout;

        public override int ReadTimeout { get => this.stream.ReadTimeout; set => this.stream.ReadTimeout = value; }
 
        public override int WriteTimeout { get => this.stream.WriteTimeout; set => this.stream.WriteTimeout = value; }

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => stream.CanWrite;

        public override long Length => stream.Length;

        public override long Position { get => stream.Position; set => stream.Position = value; }

        public override void Flush()
        {
            stream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }
    }
}
