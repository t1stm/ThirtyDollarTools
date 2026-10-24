using EditorScene.Scenes.Components;
using EditorScene.Scenes.Layout;
using EditorScene.Scenes.Views;
using OpenTK.Mathematics;
using Shared.Atlases;
using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using Sundex.Markup;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using VisualizerScene.Settings;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     Create-or-edit form for one instrument (docs/handover/editor-instrument-builder-plan.md,
///     artboard A3): a name and a Preview button on top; on the left the instrument's sounds as
///     rows and a grid of the sounds it doesn't have yet; on the right the instrument-wide
///     fields, then the selected sound's fields and what it plays as on a note at 0.
///     Pure form - it edits <see cref="Working" />, a copy, and the owner loads it, commits it
///     and shows/hides the modal.
/// </summary>
public sealed class InstrumentEditor : FlexPanel
{
    /// <summary>Rows the sound list shows before it scrolls.</summary>
    public const int VisibleRows = 6;

    private const float RowHeight = 46;
    private const float RowSpacing = 4;
    private const float IconSize = 34;

    /// <summary>The tuning fields' labels, in <see cref="TuneField" /> order.</summary>
    private static readonly string[] FieldLabels = ["Value", "Volume", "Pan", "Offset"];

    private readonly Label _allHeader;
    private readonly Label[] _chipLabels;
    private readonly Label[] _columnNames;
    private readonly InspectorForm _allForm;
    private readonly Panel _emptyList;
    private readonly FlexPanel _noSelection;
    private readonly Label[] _results = new Label[SoundRow.Columns];
    private readonly List<SoundRow> _rows = [];
    private readonly SelectedSoundHeader _selectedHeader;
    private readonly FlexPanel _soundSection;
    private readonly InspectorForm _soundForm;
    private readonly Dictionary<string, Sound> _sounds = [];
    private readonly FlexPanel _soundsBlock;
    private readonly FlexPanel _right;

    private bool _ctrl;
    private bool _shift;
    private bool _alt;
    private bool _cardHovered;
    private bool _soundFieldsHovered;
    private int? _hoveredFieldColumn;
    private SoundRow? _hoveredRow;
    private int? _hoveredCell;

    public InstrumentEditor(UIContext context, AtlasStore store) : base(context)
    {
        ID = "instrument-editor";
        Classes = ["dialog-frame"];

        // The tree is InstrumentEditor.snx.xml; its root's children become this panel's.
        var sundex = new SundexContext(context);
        sundex.RegisterElementFactory("scroll-zone", ui => new ScrollZone(ui));
        sundex.RegisterElementFactory("sound-picker", ui => new SoundPicker(ui, store));
        sundex.RegisterElementFactory("selected-sound-header", ui => new SelectedSoundHeader(ui, store));
        var tree = sundex.NewComponent(Markup.Load(context, "Scenes/Dialogs/Instrument Editor/InstrumentEditor.snx.xml"));

        // ---- header
        NameInput = tree.GetID<TextInput>("instrument-name-input");
        var keyLabel = tree.GetID<Label>("key-label");
        keyLabel.Value = KeyText();
        // The key can be rebound while the editor exists; the label follows.
        Keybinds.Changed += () => keyLabel.Value = KeyText();
        PreviewButton = tree.GetID<FlexPanel>("preview-button");
        PreviewButton.UpdateCursorOnHover = true;

        // ---- left
        var columnHeads = tree.GetID<FlexPanel>("column-heads");
        foreach (var (chip, name) in ColumnHeads())
            columnHeads.AddChild(new FlexPanel(context)
            {
                Classes = ["instrument-column-head"],
                Children =
                [
                    new FlexPanel(context)
                    {
                        Classes = ["scroll-chip"],
                        Children =
                        [
                            new Label(context, chip) { Classes = ["scroll-chip-label", "instrument-column-chip-label"] }
                        ]
                    },
                    new Label(context, name) { Classes = ["instrument-column-name"] }
                ]
            });
        ColumnHeadChips = [.. columnHeads.Children.Select(head => (FlexPanel)((FlexPanel)head).Children[0])];
        _chipLabels = [.. ColumnHeadChips.Select(chip => (Label)chip.Children[0])];
        _columnNames = [.. columnHeads.Children.Select(head => (Label)((FlexPanel)head).Children[1])];

        SoundsList = new SoundList(context, store) { ID = "instrument-sounds-list" };
        _emptyList = tree.GetID<FlexPanel>("sounds-empty");
        _soundsBlock = tree.GetID<FlexPanel>("sounds-block");

        AvailablePicker = tree.GetID<SoundPicker>("instrument-available-picker");
        AvailablePicker.MultiSelect = true;
        AvailablePicker.AvailableOnly = true;
        AvailablePicker.OnInstanceAdded = added =>
        {
            RebuildRows();
            Select(added);
            ScrollIntoView(added);
        };

        FilterInput = tree.GetID<TextInput>("filter-input");
        FilterInput.Placeholder = "Filter sounds";
        FilterInput.OnValueChanged = input => AvailablePicker.Filter(input.Value);

        // ---- right
        _allHeader = tree.GetID<Label>("all-header");
        AllCard = tree.GetID<ScrollZone>("all-card");
        AllCard.OnScroll = TuneAll;
        AllCard.OnHoverEnter = _ => SetHover(null, true);
        AllCard.OnHoverExit = _ => SetHover(_hoveredRow, false);
        _allForm = new InspectorForm(context, AllCard, Edit) { Section = "All", Modifier = "instrument-form" };
        AddFields(_allForm, () => Working);

        _selectedHeader = tree.GetID<SelectedSoundHeader>("selected-header");
        SoundFields = tree.GetID<ScrollZone>("sound-fields");
        SoundFields.OnScroll = TuneSelected;
        SoundFields.OnHoverEnter = _ =>
        {
            _soundFieldsHovered = true;
            RefreshHighlights();
        };
        SoundFields.OnHoverExit = _ =>
        {
            _soundFieldsHovered = false;
            RefreshHighlights();
        };
        _soundForm = new InspectorForm(context, SoundFields, Edit) { Section = "Sound", Modifier = "instrument-form" };
        AddFields(_soundForm, () => Selected);

        var results = tree.GetID<FlexPanel>("results");
        for (var i = 0; i < _results.Length; i++)
        {
            _results[i] = new Label(context, "") { Classes = ["instrument-result-value"] };
            results.AddChild(new FlexPanel(context)
            {
                Classes = ["instrument-result"],
                Children = [new Label(context, ColumnHeads()[i].Name) { Classes = ["instrument-result-name"] }, _results[i]]
            });
        }

        _soundSection = tree.GetID<FlexPanel>("sound-section");
        _noSelection = tree.GetID<FlexPanel>("no-selection");
        _right = tree.GetID<FlexPanel>("right-column");
        _right.RemoveChild(_soundSection); // Select attaches it once there is a sound

        // ---- footer
        FineChip = tree.GetID<FlexPanel>("fine-chip");
        ((Label)FineChip.Children[0]).Value = $"{Keybinds.PrimaryName}+Shift+Scroll";
        DoneButton = tree.GetID<Button>("done-button");

        Children = [.. ((Panel)tree.Element).Children];

        for (var i = 0; i < FieldLabels.Length; i++)
            foreach (var section in (string[])["All", "Sound"])
            {
                var input = Field($"{section}.{FieldLabels[i]}")!;
                var column = i;
                input.OnHoverEnter = _ =>
                {
                    _hoveredFieldColumn = column;
                    RefreshHighlights();
                };
                input.OnHoverExit = _ =>
                {
                    _hoveredFieldColumn = null;
                    RefreshHighlights();
                };
                input.Unit = (TuneField)i switch
                {
                    TuneField.Volume => "%",
                    TuneField.Offset => "s",
                    _ => ""
                };
            }

        Refresh();
        RefreshHighlights();
    }

    public TextInput NameInput { get; }
    public FlexPanel PreviewButton { get; }
    public TextInput FilterInput { get; }
    public SoundPicker AvailablePicker { get; }
    public Button DoneButton { get; }

    /// <summary>The "All sounds" card: the instrument-wide fields, and a scroll zone for them.</summary>
    public ScrollZone AllCard { get; }

    /// <summary>The selected sound's fields, and a scroll zone for them like <see cref="AllCard" />.</summary>
    public ScrollZone SoundFields { get; }

    /// <summary>The Value/Volume/Pan/Offset column heads' key chips, in <see cref="SoundRow.Columns" /> order.</summary>
    public IReadOnlyList<FlexPanel> ColumnHeadChips { get; }

    /// <summary>The footer's Ctrl+Shift+Scroll chip.</summary>
    public FlexPanel FineChip { get; }

    internal SoundList SoundsList { get; }

    internal IReadOnlyList<SoundRow> Rows => _rows;

    /// <summary>
    ///     The instrument as the form has it: the instrument-wide fields, and the sounds (the
    ///     picker's instances, which the rows show and the fields tune). Not committed anywhere
    ///     until the owner reads it on Done.
    /// </summary>
    public Instrument Working { get; } = new();

    /// <summary>The sound the right column tunes; null when there is none.</summary>
    public InstrumentSound? Selected { get; private set; }

    /// <summary>Fired with a sound to play on its own - the row's Preview button.</summary>
    public Action<InstrumentSound>? OnPreviewSound { get; set; }

    /// <summary>The result tiles' values, in <see cref="TuneField" /> order.</summary>
    public IReadOnlyList<string> ResultTexts => [.. _results.Select(result => result.Value.ToString().TrimEnd('\0'))];

    /// <summary>The "All sounds - N" heading's text.</summary>
    public string AllHeaderText => _allHeader.Value.ToString();

    /// <summary>A field of the right column by key: "All.Value", "Sound.Volume", ...</summary>
    public NumericInput? Field(string key) => (key.StartsWith("All.", StringComparison.Ordinal) ? _allForm : _soundForm).Field(key) as NumericInput;

    /// <summary>What a wheel notch would tune now, from the held modifiers.</summary>
    private (TuneField Field, double Step) Gesture => ScrollGesture.For(_ctrl, _shift, _alt);

    /// <summary>
    ///     The held modifiers, forwarded from the editor's input every frame. They pick what a
    ///     scroll tunes (<see cref="ScrollGesture" />), and the chips and cells light to match.
    /// </summary>
    public void SetModifiers(bool ctrl, bool shift, bool alt)
    {
        if (_ctrl == ctrl && _shift == shift && _alt == alt) return;
        (_ctrl, _shift, _alt) = (ctrl, shift, alt);
        RefreshHighlights();
    }

    /// <summary>
    ///     What a notch tunes in a zone whose read-out or field under the pointer is
    ///     <paramref name="hovered" /> (a column, in <see cref="TuneField" /> order): with a
    ///     modifier held, that modifier's own field wherever in the zone the pointer is; without
    ///     one, the hovered field, and nothing at all between fields.
    /// </summary>
    private (TuneField Field, double Step)? TargetOf(int? hovered)
    {
        if (_ctrl || _shift || _alt) return Gesture;
        return hovered is { } column ? ((TuneField)column, ScrollGesture.StepOf((TuneField)column)) : null;
    }

    /// <summary>The target in whichever zone the pointer is over, if any; see <see cref="TargetOf" />.</summary>
    private (TuneField Field, double Step)? HoveredTarget =>
        _hoveredRow != null ? TargetOf(_hoveredCell)
        : _cardHovered || _soundFieldsHovered ? TargetOf(_hoveredFieldColumn)
        : null;

    /// <summary>
    ///     A notch over a row's read-outs: tunes the read-out it landed on, <paramref name="column" />
    ///     (null between two), or the held modifier's; then selects the sound and plays it. Between
    ///     two read-outs a plain notch tunes nothing, and still isn't passed on: the list scrolling
    ///     under the pointer would turn the next notch into an edit.
    /// </summary>
    private bool TuneRow(SoundRow row, int? column, Vector2 scrollDelta)
    {
        var notches = MathF.Sign(scrollDelta.Y);
        if (notches == 0) return false;
        if (TargetOf(column) is not { } target) return true;

        ScrollGesture.Apply(row.Instance, target.Field, notches * target.Step);
        Select(row.Instance);
        OnPreviewSound?.Invoke(row.Instance);
        return true;
    }

    /// <summary>
    ///     A notch over the "All sounds" card that no field took: a held modifier's field, or
    ///     nothing. A plain notch over a field is that field's own (see
    ///     <see cref="RefreshHighlights" />), so a plain one here is between fields.
    /// </summary>
    private bool TuneAll(Vector2 scrollDelta)
    {
        var notches = MathF.Sign(scrollDelta.Y);
        if (notches == 0) return false;
        if (TargetOf(null) is not { } target) return true;

        ScrollGesture.Apply(Working, target.Field, notches * target.Step);
        Refresh();
        return true;
    }

    /// <summary>The same over the selected sound's fields, for that sound.</summary>
    private bool TuneSelected(Vector2 scrollDelta)
    {
        var notches = MathF.Sign(scrollDelta.Y);
        if (notches == 0 || Selected is not { } sound) return false;
        if (TargetOf(null) is not { } target) return true;

        ScrollGesture.Apply(sound, target.Field, notches * target.Step);
        Refresh();
        return true;
    }

    private void SetHover(SoundRow? row, bool card)
    {
        _hoveredRow = row;
        _cardHovered = card;
        RefreshHighlights();
    }

    /// <summary>
    ///     Lights what a scroll would change (<see cref="HoveredTarget" />): its column's head
    ///     chip, the hovered row's cell, and the matching field on the right - the instrument's
    ///     while the pointer is over its card, the sound's while over its fields or the selected
    ///     row. With nothing under the pointer, a held modifier still lights its chip. A field
    ///     under the pointer takes a plain notch itself; while a modifier is held it lets the
    ///     notch through to its card, so the modifier reaches its own field from there.
    /// </summary>
    private void RefreshHighlights()
    {
        var modified = _ctrl || _shift || _alt;
        var target = HoveredTarget?.Field;
        var column = (int?)target ?? (modified && _hoveredRow == null && !_cardHovered && !_soundFieldsHovered
            ? (int)Gesture.Field
            : null);

        for (var i = 0; i < ColumnHeadChips.Count; i++)
        {
            ColumnHeadChips[i].SetClass("scroll-chip-lit", i == column);
            _chipLabels[i].SetClass("scroll-chip-label-lit", i == column);
            _columnNames[i].SetClass("instrument-column-name-lit", i == column);
        }

        var fine = _ctrl && _shift && !_alt;
        FineChip.SetClass("scroll-chip-lit", fine);
        ((Label)FineChip.Children[0]).SetClass("scroll-chip-label-lit", fine);

        foreach (var row in _rows)
        {
            var hovered = ReferenceEquals(row, _hoveredRow);
            row.Highlight(hovered ? (int?)target : null, hovered);
        }

        var outlined = target is not { } lit ? null
            : _cardHovered ? Field($"All.{FieldLabels[(int)lit]}")
            : _soundFieldsHovered || (_hoveredRow != null && ReferenceEquals(_hoveredRow.Instance, Selected))
                ? Field($"Sound.{FieldLabels[(int)lit]}")
                : null;
        for (var i = 0; i < FieldLabels.Length; i++)
            foreach (var section in (string[])["All", "Sound"])
            {
                var input = Field($"{section}.{FieldLabels[i]}")!;
                input.ScrollStep = modified ? null : ScrollGesture.StepOf((TuneField)i);
                input.SetClass("instrument-field-target", ReferenceEquals(input, outlined));
            }
    }

    private static string KeyText() => Keybinds.Get(Bind.EditorPlayPause).ToString();

    private static (string Chip, string Name)[] ColumnHeads() =>
        [("Scroll", "Value"), (Keybinds.PrimaryName, "Volume"), ("Shift", "Pan"), ("Alt", "Offset")];

    /// <summary>Fills the sound grid on first use; a no-op once the picker holds icons.</summary>
    public void EnsureSounds(IEnumerable<Sound> sounds)
    {
        if (AvailablePicker.HasSounds) return;

        var list = sounds.ToList();
        foreach (var sound in list) _sounds[sound.Id] = sound;
        AvailablePicker.Fill(list);
    }

    /// <summary>
    ///     Pre-loads the form from <paramref name="source" />, copied, or blank for a fresh
    ///     instrument. Selects the first sound.
    /// </summary>
    public void Load(string name, Instrument? source)
    {
        NameInput.Value = name;
        FilterInput.Value = "";
        AvailablePicker.Filter("");

        Working.Value = source?.Value ?? 0;
        Working.Volume = source?.Volume ?? 100;
        Working.Pan = source?.Pan ?? 0;
        Working.Offset = source?.Offset ?? 0;
        AvailablePicker.SetInstances(source?.Sounds ?? []);

        RebuildRows();
        Select(Working.Sounds.FirstOrDefault());
    }

    public void Select(InstrumentSound? sound)
    {
        Selected = sound;
        foreach (var row in _rows) row.SetSelected(ReferenceEquals(row.Instance, sound));

        var showing = _right.Children.Contains(_soundSection);
        if (sound != null && !showing)
        {
            _right.RemoveChild(_noSelection);
            _right.AddChild(_soundSection);
        }
        else if (sound == null && showing)
        {
            _right.RemoveChild(_soundSection);
            _right.AddChild(_noSelection);
        }

        if (sound != null) _selectedHeader.Show(_sounds.GetValueOrDefault(sound.Sound), sound.Sound);
        Refresh();
        RefreshHighlights();
    }

    /// <summary>Inserts a copy of <paramref name="sound" /> after it, and selects the copy.</summary>
    public void Duplicate(InstrumentSound sound)
    {
        var copy = AvailablePicker.AddInstance(sound.Sound, sound);
        RebuildRows();
        Select(copy);
        ScrollIntoView(copy);
    }

    /// <summary>Scrolls the list just far enough to show <paramref name="sound" />'s row.</summary>
    private void ScrollIntoView(InstrumentSound sound)
    {
        // Laid out first: the list's scroll range still reflects the rows before this change.
        SoundsList.Layout();
        var top = Working.Sounds.IndexOf(sound) * (RowHeight + RowSpacing);
        var height = SoundsList.Computed.Height;
        if (top < SoundsList.ScrollY) SoundsList.ScrollY = top;
        else if (top + RowHeight > SoundsList.ScrollY + height) SoundsList.ScrollY = top + RowHeight - height;
    }

    /// <summary>Removes <paramref name="sound" />; a removed selection moves to the sound that took its place.</summary>
    public void Remove(InstrumentSound sound)
    {
        var index = Working.Sounds.IndexOf(sound);
        AvailablePicker.RemoveInstance(sound);
        RebuildRows();

        if (!ReferenceEquals(Selected, sound) && Selected != null)
        {
            Select(Selected);
            return;
        }

        Select(Working.Sounds.Count == 0 ? null : Working.Sounds[Math.Clamp(index, 0, Working.Sounds.Count - 1)]);
    }

    /// <summary>Applies one field edit to the working copy, then redraws everything that shows it.</summary>
    private void Edit(Action change)
    {
        change();
        Refresh();
    }

    /// <summary>
    ///     The four tuning fields, read from and written to whatever <paramref name="target" />
    ///     returns at the time - the instrument, or the selected sound - so a new selection needs
    ///     only a sync, not new fields.
    /// </summary>
    private static void AddFields(InspectorForm form, Func<object?> target)
    {
        form.NumberRow("Value", () => Get(target(), t => t.Value, s => s.Value),
            v => Set(target(), v, (t, x) => t.Value = x!.Value, (s, x) => s.Value = x!.Value),
            -TrackEditorView.MaxValue, TrackEditorView.MaxValue);
        form.NumberRow("Volume", () => Get(target(), t => t.Volume, s => s.Volume),
            v => Set(target(), v, (t, x) => t.Volume = x!.Value, (s, x) => s.Volume = x!.Value), 0, 500, 5);
        form.NumberRow("Pan", () => Get(target(), t => t.Pan, s => s.Pan),
            v => Set(target(), v, (t, x) => t.Pan = (float)x!.Value, (s, x) => s.Pan = (float)x!.Value),
            -100, 100, 10);
        // A sound can't start before its own beginning: an offset is 0 or later.
        form.NumberRow("Offset", () => Get(target(), t => t.Offset, s => s.Offset),
            v => Set(target(), v, (t, x) => t.Offset = x!.Value, (s, x) => s.Offset = x!.Value), 0,
            ScrollGesture.MaxOffset, 0.01);
    }

    private static double? Get(object? target, Func<Instrument, double?> instrument, Func<InstrumentSound, double?> sound) =>
        target switch
        {
            Instrument i => instrument(i),
            InstrumentSound s => sound(s),
            _ => null
        };

    private static void Set(object? target, double? value, Action<Instrument, double?> instrument,
        Action<InstrumentSound, double?> sound)
    {
        if (target is Instrument i) instrument(i, value);
        else if (target is InstrumentSound s) sound(s, value);
    }

    /// <summary>
    ///     Rebuilds one row per sound, after the sounds changed. The picker's instances are the
    ///     working copy's sounds: <see cref="Working" /> mirrors them here.
    /// </summary>
    private void RebuildRows()
    {
        Working.Sounds.Clear();
        Working.Sounds.AddRange(AvailablePicker.Instances);

        foreach (var row in _rows)
        {
            SoundsList.RemoveChild(row);
            row.Icon.Release();
        }

        _rows.Clear();
        _hoveredRow = null;
        _hoveredCell = null;

        foreach (var sound in Working.Sounds)
        {
            var icon = (_sounds.TryGetValue(sound.Sound, out var known)
                           ? SoundsList.Images.Create(known, IconSize)
                           : null)
                       ?? new SoundImage(Context, null, IconSize);
            var row = new SoundRow(Context, sound, icon)
            {
                OnSelect = r => Select(r.Instance),
                OnPreview = r => OnPreviewSound?.Invoke(r.Instance),
                OnDuplicate = r => Duplicate(r.Instance),
                OnRemove = r => Remove(r.Instance)
            };
            row.NumberZone.OnScroll = delta => TuneRow(row, null, delta);
            row.OnCellScroll = (column, delta) => TuneRow(row, column, delta);
            row.OnCellHover = (_, cell) =>
            {
                _hoveredCell = cell;
                RefreshHighlights();
            };
            row.NumberZone.OnHoverEnter = _ => SetHover(row, false);
            row.NumberZone.OnHoverExit = _ =>
            {
                if (ReferenceEquals(_hoveredRow, row)) SetHover(null, _cardHovered);
            };
            _rows.Add(row);
            SoundsList.AddChild(row);
        }

        var visible = Math.Min(_rows.Count, VisibleRows);
        SoundsList.Height = visible * RowHeight + Math.Max(0, visible - 1) * RowSpacing;

        // The empty state stands in for the list, so the list never shows as a zero-height box.
        var list = _rows.Count > 0 ? SoundsList : _emptyList;
        var stale = list == SoundsList ? _emptyList : SoundsList;
        if (_soundsBlock.Children.Contains(stale)) _soundsBlock.RemoveChild(stale);
        if (!_soundsBlock.Children.Contains(list)) _soundsBlock.AddChild(list);
    }

    /// <summary>Redraws everything that shows a value: headings, read-outs, fields and results.</summary>
    private void Refresh()
    {
        _allHeader.Value = $"All sounds - {Working.Sounds.Count}";
        foreach (var row in _rows) row.Refresh();
        _allForm.Sync();
        _soundForm.Sync();

        if (Selected is not { } sound) return;

        _selectedHeader.SetPosition($"Sound {Working.Sounds.IndexOf(sound) + 1} of {Working.Sounds.Count}");

        // What it plays as on a note at 0: the same combination playback uses.
        var (value, volume, pan, offset) = Working.Combine(sound);
        _results[0].Value = SoundRow.ValueText(value) ?? "0";
        _results[1].Value = $"{volume ?? 100:0.##}%"; // none left from the combination plays at 100
        _results[2].Value = SoundRow.PanText(pan) ?? "0";
        _results[3].Value = $"{offset:0.###} s";
    }

    /// <summary>
    ///     The sound list: a scroll view that also draws its rows' icons, as one batch. A row's
    ///     icon sits three levels below the list (list, row, the row's icon-and-name area), so
    ///     the batch is queued at that depth: one level up, the row's own fill would paint over it.
    /// </summary>
    internal sealed class SoundList(UIContext context, AtlasStore store) : ScrollView(context)
    {
        public SoundImages Images { get; } = new(context, store);

        private int IconLayer => Index + 3;

        protected override void DrawSelf(UIContext ctx)
        {
            base.DrawSelf(ctx);
            ctx.QueueRender(Images.Stacks, IconLayer);
        }

        public override void StopRendering()
        {
            Context.DequeueRender(Images.Stacks, IconLayer);
            base.StopRendering();
        }

        public override void ApplyClip(Vector4i? clip)
        {
            base.ApplyClip(clip);
            var x = (int)Computed.AbsoluteX;
            var y = (int)Computed.AbsoluteY;
            Images.Stacks.ClipRect = IntersectClip(new Vector4i(x, y, x + (int)Computed.Width, y + (int)Computed.Height), clip);
        }
    }

    /// <summary>The selected sound's icon and name, with its position in the list under the name.</summary>
    private sealed class SelectedSoundHeader : FlexPanel
    {
        private const float Size = 28;

        private readonly SoundImages _images;
        private readonly Label _name;
        private readonly Label _position;
        private SoundImage _icon;

        public SelectedSoundHeader(UIContext context, AtlasStore store) : base(context)
        {
            _images = new SoundImages(context, store);
            Classes = ["instrument-sound-header"];
            _icon = new SoundImage(context, null, Size);
            _name = new Label(context, "") { Classes = ["instrument-section-title"] };
            _position = new Label(context, "") { Classes = ["instrument-footer-label"] };
            Children =
            [
                _icon,
                new FlexPanel(context) { Classes = ["instrument-sound-header-text"], Children = [_name, _position] }
            ];
        }

        public void Show(Sound? sound, string name)
        {
            _name.Value = name;

            var icon = (sound == null ? null : _images.Create(sound, Size)) ?? new SoundImage(Context, null, Size);
            RemoveChild(_icon);
            _icon.Release();
            _icon = icon;
            AddChild(_icon);
            Children = [_icon, .. Children.Where(child => child != _icon)];
        }

        public void SetPosition(string text) => _position.Value = text;

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
    }
}
