using OpenTK.Mathematics;
using Shared.Atlases;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Renderer.Data_Buffers;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using VisualizerScene.Settings;
using ThirtyDollarConverter.Parser.Custom_Events;
using VisualizerScene.Objects.Playfield.Batch.Chunks;
using VisualizerScene.Objects.Playfield.Batch.Objects;
using EditorScene.Scenes.Views;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The note editor's sound picker: a wrapping grid of TDW sound icons, ported from
///     DrumMaster's SoundList. Same atlas/render-stack drawing, but click-to-select
///     through the UI input routing instead of DrumMaster's drag-and-drop.
///     In multi-select mode, the picker splits into a "Selected" grid - one icon per
///     <see cref="InstrumentSound" /> in <see cref="Instances" /> - and an "Available" grid
///     holding every sound that has no instance yet.
/// </summary>
public sealed class SoundPicker : FlexPanel
{
    private const string AnimatedShaderLocation = "Assets/Shaders/Playfield/Chunk/Animated";
    private const string StaticShaderLocation = "Assets/Shaders/Playfield/Chunk/Static";
    /// <summary>
    ///     One icon cell's box. Not in the sheet: every icon's actual width/height is
    ///     derived from this and its atlas aspect ratio, so it is an input to a
    ///     calculation rather than a value an element takes directly.
    /// </summary>
    private const float SoundElementSize = 40f;

    private readonly FlexPanel _availableGrid;
    private readonly Label _availableHeader;
    private readonly Dictionary<string, SoundIcon> _availableIcons = [];
    private readonly Dictionary<string, string> _idByName = [];
    private readonly Panel _keybindDivider;
    private readonly Label _keybindNote;
    private readonly List<string> _order = [];
    private readonly FlexPanel _selectedGrid;
    private readonly Label _selectedHeader;
    private readonly FlexPanel _selectedRow;
    private readonly Dictionary<string, Sound> _soundsById = [];
    private readonly StackCollection _stacks = new();

    private readonly AtlasStore _store;
    private int _handledRightPressId = -1;

    public SoundPicker(UIContext context, AtlasStore store) : base(context)
    {
        _store = store;
        Classes = ["sound-picker"];

        _selectedHeader = new Label(context, "Selected") { Classes = ["sound-section-header"] };
        _availableHeader = new Label(context, "Available") { Classes = ["sound-section-header"] };
        _selectedGrid = NewGrid(context);
        _availableGrid = NewGrid(context);

        // Scroll-adjust hint: pinned to the right of a non-wrapping outer row, with the
        // actual (wrapping) icon grid as its only other child - so icons wrap around
        // within the narrower space the hint leaves, instead of just trailing the last
        // icon inline. Only relevant with ShowAdjustments on (the instrument editor) and
        // at least one icon selected (nothing to scroll-adjust otherwise);
        // see RefreshKeybindNote.
        _keybindDivider = new Panel(context) { Classes = ["sound-keybind-divider"] };
        // Built once and never refreshed, which is fine: PrimaryName follows the platform,
        // not a binding, and these scroll gestures aren't rebindable.
        _keybindNote = new Label(context,
            "Right click - add another copy\n" +
            "Scroll - change value\n" +
            $"{Keybinds.PrimaryName}+Shift+Scroll - change value by 0.1\n" +
            $"{Keybinds.PrimaryName}+Scroll - change volume\n" +
            "Shift+Scroll - change pan")
        {
            Classes = ["sound-keybind-note"]
        };
        _selectedRow = new FlexPanel(context)
        {
            Classes = ["sound-selected-row"],
            Children = [_selectedGrid]
        };
    }

    /// <summary>Fired with the sound's name when an icon is clicked (single-select mode).</summary>
    public Action<string>? OnPick { get; set; }

    /// <summary>
    ///     When true, clicking an icon adds/removes it in <see cref="Instances" /> instead of
    ///     firing <see cref="OnPick" />, and the picker splits into "Selected" and "Available"
    ///     sections. Used by the track-automation sound filter; the default single-select
    ///     "active sound" picker is unaffected (single flat grid).
    /// </summary>
    public bool MultiSelect { get; set; }

