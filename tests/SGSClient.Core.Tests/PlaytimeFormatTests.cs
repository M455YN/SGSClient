using SGSClient.Core.Playtime;

namespace SGSClient.Core.Tests;

public class PlaytimeFormatTests
{
    [Fact]
    public void ToPolish_UnderOneMinute_UsesLessThanLabel()
    {
        Assert.Equal("mniej niż 1 min", PlaytimeFormat.ToPolish(12));
        Assert.Equal("mniej niż 1 min łącznie", PlaytimeFormat.ToPolishTotal(12));
    }

    [Fact]
    public void ToPolish_Minutes_UsesWholeMinutes()
    {
        Assert.Equal("1 min", PlaytimeFormat.ToPolish(60));
        Assert.Equal("45 min", PlaytimeFormat.ToPolish(45 * 60));
    }

    [Fact]
    public void ToPolish_Hours_UsesPolishDecimal()
    {
        Assert.Equal("1,0 godz.", PlaytimeFormat.ToPolish(3600));
        Assert.Equal("2,5 godz.", PlaytimeFormat.ToPolish(9000));
        Assert.Equal("12 godz.", PlaytimeFormat.ToPolish(12 * 3600));
    }
}
