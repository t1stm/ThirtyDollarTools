using OpenTK.Mathematics;
using Shared.Atlases;
using Shared.Renderer.Planes;
using EditorScene.Scenes.Views;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The Add a track dialog's pictures of each kind: what the track looks like once it is open.
///     Made-up content in the editor's own fills (Dialogs.snx.ss, track-preview*); the places are
///     here, measured on the dialog's 412 x 156 slot. The import dialog's are drawn from a file.
/// </summary>
internal static class TrackKindPreview
{
    public const float Width = 412;
    public const float Height = 156;

    // The import's piano roll: as many bars of its first segment, notes this tall, this far
    // from the top and bottom, and no more notes than this.
    private const int ShownBars = 4;
    private const float NoteHeight = 10;
    private const float NotePadding = 12;
    private const int MaxNotes = 400;

    private const int Rows = 9;
    private const int ZeroRow = 4;
    private const int OctaveRow = 2;

    /// <summary>[left, width] as fractions of the width, the row, and the color's class (null: blue).</summary>
    private static readonly (float X, float W, int Row, string? Color)[] Notes =
    [
        (0.02f, 0.10f, 5, null), (0.13f, 0.10f, 3, null), (0.24f, 0.06f, 2, null), (0.31f, 0.14f, 3, null),
        (0.46f, 0.10f, 1, "track-preview-note-purple"), (0.57f, 0.06f, 3, null), (0.64f, 0.10f, 5, null),
        (0.75f, 0.14f, 4, null), (0.90f, 0.08f, 5, null),
        (0.02f, 0.30f, 7, "track-preview-note-green"), (0.50f, 0.24f, 7, "track-preview-note-green")
    ];

    // Same metrics as ArrangementView's wave clip: 2 px bars on a 3 px pitch, 6 in from its edges.
    private const float BarWidth = 2;
    private const float BarPitch = 3;
    private const float BarPadding = 6;

    /// <summary>The note editor: row and beat lines, the zero row's band, and a few notes.</summary>
    public static Panel PianoRoll(UIContext context)
    {
        const float rowHeight = Height / Rows;
        var preview = new Panel(context) { Classes = ["track-preview"] };

        preview.AddChild(Part(context, "track-preview-zero-row", 0, ZeroRow * rowHeight, Width, rowHeight));
        preview.AddChild(Part(context, "track-preview-octave", 0, OctaveRow * rowHeight, Width, 1));
        for (var beat = 1; beat < 8; beat++)
            preview.AddChild(Part(context, beat == 4 ? "track-preview-octave" : "track-preview-line",
                MathF.Round(beat * Width / 8), 0, 1, Height));

        foreach (var (x, w, row, color) in Notes)
        {
            var note = Part(context, "track-preview-note", x * Width, (row + 0.1f) * rowHeight, w * Width,
                rowHeight * 0.8f);
            if (color != null) note.Classes.Add(color);
            preview.AddChild(note);
        }

        return preview;
    }

    /// <summary>
    ///     A file's Piano Roll import: the opening bars of its first segment, with the notes where the
    ///     importer puts them (<see cref="ImportPreview.Notes" />), higher pitches higher. A note the
    ///     grid snaps sits on a yellow outline. Each sound takes the palette's colors in turn.
    /// </summary>
    public static Panel PianoRoll(UIContext context, ImportPreview import, IReadOnlyList<Vector4> palette)
    {
        var preview = new Panel(context) { Classes = ["track-preview"] };
        if (import.Segments.Count == 0) return preview;

        var segment = import.Segments[0];
        var bar = segment.Numerator * segment.StepsPerBeat;
        var steps = Math.Max(1, Math.Min(segment.Bars, ShownBars) * bar);
        var stepWidth = Width / steps;

        for (var step = segment.StepsPerBeat; step < steps; step += segment.StepsPerBeat)
            preview.AddChild(Part(context, step % bar == 0 ? "track-preview-octave" : "track-preview-line",
                MathF.Round(step * stepWidth), 0, 1, Height));

        var notes = import.Notes.TakeWhile(note => note.Segment == 0).Where(note => note.Step < steps)
            .Take(MaxNotes).ToList();
        if (notes.Count == 0) return preview;

        var low = notes.Min(note => note.Value);
        var high = notes.Max(note => note.Value);
        var lanes = import.Tracks.Select((sound, i) => (sound, i)).ToDictionary(pair => pair.sound, pair => pair.i);
        var width = MathF.Max(3, stepWidth - 1);
        foreach (var note in notes)
        {
            var x = MathF.Round(note.Step * stepWidth);
            var y = high > low
                ? NotePadding + (float)((high - note.Value) / (high - low)) * (Height - 2 * NotePadding - NoteHeight)
                : (Height - NoteHeight) / 2;
            if (note.Snapped)
                preview.AddChild(Part(context, "track-preview-snapped", x - 1.5f, y - 1.5f, width + 3, NoteHeight + 3));

            var part = Part(context, "track-preview-sound-note", x, y, width, NoteHeight);
            if (palette.Count > 0) part.Background = new ColoredPlane { Color = palette[lanes[note.Sound] % palette.Count] };
            preview.AddChild(part);
        }

        return preview;
    }

