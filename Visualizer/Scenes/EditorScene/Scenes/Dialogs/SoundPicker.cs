using OpenTK.Mathematics;
using Shared.Atlases;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     A wrapping grid of TDW sound icons drawn from the atlas render stacks and selected by
///     click.
///     In multi-select mode, the picker splits into a "Selected" grid - one icon per
///     <see cref="InstrumentSound" /> in <see cref="Instances" /> - and an "Available" grid
///     holding every sound that has no instance yet. With <see cref="AvailableOnly" /> the
///     Selected grid is left out: the instrument editor lists its sounds as rows instead.
/// </summary>
public sealed class SoundPicker : FlexPanel
{
    /// <summary>
    ///     One icon cell's box. Each icon's actual width and height are derived from this and
    ///     its atlas aspect ratio, so it is an input to that calculation rather than a
    ///     stylesheet setting.
    /// </summary>
    private const float SoundElementSize = 40f;

    private readonly FlexPanel _availableGrid;
    private readonly Label _availableHeader;
    private readonly Dictionary<string, SoundIcon> _availableIcons = [];
    private readonly Dictionary<string, string> _idByName = [];
    private readonly SoundImages _images;
    private readonly List<string> _order = [];
    private readonly FlexPanel _selectedGrid;
    private readonly Label _selectedHeader;
    private readonly Dictionary<string, Sound> _soundsById = [];
    private string _filter = "";

    public SoundPicker(UIContext context, AtlasStore store) : base(context)
    {
        _images = new SoundImages(context, store);
        Classes = ["sound-picker"];

        _selectedHeader = new Label(context, "Selected") { Classes = ["sound-section-header"] };
        _availableHeader = new Label(context, "Available") { Classes = ["sound-section-header"] };
        _selectedGrid = NewGrid(context);
        _availableGrid = NewGrid(context);
    }

    /// <summary>Fired with the sound's name when an icon is clicked (single-select mode).</summary>
    public Action<string>? OnPick { get; set; }

    /// <summary>
    ///     When true, clicking an icon adds/removes it in <see cref="Instances" /> instead of
    ///     firing <see cref="OnPick" />, and the picker splits into "Selected" and "Available"
    ///     sections. Off by default: one flat grid picking a single active sound.
    /// </summary>
    public bool MultiSelect { get; set; }

    /// <summary>
    ///     Multi-select without the "Selected" section: only the sounds not in
    ///     <see cref="Instances" /> show, and a click adds one (see <see cref="OnInstanceAdded" />).
    /// </summary>
    public bool AvailableOnly { get; set; }

    /// <summary>Fired with the instance a click on an Available icon just added.</summary>
    public Action<InstrumentSound>? OnInstanceAdded { get; set; }

    /// <summary>
    ///     The picked sounds, in grid order, each with its own value/volume/pan. The same
    ///     sound may appear more than once (the instrument editor's Duplicate).
    /// </summary>
    public List<InstrumentSound> Instances { get; } = [];

    /// <summary>
    ///     The distinct picked sound names, for consumers that don't care about adjustments.
    /// </summary>
    public HashSet<string> Selected => [.. Instances.Select(instance => instance.Sound)];

    /// <summary>
    ///     Whether the picker holds any icon. Counts icons rather than known sounds, since
    ///     <see cref="Fill" /> produces none while the atlases are still loading, and callers
    ///     guarding on this have to retry.
    /// </summary>
    public bool HasSounds => _availableIcons.Count > 0 || _selectedGrid.Children.Count > 0;

    /// <summary>
    ///     The sounds the Available grid offers, in grid order: every known sound minus those
    ///     already picked (multi-select) and those the <see cref="Filter" /> hides. A sound the
    ///     atlases have no image for yet is listed here but gets no icon.
    /// </summary>
    public IEnumerable<string> AvailableSounds => _order.Where(name =>
        !(MultiSelect && Instances.Any(instance => instance.Sound == name)) && Matches(_soundsById[name]));

