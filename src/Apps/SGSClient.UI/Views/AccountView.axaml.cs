using SGSClient.Core.Localization;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input.Platform;
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

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            AvatarImage.Source = bitmap;
            _avatarBitmap = bitmap;
            _editForm?.SetAvatar(bitmap);
        });
    }

    private Bitmap? _avatarBitmap;
    private ProfileEditForm? _editForm;

    private async void EditButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AccountViewModel vm)
        {
            return;
        }

        vm.BeginEditCommand.Execute(null);
        var form = new ProfileEditForm { DataContext = vm };
        form.SetAvatar(_avatarBitmap);
        form.AvatarChanged += RefreshSidebar;
        _editForm = form;

        var dialog = new FluentAvalonia.UI.Controls.FAContentDialog
        {
            Title = Loc.T("Edytuj profil"),
            Content = form,
            PrimaryButtonText = Loc.T("Zapisz"),
            CloseButtonText = Loc.T("Anuluj"),
            DefaultButton = FluentAvalonia.UI.Controls.FAContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                await vm.SaveProfileCommand.ExecuteAsync(null);

                // A failed save leaves the editor open with the reason in AccountStatus.
                if (vm.IsEditing)
                {
                    args.Cancel = true;
                    form.ShowError(vm.AccountStatus);
                }
            }
            catch (Exception ex)
            {
                args.Cancel = true;
                form.ShowError(ex.Message);
            }
            finally
            {
                deferral.Complete();
            }
        };

        await dialog.ShowAsync(TopLevel.GetTopLevel(this));
        _editForm = null;
        if (vm.IsEditing)
        {
            vm.CancelEditCommand.Execute(null);
        }

        RefreshSidebar();
    }

    private MainWindowViewModel? Main => TopLevel.GetTopLevel(this)?.DataContext as MainWindowViewModel;

    private void OpenLibrary_OnClick(object? sender, RoutedEventArgs e) => Main?.NavigateCommand.Execute("Library");

    private void OpenStore_OnClick(object? sender, RoutedEventArgs e) => Main?.NavigateCommand.Execute("Games");

    private void OpenGame_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: RecentGameItem { GameId: > 0 } item })
        {
            Main?.OpenGameDetails(item.GameId, "Library");
        }
    }

    private void OpenFriendProfile_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: SocialPersonItem person })
        {
            Main?.OpenUserProfile(person.UserId);
        }
    }

    private void OpenAuthor_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: ProfileCommentItem comment })
        {
            Main?.OpenUserProfile(comment.AuthorUserId);
        }
    }

    private void FindFriends_OnClick(object? sender, RoutedEventArgs e) =>
        (DataContext as AccountViewModel)?.OpenFriendsCommand.Execute("Search");

    private async void CopyTag_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel { PlayerTagDisplay.Length: > 0 } vm
            && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(vm.PlayerTagDisplay);
        }
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