    /// <summary>
    ///     When true, every selected icon (Available icons stay plain) shows a value/volume/pan
    ///     readout - formatted exactly like the playfield's sound badges, see
    ///     <see cref="RenderableFactory.FormatValueText" /> and friends - backed by its own
    ///     <see cref="InstrumentSound" />, and scroll-adjustable: plain scroll changes value,
    ///     Ctrl+scroll volume, Shift+scroll pan, Ctrl+Shift+scroll value in 0.1 steps. Also
    ///     enables right-click-to-duplicate, since
    ///     several instances of one sound only differ by their adjustments. Set once by the
    ///     instrument editor; other <see cref="SoundPicker" /> consumers (the active-sound
    ///     picker, the track-automation filter) leave it off.
    /// </summary>
    public bool ShowAdjustments { get; set; }

    /// <summary>
    ///     The picked sounds, in grid order, each with its own value/volume/pan. The same
    ///     sound may appear more than once (right-click an icon to add another copy) - only
    ///     useful with <see cref="ShowAdjustments" />, where the copies can be tuned apart.
    /// </summary>
    public List<InstrumentSound> Instances { get; } = [];

    /// <summary>
    ///     Same modifier state as the track editor's scroll-zoom (see
    ///     EditorInterface.SetModifiers) - Ctrl adjusts volume, Shift adjusts pan.
    /// </summary>
    public bool CtrlHeld { get; set; }

    public bool ShiftHeld { get; set; }

    /// <summary>
    ///     Fired with an instance whenever scrolling its icon changes it. The picker has
    ///     no playback of its own - the owner wires this the same way it wires
    ///     TrackEditorView.OnPreviewNote.
    /// </summary>
    public Action<InstrumentSound>? OnPreviewSound { get; set; }

    /// <summary>
    ///     The distinct picked sound names - what consumers that don't care about
    ///     adjustments (the track-automation filter) read.
    /// </summary>
    public HashSet<string> Selected => [.. Instances.Select(instance => instance.Sound)];

    /// <summary>
    ///     Counts icons, not known sounds: <see cref="Fill" /> may run while the atlases
    ///     are still loading and produce none, and the owner's lazy guard has to retry then.
    /// </summary>
    public bool HasSounds => _availableIcons.Count > 0 || _selectedGrid.Children.Count > 0;

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

    /// <summary>
    ///     A sound's ID, given either its ID or the emoji a sequence/older project
    ///     saved it as. Icons are keyed by ID (an emoji can't be drawn by a
    ///     <see cref="Label" />), so anything arriving from outside is mapped through here.
    /// </summary>
    private string Canonical(string name)
    {
        return _idByName.GetValueOrDefault(name, name);
    }

    /// <summary>
    ///     Reseeds <see cref="Instances" /> with plain, unadjusted sounds - call each
    ///     time a multi-select picker is reopened, since it may edit a different filter.
    /// </summary>
    public void SetSelected(IEnumerable<string> sounds)
    {
        SetInstances(sounds.Select(sound => new InstrumentSound { Sound = sound }));
    }

