using System.Reflection;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

public class CreativeCommentsViewModelTests
{
    /// <summary>Implements only the three comment members of ISgsApiService; everything else is unsupported.</summary>
    private class FakeApi : DispatchProxy
    {
        public List<CreativeCommentDto> Store { get; } = [];
        public bool Fail { get; set; }
        public string? LastSection { get; private set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (Fail)
            {
                throw new InvalidOperationException("boom");
            }

            switch (method!.Name)
            {
                case nameof(ISgsApiService.GetCreativeCommentsAsync):
                    LastSection = (string)args![0]!;
                    return Task.FromResult<IReadOnlyList<CreativeCommentDto>>(Store.ToList());
                case nameof(ISgsApiService.AddCreativeCommentAsync):
                    var dto = new CreativeCommentDto
                    {
                        Id = Store.Count + 1,
                        ItemId = (int)args![1]!,
                        AuthorDisplayName = "Ala",
                        Text = (string)args[2]!,
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        CanDelete = true
                    };
                    Store.Add(dto);
                    return Task.FromResult<(CreativeCommentDto?, IReadOnlyList<string>)>((dto, []));
                case nameof(ISgsApiService.DeleteCreativeCommentAsync):
                    return Task.FromResult(Store.RemoveAll(c => c.Id == (long)args![2]!) > 0);
                default:
                    throw new NotSupportedException(method.Name);
            }
        }
    }

    private static (CreativeCommentsViewModel Vm, FakeApi Api) Create()
    {
        var api = DispatchProxy.Create<ISgsApiService, FakeApi>();
        return (new CreativeCommentsViewModel(api, "music"), (FakeApi)(object)api);
    }

    [Fact]
    public async Task Post_AddsCommentClearsInputAndDeleteRemovesIt()
    {
        var (vm, api) = Create();
        await vm.LoadAsync(7);
        Assert.False(vm.HasComments);
        Assert.False(vm.PostCommand.CanExecute(null));

        vm.NewCommentText = "  Super kawałek  ";
        Assert.True(vm.PostCommand.CanExecute(null));
        await vm.PostCommand.ExecuteAsync(null);

        Assert.Equal("music", api.LastSection);
        Assert.Single(vm.Comments);
        Assert.Equal("Super kawałek", vm.Comments[0].Text);
        Assert.Equal(string.Empty, vm.NewCommentText);
        Assert.True(vm.HasComments);

        await vm.DeleteCommand.ExecuteAsync(vm.Comments[0]);
        Assert.Empty(vm.Comments);
        Assert.Empty(api.Store);
    }

    [Fact]
    public async Task ApiFailure_ShowsStatusInsteadOfThrowing()
    {
        var (vm, api) = Create();
        api.Fail = true;
        await vm.LoadAsync(1);
        Assert.False(string.IsNullOrEmpty(vm.StatusText));
        Assert.False(vm.IsBusy);
    }
}
