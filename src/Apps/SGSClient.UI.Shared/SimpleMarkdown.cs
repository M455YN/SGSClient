namespace SGSClient.UI;

public enum MarkdownLineKind { Paragraph, Heading, Bullet }

public readonly record struct MarkdownSpan(string Text, bool Bold, bool Italic);

public sealed record MarkdownLine(MarkdownLineKind Kind, IReadOnlyList<MarkdownSpan> Spans);

/// <summary>
/// The small Markdown subset creators use in changelogs: **bold**, *italic*, "- " / "* " bullets, "#" headings.
/// UI-agnostic; WinUI and Avalonia turn the result into inlines.
/// </summary>
public static class SimpleMarkdown
{
    // shortcut: no links, code or nested lists; switch to Markdig if creators need more.
    public static IReadOnlyList<MarkdownLine> Parse(string? text)
    {
        var lines = new List<MarkdownLine>();
        foreach (var raw in (text ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('#'))
            {
                lines.Add(new MarkdownLine(MarkdownLineKind.Heading, ParseInline(trimmed.TrimStart('#').Trim())));
            }
            // "-", "–", "—", "*", "•" + any whitespace (editors often paste a non-breaking space or an en dash)
            else if (trimmed.Length > 1 && "-–—*•".Contains(trimmed[0]) && char.IsWhiteSpace(trimmed[1]))
            {
                lines.Add(new MarkdownLine(MarkdownLineKind.Bullet, ParseInline(trimmed[2..].Trim())));
            }
            else
            {
                lines.Add(new MarkdownLine(MarkdownLineKind.Paragraph, ParseInline(line)));
            }
        }

        return lines;
    }

    private static List<MarkdownSpan> ParseInline(string text)
    {
        var spans = new List<MarkdownSpan>();
        var buffer = new System.Text.StringBuilder();
        bool bold = false, italic = false;

        void Flush()
        {
            if (buffer.Length > 0)
            {
                spans.Add(new MarkdownSpan(buffer.ToString(), bold, italic));
                buffer.Clear();
            }
        }

        for (var i = 0; i < text.Length; i++)
        {
            var isBold = string.CompareOrdinal(text, i, "**", 0, 2) == 0;
            var marker = isBold ? "**" : text[i] == '*' ? "*" : null;
            var open = isBold ? bold : italic;
            // A marker toggles only when it closes an open run or has a matching closer later; otherwise it's a literal "*".
            if (marker is not null && (open || text.IndexOf(marker, i + marker.Length, StringComparison.Ordinal) > i + marker.Length))
            {
                Flush();
                if (isBold)
                {
                    bold = !bold;
                }
                else
                {
                    italic = !italic;
                }

                i += marker.Length - 1;
                continue;
            }

            buffer.Append(text[i]);
        }

        Flush();
        return spans;
    }
}
