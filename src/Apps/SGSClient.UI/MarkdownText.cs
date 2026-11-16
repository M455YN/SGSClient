using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace SGSClient.UI;

/// <summary>Attached <c>MarkdownText.Text</c>: renders <see cref="SimpleMarkdown"/> into a TextBlock's inlines.</summary>
public sealed class MarkdownText : AvaloniaObject
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<MarkdownText, TextBlock, string?>("Text");

    static MarkdownText() =>
        TextProperty.Changed.AddClassHandler<TextBlock>((block, e) => Apply(block, e.NewValue as string));

    public static string? GetText(TextBlock block) => block.GetValue(TextProperty);

    public static void SetText(TextBlock block, string? value) => block.SetValue(TextProperty, value);

    private static void Apply(TextBlock block, string? text)
    {
        var inlines = new InlineCollection();
        var first = true;
        foreach (var line in SimpleMarkdown.Parse(text))
        {
            if (!first)
            {
                inlines.Add(new LineBreak());
            }

            first = false;
            if (line.Kind == MarkdownLineKind.Bullet)
            {
                inlines.Add(new Run("•  "));
            }

            foreach (var span in line.Spans)
            {
                var run = new Run(span.Text);
                if (span.Bold || line.Kind == MarkdownLineKind.Heading)
                {
                    run.FontWeight = FontWeight.SemiBold;
                }

                if (span.Italic)
                {
                    run.FontStyle = FontStyle.Italic;
                }

                if (line.Kind == MarkdownLineKind.Heading)
                {
                    run.FontSize = block.FontSize * 1.2;
                }

                inlines.Add(run);
            }
        }

        block.Inlines = inlines;
    }
}
