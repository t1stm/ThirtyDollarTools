using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Layout;

/// <summary>
///     Generic row builders shared by every <see cref="InspectorPanel" /> section:
///     labeled inputs keyed "Section.Label" for headless field lookup, sync callbacks
///     that refresh values in place without interrupting a focused input, and cards
///     that box a group of rows. Pure builder - no domain knowledge of what a section
///     contains, that's <see cref="InspectorPanel" />'s job.
/// </summary>
public sealed class InspectorForm
{
    private const float LabelWidth = 84f;
    private const float FieldWidth = 140f;
    private const float RowHeight = 34f;

    private static readonly Vector4 HeaderColor = EditorPalette.Header;
    private static readonly Vector4 NameColor = EditorPalette.TextMuted;
    private static readonly Vector4 InputColor = EditorPalette.InputBackground;

    private readonly UIContext _context;
    private readonly Dictionary<string, UIElement> _fields = [];
    private readonly Panel _root;
    private readonly EditorState _state;
    private readonly List<Action> _syncs = [];
    private Panel _container;

    public InspectorForm(UIContext context, EditorState state, Panel root)
    {
        _context = context;
        _state = state;
        _root = root;
        _container = root;
    }

    /// <summary>
    ///     The field's key prefix ("Section" in "Section.Label") - the current
    ///     section/card content methods are building rows for.
    /// </summary>
    public string Section { get; set; } = "";

    /// <summary>The input element showing a field, keyed "Section.Label" (e.g. "Track.Name").</summary>
    public UIElement? Field(string key)
    {
        return _fields.GetValueOrDefault(key);
    }

    /// <summary>Clears every row/field/sync and resets the build target to <paramref name="root" />. Call before rebuilding.</summary>
    public void Reset()
    {
        _fields.Clear();
        _syncs.Clear();
        _container = _root;
        Section = "";
    }

    /// <summary>Writes the model values into the rows. Call on any model change.</summary>
    public void Sync()
    {
        // Snapshot: a sync-triggered handler may Rebuild (and clear) the list.
        foreach (var sync in _syncs.ToArray()) sync();
    }

    /// <summary>
    ///     Section header; <paramref name="trailing" /> elements (e.g. action buttons)
    ///     sit in a horizontal row right after the label.
    /// </summary>
    public void Header(string text, params UIElement[] trailing)
    {
        Section = text;
        var label = new Label(_context, text) { FontSizePx = 15f, Color = HeaderColor };
        if (trailing.Length == 0)
        {
            _container.AddChild(label);
            return;
        }

        _container.AddChild(new FlexPanel(_context)
        {
            Width = LiteralOrComputable.Percent(100),
            Spacing = 8,
            VerticalAlign = Align.Center,
            Children = [label, .. trailing]
        });
    }

    /// <summary>
    ///     Boxes one automation/keyframe entry as its own background-filled card so
    ///     adjacent entries in the flat, uniformly-spaced row list read as distinct
    ///     groups instead of one continuous run of rows.
    /// </summary>
    public void Card(Vector4 color, Action build)
    {
        var card = new FlexPanel(_context)
        {
            Direction = LayoutDirection.Vertical,
            Width = LiteralOrComputable.Percent(100),
            Padding = 8,
            Spacing = 8,
            Background = new ColoredPlane { Color = color, BorderRadius = 4f }
        };
        var previous = _container;
        previous.AddChild(card);
        _container = card;
        build();
        _container = previous;
    }

    private void Row(string label, UIElement input, UIElement? extra = null)
    {
        _fields[$"{Section}.{label}"] = input;
        input.X = LabelWidth;
        var row = new Panel(_context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = RowHeight
        };
        row.AddChild(new Label(_context, label) { FontSizePx = 13f, Color = NameColor, Y = 9 });
        row.AddChild(input);
        if (extra != null) row.AddChild(extra);
        _container.AddChild(row);
    }

    public void TextRow(string label, Func<string> get, Action<string> set)
    {
        var input = new TextInput(_context, get())
        {
            Width = FieldWidth,
            FontSizePx = 14f,
            BorderRadius = 4,
            Background = new ColoredPlane { Color = InputColor },
            OnValueChanged = i => set(i.Value)
        };
        Row(label, input);
        _syncs.Add(() =>
        {
            if (!input.IsFocused) input.Value = get();
        });
    }

