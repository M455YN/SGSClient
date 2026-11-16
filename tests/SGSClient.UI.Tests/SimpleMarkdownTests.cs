namespace SGSClient.UI.Tests;

public sealed class SimpleMarkdownTests
{
    [Fact]
    public void Parse_HandlesBoldItalicBulletsHeadingsAndLiteralStars()
    {
        var lines = SimpleMarkdown.Parse("# Nowości\r\n- **Nowa** mapa i *szybszy* start\n2 * 3 = 6");

        Assert.Equal(MarkdownLineKind.Heading, lines[0].Kind);
        Assert.Equal("Nowości", Assert.Single(lines[0].Spans).Text);

        Assert.Equal(MarkdownLineKind.Bullet, lines[1].Kind);
        Assert.Equal(
            [
                new MarkdownSpan("Nowa", true, false),
                new MarkdownSpan(" mapa i ", false, false),
                new MarkdownSpan("szybszy", false, true),
                new MarkdownSpan(" start", false, false)
            ],
            lines[1].Spans);

        // an unmatched "*" stays as text
        Assert.Equal(new MarkdownSpan("2 * 3 = 6", false, false), Assert.Single(lines[2].Spans));
    }

    [Theory]
    [InlineData("- Nowa mapa")]
    [InlineData("– Nowa mapa")]
    [InlineData("• Nowa mapa")]
    [InlineData("  - Nowa mapa")]
    public void Parse_AcceptsPastedBulletVariants(string line)
    {
        var parsed = Assert.Single(SimpleMarkdown.Parse(line));
        Assert.Equal(MarkdownLineKind.Bullet, parsed.Kind);
        Assert.Equal("Nowa mapa", Assert.Single(parsed.Spans).Text);
    }
}
