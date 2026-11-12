using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.ViewModels;

public partial class NotificationsViewModel : ObservableObject
{
    private readonly IUserApiClient? _userApi;
    private readonly IAuthSession? _auth;
    private readonly SocialRealtimeService? _realtime;

    public NotificationsViewModel()
        : this(null, null, null)
    {
    }

    public NotificationsViewModel(
        IUserApiClient? userApi,
        IAuthSession? auth,
        SocialRealtimeService? realtime)
    {
        _userApi = userApi;
        _auth = auth;
        _realtime = realtime;
        if (_realtime is not null)
        {
            _realtime.NotificationsChanged += () => _ = RefreshAsync();
            _realtime.NotificationReceived += OnNotification;
        }
    }

    public ObservableCollection<NotificationDto> Items { get; } = [];

    [ObservableProperty]
    private int _unreadCount;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isBusy;

    public bool HasUnread => UnreadCount > 0;

    partial void OnUnreadCountChanged(int value) => OnPropertyChanged(nameof(HasUnread));

    public async Task StartAsync()
    {
        if (_auth is { IsAvailable: true, IsLoggedIn: false })
        {
            return;
        }

        if (_realtime is not null)
        {
            await _realtime.StartAsync();
        }

        await RefreshAsync();
    }

    public async Task StopAsync()
    {
        if (_realtime is not null)
        {
            await _realtime.StopAsync();
        }

        Items.Clear();
        UnreadCount = 0;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_userApi is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var list = await _userApi.GetNotificationsAsync();
            Items.Clear();
            foreach (var item in list)
            {
                Items.Add(item);
            }

            UnreadCount = await _userApi.GetUnreadNotificationCountAsync();
        }
        catch
        {
            // keep last known
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        if (_userApi is null)
        {
            return;
        }

        await _userApi.MarkAllNotificationsReadAsync();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task MarkReadAsync(NotificationDto? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.Kind == ILobbyService.InviteNotificationKind
            && TryReadInvite(item.DataJson, out var gameId, out var code))
        {
            await LobbyJoin.RequestAsync(gameId, code);
        }

        if (_userApi is null || item.IsRead)
        {
            return;
        }

        await _userApi.MarkNotificationReadAsync(item.Id);
        await RefreshAsync();
    }

    private static bool TryReadInvite(string? dataJson, out int gameId, out string code)
    {
        gameId = 0;
        code = string.Empty;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(dataJson ?? string.Empty);
            code = doc.RootElement.GetProperty("code").GetString() ?? string.Empty;
            gameId = doc.RootElement.GetProperty("gameId").GetInt32();
            return code.Length > 0 && gameId > 0;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }

    private void OnNotification(NotificationDto dto)
    {
        Items.Insert(0, dto);
        UnreadCount++;
    }
}
