using OpenTK.Mathematics;
using Shared.Atlases;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Renderer.Data_Buffers;
using ThirtyDollarConverter.Editor;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using VisualizerScene.Objects.Playfield.Batch.Chunks;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace EditorScene.Scenes.Components;

/// <summary>
///     The note editor's sound picker: a wrapping grid of TDW sound icons, ported from
///     DrumMaster's SoundList. Same atlas/render-stack drawing, but click-to-select
///     through the UI input routing instead of DrumMaster's drag-and-drop.
///     In multi-select mode, icons live in one of two sub-grids - "Selected" and
///     "Available" - and hop between them as they're toggled.
/// </summary>
public sealed class SoundPicker : FlexPanel
{
    private const string AnimatedShaderLocation = "Assets/Shaders/Playfield/Chunk/Animated";
    private const string StaticShaderLocation = "Assets/Shaders/Playfield/Chunk/Static";
    private const float SoundElementSize = 40f;
    private static readonly Vector4 HeaderColor = EditorPalette.Header;
    private static readonly Vector4 DividerColor = EditorPalette.Divider;
    private static readonly Vector4 BlandColor = EditorPalette.TextMuted;

    private readonly AtlasStore _store;
    private readonly Label _selectedHeader;
    private readonly Label _availableHeader;
    private readonly FlexPanel _selectedRow;
    private readonly FlexPanel _selectedGrid;
    private readonly FlexPanel _availableGrid;
    private readonly Panel _keybindDivider;
    private readonly Label _keybindNote;
    private readonly StackCollection _stacks = new();

    public SoundPicker(UIContext context, AtlasStore store) : base(context)
    {
        _store = store;
        Direction = LayoutDirection.Vertical;
        Spacing = 8;
        Padding = 8;

        _selectedHeader = new Label(context, "Selected") { FontSizePx = 13f, Color = HeaderColor };
        _availableHeader = new Label(context, "Available") { FontSizePx = 13f, Color = HeaderColor };
        _selectedGrid = NewGrid(context);
        _availableGrid = NewGrid(context);

        // Scroll-adjust hint: pinned to the right of a non-wrapping outer row, with the
        // actual (wrapping) icon grid as its only other child - so icons wrap around
        // within the narrower space the hint leaves, instead of just trailing the last
        // icon inline. Only relevant with ShowAdjustments on (the instrument editor) and
        // at least one icon selected (nothing to scroll-adjust otherwise);
        // see RefreshKeybindNote.
        _keybindDivider = new Panel(context)
        {
            Width = 1,
            Height = SoundElementSize,
            Background = new ColoredPlane { Color = DividerColor }
        };
        _keybindNote = new Label(context,
            "Scroll - change value\n" +
            "Ctrl+Scroll - change volume\n" +
            "Shift+Scroll change pan")
        {
            FontSizePx = 12f,
            Color = BlandColor
        };
        _selectedRow = new FlexPanel(context)
        {
            Direction = LayoutDirection.Horizontal,
            Width = LiteralOrComputable.Percent(100),
            Spacing = 14,
            Children = [_selectedGrid]
        };
    }

    private static FlexPanel NewGrid(UIContext context)
    {
        return new FlexPanel(context)
        {
            Direction = LayoutDirection.Horizontal,
            Width = LiteralOrComputable.Percent(100),
            Wrap = true,
            Spacing = 6
        };
    }

    /// <summary>Fired with the sound's name when an icon is clicked (single-select mode).</summary>
    public Action<string>? OnPick { get; set; }

    /// <summary>
    ///     When true, clicking an icon toggles it in <see cref="Selected" /> and tints it
    ///     instead of firing <see cref="OnPick" />, and the picker splits into "Selected"
    ///     and "Available" sections. Used by the track-automation sound filter; the
    ///     default single-select "active sound" picker is unaffected (single flat grid).
    /// </summary>
    public bool MultiSelect { get; set; }

    /// <summary>
    ///     When true, every selected icon (Available icons stay plain) shows a value/volume/pan
    ///     readout - formatted exactly like the playfield's sound badges, see
    ///     <see cref="RenderableFactory.FormatValueText" /> and friends - backed by
    ///     <see cref="Adjustments" />, and scroll-adjustable: plain scroll changes value,
    ///     Ctrl+scroll volume, Shift+scroll pan. Set once by the instrument editor; other
    ///     <see cref="SoundPicker" /> consumers (the active-sound picker, the track-automation
    ///     filter) leave it off.
    /// </summary>
    public bool ShowAdjustments { get; set; }

    /// <summary>Per-sound value/volume/pan tuning, keyed by sound name - only meaningful
    /// (and only editable via scroll) when <see cref="ShowAdjustments" /> is set.</summary>
    public Dictionary<string, SoundAdjustment> Adjustments { get; } = new();

    /// <summary>Same modifier state as the track editor's scroll-zoom (see
    /// EditorInterface.SetModifiers) - Ctrl adjusts volume, Shift adjusts pan.</summary>
    public bool CtrlHeld { get; set; }

