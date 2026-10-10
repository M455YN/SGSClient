using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Core.Playtime;

namespace SGSClient.UI.ViewModels;

public partial class GameDetailsViewModel
{
    private async Task SyncAndRefreshPlaytimeAsync(int gameId)
    {
        if (_scopes is null)
        {
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var store = scope.ServiceProvider.GetService<IPlaytimeStore>();
            if (store is null)
            {
                return;
            }

            await store.SyncFromServerAsync();
        }
        catch
        {
        }

        await RefreshPlaytimeAsync(gameId);
    }

    private async Task RefreshPlaytimeAsync(int gameId)
    {
        if (_scopes is null)
        {
            PlaytimeText = string.Empty;
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var store = scope.ServiceProvider.GetService<IPlaytimeStore>();
            var record = store is null ? null : await store.GetAsync(gameId);
            PlaytimeText = record is { TotalSeconds: > 0 }
                ? PlaytimeFormat.ToPolishTotal(record.TotalSeconds)
                : string.Empty;
        }
        catch
        {
            PlaytimeText = string.Empty;
        }
    }
}