    /// <summary>A wave clip as the arrangement draws one, its bars following a made-up envelope.</summary>
    public static Panel Wave(UIContext context)
    {
        const float clipWidth = Width - 20;
        const float clipHeight = Height - 48;
        const float band = clipHeight - 2 * BarPadding;

        var clip = Part(context, "track-preview-clip", 10, 24, clipWidth, clipHeight);
        for (var i = 0; BarPadding + i * BarPitch + BarWidth <= clipWidth - BarPadding; i++)
        {
            var envelope = 0.35f + 0.65f * MathF.Abs(MathF.Sin(i * 0.09f));
            var jitter = 0.55f + 0.45f * MathF.Abs(MathF.Sin(i * 1.7f) * MathF.Cos(i * 0.53f));
            var height = MathF.Round(band * (0.12f + 0.88f * envelope * jitter));
            clip.AddChild(Part(context, "track-preview-wave-bar", BarPadding + i * BarPitch,
                MathF.Round((clipHeight - height) / 2), BarWidth, height));
        }

        var preview = new Panel(context) { Classes = ["track-preview"] };
        preview.AddChild(clip);
        return preview;
    }

    internal static Panel Part(UIContext context, string cls, float x, float y, float width, float height)
    {
        return new Panel(context) { Classes = [cls], X = x, Y = y, Width = width, Height = height };
    }
}

/// <summary>
///     The faithful editor in miniature: the Instruments and Actions boxes over the Sequence box,
///     with real sound and action icons. The icons are one batch (<see cref="SoundImages" />) and
///     come in with <see cref="Fill" />, once the atlases have loaded.
/// </summary>
internal sealed class FaithfulPreview : FlexPanel
{
    // Five levels down: the band, a box, its icon row, a cell, the icon.
    private const int IconDepth = 5;

    private static readonly string[] InstrumentSounds = ["fnf_left", "noteblock_harp"];

    private static readonly string[] SequenceSounds =
    [
        "!speed", "fnf_left", "fnf_down", "cowbell", "fnf_up", "fnf_right", "boom", "noteblock_harp", "noteblock_bell",
        "boom", "fnf_left", "fnf_up", "cowbell", "fnf_down", "!stop", "boom", "noteblock_harp", "noteblock_harp",
        "noteblock_bell", "fnf_right", "boom", "fnf_left", "fnf_down", "fnf_up", "cowbell", "hitmarker", "boom",
        "noteblock_bell", "noteblock_harp", "fnf_left", "fnf_right", "fnf_up", "boom", "fnf_down", "cowbell",
        "noteblock_harp", "noteblock_bell", "hitmarker", "boom", "fnf_left", "fnf_up", "fnf_right", "boom",
        "noteblock_harp", "noteblock_bell", "fnf_down", "cowbell", "boom", "bruh", "fnf_left", "fnf_down", "fnf_up"
    ];

    // What the boxes hold at these icon sizes: one row of instrument cells, the last of them a
    // "+N" when there are more, and three rows of events.
    private const int MaxInstruments = 7;
    private const int MaxEvents = 63;

