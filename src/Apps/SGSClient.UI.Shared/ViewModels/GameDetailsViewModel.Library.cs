using SGSClient.Core.Localization;
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
    private async Task AddToLibraryAsync()
    {
        if (!CanAdd || _scopes is null || GameId <= 0)
        {
            return;
        }

        if (_auth is { IsAvailable: true } && !_auth.IsLoggedIn)
        {
            StatusText = Loc.T("Zaloguj się, żeby dodać grę do biblioteki.");
            _openAccount?.Invoke();
            return;
        }

        var preferred = Builds.FirstOrDefault(b => b.IsPreferred) ?? Builds.FirstOrDefault();
        if (preferred is null || preferred.BuildId <= 0)
        {
            StatusText = Loc.T("Brak builda dla tej platformy.");
            return;
        }

        var selectedVersion = preferred.SelectedVersion?.Version ?? preferred.Version;

        try
        {
            if (_api is not null)
            {
                var claimed = await _api.ClaimGameAsync(GameId);
                if (claimed is null)
                {
                    StatusText = Loc.T("Nie udało się dodać gry (zaloguj się lub sprawdź uprawnienia).");
                    return;
                }
            }

            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            await library.UpsertClaimedAsync(new GameLibraryItem
            {
                GameId = GameId,
                BuildId = preferred.BuildId,
                Symbol = Symbol,
                Title = Title,
                Platform = preferred.Platform,
                Architecture = preferred.Architecture,
                InstallState = InstallState.NotInstalled,
                InstalledVersion = null,
                TargetVersion = selectedVersion,
                CoverUrl = _coverUrl,
                LogoUrl = _logoUrl,
                ExecutablePath = null,
                InstallPath = null
            });

            InLibrary = true;
            InWishlist = false;
            WishlistLabel = Loc.T("Lista życzeń");
            CanAdd = false;
            PrimaryActionLabel = Loc.T("W bibliotece");
            StatusText = preferred.SelectedVersion?.IsCurrent == true || preferred.SelectedVersion is null
                ? Loc.T("Dodano do biblioteki (najnowsza wersja).")
                : Loc.F("Dodano do biblioteki (wersja {0}).", selectedVersion);
            await RefreshLibraryStateAsync(GameId);
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Nie udało się dodać: {0}", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ToggleWishlistAsync()
    {
        if (_api is null || GameId <= 0)
        {
            return;
        }

        if (_auth is { IsAvailable: true } && !_auth.IsLoggedIn)
        {
            StatusText = Loc.T("Zaloguj się, żeby zarządzać listą życzeń.");
            _openAccount?.Invoke();
            return;
        }

        try
        {
            if (InWishlist)
            {
                if (await _api.RemoveFromWishlistAsync(GameId))
                {
                    InWishlist = false;
                    WishlistLabel = Loc.T("Lista życzeń");
                    StatusText = Loc.T("Usunięto z listy życzeń.");
                }
            }
            else
            {
                if (await _api.AddToWishlistAsync(GameId) is not null)
                {
                    InWishlist = true;
                    WishlistLabel = Loc.T("Na liście życzeń");
                    StatusText = Loc.T("Dodano do listy życzeń.");
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Lista życzeń: {0}", ex.Message);
        }
    }

    [RelayCommand]
    private void OpenProperties()
    {
        if (_libraryItem is null)
        {
            return;
        }

        _openProperties?.Invoke(_libraryItem);
    }

    private async Task RefreshWishlistStateAsync(int gameId)
    {
        InWishlist = false;
        WishlistLabel = Loc.T("Lista życzeń");
        if (_api is null)
        {
            return;
        }

        try
        {
            var wish = await _api.GetWishlistAsync();
            InWishlist = wish.Any(g => g.Id == gameId);
            WishlistLabel = InWishlist ? Loc.T("Na liście życzeń") : Loc.T("Lista życzeń");
        }
        catch
        {
            // ignore
        }
    }

    private async Task RefreshLibraryStateAsync(int gameId)
    {
        _libraryItem = null;
        ShowLibraryActions = false;
        ShowStoreActions = true;
        CanLaunch = false;
        CanInstall = false;
        CanUninstall = false;

        if (_scopes is null)
        {
            InLibrary = false;
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            _libraryItem = (await library.GetOwnedAsync()).FirstOrDefault(x => x.GameId == gameId);
            InLibrary = _libraryItem is not null;
            if (_libraryItem is not null)
            {
                ApplyLibraryActions(_libraryItem);
            }

            await RefreshPlaytimeAsync(gameId);
        }
        catch
        {
            InLibrary = false;
        }
    }

    private void ApplyLibraryActions(GameLibraryItem item)
    {
        ShowLibraryActions = true;
        ShowStoreActions = false;
        CanLaunch = !IsDlc
            && item.InstallState == InstallState.Installed
            && !string.IsNullOrWhiteSpace(item.ExecutablePath);
        CanInstall = item.InstallState is InstallState.NotInstalled or InstallState.Failed;
        CanUninstall = item.InstallState == InstallState.Installed;
    }

    private async Task<bool> IsInLibraryAsync(int gameId)
    {
        await RefreshLibraryStateAsync(gameId);
        return InLibrary;
    }
}
