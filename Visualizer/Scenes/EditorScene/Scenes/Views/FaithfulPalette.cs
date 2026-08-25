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
///     sample grid is replaced by one row per project instrument (name on the left, that
///     instrument's sounds drawn on the right), and the sixteen actions keep their place to
///     the right of it. Both are drawn with the playfield's own look - see
///     <see cref="EventCanvas" />.
///     Pure view: clicking an entry fires the matching callback and the owner
///     (<see cref="Scenes.EditorInterface" />) decides what it inserts.
/// </summary>
public sealed class FaithfulPalette : FlexPanel
{
    private readonly EventCanvas _actions;

    /// <summary>Every instrument row's canvas, so a scale change can reach all of them.</summary>
    private readonly List<EventCanvas> _rowCanvases = [];

    private readonly FaithfulScale _scale;
    private readonly Button _newInstrumentRow;
    private bool _actionsFilled;

    private readonly ScrollView _instruments;
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
        _newInstrumentRow = new Button(context, "+ New instrument")
        {
            Classes = ["menu-row"],
            OnClick = _ => OnNewInstrument?.Invoke()
        };
        _instruments.AddChild(_newInstrumentRow);

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
            Section(context, "Instruments", _instruments, null),
            Section(context, "Actions", Scroller(context, "faithful-actions", _actions), "faithful-section-actions")
        ];
    }

    /// <summary>Clicking an instrument row - appends a sound item playing it.</summary>
    public Action<Instrument>? OnPickInstrument { get; set; }

    /// <summary>Right-clicking an instrument row.</summary>
    public Action<Instrument>? OnPreviewInstrument { get; set; }

    /// <summary>Clicking an action - the owner prompts for its amount when it takes one.</summary>
    public Action<FaithfulAction>? OnPickAction { get; set; }

    /// <summary>The "+ New instrument" trailer; wired to the instrument workflow's editor.</summary>
    public Action? OnNewInstrument { get; set; }

    /// <summary>Hover/right-click hint text, relayed to the hint bar. Null clears it.</summary>
    public Action<string?>? OnHint { get; set; }

    /// <summary>Redraws every palette canvas at the shared scale; wired to FaithfulScale.Changed.</summary>
    public void RefreshScale()
    {
        _actions.RefreshScale();
        foreach (var canvas in _rowCanvases) canvas.RefreshScale();
    }

    /// <summary>
    ///     Rebuilds the instrument rows. Called on every project change, including per-frame
    ///     ones, so it skips out when nothing about the rows would differ - same guard as
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

        // The trailer is pulled out and re-appended so it always trails, exactly as
        // TrackListPanel does with its "+ Add track" row.
        _instruments.RemoveChild(_newInstrumentRow);
        foreach (var child in _instruments.Children.ToArray()) _instruments.RemoveChild(child);
        _rowCanvases.Clear();

        foreach (var instrument in instruments)
            _instruments.AddChild(NewRow(instrument));
        _instruments.AddChild(_newInstrumentRow);

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
    ///     One palette row: the instrument's name, then its sounds as they would sound - the
    ///     canvas draws each <see cref="InstrumentSound" />'s own tuning as a value badge, so a
    ///     dual-octave instrument reads as two icons at 0 and -12.
    /// </summary>
    private FlexPanel NewRow(Instrument instrument)
    {
        var sounds = new EventCanvas(Context, _settings, FaithfulSizing.SoundSize, FaithfulSizing.Margin)
        {
            // All on one line, and content-sized: a full-width canvas would cover the whole
            // row and eat the clicks that are meant for it.
            PerLine = Math.Max(1, instrument.Sounds.Count),
            BreakOnDividers = false,
            // The row is what a click hits, and the row's own fill lights up for it - a
            // panel behind one sound here would point at something that isn't the target.
            HighlightHover = false,
            Scale = _scale,
            // The row is the clickable unit, not the individual sounds - a click anywhere
            // on it appends the whole instrument.
            OnPick = _ => OnPickInstrument?.Invoke(instrument),
            OnPreview = _ => OnPreviewInstrument?.Invoke(instrument)
        };
        // Same shape Note.ToEvents() gives at value 0 - the instrument as it plays untouched.
        sounds.SetEvents(instrument.Sounds.Select(sound => new ExtendedEvent
        {
            SoundEvent = sound.Sound,
            Value = sound.Value,
            Volume = sound.Volume,
            Pan = sound.Pan,
            ValueScale = ValueScale.None
        }));

        _rowCanvases.Add(sounds);

        return new FlexPanel(Context)
        {
            Classes = ["faithful-palette-row"],
            UpdateCursorOnHover = true,
            OnClick = _ => OnPickInstrument?.Invoke(instrument),
            OnHoverEnter = _ => OnHint?.Invoke($"Click to add \"{instrument.Name}\", right-click to preview it"),
            OnHoverExit = _ => OnHint?.Invoke(null),
            Children =
            [
                new Label(Context, instrument.Name) { Classes = ["body-label"] },
                sounds
            ]
        };
    }

    /// <summary>One titled box of the palette band.</summary>
    private static FlexPanel Section(UIContext context, string title, UIElement content, string? modifier)
    {
        var classes = modifier is null ? new List<string> { "faithful-section" } : ["faithful-section", modifier];
        return new FlexPanel(context)
        {
            Classes = classes,
            Children = [new Label(context, title) { Classes = ["sound-section-header"] }, content]
        };
    }

    private static ScrollView Scroller(UIContext context, string cssClass, UIElement content)
    {
        var view = new ScrollView(context) { Classes = [cssClass] };
        view.AddChild(content);
        return view;
    }
}
