using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;

namespace Sundex.Components.Panels;

/// <summary>
///     HTML's <c>&lt;details&gt;</c>: the first child is the summary, and clicking it shows or hides
///     the rest. <see cref="Open" /> can be bound both ways.
/// </summary>
[MarkupTag("details")]
public class Details : FlexPanel
{
    public Details(UIContext context) : base(context)
    {
        Direction = LayoutDirection.Vertical;
        AlignItems = Align.Stretch;
    }

    public override string Tag => "details";

    public bool Open
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Sync();
            OnOpenChanged?.Invoke(this);
        }
    }

    public Action<Details>? OnOpenChanged { get; set; }

    public override void AddChild(UIElement child)
    {
        base.AddChild(child);
        if (Children.Count == 1)
        {
            child.OnClick ??= _ => Open = !Open;
            if (child.Cursor == CursorType.Default) child.Cursor = CursorType.Pointer;
        }
        Sync();
    }

    private void Sync()
    {
        for (var index = 1; index < Children.Count; index++) Children[index].Visible = Open;
    }
}
