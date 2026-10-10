using SGSClient.UI;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

public sealed class FriendsViewModelTests
{
    [Fact]
    public void ChatTab_StartsPollingTimer_AndOtherTabStopsIt()
    {
        var dispatcher = new FakeDispatcher();
        var vm = new FriendsViewModel(userApi: null, auth: null, realtime: null, dispatcher: dispatcher);

        Assert.Equal(TimeSpan.FromSeconds(5), dispatcher.Interval);
        Assert.False(dispatcher.Timer.Running);

        vm.SelectedTab = "Chat";
        Assert.True(dispatcher.Timer.Running);

        vm.SelectedTab = "Friends";
        Assert.False(dispatcher.Timer.Running);
    }

    [Fact]
    public void WorksWithoutDispatcher()
    {
        var vm = new FriendsViewModel();

        vm.SelectedTab = "Chat";
        vm.SelectedTab = "Friends";

        Assert.Equal("Friends", vm.SelectedTab);
    }

    private sealed class FakeDispatcher : IUiDispatcher
    {
        public FakeTimer Timer { get; } = new();
        public TimeSpan Interval { get; private set; }

        public void Post(Action action) => action();

        public IUiTimer CreateTimer(TimeSpan interval, Func<Task> onTick)
        {
            Interval = interval;
            return Timer;
        }
    }

    private sealed class FakeTimer : IUiTimer
    {
        public bool Running { get; private set; }
        public void Start() => Running = true;
        public void Stop() => Running = false;
    }
}
