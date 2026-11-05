using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Sundex.Components.Markdown;

public abstract record MarkdownBlock;

public sealed record MarkdownHeading(int Level, IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record MarkdownParagraph(IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

/// <param name="Marker">"•" or the item's number, "2.".</param>
public sealed record MarkdownListItem(string Marker, IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record MarkdownQuote(IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record MarkdownCodeBlock(string Code) : MarkdownBlock;

/// <param name="Url">Always http(s).</param>
/// <param name="Width">The tag's <c>width</c> attribute, when it has one in pixels.</param>
/// <param name="Height">The tag's <c>height</c> attribute, when it has one in pixels.</param>
public sealed record MarkdownImage(string Url, string Alt, int? Width, int? Height) : MarkdownBlock;

/// <summary>A run of text with one look. <c>\n</c> in <paramref name="Text" /> is a line break.</summary>
/// <param name="Url">Where it links to. Always http(s), null for anything else.</param>
public readonly record struct MarkdownInline(string Text, bool Bold = false, bool Code = false, string? Url = null);

/// <summary>
///     Reads the subset of Markdown that GitHub release bodies use, plus the raw <c>&lt;img&gt;</c>
///     tags they carry their screenshots in. Pure: it needs no GL context.
///     <para>
///         The text is untrusted, so only http and https come out as a link or an image: anything
///         else, a file: URL or a local path, stays plain text. As on GitHub, a single newline
///         inside a paragraph is a line break. Tables come out as text, and the rest of HTML is
///         dropped with its text kept. <c>*italic*</c> loses its markers: no italic face is bundled.
///     </para>
/// </summary>
// ponytail: nested lists flatten, and setext headings, reference links and thematic breaks read as
// text. No release uses them; Markdig is the upgrade if this ever takes arbitrary Markdown.
public static partial class MarkdownParser
{
    public static IReadOnlyList<MarkdownBlock> Parse(string markdown)
    {
        var blocks = new List<MarkdownBlock>();
        var open = new OpenBlock(blocks);
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            if (Fence().Match(lines[i]) is { Success: true } fence)
            {
                open.Close();
                i = ReadFence(lines, i + 1, fence.Groups[1].Value, blocks);
                continue;
            }

            var position = 0;
            foreach (Match image in Image().Matches(lines[i]))
            {
                ReadText(lines[i][position..image.Index], open, blocks);
                ReadImage(image, open, blocks);
                position = image.Index + image.Length;
            }

            ReadText(lines[i][position..], open, blocks, position == 0);
        }

        open.Close();
        return blocks;
    }

    /// <summary>The address as a normalised http(s) URL. False for anything else, which must not reach the shell or the disk.</summary>
    public static bool TryWebUrl(string url, out string webUrl)
    {
        webUrl = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return false;

        webUrl = uri.AbsoluteUri;
        return true;
    }

    /// <returns>The index of the fence's closing line, or the last line if it never closes.</returns>
    private static int ReadFence(string[] lines, int start, string marker, List<MarkdownBlock> blocks)
    {
        var end = start;
        while (end < lines.Length && !IsClosingFence(lines[end], marker)) end++;

        blocks.Add(new MarkdownCodeBlock(string.Join('\n', lines[start..end])));
        return end;
    }

    private static bool IsClosingFence(string line, string marker)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= marker.Length && trimmed.All(c => c == marker[0]);
    }

    // wholeLine is false for the text beside an image: blank there says nothing about the paragraph.
    private static void ReadText(string line, OpenBlock open, List<MarkdownBlock> blocks, bool wholeLine = false)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            if (wholeLine) open.Close();
            return;
        }

        if (Heading().Match(line) is { Success: true } heading)
        {
            open.Close();
            blocks.Add(new MarkdownHeading(heading.Groups[1].Length, ParseInlines(heading.Groups[2].Value)));
        }
        else if (ListItem().Match(line) is { Success: true } item)
        {
            var marker = item.Groups[1].Value;
            open.Start(text => new MarkdownListItem(
                char.IsDigit(marker[0]) ? $"{marker[..^1]}." : "•", ParseInlines(text)), item.Groups[2].Value);
        }
        else if (Quote().Match(line) is { Success: true } quote)
        {
            if (open.IsQuote) open.Append(quote.Groups[1].Value);
            else open.Start(text => new MarkdownQuote(ParseInlines(text)), quote.Groups[1].Value, true);
        }
        else if (open.IsOpen)
        {
            // A plain line continues whatever is open, as it does on GitHub.
            open.Append(line);
        }
        else
        {
            open.Start(text => new MarkdownParagraph(ParseInlines(text)), line);
        }
    }

    private static void ReadImage(Match match, OpenBlock open, List<MarkdownBlock> blocks)
    {
        string url, alt;
        int? width = null, height = null;

        if (match.Groups["md"].Success)
        {
            alt = match.Groups["alt"].Value;
            url = match.Groups["md"].Value;
        }
        else
        {
            var attributes = Attribute().Matches(match.Value)
                .ToLookup(a => a.Groups[1].Value.ToLowerInvariant(), a => a.Groups[2].Value + a.Groups[3].Value + a.Groups[4].Value);
            url = attributes["src"].FirstOrDefault() ?? string.Empty;
            alt = attributes["alt"].FirstOrDefault() ?? string.Empty;
            width = Pixels(attributes["width"].FirstOrDefault());
            height = Pixels(attributes["height"].FirstOrDefault());
        }

        open.Close();
        alt = WebUtility.HtmlDecode(alt);
        if (TryWebUrl(WebUtility.HtmlDecode(url), out var webUrl))
            blocks.Add(new MarkdownImage(webUrl, alt, width, height));
        else if (!string.IsNullOrWhiteSpace(alt))
            blocks.Add(new MarkdownParagraph([new MarkdownInline(alt)]));
    }

    private static int? Pixels(string? value)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var pixels) && pixels > 0
            ? pixels
            : null;
    }

    private static IReadOnlyList<MarkdownInline> ParseInlines(string text)
    {
        var inlines = new List<MarkdownInline>();
        var run = new StringBuilder();
        var bold = false;
        char? italic = null;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var previous = i > 0 ? text[i - 1] : ' ';
            var next = i + 1 < text.Length ? text[i + 1] : ' ';

            switch (c)
            {
                case '\\' when char.IsPunctuation(next) || char.IsSymbol(next):
                    run.Append(next);
                    i++;
                    continue;

                case '`':
                {
                    var length = 1;
                    while (i + length < text.Length && text[i + length] == '`') length++;
                    var fence = new string('`', length);
                    var close = text.IndexOf(fence, i + length, StringComparison.Ordinal);
                    if (close < 0) break;

                    Flush();
                    inlines.Add(new MarkdownInline(text[(i + length)..close].Trim(), bold, true));
                    i = close + length - 1;
                    continue;
                }

                case '*' or '_' when next == c:
                {
                    var marker = text.Substring(i, 2);
                    // snake__case stays text; only an opening marker needs a closing one ahead.
                    var opens = (c == '*' || !char.IsLetterOrDigit(previous)) &&
                                text.IndexOf(marker, i + 2, StringComparison.Ordinal) >= 0;
                    if (!bold && !opens) break;

                    Flush();
                    bold = !bold;
                    i++;
                    continue;
                }

                case '*' or '_':
                {
                    if (italic == c && !char.IsWhiteSpace(previous))
                    {
                        italic = null;
                        continue;
                    }

                    var opens = italic == null && !char.IsWhiteSpace(next) && !char.IsLetterOrDigit(previous) &&
                                text.IndexOf(c, i + 1) >= 0;
                    if (!opens) break;

                    italic = c;
                    continue;
                }

                case '[' when Link().Match(text, i) is { Success: true } link:
                {
                    var label = link.Groups[1].Value;
                    if (TryWebUrl(WebUtility.HtmlDecode(link.Groups[2].Value), out var url))
                    {
                        Flush();
                        inlines.Add(new MarkdownInline(WebUtility.HtmlDecode(label), bold, false, url));
                    }
                    else
                    {
                        run.Append(label);
                    }

                    i += link.Length - 1;
                    continue;
                }

                case '<' when Tag().Match(text, i) is { Success: true } tag:
                {
                    // <https://...> is Markdown's own way of writing a link.
                    if (tag.Groups[1].Success && TryWebUrl(tag.Groups[1].Value, out var url))
                    {
                        Flush();
                        inlines.Add(new MarkdownInline(tag.Groups[1].Value, bold, false, url));
                    }

                    i += tag.Length - 1;
                    continue;
                }

                case 'h' when !char.IsLetterOrDigit(previous) && BareUrl().Match(text, i) is { Success: true } bare:
                {
                    var address = bare.Value.TrimEnd('.', ',', ':', ';', '!', '?');
                    if (!TryWebUrl(address, out var url)) break;

                    Flush();
                    inlines.Add(new MarkdownInline(address, bold, false, url));
                    i += address.Length - 1;
                    continue;
                }
            }

            run.Append(c);
        }

        Flush();
        return inlines;

        void Flush()
        {
            if (run.Length == 0) return;
            var value = Spaces().Replace(WebUtility.HtmlDecode(run.ToString()), " ");
            run.Clear();
            if (value.Length > 0) inlines.Add(new MarkdownInline(value, bold));
        }
    }

    /// <summary>The paragraph, list item or quote still being read, until a blank line or another block ends it.</summary>
    private sealed class OpenBlock(List<MarkdownBlock> blocks)
    {
        private readonly List<string> _lines = [];
        private Func<string, MarkdownBlock>? _build;

        public bool IsOpen => _build != null;
        public bool IsQuote { get; private set; }

        public void Start(Func<string, MarkdownBlock> build, string line, bool quote = false)
        {
            Close();
            _build = build;
            IsQuote = quote;
            _lines.Add(line.Trim());
        }

        public void Append(string line)
        {
            _lines.Add(line.Trim());
        }

        public void Close()
        {
            if (_build != null) blocks.Add(_build(string.Join('\n', _lines)));
            _build = null;
            IsQuote = false;
            _lines.Clear();
        }
    }

    [GeneratedRegex("^ {0,3}(`{3,}|~{3,})")]
    private static partial Regex Fence();

    [GeneratedRegex(@"^ {0,3}(#{1,6})[ \t]+(.*?)(?:[ \t]+#+)?[ \t]*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^\s*([-*+]|\d{1,9}[.)])[ \t]+(.*)$")]
    private static partial Regex ListItem();

    [GeneratedRegex("^ {0,3}> ?(.*)$")]
    private static partial Regex Quote();

    [GeneratedRegex("""<img\b[^>]*>|!\[(?<alt>[^\]]*)\]\(\s*<?(?<md>[^\s)>]+)>?(?:\s+"[^"]*")?\s*\)""", RegexOptions.IgnoreCase)]
    private static partial Regex Image();

    [GeneratedRegex("""([\w-]+)\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))""")]
    private static partial Regex Attribute();

    [GeneratedRegex("""\G\[([^\]\n]*)\]\(\s*<?([^\s)>]+)>?(?:\s+"[^"]*")?\s*\)""")]
    private static partial Regex Link();

    // A comment, an autolink, or any tag.
    [GeneratedRegex(@"\G(?:<!--.*?-->|<(https?://[^\s>]+)>|</?[a-zA-Z][^>\n]*>)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Tag();

    [GeneratedRegex("""\Ghttps?://[^\s<>()\[\]"'`]+""")]
    private static partial Regex BareUrl();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex Spaces();
}
