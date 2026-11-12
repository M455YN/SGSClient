using System.Reflection;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

public sealed class UserProfileViewModelTests
{
    [Fact]
    public async Task Load_FillsHeaderRelationAndHiddenAchievements()
    {
        var api = FakeApi.Create();
        api.Profile = new PublicUserProfileDto
        {
            User = new SocialUserDto
            {
                Id = 7,
                DisplayName = "Eris Smith",
                PlayerTag = "eris",
                Location = "Kraków",
                LastSeenUtc = DateTimeOffset.UtcNow.AddDays(-3),
                FriendRelation = FriendRelation.Friends
            },
            AchievementsHidden = true,
            Comments =
            [
                new ProfileCommentDto { Id = 1, AuthorUserId = 9, AuthorDisplayName = "Kai", Text = "gg" }
            ]
        };
        var vm = new UserProfileViewModel(api.Proxy);

        await vm.LoadAsync(7);

        Assert.False(vm.HasError);
        Assert.Equal("@eris", vm.PlayerTagDisplay);
        Assert.Equal("ES", vm.Initials);
        Assert.True(vm.HasLocation);
        Assert.False(vm.IsOnline);
        Assert.StartsWith("Ostatnio online ", vm.PresenceText);
        Assert.True(vm.Person!.ShowMessage);
        Assert.False(vm.Person.ShowAddFriend);
        Assert.Equal("Ten gracz ukrył swoje osiągnięcia.", vm.AchievementsText);
        Assert.Equal(9, Assert.Single(vm.Comments).AuthorUserId);
    }

    [Fact]
    public async Task Load_ShowsPlayedGamesWithBannerAndProgress_OrWhyThereAreNone()
    {
        var api = FakeApi.Create();
        api.Profile = new PublicUserProfileDto
        {
            User = new SocialUserDto { Id = 7, DisplayName = "Eris" },
            MemberSinceUtc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            GamesCount = 2,
            TotalPlaytimeSeconds = 7200,
            Achievements = [new UserAchievementSummaryDto { GameId = 3, GameTitle = "Demo", Unlocked = 1, Total = 4 }],
            Games =
            [
                new PublicGameActivityDto { GameId = 3, Title = "Demo", HeaderUrl = "https://example.test/h.jpg", TotalSeconds = 3600 },
                new PublicGameActivityDto { GameId = 4, Title = "Other", TotalSeconds = 3600 }
            ]
        };
        var vm = new UserProfileViewModel(api.Proxy);

        await vm.LoadAsync(7);

        Assert.Equal(2, vm.Games.Count);
        Assert.Equal("https://example.test/h.jpg", vm.BannerImageUrl);
        Assert.Equal(0.25, vm.Games[0].Achievements!.Fraction);
        Assert.Null(vm.Games[1].Achievements);
        Assert.StartsWith("Na SGS od ", vm.MemberSinceText);
        Assert.Equal(string.Empty, vm.GamesText);

        api.Profile = new PublicUserProfileDto { User = new SocialUserDto { Id = 7, DisplayName = "Eris" }, GameActivityHidden = true };
        await vm.LoadAsync(7);

        Assert.False(vm.HasGames);
        Assert.Equal("Ten gracz ukrył swoje gry i czas gry.", vm.GamesText);
    }

    [Fact]
    public async Task Load_ShowsAnErrorWhenTheProfileIsMissing()
    {
        var vm = new UserProfileViewModel(FakeApi.Create().Proxy);

        await vm.LoadAsync(7);

        Assert.True(vm.HasError);
        Assert.False(vm.IsLoaded);
    }

    /// <summary>IUserApiClient is large and only the profile call matters here.</summary>
    private class FakeApi : DispatchProxy
    {
        public PublicUserProfileDto? Profile { get; set; }
        public IUserApiClient Proxy => (IUserApiClient)(object)this;

        public static FakeApi Create() => (FakeApi)(object)DispatchProxy.Create<IUserApiClient, FakeApi>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            object? result = targetMethod!.Name == nameof(IUserApiClient.GetPublicProfileAsync) ? Profile : null;
            var taskResult = targetMethod.ReturnType.GetGenericArguments()[0];
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(taskResult)
                .Invoke(null, [result]);
        }
    }
}
