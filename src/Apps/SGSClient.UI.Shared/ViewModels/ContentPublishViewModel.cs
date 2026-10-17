using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;
using SGSClient.Core.Packaging;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.ViewModels;

/// <summary>
/// Publishes books, music and art: the user picks files, the app stages them with a generated
/// manifest (platform "any") and reuses the game packer/upload pipeline.
/// </summary>
public partial class ContentPublishViewModel : ObservableObject
{
    private readonly IGamePackagePacker? _packer;
    private readonly IGameUploadClient? _upload;
    private readonly IAuthSession? _auth;

    public ContentPublishViewModel()
        : this(null, null, null)
    {
    }

    public ContentPublishViewModel(IGamePackagePacker? packer, IGameUploadClient? upload, IAuthSession? auth)
    {
        _packer = packer;
        _upload = upload;
        _auth = auth;
        RefreshAuthHint();
    }

    public ObservableCollection<string> Files { get; } = [];
    public ObservableCollection<string> Issues { get; } = [];

    /// <summary>book | music | art</summary>
    [ObservableProperty] public partial string Kind { get; set; } = "book";
    [ObservableProperty] public partial string ContentTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string ContentSymbol { get; set; } = string.Empty;
    [ObservableProperty] public partial string ContentDescription { get; set; } = string.Empty;
    [ObservableProperty] public partial string Version { get; set; } = "1.0.0";
    [ObservableProperty] public partial string CoverPath { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial string AuthHint { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool ShowProgress { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }

    public string PageTitle => Loc.T(Kind switch
    {
        "music" => "Publikuj muzykę",
        "art" => "Publikuj grafikę",
        _ => "Publikuj książkę"
    });

    public string FilesSummary => Files.Count == 0
        ? Loc.T("Nie wybrano plików.")
        : string.Join(", ", Files.Select(Path.GetFileName));

    partial void OnKindChanged(string value) => OnPropertyChanged(nameof(PageTitle));

    partial void OnContentTitleChanged(string value)
    {
        // Keep the symbol in step with the title until the author edits it by hand.
        if (string.IsNullOrWhiteSpace(ContentSymbol) || ContentSymbol == Slugify(_lastTitle))
        {
            ContentSymbol = Slugify(value);
        }

        _lastTitle = value;
    }

    private string _lastTitle = string.Empty;

    public void RefreshAuthHint() =>
        AuthHint = _auth?.IsAvailable != true
            ? Loc.T("Logowanie jest wyłączone — publikacja nastąpi bez konta.")
            : _auth.IsLoggedIn
                ? $"Zalogowano jako {_auth.CurrentUser?.DisplayName ?? "gracz"}."
                : Loc.T("Zaloguj się w Profilu przed publikacją.");

    public void PrepareFor(string kind)
    {
        Kind = kind;
        StatusText = Loc.T("Wybierz pliki, uzupełnij dane i opublikuj.");
        RefreshAuthHint();
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p) && !Files.Contains(p)))
        {
            Files.Add(path);
        }

