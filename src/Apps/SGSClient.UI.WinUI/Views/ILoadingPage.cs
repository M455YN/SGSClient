namespace SGSClient.UI.WinUI.Views;

/// <summary>
/// A page that loads its data after navigation. The shell keeps its loading overlay up until
/// <see cref="Ready"/> completes, so the page is never shown half-filled.
/// </summary>
public interface ILoadingPage
{
    /// <summary>Set in OnNavigatedTo; completes when the page's first data load has finished.</summary>
    Task Ready { get; }
}
