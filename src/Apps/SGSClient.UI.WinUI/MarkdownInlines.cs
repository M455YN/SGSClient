using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace SGSClient.UI.WinUI;

/// <summary>Attached <c>MarkdownInlines.Text</c>: renders <see cref="SimpleMarkdown"/> into a TextBlock's inlines.</summary>
public static class MarkdownInlines
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(MarkdownInlines),
        new PropertyMetadata(null, (d, e) =>
        {
            if (d is TextBlock block)
            {
                Apply(block, e.NewValue as string);
            }
        }));

    public static string? GetText(TextBlock block) => (string?)block.GetValue(TextProperty);

    public static void SetText(TextBlock block, string? value) => block.SetValue(TextProperty, value);

    public static void Apply(TextBlock target, string? text)
    {
        target.Inlines.Clear();
        var first = true;
        foreach (var line in SimpleMarkdown.Parse(text))
        {
            if (!first)
            {
                target.Inlines.Add(new LineBreak());
            }

            first = false;
            if (line.Kind == MarkdownLineKind.Bullet)
            {
                target.Inlines.Add(new Run { Text = "•  " });
            }

            foreach (var span in line.Spans)
            {
                var run = new Run { Text = span.Text };
                if (span.Bold || line.Kind == MarkdownLineKind.Heading)
                {
                    run.FontWeight = FontWeights.SemiBold;
                }

                if (span.Italic)
                {
                    run.FontStyle = Windows.UI.Text.FontStyle.Italic;
                }

                if (line.Kind == MarkdownLineKind.Heading)
                {
                    run.FontSize = target.FontSize * 1.2;
                }

                target.Inlines.Add(run);
            }
        }
    }
}
