using System.Text.RegularExpressions;
using GoomeSharp;

namespace Launchpad.Ui;

using Ui = GoomeSharp.Ui;

public sealed record MarkdownWord(double X, double CenterY, string Text, Font Font, double Size, uint Color, bool Bold, string? Url, double Width);

public sealed record MarkdownBox(double X, double Y, double Width, double Height, uint Fill);

public sealed class MarkdownLayout
{
    public List<MarkdownWord> Words { get; } = [];
    public List<MarkdownBox> Boxes { get; } = [];
    public double Height { get; set; }
}

public static partial class Markdown
{
    private sealed record Span(string Text, bool Bold, bool Code, string? Url);

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)|\*\*(.+?)\*\*|`([^`]+)`|(https?://[^\s)<>]+)")]
    private static partial Regex Inline();

    [GeneratedRegex(@"^(\s*)([-*+]|\d+\.)\s+(.*)$")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex Heading();

    public static MarkdownLayout Layout(string? source, double width)
    {
        var layout = new MarkdownLayout();
        var y = 0.0;
        var paragraph = new List<string>();
        var code = new List<string>();
        var inCode = false;

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
                return;

            y = Place(layout, Spans(string.Join(' ', paragraph)), 0, y, width, 15, Palette.Text) + 10;
            paragraph.Clear();
        }

        foreach (var raw in (source ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();

            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode)
                    y = PlaceCode(layout, code, y, width) + 10;
                else
                    FlushParagraph();

                code.Clear();
                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                code.Add(line);
                continue;
            }

            if (line.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            Match heading = Heading().Match(line);
            Match item = ListItem().Match(line);

            if (heading.Success)
            {
                FlushParagraph();
                var size = heading.Groups[1].Length switch { 1 => 24.0, 2 => 21.0, 3 => 18.0, _ => 16.0 };
                y = Place(layout, Spans(heading.Groups[2].Value, true), 0, y + 6, width, size, Palette.Text) + 8;
            }
            else if (item.Success)
            {
                FlushParagraph();
                var indent = 16 + item.Groups[1].Length * 6;
                var bullet = char.IsDigit(item.Groups[2].Value[0]) ? item.Groups[2].Value : "•";
                layout.Words.Add(new MarkdownWord(indent - 16, y + 12, bullet, Fonts.Main, 15, Palette.Muted, false, null, 0));
                y = Place(layout, Spans(item.Groups[3].Value), indent + 6, y, width, 15, Palette.Text) + 4;
            }
            else if (line.TrimStart().StartsWith('>'))
            {
                FlushParagraph();
                var quoteTop = y;
                y = Place(layout, Spans(line.TrimStart().TrimStart('>').Trim()), 14, y, width, 15, Palette.Muted) + 10;
                layout.Boxes.Add(new MarkdownBox(0, quoteTop, 3, y - 10 - quoteTop, Palette.LineStrong));
            }
            else if (Regex.IsMatch(line, @"^\s*([-*_])\1{2,}\s*$"))
            {
                FlushParagraph();
                layout.Boxes.Add(new MarkdownBox(0, y + 6, width, 1, Palette.Line));
                y += 18;
            }
            else
            {
                paragraph.Add(line.Trim());
            }
        }

        if (inCode)
            y = PlaceCode(layout, code, y, width) + 10;

        FlushParagraph();
        layout.Height = Math.Max(0, y - 10);
        return layout;
    }

    private static List<Span> Spans(string text, bool bold = false)
    {
        var spans = new List<Span>();
        var at = 0;

        foreach (Match match in Inline().Matches(text))
        {
            if (match.Index > at)
                spans.Add(new Span(Clean(text[at..match.Index]), bold, false, null));

            if (match.Groups[1].Success)
                spans.Add(new Span(match.Groups[1].Value, bold, false, match.Groups[2].Value));
            else if (match.Groups[3].Success)
                spans.Add(new Span(match.Groups[3].Value, true, false, null));
            else if (match.Groups[4].Success)
                spans.Add(new Span(match.Groups[4].Value, bold, true, null));
            else
                spans.Add(new Span(match.Value, bold, false, match.Value));

            at = match.Index + match.Length;
        }

        if (at < text.Length)
            spans.Add(new Span(Clean(text[at..]), bold, false, null));

        return spans;
    }

    private static string Clean(string text) => text.Replace("*", "").Replace("\\", "");

    private static double Place(MarkdownLayout layout, List<Span> spans, double left, double top, double width, double size, uint color)
    {
        var lineHeight = size * 1.6;
        var x = left;
        var y = top;

        foreach (Span span in spans)
        {
            Font font = span.Code ? Fonts.Mono : Fonts.Main;
            var fontSize = span.Code ? size - 1.5 : size;
            var wordColor = span.Url != null ? Palette.Accent : span.Code ? Palette.Muted : color;

            var words = span.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var space = font.Measure(fontSize, " ");

            if (span.Text.StartsWith(' ') && x > left)
                x += space;

            for (var i = 0; i < words.Length; i++)
            {
                var wordWidth = font.Measure(fontSize, words[i]);

                if (wordWidth > width - left)
                {
                    words[i] = Chrome.Clip(font, fontSize, words[i], width - left);
                    wordWidth = font.Measure(fontSize, words[i]);
                }

                if (x > left && x + wordWidth > width)
                {
                    x = left;
                    y += lineHeight;
                }

                layout.Words.Add(new MarkdownWord(x, y + lineHeight / 2, words[i], font, fontSize, wordColor, span.Bold, span.Url, wordWidth));
                x += wordWidth;

                if (i < words.Length - 1 || span.Text.EndsWith(' '))
                    x += space;
            }
        }

        return y + lineHeight;
    }

    private static double PlaceCode(MarkdownLayout layout, List<string> lines, double y, double width)
    {
        var lineHeight = 20.0;
        var height = lines.Count * lineHeight + 20;
        layout.Boxes.Add(new MarkdownBox(0, y, width, height, Palette.Bg));

        for (var i = 0; i < lines.Count; i++)
            layout.Words.Add(new MarkdownWord(12, y + 10 + i * lineHeight + lineHeight / 2, Chrome.Clip(Fonts.Mono, 13, lines[i], width - 24), Fonts.Mono, 13, Palette.Muted, false, null, 0));

        return y + height;
    }

    public static void Draw(Ui ui, MarkdownLayout layout, string idPrefix, double left, double top, double originX, double originY, Action<string> openUrl)
    {
        Canvas c = ui.Canvas()!;

        foreach (MarkdownBox box in layout.Boxes)
            c.DrawRRect(originX + left + box.X, originY + top + box.Y, box.Width, box.Height, box.Fill == Palette.Bg ? 8 : 0, box.Fill);

        for (var i = 0; i < layout.Words.Count; i++)
        {
            MarkdownWord word = layout.Words[i];

            if (word.Url != null)
            {
                Item hit = Chrome.Hit(ui, idPrefix + i, left + word.X, top + word.CenterY - 11, word.Width, 22);
                c = ui.Canvas()!;
                Chrome.Text(c, word.Font, word.Size, hit.X, hit.Y + 11, word.Text, hit.Hovered ? 0xFFE46A49 : word.Color);
                c.DrawRect(hit.X, hit.Y + 11 + word.Size * 0.6, word.Width, 1, hit.Hovered ? 0xFFE46A49 : word.Color);

                if (hit.Clicked)
                    openUrl(word.Url);

                continue;
            }

            var wordLeft = originX + left + word.X;
            var wordCenter = originY + top + word.CenterY;

            if (word.Bold)
                Chrome.BoldText(c, word.Font, word.Size, wordLeft, wordCenter, word.Text, word.Color);
            else
                Chrome.Text(c, word.Font, word.Size, wordLeft, wordCenter, word.Text, word.Color);
        }
    }
}
