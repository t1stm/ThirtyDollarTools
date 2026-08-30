using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;
using VisualizerScene.Objects.Playfield;

namespace EditorScene.Scenes.Views;

/// <summary>
///     The website's Sounds + Actions sections, as the faithful editor's palette: the whole
///     sample grid is replaced by a wrapping grid of project instruments (a captioned tile
///     each), and the sixteen actions keep their place to the right of it. Both are drawn
///     with the playfield's own look - see
///     <see cref="EventCanvas" />.
///     Pure view: clicking an entry fires the matching callback and the owner
///     (<see cref="Scenes.EditorInterface" />) decides what it inserts.
/// </summary>
public sealed class FaithfulPalette : FlexPanel
{
    private readonly EventCanvas _actions;

    /// <summary>Every instrument cell's canvas, so a scale change can reach all of them.</summary>
    private readonly List<EventCanvas> _cellCanvases = [];

    /// <summary>
    ///     Longest instrument name a cell caption shows. Labels neither wrap nor ellipsise and
    ///     a cell is barely wider than its tile, so a longer name is cut here and the hint bar
    ///     carries the whole one.
    /// </summary>
    private const int NameLimit = 12;

    private readonly FaithfulScale _scale;
    private bool _actionsFilled;

    private readonly ScrollView _instruments;

    /// <summary>The wrapping grid inside <see cref="_instruments" /> - the cells' parent.</summary>
    private readonly FlexPanel _grid;

    private readonly PlayfieldSettings _settings;
    private readonly EditorState _state;

    /// <summary>Instruments as of the last rebuild, so an unrelated project change can skip one.</summary>
    private readonly List<(Instrument Instrument, string Name, int Sounds)> _built = [];

    public FaithfulPalette(UIContext context, EditorState state, PlayfieldSettings settings, FaithfulScale scale)
        : base(context)
    {
        _state = state;
        _settings = settings;
        _scale = scale;
        Classes = ["faithful-palette"];

        _instruments = new ScrollView(context) { Classes = ["faithful-instruments"] };
        _grid = new FlexPanel(context) { Classes = ["faithful-palette-grid"] };
        _instruments.AddChild(_grid);
        var modify = new Button(context, "Modify")
        {
            Classes = ["tool-button"],
            OnClick = _ => OnModifyInstruments?.Invoke()
        };

        _actions = new EventCanvas(context, settings, FaithfulSizing.SoundSize, FaithfulSizing.Margin)
        {
            Width = LiteralOrComputable.Percent(100),
            BreakOnDividers = false, // "!divider" is one palette entry here, not a line break
            Scale = scale,
            OnPick = index => OnPickAction?.Invoke(FaithfulAction.All[index]),
            OnPreview = index => OnHint?.Invoke(FaithfulAction.All[index].Hint)
        };

        // Both sections are boxes with a heading, like the site's - the wrapper carries the
        // fill, the scroller inside it stays transparent.
        Children =
        [
            Section(context, "Instruments", _instruments, "faithful-section-instruments", modify),
            Section(context, "Actions", Scroller(context, "faithful-actions", _actions), "faithful-section-actions")
        ];
    }

    /// <summary>Clicking an instrument cell - appends a sound item playing it.</summary>
    public Action<Instrument>? OnPickInstrument { get; set; }

    /// <summary>Right-clicking an instrument cell.</summary>
    public Action<Instrument>? OnPreviewInstrument { get; set; }

    /// <summary>Clicking an action - the owner prompts for its amount when it takes one.</summary>
    public Action<FaithfulAction>? OnPickAction { get; set; }

    /// <summary>
    ///     The header's "Modify" button; wired to the same instrument selector the note
    ///     editor's "Instrument: -" opens, so adding, editing and deleting one is the same
    ///     dialog in both editors.
    /// </summary>
    public Action? OnModifyInstruments { get; set; }

    /// <summary>Hover/right-click hint text, relayed to the hint bar. Null clears it.</summary>
    public Action<string?>? OnHint { get; set; }

    /// <summary>Redraws every palette canvas at the shared scale; wired to FaithfulScale.Changed.</summary>
    public void RefreshScale()
    {
        _actions.RefreshScale();
        foreach (var canvas in _cellCanvases) canvas.RefreshScale();
    }

    /// <summary>
    ///     Rebuilds the instrument cells. Called on every project change, including per-frame
    ///     ones, so it skips out when nothing about the cells would differ - same guard as
    ///     <see cref="Scenes.Layout.TrackListPanel.Rebuild" />.
    /// </summary>
    public void Rebuild()
    {
        // The atlases finish loading after the editor is built, and drawing a sound the store
        // doesn't hold yet throws - so nothing is drawn until they are in. Rebuild is called
        // again when a faithful track is opened, by which point they always are.
        if (!_settings.AtlasStore.StaticSounds.ContainsKey("#missing")) return;

        if (!_actionsFilled)
        {
            _actions.SetEvents(FaithfulAction.All.Select(action => action.PaletteEvent()));
            _actionsFilled = true;
        }

        var instruments = _state.Project.Instruments;
        if (Unchanged(instruments)) return;

        foreach (var child in _grid.Children.ToArray()) _grid.RemoveChild(child);
        _cellCanvases.Clear();

        foreach (var instrument in instruments)
            _grid.AddChild(NewCell(instrument));

        _built.Clear();
        _built.AddRange(instruments.Select(i => (i, i.Name, i.Sounds.Count)));
    }

