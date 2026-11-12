using System.Reflection;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

public sealed class ChatModalViewModelTests
{
    [Fact]
    public async Task Load_ShowsTheConversationUnderThePeersTag()
    {
        var api = FakeApi.Create();
        api.Conversation = new ChatConversationDto
        {
            Peer = Peer(),
            Messages = [Message(1, "cześć"), Message(2, "hej")]
        };
        var vm = new ChatModalViewModel(api.Proxy, null, null, peerUserId: 7, title: "Eris");

        Assert.False(vm.HasMessages);
        await vm.LoadAsync();

        Assert.Equal("eris", vm.Title);
        Assert.Equal("ER", vm.Initials);
        Assert.Equal(["cześć", "hej"], vm.Messages.Select(m => m.Text));
    }

    [Fact]
    public async Task Send_AddsTheMessageOnce_EvenWhenItIsAlreadyInTheList()
    {
        var api = FakeApi.Create();
        api.Conversation = new ChatConversationDto { Peer = Peer(), Messages = [Message(1, "cześć")] };
        api.Sent = Message(1, "cześć");
        var vm = new ChatModalViewModel(api.Proxy, null, null, 7, "Eris");
        await vm.LoadAsync();

        vm.Draft = "  cześć  ";
        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("cześć", api.SentText);
        Assert.Equal(string.Empty, vm.Draft);
        Assert.Single(vm.Messages);
    }

    [Fact]
    public async Task Send_KeepsTheDraftAndShowsWhyWhenItFails_AndIgnoresEmptyText()
    {
        var api = FakeApi.Create();
        var vm = new ChatModalViewModel(api.Proxy, null, null, 7, "Eris");

        vm.Draft = "   ";
        await vm.SendCommand.ExecuteAsync(null);
        Assert.Null(api.SentText);

        api.Sent = null;
        api.Error = "Tylko znajomi.";
        vm.Draft = "hej";
        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("hej", vm.Draft);
        Assert.Equal("Tylko znajomi.", vm.Status);
        Assert.Empty(vm.Messages);
    }

    [Fact]
    public async Task Ownership_ComesFromTheSender_NotFromTheDtosIsMine()
    {
        // The hub pushes the sender's DTO (IsMine = true) to the recipient as well.
        var api = FakeApi.Create();
        api.Conversation = new ChatConversationDto
        {
            Peer = Peer(),
            Messages =
            [
                new() { Id = 1, SenderUserId = 7, RecipientUserId = 1, Text = "od Eris", IsMine = true },
                new() { Id = 2, SenderUserId = 1, RecipientUserId = 7, Text = "ode mnie", IsMine = false }
            ]
        };
        var vm = new ChatModalViewModel(api.Proxy, null, null, 7, "Eris");

        await vm.LoadAsync();

        Assert.Equal([false, true], vm.Messages.Select(m => m.IsMine));
    }

    private static SocialUserDto Peer() => new() { Id = 7, DisplayName = "Eris Smith", PlayerTag = "eris" };

    private static ChatMessageDto Message(long id, string text) =>
        new() { Id = id, SenderUserId = 7, RecipientUserId = 1, Text = text, CreatedAtUtc = DateTimeOffset.UtcNow };

    /// <summary>IUserApiClient is large and only two calls matter here, so the rest answers with defaults.</summary>
    private class FakeApi : DispatchProxy
    {
        public ChatConversationDto? Conversation { get; set; }
        public ChatMessageDto? Sent { get; set; }
        public string? Error { get; set; }
        public string? SentText { get; private set; }
        public IUserApiClient Proxy => (IUserApiClient)(object)this;

        public static FakeApi Create() => (FakeApi)(object)DispatchProxy.Create<IUserApiClient, FakeApi>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            object? result = targetMethod!.Name switch
            {
                nameof(IUserApiClient.GetChatConversationAsync) => Conversation,
                nameof(IUserApiClient.SendChatMessageAsync) => Send((string)args![1]!),
                _ => null
            };
            var taskResult = targetMethod.ReturnType.GetGenericArguments()[0];
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(taskResult)
                .Invoke(null, [result]);
        }

        private (ChatMessageDto?, string?) Send(string text)
        {
            SentText = text;
            return (Sent, Error);
        }
    }
}
