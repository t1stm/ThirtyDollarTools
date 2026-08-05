using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

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
    /// <summary>
    ///     Where a row's input starts, and what the "×" in a modifier row is offset from.
    ///     A layout offset inside an absolutely-positioned row rather than a look, so it
    ///     stays here; the row's own height and the field widths are in the sheet.
    /// </summary>
    private const float LabelWidth = 84f;

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
        var label = new Label(_context, text) { Classes = ["title-label"] };
        if (trailing.Length == 0)
        {
            _container.AddChild(label);
            return;
        }

        _container.AddChild(new FlexPanel(_context)
        {
            Classes = ["inspector-header-row"],
            Children = [label, .. trailing]
        });
    }

    /// <summary>
    ///     Boxes one automation/keyframe entry as its own background-filled card so
    ///     adjacent entries in the flat, uniformly-spaced row list read as distinct
    ///     groups instead of one continuous run of rows. <paramref name="shadeClass" />
    ///     picks how far the card sits above the panel - see the inspector-card-* classes.
    /// </summary>
    public void Card(string shadeClass, Action build)
    {
        var card = new FlexPanel(_context) { Classes = ["inspector-card", shadeClass] };
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
        var row = new Panel(_context) { Classes = ["form-row"] };
        // Y centers the text against form-row's height - a layout offset, not a look.
        row.AddChild(new Label(_context, label) { Classes = ["muted-label"], Y = 9 });
        row.AddChild(input);
        if (extra != null) row.AddChild(extra);
        _container.AddChild(row);
    }

    public void TextRow(string label, Func<string> get, Action<string> set)
    {
        var input = new TextInput(_context, get())
        {
            Classes = ["text-field", "inspector-field"],
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
            Classes = ["text-field", "text-field-tall", "inspector-field"],
            Min = min,
            Max = max,
            Step = step,
            AllowNull = allowNull || mixed != null
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
            Classes = ["inspector-check-row"],
            Children = [box, .. extras.Select(extra => (UIElement)Check(extra.Label, extra.Get, extra.Set))]
        });
    }

    private Checkbox Check(string label, Func<bool> get, Action<bool> set)
    {
        var box = new Checkbox(_context, label, get()) { OnCheckedChanged = b => set(b.Checked) };
        box.Label.Classes = ["inspector-check-label"];
        _fields[$"{Section}.{label}"] = box;
        _syncs.Add(() => box.Checked = get());
        return box;
    }

    public void InfoRow(string label, Func<string> get)
    {
        var value = new Label(_context, get()) { Classes = ["inspector-check-label"], Y = 9 };
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
        var button = new Button(_context, label) { Classes = ["inspector-check-label"], OnClick = _ => onClick() };
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
            Classes = ["text-field", "inspector-field-narrow"],
            Min = -10000,
            Max = 10000
        };
        var multiply = new Checkbox(_context, "×", initial.Kind == ModifierKind.Multiply)
        {
            X = LabelWidth + 108,
            Y = 6
        };
        multiply.Label.Classes = ["inspector-check-label"];

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