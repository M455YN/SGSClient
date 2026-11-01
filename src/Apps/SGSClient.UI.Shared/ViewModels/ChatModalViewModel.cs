using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.ViewModels;

/// <summary>One conversation shown in a dialog. Live messages arrive over the social hub while it is open.</summary>
public sealed partial class ChatModalViewModel : ObservableObject, IDisposable
{
    private readonly IUserApiClient? _api;
    private readonly SocialRealtimeService? _realtime;
    private readonly IUiDispatcher? _ui;

    public ChatModalViewModel(
        IUserApiClient? api,
        SocialRealtimeService? realtime,
        IUiDispatcher? ui,
        int peerUserId,
        string title)
    {
        _api = api;
        _realtime = realtime;
        _ui = ui;
        PeerUserId = peerUserId;
        Title = title;
        Messages.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMessages));
        if (_realtime is not null)
        {
            _realtime.MessageReceived += OnRealtimeMessage;
        }
    }

    public int PeerUserId { get; }
    public ObservableCollection<ChatMessageItem> Messages { get; } = [];
    public string Initials => Title.Length == 0 ? "?" : Title[..Math.Min(2, Title.Length)].ToUpperInvariant();
    public bool HasMessages => Messages.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initials))]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Draft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    public void Dispose()
    {
        if (_realtime is not null)
        {
            _realtime.MessageReceived -= OnRealtimeMessage;
        }
    }

    public async Task LoadAsync()
    {
        if (_api is null)
        {
            return;
        }

        try
        {
            if (_realtime is not null)
            {
                await _realtime.EnsureConnectedAsync();
            }

            var convo = await _api.GetChatConversationAsync(PeerUserId);
            if (convo is null)
            {
                Status = Loc.T("Nie udało się wczytać rozmowy (tylko znajomi).");
                return;
            }

            Title = string.IsNullOrWhiteSpace(convo.Peer.PlayerTag) ? convo.Peer.DisplayName : convo.Peer.PlayerTag!;
            foreach (var message in convo.Messages)
            {
                Add(message);
            }

            Status = string.Empty;
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = Draft.Trim();
        if (_api is null || text.Length == 0)
        {
            return;
        }

        Draft = string.Empty;
        var (message, error) = await _api.SendChatMessageAsync(PeerUserId, text);
        if (message is null)
        {
            Draft = text;
            Status = error ?? Loc.T("Nie wysłano.");
            return;
        }

        Add(message);
        Status = string.Empty;
    }

    private void OnRealtimeMessage(ChatMessageDto message)
    {
        if (message.SenderUserId != PeerUserId && message.RecipientUserId != PeerUserId)
        {
            return;
        }

        if (_ui is null)
        {
            Add(message);
        }
        else
        {
            _ui.Post(() => Add(message));
        }
    }

    // The hub also echoes your own message back, so the same id can arrive twice.
    private void Add(ChatMessageDto message)
    {
        if (Messages.All(m => m.Id != message.Id))
        {
            Messages.Add(ChatMessageItem.FromDto(message));
        }
    }
}
