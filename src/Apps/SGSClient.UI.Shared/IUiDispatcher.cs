namespace SGSClient.UI;

/// <summary>
/// UI-thread access for shared ViewModels. WinUI and Avalonia each provide an implementation,
/// so ViewModels never reference a UI framework.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Runs <paramref name="action"/> on the UI thread; may run inline when no dispatcher is available.</summary>
    void Post(Action action);

    /// <summary>Creates a stopped repeating timer whose ticks run on the UI thread.</summary>
    IUiTimer CreateTimer(TimeSpan interval, Func<Task> onTick);
}

public interface IUiTimer
{
    void Start();
    void Stop();
}