        OnPropertyChanged(nameof(FilesSummary));
    }

    public void ClearFiles()
    {
        Files.Clear();
        OnPropertyChanged(nameof(FilesSummary));
    }

    [RelayCommand]
    private async Task PublishAsync()
    {
        if (_packer is null || _upload is null || IsBusy)
        {
            return;
        }

        Issues.Clear();
        var symbol = string.IsNullOrWhiteSpace(ContentSymbol) ? Slugify(ContentTitle) : ContentSymbol.Trim();
        if (string.IsNullOrWhiteSpace(ContentTitle) || Files.Count == 0 || !SgsPackageRules.IsValidSymbol(symbol))
        {
            Issues.Add(Loc.T("Podaj nazwę, wybierz co najmniej jeden plik i użyj symbolu z małych liter i myślników."));
            StatusText = Loc.T("Uzupełnij dane.");
            return;
        }

        RefreshAuthHint();
        IsBusy = true;
        string? stagingDir = null;
        string? zipPath = null;
        try
        {
            StatusText = Loc.T("Pakuję pliki…");
            stagingDir = Path.Combine(Path.GetTempPath(), $"sgs-content-{Guid.NewGuid():N}");
            var (manifest, stageError) = Stage(stagingDir, symbol);
            if (manifest is null)
            {
                Issues.Add(stageError!);
                StatusText = Loc.T("Nie udało się przygotować paczki.");
                return;
            }

            var packed = await _packer.PackFolderAsync(stagingDir, manifest);
            if (!packed.Success || string.IsNullOrWhiteSpace(packed.ZipPath))
            {
                foreach (var error in packed.Errors)
                {
                    Issues.Add(error);
                }

                StatusText = Loc.T("Nie udało się przygotować paczki.");
                return;
            }

            zipPath = packed.ZipPath;
            ShowProgress = true;
            Progress = 0;
            StatusText = Loc.T("Wysyłanie do sklepu…");
            var result = await _upload.UploadAsync(zipPath, new Progress<double>(p => Progress = p));
            if (!result.Success || result.Game is null)
            {
                foreach (var error in result.Errors)
                {
                    Issues.Add(error);
                }

                StatusText = Loc.T("Publikacja nie powiodła się.");
                return;
            }

            StatusText = Loc.F("Opublikowano: {0}. Znajdziesz to w Sklepie.", result.Game.Title);
            ClearForm();
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Błąd wysyłania: {0}", ex.Message);
        }
        finally
        {
            IsBusy = false;
            ShowProgress = false;
            TryDelete(stagingDir, isDirectory: true);
            TryDelete(zipPath, isDirectory: false);
        }
    }

    private (SgsPackageManifest? Manifest, string? Error) Stage(string dir, string symbol)
    {
        try
        {
            var filesDir = Path.Combine(dir, "files");
            Directory.CreateDirectory(filesDir);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? first = null;
            foreach (var source in Files)
            {
                var name = Path.GetFileName(source);
                for (var i = 2; !names.Add(name); i++)
                {
                    name = $"{Path.GetFileNameWithoutExtension(source)}-{i}{Path.GetExtension(source)}";
                }

                File.Copy(source, Path.Combine(filesDir, name));
                first ??= $"files/{name}";
            }

            SgsPackageMediaInfo? media = null;
            if (!string.IsNullOrWhiteSpace(CoverPath))
            {
                var ext = Path.GetExtension(CoverPath).ToLowerInvariant();
                if (!SgsPackageRules.AllowedImageExtensions.Contains(ext))
                {
                    return (null, Loc.T("Okładka musi być obrazem (png, jpg, webp, gif)."));
                }

                Directory.CreateDirectory(Path.Combine(dir, "media"));
                File.Copy(CoverPath, Path.Combine(dir, "media", $"cover{ext}"));
                media = new SgsPackageMediaInfo { Cover = $"media/cover{ext}" };
            }

            return (new SgsPackageManifest
            {
                Game = new SgsPackageGameInfo
                {
                    Title = ContentTitle.Trim(),
                    Symbol = symbol,
                    Kind = Kind,
                    Description = string.IsNullOrWhiteSpace(ContentDescription) ? null : ContentDescription.Trim(),
                    Media = media
                },
                Build = new SgsPackageBuildInfo
                {
                    Version = Version.Trim(),
                    Platform = "any",
                    Executable = first!
                }
            }, null);
        }
        catch (Exception ex)
        {
            return (null, Loc.F("Nie udało się przygotować plików: {0}", ex.Message));
        }
    }

    private void ClearForm()
    {
        ContentTitle = string.Empty;
        ContentSymbol = string.Empty;
        ContentDescription = string.Empty;
        CoverPath = string.Empty;
        Version = "1.0.0";
        ClearFiles();
    }

    private static string Slugify(string? title) =>
        Regex.Replace((title ?? string.Empty).Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private static void TryDelete(string? path, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (isDirectory)
            {
                Directory.Delete(path, recursive: true);
            }
            else
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort temp cleanup
        }
    }
}
