using System.Reflection;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

[Collection("Loc")]
public sealed class AchievementDialogFlowTests
{
    [Fact]
    public async Task NewAchievement_KeyFollowsTitle_StatKindNeedsTarget_AndCommitSavesAtOnce()
    {
        var (editor, synced) = await CreateEditorAsync();

        var draft = editor.BeginEdit(null)!;
        Assert.True(draft.HasValidationError); // no name yet

        draft.EditTitle = "Pogromca bossa!";
        Assert.Equal("ach_pogromca_bossa", draft.EditKey);
        Assert.False(draft.HasValidationError);

        draft.EditKindIndex = StudioAchievementItem.KindStat;
        Assert.Equal("num_games", draft.EditStatKey); // the only stat is picked
        Assert.True(draft.HasValidationError); // a stat achievement needs a target
        draft.EditTarget = "10";
        Assert.False(draft.HasValidationError);

        Assert.True(await editor.CommitEditAsync());
        var sent = Assert.Single(synced);
        Assert.Equal(["ach_first", "ach_pogromca_bossa"], sent.Select(d => d.Key));
        Assert.Equal(("num_games", (int?)10), (sent[1].StatKey, sent[1].Target));
        Assert.Equal(2, editor.Items.Count);
        Assert.False(draft.IsNew);
    }

    [Fact]
    public async Task TypedKeyStopsFollowingTheTitle_AndATakenKeyIsReported()
    {
        var (editor, _) = await CreateEditorAsync();
        var draft = editor.BeginEdit(null)!;

        draft.EditTitle = "First";
        Assert.Equal("ach_first", draft.EditKey);
        Assert.True(draft.HasValidationError); // already used by the existing achievement

        draft.EditKey = "ACH_OTHER";
        draft.EditTitle = "Something else";
        Assert.Equal("ACH_OTHER", draft.EditKey);
        Assert.False(draft.HasValidationError);
    }

    [Fact]
    public async Task CancellingAnEditLeavesTheTableUntouched()
    {
        var (editor, synced) = await CreateEditorAsync();
        var row = editor.Items.Single();

        var draft = editor.BeginEdit(row)!;
        draft.EditTitle = "Changed";
        draft.EditKindIndex = StudioAchievementItem.KindCounter;
        editor.EndEdit();

        Assert.Equal("First", row.EditTitle);
        Assert.Equal(StudioAchievementItem.KindOneShot, row.EditKindIndex);
        Assert.Empty(synced);
    }

    [Fact]
    public void OneShotKindDropsTargetAndStat()
    {
        var item = StudioAchievementItem.CreateNew("https://api/");
        item.EditTitle = "X";
        item.EditTarget = "10";
        item.EditStatKey = "num_games";
        item.EditKindIndex = StudioAchievementItem.KindOneShot;

        Assert.True(item.TryToDto(0, out var dto, out _));
        Assert.Equal(((int?)null, (string?)null), (dto!.Target, dto.StatKey));
        Assert.Equal(Loc.T("Ukryte osiągnięcie"), WithHidden(item).PreviewLockedTitle);
    }

    [Fact]
    public async Task NewStat_NeedsAFreeApiName_AndCommitSavesItWithTheExistingOnes()
    {
        var (editor, _) = await CreateEditorAsync();

        var draft = editor.BeginEditStat(null)!;
        Assert.True(draft.HasValidationError); // no API name yet

        draft.EditKey = "NUM_GAMES";
        Assert.True(draft.HasValidationError); // taken by the existing stat

        draft.EditKey = "distance_m";
        draft.IsFloatType = true;
        draft.EditMaxChange = "500";
        Assert.False(draft.HasValidationError);

        Assert.True(await editor.CommitStatEditAsync());
        var sent = Assert.Single(_syncedStats)!;
        Assert.Equal(["num_games", "distance_m"], sent.Select(s => s.Key));
        Assert.Equal((GameStatType.Float, (double?)500), (sent[1].Type, sent[1].MaxChange));
        Assert.False(draft.IsNew);
    }

    [Fact]
    public async Task CancellingAStatEdit_LeavesTheTableUntouched_AndDeleteSavesWithoutIt()
    {
        var (editor, _) = await CreateEditorAsync();
        var row = editor.Stats.Single();

        var draft = editor.BeginEditStat(row)!;
        draft.EditDisplayName = "Changed";
        editor.EndEditStat();
        Assert.Equal(string.Empty, row.EditDisplayName);
        Assert.Empty(_syncedStats);

        await editor.DeleteStatAsync(row);
        Assert.Empty(Assert.Single(_syncedStats)!);
    }

    private static StudioAchievementItem WithHidden(StudioAchievementItem item)
    {
        item.EditHidden = true;
        return item;
    }

    private readonly List<IReadOnlyList<GameStatDefinitionDto>?> _syncedStats = [];

    private async Task<(AchievementsEditorViewModel Editor, List<IReadOnlyList<AchievementDefinitionDto>> Synced)> CreateEditorAsync()
    {
        var synced = new List<IReadOnlyList<AchievementDefinitionDto>>();
        var api = FakeApi.Create((method, args) => method.Name switch
        {
            nameof(ISgsApiService.GetAchievementDefinitionsAsync) => Task.FromResult<IReadOnlyList<AchievementDefinitionDto>>(
                [new AchievementDefinitionDto { Key = "ach_first", Title = "First" }]),
            nameof(ISgsApiService.GetStatDefinitionsAsync) => Task.FromResult<IReadOnlyList<GameStatDefinitionDto>>(
                [new GameStatDefinitionDto { Key = "num_games" }]),
            nameof(ISgsApiService.SyncAchievementDefinitionsAsync) => Sync(
                (IReadOnlyList<AchievementDefinitionDto>)args![1]!,
                (IReadOnlyList<GameStatDefinitionDto>?)args[2]),
            _ => null
        });

        var editor = new AchievementsEditorViewModel(api, "https://api.example/", _ => { }, _ => { });
        await editor.LoadAsync(1);
        return (editor, synced);

        Task<(IReadOnlyList<AchievementDefinitionDto>?, string?)> Sync(
            IReadOnlyList<AchievementDefinitionDto> items,
            IReadOnlyList<GameStatDefinitionDto>? stats)
        {
            synced.Add(items);
            _syncedStats.Add(stats);
            IReadOnlyList<AchievementDefinitionDto> saved = items
                .Select(i => new AchievementDefinitionDto { Key = i.Key.ToLowerInvariant(), Title = i.Title, Target = i.Target, StatKey = i.StatKey })
                .ToList();
            return Task.FromResult<(IReadOnlyList<AchievementDefinitionDto>?, string?)>((saved, null));
        }
    }

    /// <summary>ISgsApiService where only the handled calls do anything; the rest return defaults.</summary>
    public class FakeApi : DispatchProxy
    {
        private Func<MethodInfo, object?[]?, object?> _handler = (_, _) => null;

        public static ISgsApiService Create(Func<MethodInfo, object?[]?, object?> handler)
        {
            var proxy = Create<ISgsApiService, FakeApi>();
            ((FakeApi)(object)proxy)._handler = handler;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (_handler(method!, args) is { } handled)
            {
                return handled;
            }

            var type = method!.ReturnType;
            if (type == typeof(Task))
            {
                return Task.CompletedTask;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var result = type.GetGenericArguments()[0];
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(result)
                    .Invoke(null, [result.IsValueType ? Activator.CreateInstance(result) : null]);
            }

            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
