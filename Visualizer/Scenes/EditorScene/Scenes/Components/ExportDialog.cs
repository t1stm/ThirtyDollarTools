using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Components;

/// <summary>
///     The export options form (ModalLayer content): the <see cref="SequenceStyle" />
///     cosmetics plus the two export actions. Pure form - the owner handles file
///     dialogs and the actual writing.
/// </summary>
public sealed class ExportDialog : FlexPanel
{
    private const float LabelWidth = 190f;
    private static readonly Vector4 ButtonBlandColor = EditorPalette.Divider;
    private static readonly Vector4 ButtonMainColor = EditorPalette.Accent;

    public ExportDialog(UIContext context) : base(context)
    {
        Direction = LayoutDirection.Vertical;
        Width = 420;
        Padding = 14;
        Spacing = 10;
        Background = new ColoredPlane { Color = EditorPalette.Panel };

        var defaults = new SequenceStyle();
        DividerEveryBars = new NumericInput(context, 2)
        {
            Width = 120,
            Height = 32,
            FontSizePx = 14,
            Min = 0,
            Max = 1024
        };
        DividerOnSpeedChanges = new Checkbox(context, "Divider on !speed changes")
        {
            Label =
            {
                FontSizePx = 13f
            }
        };
        MigrateToStop = new NumericInput(context, defaults.MigrateToStop)
        {
            Width = 120,
            Height = 32,
            FontSizePx = 14,
            Min = 1,
            Max = 4096,
            AllowNull = true
        };

        TdwButton = new Button(context, "Export .tdw")
        { FontSizePx = 14, Background = new ColoredPlane { Color = ButtonMainColor }, BorderRadius = 6 };
        WavButton = new Button(context, "Export .wav")
        { FontSizePx = 14, Background = new ColoredPlane { Color = ButtonMainColor }, BorderRadius = 6 };
        CancelButton = new Button(context, "Cancel")
        { FontSizePx = 14, Background = new ColoredPlane { Color = ButtonBlandColor }, BorderRadius = 6 };

        AddChild(new Label(context, "Export") { FontSizePx = 18f, Color = EditorPalette.Header });
        Row("Divider every N bars (0 = off)", DividerEveryBars);
        Row("!stop after N pauses (0 = never)", MigrateToStop);
        AddChild(DividerOnSpeedChanges);

        // Percent-width spacer soaks up the free space so Cancel sits flush left and the
        // export actions flush right - this framework has no space-between align.
        var buttonRowSpacer = new Panel(context) { Width = LiteralOrComputable.Percent(100) };
        AddChild(new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 44,
            Spacing = 10,
            VerticalAlign = Align.Center,
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
        var row = new Panel(Context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 34
        };
        row.AddChild(new Label(Context, label)
        {
            FontSizePx = 13f,
            Y = 9,
            Color = EditorPalette.TextMuted
        });
        row.AddChild(input);
        AddChild(row);
    }
}