using SGSClient.Core.DTOs;

namespace SGSClient.UI.ViewModels;

public static class GameMediaActions
{
    public static async Task PickAsync(
        Func<Task<string?>>? pick,
        int? gameId,
        Func<int, string, Task<GameDto?>> upload,
        Func<GameDto?, string, Task> reload,
        string okText)
    {
        if (pick is null || gameId is null)
        {
            return;
        }

        var path = await pick();
        if (path is null)
        {
            return;
        }

        await reload(await upload(gameId.Value, path), okText);
    }

    public static async Task ClearAsync(
        int? gameId,
        Func<int, Task<GameDto?>> clear,
        Func<GameDto?, string, Task> reload,
        string okText)
    {
        if (gameId is null)
        {
            return;
        }

        await reload(await clear(gameId.Value), okText);
    }
}
