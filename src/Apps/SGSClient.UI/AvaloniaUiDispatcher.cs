using Avalonia.Threading;

namespace SGSClient.UI;

/// <summary><see cref="IUiDispatcher"/> backed by the Avalonia UI thread.</summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);

    public IUiTimer CreateTimer(TimeSpan interval, Func<Task> onTick)
    {
        var timer = new DispatcherTimer { Interval = interval };
        timer.Tick += async (_, _) => await onTick();
        return new Timer(timer);
    }

    private sealed class Timer(DispatcherTimer inner) : IUiTimer
    {
        public void Start() => inner.Start();
        public void Stop() => inner.Stop();
    }
}