    private bool Unchanged(IReadOnlyList<Instrument> instruments)
    {
        if (instruments.Count != _built.Count) return false;
        for (var i = 0; i < instruments.Count; i++)
            if (instruments[i] != _built[i].Instrument || instruments[i].Name != _built[i].Name ||
                instruments[i].Sounds.Count != _built[i].Sounds)
                return false;
        return true;
    }

    /// <summary>
    ///     One palette cell: the instrument's name above the sound it leads with, drawn at the
    ///     shared scale so a tile here matches the one it inserts. Only the first sound is
    ///     drawn - a cell is barely wider than one tile - with a "xN" beside the name for a
    ///     layered instrument. The count rides in the caption rather than on the tile, so the
    ///     tile itself keeps the site's own look.
    /// </summary>
    private FlexPanel NewCell(Instrument instrument)
    {
        var sounds = new EventCanvas(Context, _settings, FaithfulSizing.SoundSize, FaithfulSizing.Margin)
        {
            // One tile, content-sized: a full-width canvas would cover the whole cell and eat
            // the clicks that are meant for it.
            PerLine = 1,
            BreakOnDividers = false,
            // The cell is what a click hits, and the cell's own fill lights up for it - a
            // panel behind the tile here would point at something that isn't the target.
            HighlightHover = false,
            Scale = _scale,
            // The cell is the clickable unit, not the tile - a click anywhere on it appends
            // the whole instrument.
            OnPick = _ => OnPickInstrument?.Invoke(instrument),
            OnPreview = _ => OnPreviewInstrument?.Invoke(instrument)
        };
        // Same shape Note.ToEvents() gives at value 0 - the instrument as it plays untouched.
        sounds.SetEvents(instrument.Sounds.Take(1).Select(sound => new ExtendedEvent
        {
            SoundEvent = sound.Sound,
            Value = sound.Value,
            Volume = sound.Volume,
            Pan = sound.Pan,
            ValueScale = ValueScale.None
        }));

        _cellCanvases.Add(sounds);

        var layered = instrument.Sounds.Count > 1;
        var caption = new FlexPanel(Context) { Classes = ["faithful-cell-caption"] };
        // The count costs the name a few characters rather than widening the cell, so every
        // cell in the grid stays the same width.
        caption.AddChild(new Label(Context, Shorten(instrument.Name, layered ? NameLimit - 3 : NameLimit))
        {
            Classes = ["cell-name"]
        });
        if (layered) caption.AddChild(new Label(Context, $"x{instrument.Sounds.Count}") { Classes = ["cell-count"] });

        return new FlexPanel(Context)
        {
            Classes = ["faithful-palette-cell"],
            UpdateCursorOnHover = true,
            OnClick = _ => OnPickInstrument?.Invoke(instrument),
            // The caption is cut to fit, so the hint bar is where the whole name lives.
            OnHoverEnter = _ => OnHint?.Invoke($"Click to add \"{instrument.Name}\", right-click to preview it"),
            OnHoverExit = _ => OnHint?.Invoke(null),
            Children = [caption, sounds]
        };
    }

    /// <summary>Cuts a name to <paramref name="limit" /> characters - see <see cref="NameLimit" />.</summary>
    private static string Shorten(string name, int limit) =>
        name.Length <= limit ? name : string.Concat(name.AsSpan(0, limit - 1), "\u2026");

    /// <summary>
    ///     One titled box of the palette band. <paramref name="trailing" /> is pushed flush
    ///     right of the title, the same bar the sequence box hangs its tools on.
    /// </summary>
    private static FlexPanel Section(UIContext context, string title, UIElement content, string? modifier,
        UIElement? trailing = null)
    {
        var classes = modifier is null ? new List<string> { "faithful-section" } : ["faithful-section", modifier];

        var bar = new FlexPanel(context) { Classes = ["faithful-section-bar"] };
        bar.AddChild(new Label(context, title) { Classes = ["sound-section-header"] });
        if (trailing is not null)
        {
            // The spacer is the bar's only percent-width child, so it soaks up the free
            // space on its own and the button lands flush right - see FaithfulPanel.snx.xml.
            bar.AddChild(new Panel(context) { Classes = ["spacer"] });
            bar.AddChild(trailing);
        }

        return new FlexPanel(context) { Classes = classes, Children = [bar, content] };
    }

    private static ScrollView Scroller(UIContext context, string cssClass, UIElement content)
    {
        var view = new ScrollView(context) { Classes = [cssClass] };
        view.AddChild(content);
        return view;
    }
}
