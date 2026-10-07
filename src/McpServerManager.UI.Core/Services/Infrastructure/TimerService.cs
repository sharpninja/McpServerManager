using System;
using System.Threading;
using System.Threading.Tasks;
using McpServerManager.UI.Core.Services;

namespace McpServerManager.UI.Core.Services.Infrastructure;

/// <summary>
/// Host implementation of <see cref="ITimerService"/> backed by <see cref="System.Threading.Timer"/>.
/// </summary>
public sealed class TimerService : ITimerService
{
    public ITimerHandle CreateRecurring(TimeSpan interval, Func<CancellationToken, Task> callback)
        => new TimerHandle(interval, callback, recurring: true);

    public ITimerHandle CreateOneShot(TimeSpan delay, Func<CancellationToken, Task> callback)
        => new TimerHandle(delay, callback, recurring: false);

    private sealed class TimerHandle : ITimerHandle
    {
        // Serializes the start of each callback invocation against Stop/Restart/Dispose.
        // System.Threading.Timer.Change does not wait for queued or in-flight callbacks,
        // so without this gate a tick that passed its checks could still invoke the
        // callback after Stop() or Dispose() returned (HV-R4-01). The gate is re-entrant,
        // so a callback may stop, restart, or dispose its own handle.
        private readonly object _gate = new();
        private readonly Func<CancellationToken, Task> _callback;
        private readonly bool _recurring;
        private Timer? _timer;
        private CancellationTokenSource? _cts;
        private TimeSpan _interval;
        private volatile bool _running;

        public TimerHandle(TimeSpan interval, Func<CancellationToken, Task> callback, bool recurring)
        {
            _interval = interval;
            _callback = callback;
            _recurring = recurring;
            _cts = new CancellationTokenSource();
            _running = true;
            _timer = new Timer(OnTick, null, interval, recurring ? interval : Timeout.InfiniteTimeSpan);
        }

        private async void OnTick(object? state)
        {
            Task pending;
            lock (_gate)
            {
                var cts = _cts;
                if (!_running || cts is null || cts.IsCancellationRequested)
                    return;

                try
                {
                    pending = _callback(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }

        public void Stop()
        {
            // Close the gate first so ticks waiting on the lock bail out, then wait for any
            // invocation currently inside the gate to finish starting.
            _running = false;
            lock (_gate)
            {
                _running = false;
                _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
        }

        public void Restart(TimeSpan? newInterval = null)
        {
            lock (_gate)
            {
                if (newInterval.HasValue)
                    _interval = newInterval.Value;

                if (_timer is null)
                    return;

                _running = true;
                _timer.Change(_interval, _recurring ? _interval : Timeout.InfiniteTimeSpan);
            }
        }

        public void Dispose()
        {
            _running = false;
            Timer? timer;
            CancellationTokenSource? cts;
            lock (_gate)
            {
                _running = false;
                timer = _timer;
                _timer = null;
                cts = _cts;
                _cts = null;
            }

            cts?.Cancel();
            cts?.Dispose();
            timer?.Dispose();
        }
    }
}