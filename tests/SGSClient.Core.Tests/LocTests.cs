using System.Text.RegularExpressions;
using SGSClient.Core.Localization;

namespace SGSClient.Core.Tests;

[Collection("Loc")]
public class LocTests
{
    [Fact]
    public void Entries_KeepSamePlaceholdersAsSource()
    {
        var rx = new Regex(@"\{(\d+)[^}]*\}");
        foreach (var (pl, en) in Loc.Entries)
        {
            var plIdx = rx.Matches(pl).Select(m => m.Groups[1].Value).Distinct().Order().ToArray();
            var enIdx = rx.Matches(en).Select(m => m.Groups[1].Value).Distinct().Order().ToArray();
            Assert.True(plIdx.SequenceEqual(enIdx), $"Placeholder mismatch: {pl}");
        }
    }

    [Fact]
    public void T_FallsBackToPolishWhenMissingAndKeepsEdgeSpaces()
    {
        try
        {
            Loc.Initialize("en");
            Assert.Equal("Nie ma takiego tekstu", Loc.T("Nie ma takiego tekstu"));
            Assert.Equal(" · default", Loc.T(" · domyślny"));
            Assert.Equal("Library folder added.", Loc.T("Dodano folder biblioteki."));
        }
        finally
        {
            Loc.Initialize("pl");
        }
    }

    [Fact]
    public void TS_TranslatesServerMessagesIncludingCapturedValues()
    {
        try
        {
            Loc.Initialize("en");
            Assert.Equal("The package is empty.", Loc.TS("Paczka jest pusta."));
            Assert.Equal("The package is too large (max 200 MB).", Loc.TS("Paczka jest za duża (max 200 MB)."));
            Assert.Equal("At most 5 genres per game.", Loc.TS("Maksymalnie 5 gatunków na grę."));
            Assert.Equal("Nieznany komunikat", Loc.TS("Nieznany komunikat"));
        }
        finally
        {
            Loc.Initialize("pl");
        }
    }
}
