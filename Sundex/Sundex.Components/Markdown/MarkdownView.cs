using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using Sundex.Engine.Asset_Management.Types.Asset;

namespace Sundex.Components.Markdown;

/// <summary>
///     Renders Markdown (see <see cref="MarkdownParser" /> for how much of it) as a scrolling
///     column, styled by <c>md-*</c> classes in the host's sheet. Web images load as the view
///     gets its first width, each in a frame that holds its place until it lands.
///     <para>
///         The text is untrusted. Only http and https ever become a link or a fetch, which the
///         parser guarantees and this class leans on: <see cref="Link.Open()" /> hands its URL to
///         the OS shell, where a path can launch a program.
///     </para>
/// </summary>
public class MarkdownView(UIContext context) : ScrollView(context)
{
    /// <summary>The body text's font. Headings, bold and the rest of the interface are in Lato Bold.</summary>
    public const string BodyFont = "Lato Regular";

    /// <summary>What each wrapped element does with the view's new inner width.</summary>
    private readonly List<Action<float>> _reflows = [];

    private float _reflowedWidth;

    public override string Tag => "markdown";

    [NamedSetting("text-value")]
    public string Markdown
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Rebuild();
        }
    } = string.Empty;

    /// <summary>
    ///     Before the scroll view measures, so the height of the new wrapping is what reaches the
    ///     scroll range. Nothing happens below any width: a label would stop wrapping at 0, and an
    ///     image needs the width to know its decode size.
    /// </summary>
    protected override void DoLayout()
    {
        var width = InnerWidth;
        if (width > 0 && Math.Abs(width - _reflowedWidth) > 0.5f)
        {
            _reflowedWidth = width;
            foreach (var reflow in _reflows) reflow(width);
        }

        base.DoLayout();
    }

    // ponytail: the old labels' GL buffers wait for the GC; nothing replaces its text yet. Dispose
    // their TextBuffers here if a host ever re-renders Markdown often.
    private void Rebuild()
    {
        foreach (var child in Children.ToArray()) RemoveChild(child);
        _reflows.Clear();
        _reflowedWidth = 0;

        FlexPanel? list = null;
        foreach (var block in MarkdownParser.Parse(Markdown))
        {
            if (block is MarkdownListItem item)
            {
                if (list == null) AddChild(list = new FlexPanel(Context) { Classes = ["md-list"] });
                list.AddChild(BuildListItem(item));
                continue;
            }

            list = null;
            if (block is MarkdownHeading && Children.Count > 0)
                AddChild(new Panel(Context) { Classes = ["md-heading-gap"] });
            AddChild(Build(block));
        }

        InvalidateLayout();
    }

    private UIElement Build(MarkdownBlock block)
    {
        return block switch
        {
            MarkdownHeading { Level: var level, Inlines: var inlines } =>
                BuildText(inlines, level switch { 1 => "md-h1", 2 => "md-h2", _ => "md-h3" }, null, () => 0),
            MarkdownParagraph { Inlines: var inlines } => BuildText(inlines, "md-p", BodyFont, () => 0),
            MarkdownQuote quote => BuildQuote(quote),
            MarkdownCodeBlock code => BuildCodeBlock(code),
            MarkdownImage image => BuildImage(image),
            _ => throw new ArgumentOutOfRangeException(nameof(block), block, "Unknown Markdown block.")
        };
    }

    private FlexPanel BuildListItem(MarkdownListItem item)
    {
        var marker = new Label(Context, item.Marker, BodyFont) { Classes = ["md-li-marker"] };
        var row = new FlexPanel(Context) { Classes = ["md-li"], Children = [marker] };
        row.AddChild(BuildText(item.Inlines, "md-p", BodyFont, () => marker.Width.Value + row.Spacing));
        return row;
    }

    private FlexPanel BuildQuote(MarkdownQuote quote)
    {
        var gap = new Panel(Context) { Classes = ["md-quote-gap"] };
        var row = new FlexPanel(Context) { Classes = ["md-quote"], Children = [gap] };
        row.AddChild(BuildText(quote.Inlines, "md-quote-text", BodyFont, () => gap.Width.Value + row.Spacing));
        return row;
    }

    private FlexPanel BuildCodeBlock(MarkdownCodeBlock code)
    {
        var text = new Label(Context, code.Code, BodyFont) { Classes = ["md-code-text"] };
        var box = new FlexPanel(Context) { Classes = ["md-code-block"], Children = [text] };
        _reflows.Add(width => text.WrapWidth = width - 2 * box.Padding);
        return box;
    }

    private MarkdownImageFrame BuildImage(MarkdownImage image)
    {
        var frame = new MarkdownImageFrame(Context, image);
        _reflows.Add(frame.SetViewWidth);
        return frame;
    }

    /// <summary>
    ///     Text with one look is one wrapped label. Anything richer is a wrapping row of one element
    ///     per word, with no spacing, so both kinds keep the font's own line gap and read alike.
    /// </summary>
    // font is the plain text's, null for the interface's bold. inset is how much of the view's width
    // is taken beside this text, read at reflow, once styled.
    private UIElement BuildText(IReadOnlyList<MarkdownInline> inlines, string textClass, string? font,
        Func<float> inset)
    {
        if (inlines.All(inline => inline is { Bold: false, Code: false, Url: null }))
        {
            var label = new Label(Context, string.Concat(inlines.Select(inline => inline.Text)), font)
                { Classes = [textClass] };
            _reflows.Add(width => label.WrapWidth = width - inset());
            return label;
        }

        var lines = SplitLines(inlines).Select(line => BuildFlow(line, textClass, font, inset)).ToList();
        return lines.Count == 1
            ? lines[0]
            : new FlexPanel(Context) { Direction = LayoutDirection.Vertical, Children = [.. lines] };
    }

    private FlexPanel BuildFlow(IEnumerable<MarkdownInline> inlines, string textClass, string? font, Func<float> inset)
    {
        var flow = new FlexPanel(Context) { Wrap = true };
        _reflows.Add(width =>
        {
            // FlexPanel's width has no dirty check of its own.
            flow.Width = width - inset();
            flow.InvalidateLayout();
        });

        // Elements with no space between them must stay on a line together: a comma after a link.
        var unbroken = new List<UIElement>();
        foreach (var inline in inlines)
        {
            var links = new List<Link>();
            foreach (var word in inline.Code ? [$" {inline.Text} "] : Words(inline.Text))
            {
                unbroken.Add(BuildWord(inline, word, textClass, font, links));
                if (!inline.Code && !word.EndsWith(' ')) continue;

                flow.AddChild(unbroken.Count == 1
                    ? unbroken[0]
                    : new FlexPanel(Context) { Children = [.. unbroken] });
                unbroken.Clear();
            }
        }

        if (unbroken.Count > 0)
            flow.AddChild(unbroken.Count == 1 ? unbroken[0] : new FlexPanel(Context) { Children = [.. unbroken] });
        return flow;
    }

    private UIElement BuildWord(MarkdownInline inline, string word, string textClass, string? font, List<Link> links)
    {
        if (inline.Code)
            return new FlexPanel(Context)
            {
                Classes = ["md-code"],
                Children = [new Label(Context, word, BodyFont) { Classes = ["md-code-text"] }]
            };

        if (inline.Url is not { } url)
            return inline.Bold
                ? new Label(Context, word) { Classes = [textClass, "md-strong"] }
                : new Label(Context, word, font) { Classes = [textClass] };

        var link = new Link(Context, word, url, inline.Bold ? null : font)
        {
            Classes = [textClass, "md-link"],
            // Hovering one word of a link underlines all of it.
            OnHoverEnter = _ => links.ForEach(each => each.Underlined = true),
            OnHoverExit = _ => links.ForEach(each => each.Underlined = false)
        };
        links.Add(link);
        return link;
    }

    private static IEnumerable<IReadOnlyList<MarkdownInline>> SplitLines(IReadOnlyList<MarkdownInline> inlines)
    {
        var line = new List<MarkdownInline>();
        foreach (var inline in inlines)
        {
            var parts = inline.Text.Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    yield return line;
                    line = [];
                }

                if (parts[i].Length > 0) line.Add(inline with { Text = parts[i] });
            }
        }

        yield return line;
    }

    /// <summary>Each word with the spaces after it, so the row keeps the gaps without any spacing.</summary>
    private static IEnumerable<string> Words(string text)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != ' ' || (i + 1 < text.Length && text[i + 1] == ' ')) continue;
            yield return text[start..(i + 1)];
            start = i + 1;
        }

        if (start < text.Length) yield return text[start..];
    }
}

