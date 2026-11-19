using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace Sundex.Components.Inputs;

/// <summary>
///     A segmented control: a radio group drawn as a strip of labelled buttons, one of them picked.
///     Each segment is a button with the <c>segment</c> class (and <c>picked</c> while it is the
///     value) holding a <c>segment-text</c> label, all styled by the control's own sheet.
/// </summary>
[MarkupTag("segmented")]
public class Segmented : FlexPanel
{
    private readonly List<(string Value, Button Button, Label Label)> _segments = [];

    public Segmented(UIContext context) : base(context)
    {
        Direction = LayoutDirection.Horizontal;
    }

    public override string Tag => "segmented";

    /// <summary>The choices, as (value, label) pairs, in order.</summary>
    public IReadOnlyList<(string Value, string Label)> Options
    {
        get;
        set
        {
            field = value;
            Build();
        }
    } = [];

    public string Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Mark();
            OnValueChanged?.Invoke(this);
        }
    } = "";

    public Action<Segmented>? OnValueChanged { get; set; }

    private void Build()
    {
        foreach (var segment in _segments) segment.Button.Dispose();
        _segments.Clear();
        foreach (var (value, text) in Options)
        {
            var label = new Label(Context, text);
            label.Classes.Add("segment-text");
            var button = new Button(Context, label);
            button.Classes.Add("segment");
            button.OnClick = _ =>
            {
                if (!Disabled) Value = value;
            };
            if (StoredStyleSheet is { } sheet) button.ApplyStyleSheet(sheet);
            _segments.Add((value, button, label));
            AddChild(button);
        }

        Mark();
    }

    private void Mark()
    {
        foreach (var (value, button, label) in _segments)
        {
            button.SetClass("picked", value == Value);
            label.SetClass("picked", value == Value);
        }
    }

    public override void ApplyStyleSheet(Sundex.Style.DSL.StyleSheet styleSheet)
    {
        base.ApplyStyleSheet(styleSheet);
        foreach (var segment in _segments) segment.Button.ApplyStyleSheet(styleSheet);
    }
}