    /// <summary>
    ///     A numeric field routed through <see cref="EditorState.Edit" />. Non-nullable
    ///     fields ignore the transient null while the text is mid-edit ("", "-");
    ///     nullable ones commit it (empty = inherit).
    /// </summary>
    /// <param name="mixed">
    ///     For multi-selection rows: when it returns true, the field renders empty
    ///     instead of an arbitrary single value - same as an unset nullable field.
    ///     // ponytail: no literal "mixed" placeholder word (that needs a Placeholder
    ///     feature on TextInput/NumericInput, unused anywhere else); empty reads the
    ///     same as every other "no single value" field. Add a Placeholder property if
    ///     the empty box alone proves ambiguous to users.
    /// </param>
    public void NumberRow(string label, Func<double?> get, Action<double?> set,
        double min, double max, double step = 1, bool allowNull = false, Func<bool>? mixed = null)
    {
        var input = new NumericInput(_context, mixed?.Invoke() == true ? null : get())
        {
            Width = FieldWidth,
            Height = 32,
            FontSizePx = 14f,
            Min = min,
            Max = max,
            Step = step,
            AllowNull = allowNull || mixed != null,
            BorderRadius = 4,
            Background = new ColoredPlane { Color = InputColor }
        };
        input.OnValueChanged = _ =>
        {
            var value = input.Value;
            if (value != null || allowNull) _state.Edit(() => set(value));
        };
        Row(label, input);
        _syncs.Add(() =>
        {
            if (input.IsFocused) return;
            input.Value = mixed?.Invoke() == true ? null : get();
        });
    }

    public void IntRow(string label, Func<int> get, Action<int> set, int min, int max)
    {
        NumberRow(label, () => get(), v => set((int)Math.Round(v!.Value)), min, max);
    }

    /// <summary>
    ///     A checkbox; <paramref name="extras" /> (e.g. flags that only apply while
    ///     it's on) sit right next to it on the same line, wrapping if the panel is too narrow.
    /// </summary>
    public void CheckRow(string label, Func<bool> get, Action<bool> set,
        params (string Label, Func<bool> Get, Action<bool> Set)[] extras)
    {
        var box = Check(label, get, set);
        if (extras.Length == 0)
        {
            _container.AddChild(box);
            return;
        }

        _container.AddChild(new FlexPanel(_context)
        {
            Width = LiteralOrComputable.Percent(100),
            Spacing = 12,
            Wrap = true,
            VerticalAlign = Align.Center,
            Children = [box, .. extras.Select(extra => (UIElement)Check(extra.Label, extra.Get, extra.Set))]
        });
    }

    private Checkbox Check(string label, Func<bool> get, Action<bool> set)
    {
        var box = new Checkbox(_context, label, get()) { OnCheckedChanged = b => set(b.Checked) };
        box.Label.FontSizePx = 13f;
        _fields[$"{Section}.{label}"] = box;
        _syncs.Add(() => box.Checked = get());
        return box;
    }

    public void InfoRow(string label, Func<string> get)
    {
        var value = new Label(_context, get()) { FontSizePx = 13f, Y = 9 };
        Row(label, value);
        _syncs.Add(() => value.SetTextContents(get()));
    }

    public void ActionRow(string label, Action onClick)
    {
        ActionRow(label, label, onClick);
    }

    /// <summary>
    ///     A key/label split for buttons whose display text is dynamic (e.g. a
    ///     selection count) but whose field key must stay stable.
    /// </summary>
    public void ActionRow(string key, string label, Action onClick)
    {
        var button = new Button(_context, label) { FontSizePx = 13f, OnClick = _ => onClick() };
        _fields[$"{Section}.{key}"] = button;
        _container.AddChild(button);
    }

    /// <summary>
    ///     A relative-change field: amount plus a "×" checkbox choosing multiply over
    ///     add. Committed as one <see cref="Modifier" /> whenever either part changes.
    /// </summary>
    public void ModifierRow(string label, Func<Modifier> get, Action<Modifier> set)
    {
        var initial = get();
        var amount = new NumericInput(_context, initial.Amount)
        {
            Width = 100,
            Height = 32,
            FontSizePx = 14,
            Min = -10000,
            Max = 10000,
            BorderRadius = 4,
            Background = new ColoredPlane { Color = InputColor }
        };
        var multiply = new Checkbox(_context, "×", initial.Kind == ModifierKind.Multiply)
        {
            X = LabelWidth + 108,
            Y = 6
        };
        multiply.Label.FontSizePx = 13f;

        void Commit()
        {
            if (amount.Value is not { } value) return; // mid-edit ("", "-")
            _state.Edit(() =>
                set(new Modifier(value, multiply.Checked ? ModifierKind.Multiply : ModifierKind.Add)));
        }

        amount.OnValueChanged = _ => Commit();
        multiply.OnCheckedChanged = _ => Commit();

        _fields[$"{Section}.{label}.Kind"] = multiply;
        Row(label, amount, multiply);
        _syncs.Add(() =>
        {
            if (!amount.IsFocused) amount.Value = get().Amount;
            multiply.Checked = get().Kind == ModifierKind.Multiply;
        });
    }
}