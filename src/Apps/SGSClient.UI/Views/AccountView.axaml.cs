using SGSClient.Core.Localization;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class AccountView : UserControl
{
    private void OpenLibraryAchievements_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm && sender is Control { Tag: RecentGameItem item })
        {
            vm.OpenLibraryAchievementsCommand.Execute(item);
        }
    }

    private void OpenGameAchievements_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm && sender is Control { Tag: AchievementSummaryItem item })
        {
            vm.OpenGameAchievementsCommand.Execute(item);
        }
    }

    public AccountView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            BindWindowHandle();
            WatchAvatar();
            WatchAchievements();
        };
        AttachedToVisualTree += (_, _) => BindWindowHandle();
    }

    private AccountViewModel? _achievementsHost;

    private void WatchAchievements()
    {
        if (DataContext is not AccountViewModel vm || ReferenceEquals(vm, _achievementsHost))
        {
            return;
        }

        _achievementsHost = vm;
        vm.AchievementsRequested += async (gameId, title) =>
        {
            var modal = await vm.CreateAchievementsModalAsync(gameId, title);
            var dialog = new FluentAvalonia.UI.Controls.FAContentDialog
            {
                Content = new AchievementsModal { DataContext = modal },
                CloseButtonText = Loc.T("Zamknij"),
                DefaultButton = FluentAvalonia.UI.Controls.FAContentDialogButton.Close
            };
            await dialog.ShowAsync(TopLevel.GetTopLevel(this));
        };
    }

    private AccountViewModel? _watched;
    private string? _loadedAvatarUrl;

    /// <summary>The shared ViewModel only exposes <c>AvatarUrl</c>; this view turns it into a bitmap.</summary>
    private void WatchAvatar()
    {
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _watched = DataContext as AccountViewModel;
        if (_watched is null)
        {
            return;
        }

        _watched.PropertyChanged += OnViewModelPropertyChanged;
        _ = LoadAvatarAsync(_watched.AvatarUrl);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccountViewModel.AvatarUrl) && _watched is not null)
        {
            _ = LoadAvatarAsync(_watched.AvatarUrl);
        }
    }

    private async Task LoadAvatarAsync(string? url)
    {
        if (string.Equals(url, _loadedAvatarUrl, StringComparison.Ordinal))
        {
            return;
        }

        _loadedAvatarUrl = url;
        Bitmap? bitmap = null;
        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                if (File.Exists(url))
                {
                    bitmap = new Bitmap(url);
                }
                else
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                    await using var stream = await http.GetStreamAsync(url);
                    await using var memory = new MemoryStream();
                    await stream.CopyToAsync(memory);
                    memory.Position = 0;
                    bitmap = new Bitmap(memory);
                }
            }
            catch
            {
                bitmap = null;
            }
        }

        // Ignore a result that was superseded while downloading.
        if (!string.Equals(url, _loadedAvatarUrl, StringComparison.Ordinal))
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => AvatarImage.Source = bitmap);
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
            Title = Loc.T("Wybierz zdjęcie profilowe"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Loc.T("Obrazy"))
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"]
                }
            ]
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        var path = file.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            await vm.UploadAvatarFromPathAsync(path);
        }
        else
        {
            await using var stream = await file.OpenReadAsync();
            await vm.UploadAvatarFromStreamAsync(stream, file.Name ?? "avatar.png");
        }

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

    private async void PostWall_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm)
        {
            await vm.PostWallCommentCommand.ExecuteAsync(null);
        }
    }

    private async void DeleteWall_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm && sender is Control { Tag: ProfileCommentItem comment })
        {
            await vm.DeleteWallCommentCommand.ExecuteAsync(comment);
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
