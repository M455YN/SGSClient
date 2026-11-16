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
    private readonly List<NotificationDto> _local = [];
    private static long _localId;
    private static event Action<NotificationDto>? LocalPosted;

    /// <summary>Client-only notice (e.g. a background upload finished). Not stored on the server; gone after restart.</summary>
    public static void PostLocal(string title, string? body = null) =>
        LocalPosted?.Invoke(new NotificationDto
        {
            Id = Interlocked.Decrement(ref _localId),
            Kind = "local",
            Title = title,
            Body = body,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

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

        LocalPosted += n =>
        {
            _local.Insert(0, n);
            OnNotification(n);
        };
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
        _local.Clear();
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
            var unread = await _userApi.GetUnreadNotificationCountAsync();
            Items.Clear();
            foreach (var item in list.Concat(_local).OrderByDescending(n => n.CreatedAtUtc))
            {
                Items.Add(item);
            }

            UnreadCount = unread + _local.Count(n => !n.IsRead);
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

        for (var i = 0; i < _local.Count; i++)
        {
            _local[i] = AsRead(_local[i]);
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

        if (item.Id < 0)
        {
            var local = _local.FindIndex(n => n.Id == item.Id);
            var shown = Items.IndexOf(item);
            if (local >= 0 && !item.IsRead)
            {
                _local[local] = AsRead(item);
                if (shown >= 0)
                {
                    Items[shown] = _local[local];
                }

                UnreadCount = Math.Max(0, UnreadCount - 1);
            }

            return;
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

    private static NotificationDto AsRead(NotificationDto n) => n.IsRead
        ? n
        : new NotificationDto
        {
            Id = n.Id,
            Kind = n.Kind,
            Title = n.Title,
            Body = n.Body,
            DataJson = n.DataJson,
            CreatedAtUtc = n.CreatedAtUtc,
            ReadAtUtc = DateTimeOffset.UtcNow
        };

    private void OnNotification(NotificationDto dto)
    {
        Items.Insert(0, dto);
        UnreadCount++;
    }
}
