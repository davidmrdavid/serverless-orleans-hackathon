// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace OrleansConnector.Algorithm
{

    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OrleansConnector;

    public class StreamWrapper : Stream
    {
        readonly Stream stream;
        readonly Dispatcher dispatcher;
        readonly Channel channel;

        public bool Failed => this.failed == 1;

        volatile int failed = 0;

        public StreamWrapper(Stream stream, Dispatcher dispatcher, Channel channel)
        {
            this.stream = stream;
            this.dispatcher = dispatcher;
            this.channel = channel;
        }

        void OnFailed()
        {
            if (Interlocked.CompareExchange(ref failed, 1, 0) == 0)
            {
                dispatcher.Worker.Submit(new ChannelFailedEvent()
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
            catch(ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            try
            {
                stream.Write(buffer, offset, count);
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            try
            {
                await this.stream.CopyToAsync(destination, bufferSize, cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try
            {
                return await this.stream.ReadAsync(buffer, cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            try
            {
                await this.stream.FlushAsync(cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                 return await this.stream.ReadAsync(buffer, offset, count, cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                await this.stream.WriteAsync(buffer, offset, count, cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try
            {
                await this.stream.WriteAsync(buffer, cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override async ValueTask DisposeAsync()
        {
            try
            {
                await this.stream.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            try
            {
                this.stream.CopyTo(destination, bufferSize);
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override void Close()
        {
            try
            {
                this.stream.Close();
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override int Read(Span<byte> buffer)
        {
            try
            {
                return this.stream.Read(buffer);
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override int ReadByte()
        {
            try
            {
                return this.stream.ReadByte();
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            try
            {
                this.stream.Write(buffer);
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override void WriteByte(byte value)
        {
            try
            {
                this.stream.WriteByte(value);
            }
            catch (ObjectDisposedException)
            {
                this.OnFailed();
                throw;
            }
            catch (IOException)
            {
                this.OnFailed();
                throw;
            }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            // TODO hoping Orleans does not call these
            throw new NotImplementedException();
            //return this.stream.BeginRead(buffer, offset, count, callback, state); 
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            // TODO hoping Orleans does not call these
            throw new NotImplementedException();
            //return this.stream.BeginWrite(buffer, offset, count, callback, state); 
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return this.stream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            this.stream.EndWrite(asyncResult);
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