    /// <summary>
    ///     Reseeds <see cref="Instances" /> - call each time the instrument editor
    ///     reopens on a different instrument. The instances are cloned, so scrolling an icon
    ///     can't reach the instrument that was loaded.
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
    ///     Adds one instance of a sound, right after <paramref name="copyOf" /> and
    ///     carrying its adjustments when duplicating one (right-click), otherwise plain and
    ///     appended at the end.
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
        RefreshKeybindNote();
        RefreshSections();
    }

    private void SyncSelected()
    {
        var existing = _selectedGrid.Children.OfType<SoundIcon>().ToList();
        foreach (var icon in existing)
            if (icon.Instance is null || !Instances.Contains(icon.Instance))
                DestroyIcon(icon);

        var ordered = new List<UIElement>();
        foreach (var instance in Instances)
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
        var ordered = new List<UIElement>();
        foreach (var name in _order)
        {
            var taken = MultiSelect && Instances.Any(instance => instance.Sound == name);
            _availableIcons.TryGetValue(name, out var icon);

            if (taken)
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
    ///     Shows/hides the divider + hint in the selected row, to the right of the
    ///     (fixed, always-present) icon grid - entering/leaving the tree via AddChild/RemoveChild
    ///     is what actually queues/dequeues a renderable, same as <see cref="RefreshSections" />.
    /// </summary>
    private void RefreshKeybindNote()
    {
        var shouldShow = ShowAdjustments && Instances.Count > 0;
        var showing = _selectedRow.Children.Contains(_keybindNote);
        if (shouldShow == showing) return;

        if (shouldShow)
        {
            _selectedRow.AddChild(_keybindDivider);
            _selectedRow.AddChild(_keybindNote);
        }
        else
        {
            _selectedRow.RemoveChild(_keybindDivider);
            _selectedRow.RemoveChild(_keybindNote);
        }
    }

    /// <summary>
    ///     Shows/hides each section's header + grid depending on whether it has icons,
    ///     and keeps "Selected" above "Available" in the child order.
    /// </summary>
    private void RefreshSections()
    {
        var desired = new List<UIElement>();
        var showSelected = MultiSelect && _selectedGrid.Children.Count > 0;
        if (showSelected)
        {
            desired.Add(_selectedHeader);
            desired.Add(_selectedRow);
        }

        if (_availableGrid.Children.Count > 0)
        {
            if (showSelected) desired.Add(_availableHeader);
            desired.Add(_availableGrid);
        }

        if (Children.SequenceEqual(desired)) return;

        // Entering/leaving the tree must go through AddChild/RemoveChild - they're the
        // ones that queue/dequeue renderables. A bulk Children= only reorders already-live
        // elements; it never queues one that's appearing for the first time.
        foreach (var stale in Children.Where(c => !desired.Contains(c)).ToList())
            RemoveChild(stale);
        foreach (var incoming in desired.Where(c => !Children.Contains(c)))
            AddChild(incoming);

        if (!Children.SequenceEqual(desired))
            Children = desired; // pure reorder of elements that are all already live
    }

    /// <summary>
    ///     Builds one icon, pushing its quad into the render stack of the atlas holding the
    ///     sound - null when the sound has no image (still loading, or unknown). The atlas is
    ///     keyed by <see cref="Sound.Filename" /> - how TDW writes the sound, an emoji for the
    ///     44-odd sounds that have one - while the icon (and everything the editor stores or
    ///     labels) uses the ID. Pass an instance for a Selected icon, null for an Available one.
    /// </summary>
    private SoundIcon? CreateIcon(string soundName, InstrumentSound? instance)
    {
        if (!_soundsById.TryGetValue(soundName, out var sound)) return null;
        var atlasKey = sound.Filename;

        SoundIcon icon;
        if (_store.AnimatedSounds.TryGetValue(atlasKey, out var framedAtlas))
        {
            if (!_stacks.AnimatedStacks.TryGetValue(framedAtlas, out var stack))
            {
                var shader = Context.AssetProvider.ShaderPool.GetOrLoad(AnimatedShaderLocation);
                stack = new RenderStack<SoundData>(Context.DeleteQueue) { Shader = shader };
                _stacks.AnimatedStacks.Add(framedAtlas, stack);
            }

            stack.List.Add(new SoundData { Model = Matrix4.Identity, RGBA = Vector4.One });
            var reference = stack.List.GetReferenceAt(stack.List.Count - 1);
            var aspect = framedAtlas.CurrentRectangle.Width / (float)framedAtlas.CurrentRectangle.Height;

            icon = new SoundIcon(Context, this, soundName, instance)
            {
                AnimatedReference = reference,
                Release = () => stack.List.Remove(reference),
                Width = aspect > 1 ? SoundElementSize : SoundElementSize * aspect,
                Height = aspect > 1 ? SoundElementSize / aspect : SoundElementSize
            };
        }
        else if (_store.StaticSounds.TryGetValue(atlasKey, out var staticAtlas))
        {
            if (!staticAtlas.TryGetSound(atlasKey, out var rect)) return null;

            if (!_stacks.StaticStacks.TryGetValue(staticAtlas, out var stack))
            {
                var shader = Context.AssetProvider.ShaderPool.GetOrLoad(StaticShaderLocation);
                stack = new RenderStack<StaticSound>(Context.DeleteQueue) { Shader = shader };
                _stacks.StaticStacks.Add(staticAtlas, stack);
            }

            stack.List.Add(new StaticSound
            {
                Data = new SoundData { Model = Matrix4.Identity, RGBA = Vector4.One },
                TextureUV = QuadUV.FromRectangle(rect, staticAtlas.Width, staticAtlas.Height)
            });
            var reference = stack.List.GetReferenceAt(stack.List.Count - 1);
            var aspect = rect.Width / (float)rect.Height;

            icon = new SoundIcon(Context, this, soundName, instance)
            {
                StaticReference = reference,
                Release = () => stack.List.Remove(reference),
                Width = aspect > 1 ? SoundElementSize : SoundElementSize * aspect,
                Height = aspect > 1 ? SoundElementSize / aspect : SoundElementSize
            };
        }
        else
        {
            return null;
        }

        if (ShowAdjustments && instance != null) icon.EnableAdjustmentText();
        return icon;
    }

    /// <summary>
    ///     Drops an icon out of the tree and out of the render stack. The stack is
    ///     queued as a whole by <see cref="DrawSelf" />, so an entry left behind would keep
    ///     painting at its last matrix even with the icon gone.
    /// </summary>
    private static void DestroyIcon(SoundIcon icon)
    {
        if (icon.Parent is Panel parent) parent.RemoveChild(icon);
        icon.Release?.Invoke();
    }

    protected override void DrawSelf(UIContext context)
    {
        context.QueueRender(_stacks, Index);
    }

    public override void StopRendering()
    {
        Context.DequeueRender(_stacks, Index);
        base.StopRendering();
    }

    public override void ApplyClip(Vector4i? clip)
    {
        _stacks.ClipRect = clip;
        base.ApplyClip(clip);
    }

    /// <summary>
    ///     One icon; its screen rectangle is pushed into the instanced render stack.
    ///     A Selected icon owns one <see cref="InstrumentSound" />; with
    ///     <see cref="ShowAdjustments" /> on it also carries a value/volume/pan label and answers
    ///     scroll - see <see cref="EnableAdjustmentText" />.
    /// </summary>
    private sealed class SoundIcon : Panel
    {
        private const float AdjustableCellWidth = 60f;
        private const float AdjustmentLabelHeight = 14f;
        private const float ValueStep = 1;
        private const double FineValueStep = 0.1;
        private const float VolumeStep = 5;
        private const float PanStep = 5;

        private readonly SoundPicker _picker;
        private Label? _adjustmentLabel;
        private float _iconHeight;
        private float _iconWidth;

        public SoundIcon(UIContext context, SoundPicker picker, string soundName, InstrumentSound? instance)
            : base(context)
        {
            _picker = picker;
            SoundName = soundName;
            Instance = instance;
            UpdateCursorOnHover = true;
            Computed = new ComputedRectangle { OnUpdate = UpdateMatrix };
            OnClick = _ =>
            {
                if (!picker.MultiSelect)
                {
                    picker.OnPick?.Invoke(soundName);
                    return;
                }

                if (Instance != null) picker.RemoveInstance(Instance);
                else picker.AddInstance(soundName);
            };
        }

        public string SoundName { get; }

        /// <summary>
        ///     The instance this icon shows, or null for an Available icon (nothing
        ///     picked yet).
        /// </summary>
        public InstrumentSound? Instance { get; }

        public TrackedBufferReference<StaticSound>? StaticReference { get; init; }
        public TrackedBufferReference<SoundData>? AnimatedReference { get; init; }

        /// <summary>Frees this icon's slot in the render stack; see <see cref="DestroyIcon" />.</summary>
        public Action? Release { get; init; }

        public override ComputedRectangle Computed { get; protected set; }

        /// <summary>
        ///     Reserves room below the icon for the value/volume/pan label and adds it -
        ///     only selected sounds carry one, so the Available grid stays plain icons. Idempotent;
        ///     the first call captures the aspect-scaled icon size from <see cref="Width" />/
        ///     <see cref="Height" /> before widening them (an object initializer sets those, so
        ///     this can't run from the ctor).
        /// </summary>
        public void EnableAdjustmentText()
        {
            if (_adjustmentLabel != null) return;

            _iconWidth = Width.Value;
            _iconHeight = Height.Value;

            Width = Math.Max(_iconWidth, AdjustableCellWidth);
            Height = _iconHeight + AdjustmentLabelHeight;

            _adjustmentLabel = new Label(Context, "") { Classes = ["sound-adjustment-label"], Y = _iconHeight };
            AddChild(_adjustmentLabel);
            RefreshAdjustmentText();
        }

        /// <summary>
        ///     Redraws the label from this icon's <see cref="Instance" />, reusing the
        ///     same value/volume/pan text conventions the playfield's sound badges use. No-op when
        ///     <see cref="EnableAdjustmentText" /> was never called.
        /// </summary>
        public void RefreshAdjustmentText()
        {
            if (_adjustmentLabel is null) return;

            var ev = new ExtendedEvent
            {
                SoundEvent = SoundName,
                Value = Instance?.Value ?? 0,
                Volume = Instance?.Volume,
                Pan = Instance?.Pan ?? 0,
                ValueScale = ValueScale.None
            };

            var parts = new List<string>(3) { RenderableFactory.FormatValueText(ev) ?? "0" };
            if (RenderableFactory.FormatVolumeText(ev) is { } volumeText) parts.Add(volumeText);
            if (RenderableFactory.FormatPanText(ev) is { } panText) parts.Add(panText);

            _adjustmentLabel.Value = string.Join(" ", parts);
        }

        /// <summary>
        ///     Right click adds another copy of this sound, so one instrument can play
        ///     it twice with different tuning (0 and -12 for dual-octave playback). Duplicating
        ///     only makes sense where the copies can be told apart, i.e. with adjustments on.
        /// </summary>
        public override bool HandleRightPress(float x, float y)
        {
            if (!_picker.MultiSelect || !_picker.ShowAdjustments) return false;

            // Level-triggered: fired every frame while the button is held, and adding is not
            // idempotent - so act once per press (see UIContext.RightPressId).
            if (Context.RightPressId == _picker._handledRightPressId) return true;
            _picker._handledRightPressId = Context.RightPressId;

            _picker.AddInstance(SoundName, Instance);
            return true;
        }

        public override bool HandleScroll(Vector2 scrollDelta)
        {
            if (!_picker.ShowAdjustments || Instance is null) return false;

            var notches = MathF.Sign(scrollDelta.Y);
            if (notches == 0) return false;

            switch (_picker.CtrlHeld)
            {
                // Ctrl+Shift is the fine-value mode, so it has to be checked before the plain
                // Ctrl (volume) and Shift (pan) modes.
                case true when _picker.ShiftHeld:
                    Instance.Value = AdjustValue(notches * FineValueStep);
                    break;
                case true:
                    Instance.Volume = Math.Clamp((Instance.Volume ?? 100) + notches * VolumeStep, 0, 500);
                    break;
                default:
                {
                    if (_picker.ShiftHeld)
                        Instance.Pan = Math.Clamp(Instance.Pan + notches * PanStep, -100, 100);
                    else
                        Instance.Value = AdjustValue(notches * ValueStep);
                    break;
                }
            }

            RefreshAdjustmentText();
            _picker.OnPreviewSound?.Invoke(Instance);
            return true;
        }

        /// <summary>
        ///     Steps <see cref="Instance" />'s value, clamped to the track editor's range and
        ///     rounded - repeated 0.1 steps otherwise drift into 1.0000000000000002 territory,
        ///     which the label hides but an export would write out.
        /// </summary>
        private double AdjustValue(double delta)
        {
            var value = Math.Clamp(Instance!.Value + delta, -TrackEditorView.MaxValue, TrackEditorView.MaxValue);
            return Math.Round(value, 4);
        }

        private void UpdateMatrix()
        {
            var width = _adjustmentLabel is null ? Computed.Width : _iconWidth;
            var height = _adjustmentLabel is null ? Computed.Height : _iconHeight;
            var matrix = Matrix4.CreateScale(width, height, 1) *
                         Matrix4.CreateTranslation(Computed.AbsoluteX, Computed.AbsoluteY, 0);

            if (StaticReference != null)
            {
                var value = StaticReference.Value;
                value.Data.Model = matrix;
                StaticReference.Value = value;
            }

            if (AnimatedReference != null)
            {
                var value = AnimatedReference.Value;
                value.Model = matrix;
                AnimatedReference.Value = value;
            }
        }
    }
}