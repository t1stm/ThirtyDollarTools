using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using VisualizerScene.Settings;

namespace EditorScene.Scenes.Layout;

/// <summary>
///     Drives the hint bar under the grid area. The tree lives in
///     Scenes/Layout/Hint Bar/HintBar.snx.xml and its handles are resolved by that document's
///     logic; this type writes the text into it, wraps it, and sizes the bar - and the grid
///     area above it - to however many lines that takes.
/// </summary>
internal sealed class HintBarController
{
    /// <summary>The hint bar at one line - what it is whenever the hint fits the grid area's width.</summary>
    private const float HintBarHeight = 26;

    /// <summary>What each further line of a wrapped hint adds to that (line box + HintLines' spacing).</summary>
    private const float HintLineHeight = 15;

    /// <summary>
    ///     The most lines a hint may take. Five is what the faithful legend needs at the
    ///     1080 px minimum, where the bar has only the grid area's width to write in.
    /// </summary>
    private const int HintMaxLines = 5;

    /// <summary>How much of the measured width a line may use - slack for <see cref="HintBudget" />'s average.</summary>
    private const float HintFitMargin = 0.94f;

    private readonly FlexPanel _bar;
    private readonly UIContext _context;
    private readonly FlexPanel _gridArea;
    private readonly Panel _gutter;
    private readonly float _headerHeight;
    private readonly Label _label;
    private readonly FlexPanel _lines;

    /// <summary>Whether the faithful editor is the open view - it has a legend of its own.</summary>
    private bool _faithful;

    /// <summary>The hint currently shown, or null for the static legend. Re-split on every resize.</summary>
    private string? _hintText;

    /// <summary>The last window height <see cref="Resize" /> saw; the hint bar's Y is derived from it.</summary>
    private float _windowHeight;

    /// <summary><paramref name="headerHeight" /> is the band above the grid area, which the area's height leaves out.</summary>
    public HintBarController(UIContext context, FlexPanel bar, Panel gutter, FlexPanel lines, Label label,
        FlexPanel gridArea, float headerHeight)
    {
        _context = context;
        _bar = bar;
        _gutter = gutter;
        _lines = lines;
        _label = label;
        _gridArea = gridArea;
        _headerHeight = headerHeight;

        // The legend is written into a label once and then sits there, so a rebind has to
        // push it back out. Never unsubscribed: the editor interface is built during the boot
        // preload and lives as long as the process does.
        Keybinds.Changed += () => Show(null);
    }

    /// <summary>
    ///     The hint bar's default text: gestures and shortcuts that have no on-screen
    ///     control of their own, so nothing else in the UI hints they exist. Built from the
    ///     bind table rather than written out, so a rebound shortcut is named correctly and
    ///     a Mac reads Cmd.
    /// </summary>
    private string HintLegend => _faithful ? FaithfulLegend : GridLegend;

    /// <summary>
    ///     The faithful editor's own gestures. Nothing here has an on-screen control, and
    ///     none of it matches the grid views' bindings, so the bar says so outright.
    /// </summary>
    private static string FaithfulLegend =>
        "Click the palette to add, right-click to preview  •  " +
        "Draw: click a slot to remove it, Shift+click to copy, drag it to reorder  •  " +
        $"Select: click to select, {Keybinds.PrimaryName}+click to add, Left/Right walk it, " +
        $"{Keybinds.PrimaryName}+Shift+ extends it, Space+ moves it, " +
        "Enter places another, Del removes, Tab adds a divider, C a combine, X cuts the hovered instrument  •  " +
        $"{Keybinds.Get(Bind.EditorCopy)}/{Keybinds.Get(Bind.EditorPaste)} copy/paste  •  " +
        $"Scroll a slot (Up/Down under Select) for value, {Keybinds.PrimaryName}+ volume, Shift+ pan";

    private static string GridLegend =>
        "Double-click a track to open it, right-click for options  •  " +
        $"Drag a note's border for length, a keyframe marker to shape it ({Keybinds.PrimaryName}+drag: all)  •  " +
        $"{Keybinds.Get(Bind.EditorCopy)}/{Keybinds.Get(Bind.EditorPaste)}/{Keybinds.Get(Bind.EditorCut)} " +
        $"copy/paste/cut, {Keybinds.Get(Bind.EditorSelectAll)} select all  •  " +
        $"Middle-drag to pan, {Keybinds.PrimaryName}+scroll to zoom, Shift+drag to fine-snap";

    /// <summary>
    ///     Follows the view the grid area now shows: indents the hint text past its gutter -
    ///     the arrangement's M/S lane header, the note editor's narrower value ruler, none for
    ///     the faithful panel - so it starts at the first grid column, and reverts to that
    ///     view's legend. The bar's own padding already covers part of the offset.
    /// </summary>
    public void SetView(float gutterWidth, bool faithful)
    {
        _faithful = faithful;
        _gutter.Width = Math.Max(0, gutterWidth - _bar.Padding);
        Show(null);
    }

