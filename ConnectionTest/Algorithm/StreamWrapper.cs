// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ConnectionTest.Algorithm
{
     
    using System;
    using System.IO;

    public class StreamWrapper : Stream
    {
        readonly Stream stream;
        readonly Dispatcher dispatcher;
        readonly Channel channel;

        public bool Failed { get; private set; }

        public StreamWrapper(Stream stream, Dispatcher dispatcher, Channel channel)
        {
            this.stream = stream;
            this.dispatcher = dispatcher;
            this.channel = channel;
        }

        void OnFailed()
        {
            dispatcher.Worker.Submit(new ChannelFailedEvent()
            {
                Channel = channel,
            });
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

        #region Unmodified

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

        #endregion
    }
}
