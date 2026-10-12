using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace SGSClient.UI.WinUI;

/// <summary>One line of the tray menu; a null <see cref="Click"/> makes it a separator.</summary>
public sealed record TrayMenuItem(string Text, Action? Click);

/// <summary>
/// The tray context menu as a small borderless window with the Windows 11 Mica look. It closes as
/// soon as it loses focus, like a native menu. H.NotifyIcon's own SecondWindow mode renders empty.
/// </summary>
public sealed class TrayMenuWindow : Window
{
    private const double Width = 260;
    private const double RowHeight = 36;
    private const double SeparatorHeight = 9;
    private const double Padding = 6;

    private TrayMenuWindow(IReadOnlyList<TrayMenuItem> items)
    {
        SystemBackdrop = new MicaBackdrop();
        Title = "SGSClient";

        var panel = new StackPanel { Padding = new Thickness(Padding), Spacing = 0 };
        foreach (var item in items)
        {
            if (item.Click is null)
            {
                panel.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(8, (SeparatorHeight - 1) / 2, 8, (SeparatorHeight - 1) / 2),
                    Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"]
                });
                continue;
            }

            var captured = item.Click;
            var button = new Button
            {
                Content = item.Text,
                Height = RowHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 0, 12, 0),
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            button.Click += (_, _) =>
            {
                Close();
                captured();
            };
            panel.Children.Add(button);
        }

        Content = panel;
        Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
                Close();
            }
        };
    }

    /// <summary>Shows the menu with its bottom edge at the cursor, kept inside the work area.</summary>
    public static void ShowAtCursor(IReadOnlyList<TrayMenuItem> items)
    {
        var window = new TrayMenuWindow(items);
        var hwnd = WindowNative.GetWindowHandle(window);
        var presenter = OverlappedPresenter.CreateForContextMenu();
        presenter.IsAlwaysOnTop = true; // the tray overflow popup is topmost and would cover the menu
        window.AppWindow.SetPresenter(presenter);
        window.AppWindow.IsShownInSwitchers = false;

        var scale = GetDpiForWindow(hwnd) / 96.0;
        var rows = items.Count(i => i.Click is not null);
        var separators = items.Count - rows;
        var width = (int)(Width * scale);
        var height = (int)((rows * RowHeight + separators * SeparatorHeight + 2 * Padding) * scale);

        GetCursorPos(out var cursor);
        var area = DisplayArea.GetFromPoint(
            new global::Windows.Graphics.PointInt32(cursor.X, cursor.Y),
            DisplayAreaFallback.Primary).WorkArea;
        var x = Math.Clamp(cursor.X - width / 2, area.X, area.X + area.Width - width);
        // Clicked from the overflow popup: sit above the whole popup, not just above the cursor.
        var bottom = cursor.Y;
        var overflow = FindWindow("NotifyIconOverflowWindow", null);
        if (overflow != 0 && IsWindowVisible(overflow) && GetWindowRect(overflow, out var rect) && rect.Top < bottom)
        {
            bottom = rect.Top - (int)(8 * scale);
        }

        var y = Math.Clamp(bottom - height, area.Y, area.Y + area.Height - height);

        window.AppWindow.MoveAndResize(new global::Windows.Graphics.RectInt32(x, y, width, height));
        window.Activate();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
