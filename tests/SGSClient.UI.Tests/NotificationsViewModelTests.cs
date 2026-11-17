using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

public sealed class NotificationsViewModelTests
{
    [Fact]
    public async Task LocalNotice_ShowsAsUnread_AndMarkReadNeedsNoServer()
    {
        var vm = new NotificationsViewModel();

        NotificationsViewModel.PostLocal("Opublikowano: Test", "body");

        var item = Assert.Single(vm.Items);
        Assert.True(item.Id < 0);
        Assert.Equal(1, vm.UnreadCount);

        await vm.MarkReadCommand.ExecuteAsync(item);

        Assert.True(Assert.Single(vm.Items).IsRead);
        Assert.Equal(0, vm.UnreadCount);
    }

    [Fact]
    public async Task UploadNotice_OpensPublishForm_OtherNoticesDont()
    {
        var vm = new NotificationsViewModel();
        var opened = 0;
        NotificationsViewModel.OpenPublishForm = () => opened++;
        try
        {
            NotificationsViewModel.PostLocal("Opublikowano: Test", kind: SGSClient.Core.DTOs.NotificationDto.PublishKind);
            NotificationsViewModel.PostLocal("Inne");

            var upload = vm.Items.Single(n => n.OpensPublishForm);
            var other = vm.Items.Single(n => !n.OpensPublishForm);
            await vm.MarkReadCommand.ExecuteAsync(other);
            Assert.Equal(0, opened);
            await vm.MarkReadCommand.ExecuteAsync(upload);
            Assert.Equal(1, opened);
        }
        finally
        {
            NotificationsViewModel.OpenPublishForm = null;
        }
    }

    [Fact]
    public async Task QuitWaitsForUpload_UntilPublishIsIdle()
    {
        var publish = new PublishViewModel { IsBusy = true };

        var idle = publish.WhenIdleAsync();
        Assert.False(idle.IsCompleted);

        publish.IsBusy = false;
        await idle.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(publish.WhenIdleAsync().IsCompleted);
    }
}
