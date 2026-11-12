using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SGSClient.UI.WinUI.Views;

/// <summary>
/// Keeps a page's content column at most <c>maxWidth</c> wide and centred in the visible frame.
/// Centring through MaxWidth inside the ScrollViewer put the profile pages ~300 px to the right
/// (the column was centred in an over-wide slot), so the position is set from the frame's real width.
/// </summary>
internal static class CenteredColumn
{
    public static void Attach(Page page, FrameworkElement column, double maxWidth)
    {
        column.HorizontalAlignment = HorizontalAlignment.Left;
        Frame? frame = null;

        void Apply()
        {
            if (frame is not { ActualWidth: > 0 } f)
            {
                return;
            }

            var width = Math.Min(maxWidth, f.ActualWidth);
            column.Width = width;
            column.Margin = new Thickness(Math.Max(0, (f.ActualWidth - width) / 2), 0, 0, 0);
        }

        void OnFrameSizeChanged(object sender, SizeChangedEventArgs e) => Apply();

        page.Loaded += (_, _) =>
        {
            frame = page.Frame;
            if (frame is not null)
            {
                frame.SizeChanged += OnFrameSizeChanged;
            }

            Apply();
        };
        page.Unloaded += (_, _) =>
        {
            if (frame is not null)
            {
                frame.SizeChanged -= OnFrameSizeChanged;
            }
        };
    }
}