    private readonly SoundImages _images;
    private readonly FlexPanel _instruments;
    private readonly FlexPanel _actions;
    private readonly FlexPanel _sequence;
    private readonly AtlasStore _store;
    private bool _filled;

    public FaithfulPreview(UIContext context, AtlasStore store) : base(context)
    {
        _store = store;
        _images = new SoundImages(context, store);
        Classes = ["track-preview", "track-preview-faithful"];

        _instruments = IconRow(context);
        _actions = IconRow(context);
        _sequence = IconRow(context);

        Children =
        [
            new FlexPanel(context)
            {
                Classes = ["track-preview-band"],
                Children =
                [
                    Box(context, "track-preview-card-instruments", Bar(context, "Instruments", ("Modify", false)),
                        _instruments),
                    Box(context, "track-preview-card-actions", Bar(context, "Actions"), _actions)
                ]
            },
            Box(context, "track-preview-card-sequence",
                Bar(context, "Sequence", ("Draw", true), ("Select", false)), _sequence)
        ];
    }

    /// <summary>
    ///     Puts the icons in, the first time the atlases hold them; until then the boxes stay
    ///     empty. Sounds are looked up by ID, the actions by their own atlas key ("!speed").
    /// </summary>
    public void Fill(IReadOnlyDictionary<string, Sound> sounds)
    {
        Fill(sounds, InstrumentSounds, SequenceSounds);
    }

    /// <summary>
    ///     The same with a file's own sounds in the Instruments box and its first events in the
    ///     Sequence box, named as the file names them. A sound the sample set lacks shows the
    ///     missing icon.
    /// </summary>
    public void Fill(IReadOnlyDictionary<string, Sound> sounds, IReadOnlyList<string> instruments,
        IEnumerable<string> sequence)
    {
        // Same guard as FaithfulPalette.Rebuild: the atlases finish loading after the editor
        // is built.
        if (_filled || !_store.StaticSounds.ContainsKey("#missing")) return;
        _filled = true;

        var shown = instruments.Count > MaxInstruments ? MaxInstruments - 1 : instruments.Count;
        foreach (var id in instruments.Take(shown))
            _instruments.AddChild(new FlexPanel(Context) { Classes = ["track-preview-cell"], Children = [Icon(id, 20)] });
        if (shown < instruments.Count)
            _instruments.AddChild(new Label(Context, $"+{instruments.Count - shown}") { Classes = ["track-preview-more"] });
        foreach (var action in FaithfulAction.All)
            _actions.AddChild(Icon(action.Name, 13));
        foreach (var id in sequence.Take(MaxEvents))
            _sequence.AddChild(Icon(id, 15));
        return;

        SoundImage Icon(string id, float size)
        {
            var quad = (sounds.TryGetValue(id, out var sound) ? _images.Add(sound.Filename) : _images.Add(id)) ??
                       _images.Add("#missing");
            return new SoundImage(Context, quad, size);
        }
    }

    /// <summary>Frees the icon batch. Only for a preview that is thrown away, as the import dialog's is.</summary>
    public void Release()
    {
        _images.Stacks.Dispose();
    }

    protected override void DrawSelf(UIContext ctx)
    {
        base.DrawSelf(ctx);
        ctx.QueueRender(_images.Stacks, Index + IconDepth);
    }

    public override void StopRendering()
    {
        Context.DequeueRender(_images.Stacks, Index + IconDepth);
        base.StopRendering();
    }

    private static FlexPanel IconRow(UIContext context)
    {
        return new FlexPanel(context) { Classes = ["track-preview-icons"] };
    }

    private static FlexPanel Box(UIContext context, string cls, FlexPanel bar, FlexPanel icons)
    {
        return new FlexPanel(context) { Classes = ["track-preview-card", cls], Children = [bar, icons] };
    }

    /// <summary>A box's header: its title, then its buttons flush right.</summary>
    private static FlexPanel Bar(UIContext context, string title, params (string Name, bool On)[] buttons)
    {
        return new FlexPanel(context)
        {
            Classes = ["track-preview-card-bar"],
            Children =
            [
                new Label(context, title) { Classes = ["track-preview-card-title"] },
                new Panel(context) { Classes = ["spacer"] },
                .. buttons.Select(UIElement (button) => new FlexPanel(context)
                {
                    Classes = button.On ? ["track-preview-pill", "track-preview-pill-on"] : ["track-preview-pill"],
                    Children = [new Label(context, button.Name) { Classes = ["track-preview-pill-label"] }]
                })
            ]
        };
    }
}