/// <summary>
///     An image's place in a <see cref="MarkdownView" />: it reserves the height from the tag's
///     <c>width</c> and <c>height</c>, or 16:9 without them, and takes the texture's real aspect
///     when it lands. On a failure it follows the image's <c>height</c>, so a sheet rule can
///     collapse it; with no rule it keeps the reserved height.
/// </summary>
internal sealed class MarkdownImageFrame : Panel
{
    private readonly MarkdownImage _block;
    private readonly Label _loadingIcon;
    private float _viewWidth;

    public MarkdownImageFrame(UIContext context, MarkdownImage block) : base(context)
    {
        _block = block;
        Width = LiteralOrComputable.AutoSize;
        Height = LiteralOrComputable.AutoSize;
        Classes = ["md-image-frame"];

        _loadingIcon = new Label(context, Icons.Image, Icons.Font)
        {
            Classes = ["md-image-icon"],
            X = LiteralOrComputable.Percent(50),
            Y = LiteralOrComputable.Percent(50),
            AnchorX = Anchor.Center,
            AnchorY = Anchor.Center
        };
        Image = new Image(context)
        {
            Classes = ["md-image"],
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100),
            Storage = StorageLocation.Network
        };
        Children = [_loadingIcon, Image];
    }

    public Image Image { get; }

    /// <summary>
    ///     The room the view has, which is less than layout offers: it asks with the view's whole
    ///     width, scroll bar included. The first call starts the fetch, as it sets how wide to decode.
    /// </summary>
    public void SetViewWidth(float viewWidth)
    {
        _viewWidth = viewWidth;
        InvalidateLayout();
        if (Image.Src.Length > 0) return;

        var shown = Math.Min(viewWidth, _block.Width ?? viewWidth);
        Image.DecodeWidth = (int)MathF.Ceiling(shown * Context.PixelScale.X);
        Image.Src = _block.Url;
    }

    public override (float width, float height) Measure(float parentWidth, float parentHeight)
    {
        var texture = Image.IsLoaded ? Image.Texture : null;
        var room = _viewWidth > 0 ? Math.Min(_viewWidth, parentWidth) : parentWidth;
        var width = Math.Min(room, _block.Width ?? texture?.Width ?? room);

        if (texture is { Width: > 0 }) return (width, width * texture.Height / texture.Width);
        if (Image is { Errored: true, Height: { IsPercentage: false, Auto: false } height })
            return (width, height.Value);
        return _block is { Width: > 0, Height: > 0 }
            ? (width, width * _block.Height.Value / _block.Width.Value)
            : (width, width * 9 / 16);
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        _loadingIcon.Visible = Image is { IsLoaded: false, Errored: false };
    }
}
