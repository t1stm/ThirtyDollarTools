using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Atlases;
using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     Picks the note editor's active instrument: a header with the count, a list of the
///     project's instruments (<see cref="InstrumentRow" />) and a "New instrument" button. Pure
///     view - the owner (<see cref="InstrumentWorkflow" />) decides what "pick" means (set the
///     active instrument, or reassign notes').
/// </summary>
public sealed class InstrumentSelector : FlexPanel
{
    private const float IconSize = 28;
    // Two: a narrow fixed slot keeps every name close beside its icons; the count under the
    // name says how many there are.
    private const int MaxIcons = 2;

    private readonly Label _caption;
    private readonly FlexPanel _count;
    private readonly Label _countLabel;
    private readonly FlexPanel _empty;
    private readonly FlexPanel _header;
    private readonly IconList _list;
    private readonly Label _title;

    public InstrumentSelector(UIContext context, AtlasStore store) : base(context)
    {
        ID = "instrument-selector";
        Classes = ["dialog-frame"];

        _title = new Label(context, "Instruments") { Classes = ["dialog-title"] };
        _countLabel = new Label(context, "") { Classes = ["info-chip-label"] };
        _count = new FlexPanel(context) { Classes = ["info-chip"], Children = [_countLabel] };
        _caption = new Label(context, "") { Classes = ["dialog-subtitle"] };
        var close = new Button(context, Icons.X, font: Icons.Font)
        {
            Classes = ["icon-button"],
            OnClick = _ => OnClose?.Invoke(),
            Label = { Classes = ["icon-button-icon"] }
        };
        _header = new FlexPanel(context)
        {
            Classes = ["dialog-header"],
            Children = [_title, _count, new Panel(context) { Classes = ["spacer"] }, close]
        };

        _list = new IconList(context, store) { ID = "instrument-selector-list" };
        _empty = new FlexPanel(context)
        {
            Classes = ["instrument-selector-empty"],
            Children = [EmptyState.Build(context, Icons.Drum, "No instruments yet. New instrument makes one.")]
        };

        var newButton = new Button(context, "New instrument")
        {
            Classes = ["dialog-button-light"],
            OnClick = _ => OnNew?.Invoke(),
            Label = { Classes = ["dark-label"] }
        };
        KeyChip.Add(newButton, NewKeyName);

        Children =
        [
            _header,
            _list,
            new FlexPanel(context) { Classes = ["dialog-actions"], Children = [newButton] }
        ];
    }

    /// <summary>The New instrument key's name, on its chip; see <see cref="HandleKeyDown" />.</summary>
    private const string NewKeyName = "Shift+A";

    /// <summary>
    ///     Shift+A makes a new instrument, as in a 3D editor's "add". Not Space: that is Preview in
    ///     the instrument editor, and the rows here preview too. Reached through the modal, which
    ///     hands its content the keys no focused field took.
    /// </summary>
    public override bool HandleKeyDown(KeyboardKeyEventArgs e)
    {
        if (e is not { Key: Keys.A, Shift: true, Control: false, Alt: false, Command: false }) return false;
        OnNew?.Invoke();
        return true;
    }

    /// <summary>Fired with the picked instrument.</summary>
    public Action<Instrument>? OnPick { get; set; }

    /// <summary>Fired when a row's play button is used.</summary>
    public Action<Instrument>? OnPreview { get; set; }

    /// <summary>Fired when a row's duplicate button is used.</summary>
    public Action<Instrument>? OnDuplicate { get; set; }

    /// <summary>Fired when a row's edit button is used.</summary>
    public Action<Instrument>? OnEdit { get; set; }

    /// <summary>Fired when a row's delete button is used.</summary>
    public Action<Instrument>? OnDelete { get; set; }

    /// <summary>Fired when "New instrument" is clicked.</summary>
    public Action? OnNew { get; set; }

    /// <summary>Fired by the close button.</summary>
    public Action? OnClose { get; set; }

    /// <summary>
    ///     Rebuilds every row from scratch; the instrument list is small enough not to diff.
    /// </summary>
    /// <param name="marked">The row to highlight: the active instrument, or the one the notes being changed share.</param>
    /// <param name="sounds">Sounds by ID and emoji, for the rows' icons. Null draws none.</param>
    /// <param name="reassigning">How many notes picking changes; null when picking sets the active instrument.</param>
    public void Fill(IReadOnlyList<Instrument> instruments, Instrument? marked = null,
        IReadOnlyDictionary<string, Sound>? sounds = null, int? reassigning = null)
    {
        _title.SetTextContents(reassigning == null ? "Instruments" : "Change instrument");
        _countLabel.SetTextContents(instruments.Count.ToString());
        _caption.SetTextContents(reassigning == 1 ? "for 1 note" : $"for {reassigning} notes");
        if (reassigning == null) Replace(_header, _caption, _count);
        else Replace(_header, _count, _caption);

        foreach (var row in _list.Children.OfType<InstrumentRow>().ToArray())
        {
            _list.RemoveChild(row);
            row.ReleaseIcons();
        }

        foreach (var instrument in instruments)
        {
            _list.AddChild(new InstrumentRow(Context, instrument, _list.Images.Icons(instrument, sounds, IconSize, MaxIcons),
                i => OnPick?.Invoke(i), i => OnPreview?.Invoke(i), i => OnDuplicate?.Invoke(i), i => OnEdit?.Invoke(i),
                i => OnDelete?.Invoke(i))
            {
                Selected = instrument == marked
            });
        }

        // The empty state stands in for the list, so the list never shows as an empty box.
        if (instruments.Count > 0) Replace(this, _empty, _list);
        else Replace(this, _list, _empty);
    }

    /// <summary>Puts <paramref name="with" /> where <paramref name="old" /> is; nothing when it isn't there.</summary>
    private static void Replace(Panel parent, UIElement old, UIElement with)
    {
        var index = parent.Children.IndexOf(old);
        if (index < 0) return;
        parent.RemoveChild(old);
        parent.AddChild(with);
        parent.Children = [.. parent.Children.Take(index), with, .. parent.Children.Skip(index).SkipLast(1)];
    }

    /// <summary>
    ///     The list: a scroll view that also draws its rows' icons, as one batch. An icon sits four
    ///     levels below the list (row, the row's pick area, the icon strip, the icon), so the batch
    ///     is queued at that depth: any higher up, the row's own fill would paint over it.
    /// </summary>
    private sealed class IconList(UIContext context, AtlasStore store) : ScrollView(context)
    {
        public SoundImages Images { get; } = new(context, store);

        private int IconLayer => Index + 4;

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
}
