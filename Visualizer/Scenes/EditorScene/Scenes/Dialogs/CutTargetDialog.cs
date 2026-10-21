using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Atlases;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;
using EditorScene.Scenes.Components;
using EditorScene.Scenes.Views;
using VisualizerScene.Objects.Playfield;

namespace EditorScene.Scenes.Dialogs;

/// <summary>What a faithful cut silences - the three shapes a cut item comes in.</summary>
public enum CutTarget
{
    /// <summary>A bare "!cut": every sound the track plays.</summary>
    Every,

    /// <summary>A cut note: every sound of one instrument, following the instrument.</summary>
    Instrument,

    /// <summary>"!cut@sound", the site's own form.</summary>
    Sound
}

/// <summary>
///     Right-clicking a cut in the faithful sequence: what it silences. A target row (or 1-3)
///     picks the shape; the instrument list and the sound tiles pick what it aims at, and
///     picking either switches to the target that makes sense of the pick - a sound is "One
///     sound", an instrument keeps "One sound" or becomes "Instrument". Up/Down step the
///     instruments, Left/Right the sounds (<see cref="HandleKey" />).
///     Pure form - the owner swaps <see cref="Result" /> in and closes the modal. Built per
///     show; <see cref="Release" /> frees the tiles and icons once it is closed. The tree is
///     CutTargetDialog.snx.xml.
/// </summary>
public sealed class CutTargetDialog
{
    private const float TileSize = 56;
    private const float TileMargin = 10;
    private const float IconSize = 24;
    private const int MaxIcons = 4;

    /// <summary>Longest instrument name a row shows - a 310 px row at 13 px text, icons before it and count after.</summary>
    private const int NameLimit = 23;

    private readonly Label _caption;
    private readonly Label _count;
    private readonly IReadOnlyList<Instrument> _instruments;
    private readonly InstrumentEditor.SoundList _list;
    private readonly FlexPanel[] _modeRows;
    private readonly List<(Instrument Instrument, FlexPanel Row)> _rows = [];
    private readonly Label _soundsLabel;
    private readonly EventCanvas? _tiles;
    private readonly IReadOnlyList<string> _trackSounds;

    /// <summary>The sounds the tiles show, in tile order.</summary>
    private string[] _shown = [];

    /// <param name="store">The atlases the instrument rows' icons come from.</param>
    /// <param name="instruments">The project's instruments; the ones with no sounds are left out.</param>
    /// <param name="trackSounds">Every sound the track plays - what "Every sound" silences.</param>
    /// <param name="cut">The cut being edited; the form starts on what it silences now.</param>
    /// <param name="sounds">Sounds by ID and emoji, for the instrument rows' icons. Null draws none.</param>
    /// <param name="look">The tiles' playfield look. Null leaves the tiles out - the headless tests have no atlases.</param>
    public CutTargetDialog(UIContext context, AtlasStore store, IReadOnlyList<Instrument> instruments,
        IReadOnlyList<string> trackSounds, FaithfulItem cut, IReadOnlyDictionary<string, Sound>? sounds = null,
        PlayfieldSettings? look = null)
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Cut Target Dialog/CutTargetDialog.snx.xml");
        Element = component.GetID<FlexPanel>("cut-target-dialog");
        CloseButton = component.GetID<Button>("close-button");
        CancelButton = component.GetID<Button>("cancel-button");
        SaveButton = component.GetID<Button>("save-button");
        _count = component.GetID<Label>("cut-count");
        _caption = component.GetID<Label>("cut-caption");
        _soundsLabel = component.GetID<Label>("cut-sounds-label");
        _instruments = [.. instruments.Where(instrument => instrument.Sounds.Count > 0)];
        _trackSounds = trackSounds;

        KeyChip.Add(CancelButton, "Esc");
        KeyChip.Add(SaveButton, "Enter");

        _modeRows =
        [
            component.GetID<FlexPanel>("cut-every"),
            component.GetID<FlexPanel>("cut-whole-instrument"),
            component.GetID<FlexPanel>("cut-one-sound")
        ];
        for (var i = 0; i < _modeRows.Length; i++)
        {
            var target = (CutTarget)i;
            _modeRows[i].UpdateCursorOnHover = true;
            _modeRows[i].OnClick = _ => Pick(target);
        }