    /// <summary>Whether any text the sound carries - ID, name, source or a tag - contains the filter.</summary>
    private bool Matches(Sound sound)
    {
        return ((string?[])[sound.Id, sound.Name, sound.Source, .. sound.Tags ?? []]).Any(text =>
            text?.Contains(_filter, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static FlexPanel NewGrid(UIContext context)
    {
        return new FlexPanel(context) { Classes = ["sound-grid"] };
    }

    /// <summary>
    ///     Fills the grid from the atlas store. Call lazily - the atlases may still be
    ///     loading while the scene is constructed; sounds without an image are skipped.
    /// </summary>
    public void Fill(IEnumerable<Sound> sounds)
    {
        foreach (var sound in sounds)
        {
            _idByName[sound.Id] = sound.Id;
            if (sound.Emoji != null) _idByName[sound.Emoji] = sound.Id;
            if (_soundsById.TryAdd(sound.Id, sound)) _order.Add(sound.Id);
        }

        Sync();
    }

    /// <summary>Hides Available icons none of whose texts contain <paramref name="text" /> (any case); see <see cref="Matches" />.</summary>
    public void Filter(string text)
    {
        text = text.Trim();
        if (_filter == text) return;
        _filter = text;
        SyncAvailable();
        RefreshSections();
    }

    /// <summary>
    ///     A sound's ID, given either its ID or the emoji a sequence or older project saved it
    ///     as. Icons are keyed by ID, so anything arriving from outside is mapped through here.
    /// </summary>
    private string Canonical(string name)
    {
        return _idByName.GetValueOrDefault(name, name);
    }

    /// <summary>
    ///     Reseeds <see cref="Instances" /> with plain, unadjusted sounds. Call on every reopen
    ///     of a multi-select picker, which may be editing a different filter.
    /// </summary>
    public void SetSelected(IEnumerable<string> sounds)
    {
        SetInstances(sounds.Select(sound => new InstrumentSound { Sound = sound }));
    }

    /// <summary>
    ///     Reseeds <see cref="Instances" />. Call on every reopen of the instrument editor. The
    ///     instances are cloned, so tuning one can't reach the instrument that was loaded.
    /// </summary>
    public void SetInstances(IEnumerable<InstrumentSound> instances)
    {
        Instances.Clear();
        foreach (var instance in instances)
        {
            var copy = instance.Clone();
            copy.Sound = Canonical(copy.Sound);
            Instances.Add(copy);
        }

        Sync();
    }

    /// <summary>
    ///     Adds one instance of a sound: right after <paramref name="copyOf" /> and carrying
    ///     its adjustments when duplicating an existing instance, otherwise plain and appended
    ///     at the end.
    /// </summary>
    public InstrumentSound AddInstance(string sound, InstrumentSound? copyOf = null)
    {
        var instance = copyOf?.Clone() ?? new InstrumentSound { Sound = Canonical(sound) };
        var after = copyOf is null ? Instances.Count : Instances.IndexOf(copyOf) + 1;
        Instances.Insert(after, instance);
        Sync();
        return instance;
    }

    public void RemoveInstance(InstrumentSound instance)
    {
        if (!Instances.Remove(instance)) return;
        Sync();
    }

    /// <summary>
    ///     Rebuilds both grids from <see cref="Instances" />: one icon per instance in
    ///     the Selected grid, and an Available icon for every sound with no instance.
    /// </summary>
    private void Sync()
    {
        SyncSelected();
        SyncAvailable();
        RefreshSections();
    }

    private void SyncSelected()
    {
        var existing = _selectedGrid.Children.OfType<SoundIcon>().ToList();
        var shown = MultiSelect && !AvailableOnly ? Instances : [];
        foreach (var icon in existing)
            if (icon.Instance is null || !shown.Contains(icon.Instance))
                DestroyIcon(icon);

        var ordered = new List<UIElement>();
        foreach (var instance in shown)
        {
            var icon = existing.FirstOrDefault(candidate => ReferenceEquals(candidate.Instance, instance)
                                                            && candidate.Parent != null)
                       ?? CreateIcon(instance.Sound, instance);
            if (icon != null) ordered.Add(icon);
        }

        if (!_selectedGrid.Children.SequenceEqual(ordered)) _selectedGrid.Children = ordered;
    }

    private void SyncAvailable()
    {
        var available = AvailableSounds.ToHashSet();
        var ordered = new List<UIElement>();
        foreach (var name in _order)
        {
            _availableIcons.TryGetValue(name, out var icon);

            if (!available.Contains(name))
            {
                if (icon == null) continue;
                DestroyIcon(icon);
                _availableIcons.Remove(name);
                continue;
            }

            icon ??= CreateIcon(name, null);
            if (icon == null) continue; // no atlas image for this sound
            _availableIcons[name] = icon;
            ordered.Add(icon);
        }

        if (!_availableGrid.Children.SequenceEqual(ordered)) _availableGrid.Children = ordered;
    }

    /// <summary>
    ///     Shows/hides each section's header + grid depending on whether it has icons,
    ///     and keeps "Selected" above "Available" in the child order.
    /// </summary>
    private void RefreshSections()
    {
        var desired = new List<UIElement>();
        var showSelected = _selectedGrid.Children.Count > 0;
        if (showSelected)
        {
            desired.Add(_selectedHeader);
            desired.Add(_selectedGrid);
        }

        if (_availableGrid.Children.Count > 0)
        {
            if (showSelected) desired.Add(_availableHeader);
            desired.Add(_availableGrid);
        }

        if (Children.SequenceEqual(desired)) return;

        // Entering and leaving the tree must go through AddChild/RemoveChild: they are what
        // queue and dequeue renderables. A bulk Children= only reorders already-live elements;
        // it never queues one that is appearing for the first time.
        foreach (var stale in Children.Where(c => !desired.Contains(c)).ToList())
            RemoveChild(stale);
        foreach (var incoming in desired.Where(c => !Children.Contains(c)))
            AddChild(incoming);

        if (!Children.SequenceEqual(desired))
            Children = desired; // pure reorder of elements that are all already live
    }

    /// <summary>
    ///     Builds one icon, pushing its quad into the render stack of the atlas holding the
    ///     sound; null when the sound has no image (still loading, or unknown). Pass an
    ///     instance for a Selected icon, null for an Available one.
    /// </summary>
    private SoundIcon? CreateIcon(string soundName, InstrumentSound? instance)
    {
        if (!_soundsById.TryGetValue(soundName, out var sound)) return null;
        var quad = _images.Add(sound.Filename);
        return quad == null ? null : new SoundIcon(Context, this, soundName, instance, quad);
    }

    /// <summary>Drops an icon out of the tree and frees its render-stack slot.</summary>
    private static void DestroyIcon(SoundIcon icon)
    {
        if (icon.Parent is Panel parent) parent.RemoveChild(icon);
        icon.Release();
    }

    protected override void DrawSelf(UIContext context)
    {
        context.QueueRender(_images.Stacks, Index);
    }

    public override void StopRendering()
    {
        Context.DequeueRender(_images.Stacks, Index);
        base.StopRendering();
    }

    public override void ApplyClip(Vector4i? clip)
    {
        _images.Stacks.ClipRect = clip;
        base.ApplyClip(clip);
    }

    /// <summary>One clickable icon. A Selected icon owns one <see cref="InstrumentSound" />.</summary>
    private sealed class SoundIcon : SoundImage
    {
        public SoundIcon(UIContext context, SoundPicker picker, string soundName, InstrumentSound? instance,
            SoundQuad? quad = null) : base(context, quad, SoundElementSize)
        {
            SoundName = soundName;
            Instance = instance;
            UpdateCursorOnHover = true;
            OnClick = _ =>
            {
                if (!picker.MultiSelect)
                {
                    picker.OnPick?.Invoke(soundName);
                    return;
                }

                if (Instance != null)
                {
                    picker.RemoveInstance(Instance);
                    return;
                }

                var added = picker.AddInstance(soundName);
                picker.OnInstanceAdded?.Invoke(added);
            };
        }

        public string SoundName { get; }

        /// <summary>The instance this icon shows, or null for an Available icon.</summary>
        public InstrumentSound? Instance { get; }
    }
}
