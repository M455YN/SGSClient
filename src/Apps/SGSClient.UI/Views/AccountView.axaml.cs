using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class AccountView : UserControl
{
    public AccountView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindWindowHandle();
        AttachedToVisualTree += (_, _) => BindWindowHandle();
    }

    private void BindWindowHandle()
    {
        if (DataContext is AccountViewModel vm)
        {
            vm.SetParentWindowHandleProvider(() =>
                TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? 0);
        }
    }

    private async void SignInButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm)
        {
            await vm.LoginCommand.ExecuteAsync(null);
            RefreshSidebar();
        }
    }

    private async void SignOutButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm)
        {
            await vm.LogoutCommand.ExecuteAsync(null);
            RefreshSidebar();
        }
    }

    private void EditButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm)
        {
            vm.BeginEditCommand.Execute(null);
        }
    }

    private void CancelEditButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm)
        {
            vm.CancelEditCommand.Execute(null);
        }
    }

    private async void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm)
        {
            await vm.SaveProfileCommand.ExecuteAsync(null);
            RefreshSidebar();
        }
    }

    private async void ChangeAvatar_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AccountViewModel vm)
        {
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Wybierz zdjęcie profilowe",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Obrazy")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"]
                }
            ]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await vm.UploadAvatarFromPathAsync(path);
        RefreshSidebar();
    }

    private async void ClearAvatar_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm)
        {
            await vm.ClearAvatarCommand.ExecuteAsync(null);
            RefreshSidebar();
        }
    }

    private void RefreshSidebar()
    {
        if (TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel main)
        {
            main.Sidebar.RefreshProfile();
        }
    }
}
