using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkHUD.Services
{
    // A stream that produces zero bytes indefinitely until cancellation.
    internal sealed class InfiniteStream : Stream
    {
        private readonly CancellationToken _ct;
        public InfiniteStream(CancellationToken ct) { _ct = ct; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
        {
            // Ignore writes; this stream is used only as a data source by HttpClient
        }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // backpressure by delaying if not cancelled
            await Task.Delay(50, cancellationToken);
        }
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Delay(50, cancellationToken);
        }
    }
}