        // The instrument selector's list: a row leads with up to four of its sounds' icons, drawn
        // as one batch by the list (a row, its icon strip, then the icon - the depth it queues at).
        _list = new InstrumentEditor.SoundList(context, store) { ID = "cut-instrument-list" };
        component.GetID<FlexPanel>("cut-instrument-column").AddChild(_list);
        foreach (var instrument in _instruments)
        {
            var row = new FlexPanel(context)
            {
                Classes = ["cut-instrument", "track-kind-row-idle"],
                UpdateCursorOnHover = true,
                OnClick = _ => Pick(instrument)
            };
            row.AddChild(new FlexPanel(context)
            {
                Classes = ["cut-instrument-icons"],
                Children = [.. _list.Images.Icons(instrument, sounds, IconSize, MaxIcons)]
            });
            row.AddChild(new Label(context, FaithfulPalette.Shorten(instrument.Name, NameLimit))
            {
                Classes = ["cut-instrument-name"]
            });
            if (instrument.Sounds.Count > 1)
                row.AddChild(new Label(context, $"x{instrument.Sounds.Count}") { Classes = ["cell-count"] });
            _list.AddChild(row);
            _rows.Add((instrument, row));
        }

        if (look is not null)
        {
            _tiles = new EventCanvas(context, look, TileSize, TileMargin)
            {
                Width = LiteralOrComputable.Percent(100),
                BreakOnDividers = false,
                OnPick = index => Pick(_shown[index]),
                IsSelected = index => Target != CutTarget.Sound || _shown[index] == Sound
            };
            component.GetID<ScrollView>("cut-sound-scroll").AddChild(_tiles);
        }

        Start(cut);
        Refresh();
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    public Button CloseButton { get; }
    public Button CancelButton { get; }
    public Button SaveButton { get; }

    public CutTarget Target { get; private set; }

    /// <summary>The instrument the list has picked; null only when the project has none with sounds.</summary>
    public Instrument? Instrument { get; private set; }

    /// <summary>The sound "One sound" silences.</summary>
    public string? Sound { get; private set; }

    /// <summary>The cut the form describes, as a new item to swap in.</summary>
    public FaithfulItem Result => Target switch
    {
        CutTarget.Instrument when Instrument is not null => FaithfulItem.Cut(Instrument),
        CutTarget.Sound when Sound is not null => FaithfulItem.Cut(Sound),
        _ => FaithfulItem.Parse("!cut")!
    };

    /// <summary>What a cut silences, in a few words - the inspector's readout.</summary>
    public static string Describe(FaithfulItem cut)
    {
        return cut switch
        {
            { Note: { IsCut: true } note } => note.Instrument.Name,
            { Action: IndividualCutEvent ice } => string.Join(", ", ice.CutSounds),
            _ => "Every sound"
        };
    }

    /// <summary>
    ///     The form's starting point. A cut of several sounds - what converting a piano roll's cut
    ///     note leaves behind - reads as the instrument it matches, so saving it gives the link back.
    /// </summary>
    private void Start(FaithfulItem cut)
    {
        Instrument = _instruments.FirstOrDefault();
        switch (cut)
        {
            case { Note: { IsCut: true } note }:
                Target = CutTarget.Instrument;
                Instrument = note.Instrument;
                break;
            case { Action: IndividualCutEvent ice } when _instruments.FirstOrDefault(i => i.SoundNames.SetEquals(ice.CutSounds)) is { } match && ice.CutSounds.Count > 1:
                Target = CutTarget.Instrument;
                Instrument = match;
                break;
            case { Action: IndividualCutEvent ice } when ice.CutSounds.Count > 0:
                Target = CutTarget.Sound;
                Sound = ice.CutSounds.First();
                Instrument = Owner(Sound);
                break;
            default:
                Target = CutTarget.Every;
                break;
        }

        Sound ??= Instrument?.Sounds[0].Sound;
    }

    /// <summary>
    ///     A target row. "Instrument" needs an instrument to aim at and "One sound" a sound,
    ///     so neither is taken while the project has nothing to offer.
    /// </summary>
    public void Pick(CutTarget target)
    {
        if (target == CutTarget.Instrument && Instrument is null) return;
        if (target == CutTarget.Sound && Sound is null) return;

        // Coming back to "One sound" from the whole track, the sound is one of the instrument's.
        if (target == CutTarget.Sound && Instrument is not null && !Instrument.SoundNames.Contains(Sound!))
            Sound = Instrument.Sounds[0].Sound;
        Target = target;
        Refresh();
    }

    /// <summary>An instrument row: "One sound" moves to its first sound, anything else aims at all of it.</summary>
    public void Pick(Instrument instrument)
    {
        Instrument = instrument;
        Sound = instrument.Sounds[0].Sound;
        if (Target != CutTarget.Sound) Target = CutTarget.Instrument;
        Refresh();
    }

