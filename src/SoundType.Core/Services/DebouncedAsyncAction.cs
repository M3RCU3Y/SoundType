namespace SoundType.Core.Services;

public sealed class DebouncedAsyncAction : IAsyncDisposable
{
    private readonly TimeSpan _delay;
    private readonly Func<CancellationToken, Task> _action;
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _pendingCancellation;
    private Task? _pendingTask;
    private bool _disposed;

    public DebouncedAsyncAction(TimeSpan delay, Func<CancellationToken, Task> action)
    {
        _delay = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        _action = action;
    }

    public void Schedule()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            _pendingCancellation?.Cancel();
            CancellationTokenSource cancellation = new();
            _pendingCancellation = cancellation;
            _pendingTask = RunAfterDelayAsync(cancellation);
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? pendingCancellation;
        Task? pendingTask;
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            pendingCancellation = _pendingCancellation;
            pendingTask = _pendingTask;
            _pendingCancellation = null;
            _pendingTask = null;
        }

        if (pendingCancellation is null)
        {
            return;
        }

        pendingCancellation.Cancel();
        if (pendingTask is not null)
        {
            try
            {
                await pendingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        await RunActionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await FlushAsync().ConfigureAwait(false);
        lock (_syncRoot)
        {
            _disposed = true;
        }

        _runGate.Dispose();
    }

    private async Task RunAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(_delay, cancellation.Token).ConfigureAwait(false);
            lock (_syncRoot)
            {
                if (!ReferenceEquals(_pendingCancellation, cancellation))
                {
                    return;
                }

                _pendingCancellation = null;
                _pendingTask = null;
            }

            await RunActionAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private async Task RunActionAsync(CancellationToken cancellationToken)
    {
        await _runGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _action(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _runGate.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DebouncedAsyncAction));
        }
    }
}
