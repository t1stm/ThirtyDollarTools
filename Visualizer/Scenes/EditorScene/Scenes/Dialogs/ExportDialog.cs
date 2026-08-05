using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The export options form (ModalLayer content): the <see cref="SequenceStyle" />
///     cosmetics plus the two export actions. Pure form - the owner handles file
///     dialogs and the actual writing.
/// </summary>
public sealed class ExportDialog : FlexPanel
{
    /// <summary>
    ///     Where a row's input starts. A layout offset rather than a look - the label sits
    ///     left of it in the same absolutely-positioned row - so it stays in code.
    /// </summary>
    private const float LabelWidth = 190f;

    public ExportDialog(UIContext context) : base(context)
    {
        ID = "export-dialog";
        Classes = ["dialog-frame"];

        var defaults = new SequenceStyle();
        DividerEveryBars = new NumericInput(context, 2)
        {
            Classes = ["export-field"],
            Min = 0,
            Max = 1024
        };
        DividerOnSpeedChanges = new Checkbox(context, "Divider on !speed changes")
        {
            Label = { Classes = ["muted-check-label"] }
        };
        MigrateToStop = new NumericInput(context, defaults.MigrateToStop)
        {
            Classes = ["export-field"],
            Min = 1,
            Max = 4096,
            AllowNull = true
        };

        TdwButton = new Button(context, "Export .tdw") { Classes = ["dialog-button-primary"] };
        WavButton = new Button(context, "Export .wav") { Classes = ["dialog-button-primary"] };
        CancelButton = new Button(context, "Cancel") { Classes = ["dialog-button"] };

        AddChild(new Label(context, "Export") { Classes = ["title-label-large"] });
        Row("Divider every N bars (0 = off)", DividerEveryBars);
        Row("!stop after N pauses (0 = never)", MigrateToStop);
        AddChild(DividerOnSpeedChanges);

        // Percent-width spacer soaks up the free space so Cancel sits flush left and the
        // export actions flush right - this framework has no space-between align.
        var buttonRowSpacer = new Panel(context) { Classes = ["spacer"] };
        AddChild(new FlexPanel(context)
        {
            ID = "export-actions",
            Classes = ["dialog-actions-split"],
            Children = [CancelButton, buttonRowSpacer, TdwButton, WavButton]
        });
    }

    public NumericInput DividerEveryBars { get; }
    public Checkbox DividerOnSpeedChanges { get; }
    public NumericInput MigrateToStop { get; }
    public Button TdwButton { get; }
    public Button WavButton { get; }
    public Button CancelButton { get; }

    /// <summary>The style the form currently describes.</summary>
    public SequenceStyle Style => new()
    {
        DividerEveryBars = (int?)DividerEveryBars.Value,
        DividerOnSpeedChanges = DividerOnSpeedChanges.Checked,
        MigrateToStop = (int?)MigrateToStop.Value
    };

    private void Row(string label, UIElement input)
    {
        input.X = LabelWidth;
        var row = new Panel(Context) { Classes = ["form-row"] };
        // Y centers the text in the row - a layout offset that depends on form-row's
        // height, not something to look up separately.
        row.AddChild(new Label(Context, label) { Classes = ["muted-label"], Y = 9 });
        row.AddChild(input);
        AddChild(row);
    }
}