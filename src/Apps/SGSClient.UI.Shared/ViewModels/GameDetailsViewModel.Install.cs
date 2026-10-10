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
    [RelayCommand]
    private async Task LaunchAsync()
    {
        if (_scopes is null || _libraryItem is null || string.IsNullOrWhiteSpace(_libraryItem.ExecutablePath))
        {
            StatusText = "Gra nie jest zainstalowana.";
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var launcher = scope.ServiceProvider.GetRequiredService<IGameLauncherService>();
            var result = await launcher.LaunchAsync(new GameLaunchRequest
            {
                GameId = _libraryItem.GameId,
                Title = Title,
                ExecutablePath = _libraryItem.ExecutablePath
            });
            StatusText = result.Message;
            await RefreshPlaytimeAsync(_libraryItem.GameId);
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się uruchomić: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (_scopes is null || _libraryItem is null || IsInstalling)
        {
            return;
        }

        if (_queue is not null)
        {
            IsInstalling = true;
            StatusText = "Dodano do kolejki instalacji…";
            await _queue.EnqueueInstallAsync(GameId, _libraryItem.TargetVersion);
            SyncInstallQueueState();
            return;
        }

        IsInstalling = true;
        try
        {
            using var scope = _scopes.CreateScope();
            var installer = scope.ServiceProvider.GetRequiredService<IGameInstallService>();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var model = await library.GetByGameIdAsync(GameId);
            if (model is null)
            {
                StatusText = "Nie znaleziono gry w bibliotece.";
                return;
            }

            var progress = new Progress<InstallProgress>(p =>
            {
                InstallProgressFraction = p.Fraction;
                InstallProgressText = p.Message ?? string.Empty;
            });

            var result = await installer.InstallAsync(model, version: model.TargetVersion, progress: progress);
            await library.UpsertAsync(model);
            _libraryItem = model;
            ApplyLibraryActions(model);
            StatusText = result.Success
                ? $"Zainstalowano: {model.Title}"
                : result.Errors.FirstOrDefault() ?? "Instalacja nie powiodła się.";
        }
        catch (Exception ex)
        {
            StatusText = $"Instalacja nie powiodła się: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
            InstallProgressFraction = 0;
            InstallProgressText = string.Empty;
        }
    }

    [RelayCommand]
    private void CancelInstall()
    {
        if (_queue is null || GameId <= 0 || !IsInstalling)
        {
            return;
        }

        _queue.Cancel(GameId);
        IsInstalling = false;
        InstallProgressFraction = 0;
        InstallProgressText = string.Empty;
        StatusText = "Anulowano instalację.";
        SyncInstallQueueState();
        _ = RefreshLibraryStateAsync(GameId);
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        if (_scopes is null || _libraryItem is null || IsInstalling)
        {
            return;
        }

        IsInstalling = true;
        try
        {
            using var scope = _scopes.CreateScope();
            var installer = scope.ServiceProvider.GetRequiredService<IGameInstallService>();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var model = await library.GetByGameIdAsync(GameId);
            if (model is null)
            {
                StatusText = "Nie znaleziono gry w bibliotece.";
                return;
            }

            var result = await installer.UninstallAsync(model);
            await library.UpsertAsync(model);
            _libraryItem = model;
            ApplyLibraryActions(model);
            StatusText = result.Success
                ? $"Odinstalowano: {model.Title}"
                : result.Errors.FirstOrDefault() ?? "Odinstalowanie nie powiodło się.";
        }
        catch (Exception ex)
        {
            StatusText = $"Odinstalowanie nie powiodło się: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
        }
    }

    private void OnQueueProgress(object? sender, InstallQueueProgressEventArgs e)
    {
        if (e.GameId != GameId)
        {
            return;
        }

        InstallProgressFraction = e.Progress.Fraction;
        InstallProgressText = e.Progress.Message ?? string.Empty;
        IsInstalling = e.State is InstallState.Queued or InstallState.Downloading
            or InstallState.Installing or InstallState.Updating;
        if (!IsInstalling)
        {
            _ = RefreshLibraryStateAsync(GameId);
        }
    }

    private void SyncInstallQueueState()
    {
        if (_queue is null || GameId <= 0)
        {
            return;
        }

        var active = _queue.IsQueuedOrActive(GameId);
        IsInstalling = active;
        if (!active)
        {
            InstallProgressFraction = 0;
            if (string.Equals(InstallProgressText, "Dodano do kolejki instalacji…", StringComparison.Ordinal))
            {
                InstallProgressText = string.Empty;
            }

            var entry = _queue.Entries.FirstOrDefault(e => e.GameId == GameId);
            if (!string.IsNullOrWhiteSpace(entry?.Error))
            {
                StatusText = entry.Error!;
            }

            _ = RefreshLibraryStateAsync(GameId);
        }
    }
}
