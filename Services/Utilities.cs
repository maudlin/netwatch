using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Netwatch.Services
{
    // Simple ICommand implementation
    public sealed class RelayCommand : ICommand
    {
        private readonly Func<object?, bool> _canExecute;
        private readonly Action<object?> _execute;

        public RelayCommand(Action<object?> execute, Func<object?, bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (_ => true);
        }

        public bool CanExecute(object? parameter) => _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }

    // Infinite stream of zero bytes until cancellation is requested
    public sealed class InfiniteStream : Stream
    {
        private readonly CancellationToken _ct;
        public InfiniteStream(CancellationToken ct) { _ct = ct; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_ct.IsCancellationRequested) return 0; // EOF when canceled
            int n = Math.Max(0, Math.Min(count, buffer.Length - offset));
            if (n > 0) Array.Clear(buffer, offset, n);
            // Small delay to avoid pegging CPU
            Task.Delay(1, _ct).Wait(_ct);
            return n;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