    public bool ShiftHeld { get; set; }

    /// <summary>Fired with a sound's name and its current adjustment whenever scrolling an
    /// icon changes it. The picker has no playback of its own - the owner wires this the
    /// same way it wires TrackEditorView.OnPreviewNote.</summary>
    public Action<string, SoundAdjustment>? OnPreviewSound { get; set; }

    public HashSet<string> Selected { get; } = [];

    public bool HasSounds => _selectedGrid.Children.Count > 0 || _availableGrid.Children.Count > 0;

    /// <summary>
    ///     Fills the grid from the atlas store. Call lazily - the atlases may still be
    ///     loading while the scene is constructed; sounds without an image are skipped.
    /// </summary>
    public void Fill(IEnumerable<string> soundNames)
    {
        foreach (var name in soundNames) AddSound(name);
        RefreshSections();
    }

    /// <summary>Reseeds <see cref="Selected" /> and moves icons to match - call each
    /// time a multi-select picker is reopened, since it may edit a different filter.</summary>
    public void SetSelected(IEnumerable<string> sounds)
    {
        Selected.Clear();
        foreach (var name in sounds) Selected.Add(name);
        foreach (var icon in AllIcons())
        {
            var selected = Selected.Contains(icon.SoundName);
            MoveIcon(icon, selected);
            if (ShowAdjustments)
            {
                if (selected) icon.EnableAdjustmentText();
                else icon.DisableAdjustmentText();
            }

            icon.RefreshAdjustmentText();
        }

        RefreshKeybindNote();
        RefreshSections();
    }

    /// <summary>Reseeds <see cref="Adjustments" /> and refreshes every icon's readout - call
    /// alongside <see cref="SetSelected" /> when the instrument editor reopens on a different
    /// instrument. No-op for pickers that don't set <see cref="ShowAdjustments" />.</summary>
    public void SetAdjustments(IReadOnlyDictionary<string, SoundAdjustment> adjustments)
    {
        Adjustments.Clear();
        foreach (var (sound, adjustment) in adjustments)
            Adjustments[sound] = new SoundAdjustment
            {
                Value = adjustment.Value,
                Volume = adjustment.Volume,
                Pan = adjustment.Pan
            };

        foreach (var icon in AllIcons()) icon.RefreshAdjustmentText();
    }

    private List<SoundIcon> AllIcons()
    {
        return _selectedGrid.Children.OfType<SoundIcon>()
            .Concat(_availableGrid.Children.OfType<SoundIcon>())
            .ToList();
    }

    /// <summary>Moves an icon into the grid matching its selection state, if it isn't there already.</summary>
    private void MoveIcon(SoundIcon icon, bool selected)
    {
        var target = MultiSelect && selected ? _selectedGrid : _availableGrid;
        if (ReferenceEquals(icon.Parent, target)) return;

        if (icon.Parent is Panel current) current.RemoveChild(icon);
        target.AddChild(icon);
    }

