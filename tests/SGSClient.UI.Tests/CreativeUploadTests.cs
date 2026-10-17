using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

public sealed class CreativeUploadTests
{
    [Fact]
    public void ParseTags_LowercasesDeduplicatesAndSplitsOnSeparators()
    {
        var tags = AddMediaViewModel.ParseTags("Fantasy, fantasy;Sci-Fi\nart\r\n ,");

        Assert.Equal(["fantasy", "sci-fi", "art"], tags);
    }

    [Fact]
    public void ParseTags_DropsTooLongAndCapsAtTen()
    {
        var text = string.Join(",", Enumerable.Range(1, 15).Select(i => $"t{i}")) + "," + new string('x', 31);

        var tags = AddMediaViewModel.ParseTags(text);

        Assert.Equal(10, tags.Count);
        Assert.DoesNotContain(tags, t => t.Length > 30);
        Assert.Empty(AddMediaViewModel.ParseTags(null));
    }
}