/// <summary>
///     A file's project import: one lane per sound, as <see cref="SequenceImporter.ToProject" /> makes
///     one track each - the sound's icon and name, then a clip as long as the file with a tick where
///     the sound plays. The first five lanes, then how many more.
/// </summary>
internal sealed class ProjectPreview : Panel
{
    private const int MaxLanes = 5;
    private const float Padding = 10;
    private const float LaneHeight = 20;
    private const float LanePitch = 24;
    private const float IconSize = 16;
    private const float NameX = Padding + IconSize + 6;
    private const int NameLimit = 18;
    private const float ClipX = Padding + 128;
    private const float ClipWidth = TrackKindPreview.Width - Padding - ClipX;
    private const float TickWidth = 2;
    private const float TickHeight = 8;

    private readonly SoundImages _images;

    public ProjectPreview(UIContext context, AtlasStore store, IReadOnlyDictionary<string, Sound> sounds,
        ImportPreview import, IReadOnlyList<Vector4> palette) : base(context)
    {
        _images = new SoundImages(context, store);
        Classes = ["track-preview"];

        var lanes = import.Tracks.Take(MaxLanes).ToList();

        // The pixel columns each clip has a note in: a busy sound draws a tick per column, not per note.
        var columns = lanes.Select(_ => new bool[(int)(ClipWidth - TickWidth) + 1]).ToArray();
        var laneOf = lanes.Select((sound, i) => (sound, i)).ToDictionary(pair => pair.sound, pair => pair.i);
        foreach (var note in import.Notes)
            if (laneOf.TryGetValue(note.Sound, out var lane))
                columns[lane][(int)(note.Time * (ClipWidth - TickWidth))] = true;

        for (var i = 0; i < lanes.Count; i++)
        {
            var y = Padding + i * LanePitch;
            var color = palette.Count == 0 ? Vector4.One : palette[i % palette.Count];

            var icon = sounds.TryGetValue(lanes[i], out var sound) ? _images.Create(sound, IconSize) : null;
            icon ??= new SoundImage(context, null, IconSize);
            icon.X = Padding;
            icon.Y = y + (LaneHeight - IconSize) / 2;
            AddChild(icon);
            AddChild(new Label(context, FaithfulPalette.Shorten(lanes[i], NameLimit))
            {
                Classes = ["track-preview-lane-name"], X = NameX, Y = y + 4
            });

            var clip = TrackKindPreview.Part(context, "track-preview-lane", ClipX, y + 2, ClipWidth, LaneHeight - 4);
            clip.Background = new ColoredPlane { Color = color with { W = 0.28f } };
            var lit = columns[i];
            for (var x = 0; x < lit.Length; x++)
            {
                if (!lit[x]) continue;
                var end = x;
                while (end + 1 < lit.Length && lit[end + 1]) end++;
                var tick = new Panel(context)
                {
                    X = x, Y = (LaneHeight - 4 - TickHeight) / 2, Width = end - x + TickWidth, Height = TickHeight,
                    Background = new ColoredPlane { Color = color }
                };
                clip.AddChild(tick);
                x = end;
            }

            AddChild(clip);
        }

        var more = import.Tracks.Count - lanes.Count;
        if (more > 0)
            AddChild(new Label(context, $"and {more} more track{(more == 1 ? "" : "s")}, one per sound")
            {
                Classes = ["track-preview-more"], X = NameX, Y = Padding + lanes.Count * LanePitch
            });
    }

    protected override void DrawSelf(UIContext ctx)
    {
        base.DrawSelf(ctx);
        ctx.QueueRender(_images.Stacks, Index + 1);
    }

    public override void StopRendering()
    {
        Context.DequeueRender(_images.Stacks, Index + 1);
        base.StopRendering();
    }

    /// <summary>Frees the icon batch; call once the dialog is closed.</summary>
    public void Release()
    {
        _images.Stacks.Dispose();
    }
}