    /// <summary>A sound tile: the cut silences that sound alone.</summary>
    public void Pick(string sound)
    {
        Sound = sound;
        // Picked off the whole track's sounds, the list follows to an instrument that plays it.
        if (Instrument is null || !Instrument.SoundNames.Contains(sound)) Instrument = Owner(sound) ?? Instrument;
        Target = CutTarget.Sound;
        Refresh();
    }

    /// <summary>1-3 pick a target, Up/Down step the instruments, Left/Right the sounds.</summary>
    /// <returns>True for a key the dialog used.</returns>
    public bool HandleKey(KeyboardKeyEventArgs e)
    {
        if (e.Control || e.Alt || e.Command) return false;

        switch (e.Key)
        {
            case Keys.D1 or Keys.KeyPad1:
                Pick(CutTarget.Every);
                return true;
            case Keys.D2 or Keys.KeyPad2:
                Pick(CutTarget.Instrument);
                return true;
            case Keys.D3 or Keys.KeyPad3:
                Pick(CutTarget.Sound);
                return true;
            case Keys.Up or Keys.Down when _instruments.Count > 0:
            {
                var index = Instrument is null ? -1 : IndexOf(_instruments, Instrument);
                var next = Math.Clamp(index + (e.Key == Keys.Up ? -1 : 1), 0, _instruments.Count - 1);
                Pick(_instruments[next]);
                return true;
            }
            case Keys.Left or Keys.Right when Instrument is not null:
            {
                var sounds = Instrument.Sounds.Select(sound => sound.Sound).Distinct().ToArray();
                var index = Target == CutTarget.Sound ? Array.IndexOf(sounds, Sound) : -1;
                // From a whole-instrument target, Right lands on the first sound and Left on the last.
                var next = index < 0
                    ? e.Key == Keys.Right ? 0 : sounds.Length - 1
                    : Math.Clamp(index + (e.Key == Keys.Left ? -1 : 1), 0, sounds.Length - 1);
                Pick(sounds[next]);
                return true;
            }
            default:
                return false;
        }
    }

    /// <summary>Frees the tiles' and icons' GL buffers - nothing else owns them. Call once the modal is closed.</summary>
    public void Release()
    {
        _tiles?.SetEvents([]);
        _list.Images.Stacks.Dispose();
    }

    private Instrument? Owner(string sound)
    {
        return _instruments.FirstOrDefault(instrument => instrument.SoundNames.Contains(sound));
    }

    private static int IndexOf(IReadOnlyList<Instrument> list, Instrument instrument)
    {
        for (var i = 0; i < list.Count; i++)
            if (list[i] == instrument)
                return i;
        return -1;
    }

    private void Refresh()
    {
        for (var i = 0; i < _modeRows.Length; i++) Mark(_modeRows[i], (CutTarget)i == Target);
        foreach (var (instrument, row) in _rows)
        {
            var marked = Target != CutTarget.Every && instrument == Instrument;
            Mark(row, marked);
            // Opening on, or stepping to, an instrument far down the list scrolls to it.
            if (marked) _list.ScrollIntoView(row);
        }

        // The whole track's sounds for a bare cut; otherwise the instrument's, or the lone sound
        // a "!cut@sound" names when no instrument plays it.
        string[] shown = Target == CutTarget.Every
            ? [.. _trackSounds]
            : Instrument is not null
                ? [.. Instrument.Sounds.Select(sound => sound.Sound).Distinct()]
                : [Sound!];

        var silenced = Target == CutTarget.Sound ? 1 : shown.Length;
        _count.SetTextContents(silenced == 1 ? "1 sound" : $"{silenced} sounds");
        _soundsLabel.SetTextContents(Target == CutTarget.Every ? "Sounds in this track" : "Sounds it cuts");
        _caption.SetTextContents(Target switch
        {
            CutTarget.Every => "Silences every sound this track plays.",
            CutTarget.Instrument => "Follows the instrument if its sounds change.",
            _ => "Silences this sound only."
        });

        if (_tiles is null) return;
        if (!shown.SequenceEqual(_shown))
        {
            _shown = shown;
            _tiles.SetEvents(shown.Select(sound => new NormalEvent { SoundEvent = sound, ValueScale = ValueScale.None }));
        }

        _tiles.RefreshSelection();
    }

    /// <summary>Swapped rather than modified: a hover override comes from the first class with one.</summary>
    private static void Mark(UIElement row, bool selected)
    {
        row.SetClass("track-kind-row-idle", !selected);
        row.SetClass("track-kind-row-selected", selected);
    }
}
