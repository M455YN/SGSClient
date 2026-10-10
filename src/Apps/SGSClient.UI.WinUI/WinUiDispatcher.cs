using Microsoft.UI.Dispatching;

namespace SGSClient.UI.WinUI;

/// <summary><see cref="IUiDispatcher"/> backed by the WinUI <see cref="DispatcherQueue"/>.</summary>
public sealed class WinUiDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue? _queue = DispatcherQueue.GetForCurrentThread();

    public void Post(Action action)
    {
        if (_queue is null || !_queue.TryEnqueue(() => action()))
        {
            action();
        }
    }

    public IUiTimer CreateTimer(TimeSpan interval, Func<Task> onTick)
    {
        var timer = _queue?.CreateTimer();
        if (timer is null)
        {
            return new NullTimer();
        }

        timer.Interval = interval;
        timer.Tick += async (_, _) => await onTick();
        return new Timer(timer);
    }

    private sealed class Timer(DispatcherQueueTimer inner) : IUiTimer
    {
        public void Start() => inner.Start();
        public void Stop() => inner.Stop();
    }

    private sealed class NullTimer : IUiTimer
    {
        public void Start()
        {
        }

        public void Stop()
        {
        }
    }
}