    /// <summary>
    ///     Shows contextual text in the hint bar, or reverts to the static legend when
    ///     <paramref name="text" /> is null (hover exit). Wired into every control whose
    ///     purpose isn't obvious at a glance (see EditorTrack/LaneHeader's OnHint).
    /// </summary>
    public void Show(string? text)
    {
        _hintText = text;
        Layout();
    }

    /// <summary>The bar spans the grid area only - the track column and inspector run full height beside it.</summary>
    public void Resize(float gridWidth, float windowHeight)
    {
        _bar.Width = gridWidth;
        _windowHeight = windowHeight;
        // Sets the bar's height and Y, and the grid area's height, from how many lines the
        // hint breaks into at this width - a narrower window wraps it onto more of them.
        Layout();
    }

    /// <summary>
    ///     Breaks the current hint across as many of the bar's lines as it needs, and sizes
    ///     the bar (and the grid area above it) to match. A Label neither wraps nor clips, so
    ///     without this a legend wider than the bar paints across the inspector column and off
    ///     the window.
    /// </summary>
    private void Layout()
    {
        var budget = HintBudget();
        var lines = WrapHint(_hintText ?? HintLegend, budget, HintMaxLines);
        // The bar is sized for this view's legend, not for whatever it happens to be
        // showing: a hover hint is one line and a legend is several, and resizing between
        // them would shuffle the whole view every time the pointer crossed a control.
        var barLines = _hintText is null ? lines.Count : WrapHint(HintLegend, budget, HintMaxLines).Count;
        var wanted = Math.Max(barLines, lines.Count);

        // Exactly one label per line the bar is sized for - a spare blank one still takes a
        // line box in the stack, which then outgrows the bar and gets centred half out of it.
        // The size and color are copied off the markup's own line rather than left to the
        // hint-line class: AddChild passes down whatever sheet the element last stored, and
        // that is the root's, which never saw HintBar.snx.ss - a component's sheet only
        // styles its own document.
        while (_lines.Children.Count < wanted)
            _lines.AddChild(new Label(_context, "")
            {
                Classes = ["hint-line"], FontSizePx = _label.FontSizePx, Color = _label.Color
            });
        while (_lines.Children.Count > wanted) _lines.RemoveChild(_lines.Children[^1]);

        for (var i = 0; i < wanted; i++)
            ((Label)_lines.Children[i]).SetTextContents(i < lines.Count ? lines[i] : "");

        var height = HintBarHeight + (wanted - 1) * HintLineHeight;
        _bar.Height = height;
        _bar.Y = _windowHeight - height;
        _gridArea.Height = _windowHeight - _headerHeight - height;
    }

    /// <summary>
    ///     How many characters of hint fit on one of the bar's lines.
    ///     <para>
    ///         ponytail: derived from the legend's average glyph advance, measured once off the
    ///         whole string, because a Label remeasures by rebuilding its glyph buffer and the
    ///         hint changes on every hover. The average runs narrow on a line of unusually wide
    ///         glyphs, which <see cref="HintFitMargin" /> absorbs; measure per line if a legend
    ///         ever has to sit exactly flush.
    ///     </para>
    /// </summary>
    private int HintBudget()
    {
        var legend = HintLegend;
        if (legend.Length == 0) return int.MaxValue;

        _label.SetTextContents(legend);
        var perChar = _label.Width.Value / legend.Length;
        if (perChar <= 0) return int.MaxValue;

        var available = _bar.Width.Value - 2 * _bar.Padding - _gutter.Width.Value;
        return (int)(available * HintFitMargin / perChar);
    }

    /// <summary>
    ///     Greedily breaks <paramref name="text" /> into at most <paramref name="maxLines" />
    ///     lines of at most <paramref name="budget" /> characters, never inside a word. The
    ///     last line takes whatever is left, over budget included, so the tail of a legend is
    ///     never dropped. Internal for <c>MinResolutionLayoutTests</c>.
    /// </summary>
    internal static List<string> WrapHint(string text, int budget, int maxLines)
    {
        budget = Math.Max(1, budget);
        var lines = new List<string>();
        var rest = text.AsSpan();

        while (rest.Length > budget && lines.Count < maxLines - 1)
        {
            // Back off to the last space inside the budget; a run with none in it (no legend
            // today has one) goes out over-long rather than split mid-word.
            var cut = rest[..budget].LastIndexOf(' ');
            if (cut <= 0) break;
            lines.Add(rest[..cut].ToString());
            rest = rest[(cut + 1)..];
        }

        lines.Add(rest.ToString());
        return lines;
    }
}