    /// <summary>Shows/hides the divider + hint in the selected row, to the right of the
    /// (fixed, always-present) icon grid - entering/leaving the tree via AddChild/RemoveChild
    /// is what actually queues/dequeues a renderable, same as <see cref="RefreshSections" />.</summary>
    private void RefreshKeybindNote()
    {
        var shouldShow = ShowAdjustments && Selected.Count > 0;
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

    /// <summary>Shows/hides each section's header + grid depending on whether it has icons,
    /// and keeps "Selected" above "Available" in the child order.</summary>
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

    private void AddSound(string soundName)
    {
        if (_store.AnimatedSounds.TryGetValue(soundName, out var framedAtlas))
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

            var icon = new SoundIcon(Context, this, soundName)
            {
                AnimatedReference = reference,
                Width = aspect > 1 ? SoundElementSize : SoundElementSize * aspect,
                Height = aspect > 1 ? SoundElementSize / aspect : SoundElementSize
            };
            if (ShowAdjustments && Selected.Contains(soundName)) icon.EnableAdjustmentText();
            (MultiSelect && Selected.Contains(soundName) ? _selectedGrid : _availableGrid).AddChild(icon);
        }
        else if (_store.StaticSounds.TryGetValue(soundName, out var staticAtlas))
        {
            if (!staticAtlas.TryGetSound(soundName, out var rect)) return;

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

            var icon = new SoundIcon(Context, this, soundName)
            {
                StaticReference = reference,
                Width = aspect > 1 ? SoundElementSize : SoundElementSize * aspect,
                Height = aspect > 1 ? SoundElementSize / aspect : SoundElementSize
            };
            if (ShowAdjustments && Selected.Contains(soundName)) icon.EnableAdjustmentText();
            (MultiSelect && Selected.Contains(soundName) ? _selectedGrid : _availableGrid).AddChild(icon);
        }
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

    /// <summary>One icon; its screen rectangle is pushed into the instanced render stack.
    /// With <see cref="ShowAdjustments" /> on, it also carries a value/volume/pan label and
    /// answers scroll - see <see cref="EnableAdjustmentText" />.</summary>
    private sealed class SoundIcon : Panel
    {
        private const float AdjustableCellWidth = 60f;
        private const float AdjustmentLabelHeight = 14f;
        private const float ValueStep = 1;
        private const float VolumeStep = 5;
        private const float PanStep = 5;

        private readonly SoundPicker _picker;
        private float _iconWidth;
        private float _iconHeight;
        private Label? _adjustmentLabel;

        public SoundIcon(UIContext context, SoundPicker picker, string soundName) : base(context)
        {
            _picker = picker;
            SoundName = soundName;
            UpdateCursorOnHover = true;
            Computed = new ComputedRectangle { OnUpdate = UpdateMatrix };
            OnClick = _ =>
            {
                if (!picker.MultiSelect)
                {
                    picker.OnPick?.Invoke(soundName);
                    return;
                }

                var selected = !picker.Selected.Contains(soundName);
                if (selected) picker.Selected.Add(soundName);
                else picker.Selected.Remove(soundName);

                picker.MoveIcon(this, selected);
                if (picker.ShowAdjustments)
                {
                    if (selected) EnableAdjustmentText();
                    else DisableAdjustmentText();
                }

                picker.RefreshKeybindNote();
                picker.RefreshSections();
            };
        }

        public string SoundName { get; }
        public TrackedBufferReference<StaticSound>? StaticReference { get; init; }
        public TrackedBufferReference<SoundData>? AnimatedReference { get; init; }

        public sealed override ComputedRectangle Computed { get; protected set; }

        /// <summary>Reserves room below the icon for the value/volume/pan label and adds it -
        /// only selected sounds carry one, so the Available grid stays plain icons. Idempotent;
        /// the first call captures the aspect-scaled icon size from <see cref="Width" />/
        /// <see cref="Height" /> before widening them (an object initializer sets those, so
        /// this can't run from the ctor).</summary>
        public void EnableAdjustmentText()
        {
            if (_adjustmentLabel != null) return;

            _iconWidth = Width.Value;
            _iconHeight = Height.Value;

            Width = Math.Max(_iconWidth, AdjustableCellWidth);
            Height = _iconHeight + AdjustmentLabelHeight;

            _adjustmentLabel = new Label(Context, "") { FontSizePx = 10f, Y = _iconHeight };
            AddChild(_adjustmentLabel);
            RefreshAdjustmentText();
        }

        /// <summary>Undoes <see cref="EnableAdjustmentText" /> - removes the label and shrinks
        /// back to the plain icon size. No-op if it was never enabled.</summary>
        public void DisableAdjustmentText()
        {
            if (_adjustmentLabel is null) return;

            RemoveChild(_adjustmentLabel);
            _adjustmentLabel = null;
            Width = _iconWidth;
            Height = _iconHeight;
        }

        /// <summary>Redraws the label from <see cref="SoundPicker.Adjustments" />, reusing the
        /// same value/volume/pan text conventions the playfield's sound badges use. No-op when
        /// <see cref="EnableAdjustmentText" /> was never called.</summary>
        public void RefreshAdjustmentText()
        {
            if (_adjustmentLabel is null) return;

            var adjustment = _picker.Adjustments.GetValueOrDefault(SoundName);
            var ev = new ExtendedEvent
            {
                SoundEvent = SoundName,
                Value = adjustment?.Value ?? 0,
                Volume = adjustment?.Volume,
                Pan = adjustment?.Pan ?? 0,
                ValueScale = ValueScale.None
            };

            var parts = new List<string>(3) { RenderableFactory.FormatValueText(ev) ?? "0" };
            if (RenderableFactory.FormatVolumeText(ev) is { } volumeText) parts.Add(volumeText);
            if (RenderableFactory.FormatPanText(ev) is { } panText) parts.Add(panText);

            _adjustmentLabel.Value = string.Join(" ", parts);
        }

        public override bool HandleScroll(Vector2 scrollDelta)
        {
            if (!_picker.ShowAdjustments || !_picker.Selected.Contains(SoundName)) return false;

            var notches = MathF.Sign(scrollDelta.Y);
            if (notches == 0) return false;

            if (!_picker.Adjustments.TryGetValue(SoundName, out var adjustment))
                _picker.Adjustments[SoundName] = adjustment = new SoundAdjustment();

            if (_picker.CtrlHeld)
                adjustment.Volume = Math.Clamp((adjustment.Volume ?? 100) + notches * VolumeStep, 0, 500);
            else if (_picker.ShiftHeld)
                adjustment.Pan = Math.Clamp(adjustment.Pan + notches * PanStep, -100, 100);
            else
                adjustment.Value = Math.Clamp(adjustment.Value + notches * ValueStep,
                    -TrackEditorView.MaxValue, TrackEditorView.MaxValue);

            RefreshAdjustmentText();
            _picker.OnPreviewSound?.Invoke(SoundName, adjustment);
            return true;
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
