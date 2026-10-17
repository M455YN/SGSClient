using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>One file waiting to be published as its own artwork or track.</summary>
public sealed partial class UploadEntry : ObservableObject
{
    public required string Path { get; init; }
    public string FileName => System.IO.Path.GetFileName(Path);

    [ObservableProperty] public partial string Title { get; set; } = string.Empty;
    [ObservableProperty] public partial string StateText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsDone { get; set; }
}

/// <summary>"Dodaj grafikę" / "Dodaj muzykę": pick many files, each becomes its own item (DeviantArt / SoundCloud style).</summary>
public sealed partial class AddMediaViewModel(ISgsApiService? api, IAuthSession? auth) : ObservableObject
{
    public static readonly string[] ArtExtensions = [".png", ".jpg", ".jpeg", ".webp", ".gif"];
    public static readonly string[] MusicExtensions = [".mp3", ".flac", ".wav", ".ogg", ".m4a"];

    public ObservableCollection<UploadEntry> Entries { get; } = [];
    public ObservableCollection<string> Issues { get; } = [];

    /// <summary>art | music</summary>
    [ObservableProperty] public partial string Kind { get; set; } = "art";
    [ObservableProperty] public partial string Tags { get; set; } = string.Empty;
    [ObservableProperty] public partial string Description { get; set; } = string.Empty;

    /// <summary>Shared cover for music; artwork uses the image itself.</summary>
    [ObservableProperty] public partial string CoverPath { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial string AuthHint { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial bool HasEntries { get; set; }

    public bool IsMusic => Kind == "music";
    public bool ShowCover => IsMusic;
    public IReadOnlyList<string> Extensions => IsMusic ? MusicExtensions : ArtExtensions;

    public string PageTitle => Loc.T(IsMusic ? "Dodaj muzykę" : "Dodaj grafikę");
    public string AddFilesLabel => Loc.T(IsMusic ? "Wybierz utwory…" : "Wybierz obrazy…");
    public string DropHint => Loc.T(IsMusic
        ? "Dodaj jeden lub wiele utworów. Każdy zostanie opublikowany osobno."
        : "Dodaj jeden lub wiele obrazów. Każdy zostanie opublikowany jako osobna praca.");

    partial void OnKindChanged(string value)
    {
        OnPropertyChanged(nameof(IsMusic));
        OnPropertyChanged(nameof(ShowCover));
        OnPropertyChanged(nameof(Extensions));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(AddFilesLabel));
        OnPropertyChanged(nameof(DropHint));
    }

    public void PrepareFor(string kind)
    {
        if (Kind != kind)
        {
            Entries.Clear();
            HasEntries = false;
            CoverPath = string.Empty;
            Kind = kind;
        }

        StatusText = string.Empty;
        Issues.Clear();
        RefreshAuthHint();
    }

    public void RefreshAuthHint() => AuthHint = AuthHints.For(auth);

    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (!Extensions.Contains(ext) || Entries.Any(e => e.Path == path))
            {
                continue;
            }

            Entries.Add(new UploadEntry
            {
                Path = path,
                Title = TitleFromFileName(path),
                StateText = Loc.T("Gotowe do publikacji")
            });
        }

        HasEntries = Entries.Count > 0;
    }

    [RelayCommand]
    private void Remove(UploadEntry? entry)
    {
        if (entry is not null && !IsBusy)
        {
            Entries.Remove(entry);
            HasEntries = Entries.Count > 0;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        if (!IsBusy)
        {
            Entries.Clear();
            HasEntries = false;
        }
    }

    [RelayCommand]
    private async Task PublishAllAsync()
    {
        if (api is null || IsBusy)
        {
            return;
        }

        var pending = Entries.Where(e => !e.IsDone).ToList();
        if (pending.Count == 0 || pending.Any(e => string.IsNullOrWhiteSpace(e.Title)))
        {
            StatusText = Loc.T("Dodaj pliki i nadaj każdej pracy tytuł.");
            return;
        }

        RefreshAuthHint();
        IsBusy = true;
        Issues.Clear();
        var tags = ParseTags(Tags);
        var done = 0;
        try
        {
            foreach (var entry in pending)
            {
                entry.StateText = Loc.T("Wysyłanie…");
                var index = done;
                var progress = new Progress<double>(p => Progress = (index + p) / pending.Count);
                var errors = IsMusic
                    ? (await api.UploadTrackAsync(entry.Title.Trim(), Description, tags, entry.Path, NullIfEmpty(CoverPath), progress)).Errors
                    : (await api.UploadArtworkAsync(entry.Title.Trim(), Description, tags, entry.Path, progress)).Errors;
                if (errors.Count == 0)
                {
                    entry.IsDone = true;
                    entry.StateText = Loc.T("Opublikowano");
                    done++;
                }
                else
                {
                    entry.StateText = Loc.T("Nie udało się");
                    foreach (var error in errors)
                    {
                        Issues.Add($"{entry.Title}: {error}");
                    }
                }

                Progress = (double)(done) / pending.Count;
            }

            StatusText = done == pending.Count
                ? Loc.F("Opublikowano: {0}. Znajdziesz to w Twórczości.", done)
                : Loc.F("Opublikowano {0} z {1}. Popraw błędy i spróbuj ponownie.", done, pending.Count);
            if (done == pending.Count)
            {
                Entries.Clear();
                HasEntries = false;
                Description = string.Empty;
                Tags = string.Empty;
                CoverPath = string.Empty;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Splits comma/newline separated tags, lowercased, de-duplicated, at most 10.</summary>
    public static IReadOnlyList<string> ParseTags(string? text) =>
        (text ?? string.Empty)
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length is > 0 and <= 30)
            .Distinct()
            .Take(10)
            .ToList();

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    internal static string TitleFromFileName(string path) =>
        System.IO.Path.GetFileNameWithoutExtension(path).Replace('_', ' ').Replace('-', ' ').Trim();
}
