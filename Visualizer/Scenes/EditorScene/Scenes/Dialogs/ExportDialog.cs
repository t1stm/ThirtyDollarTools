using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The export options form (ModalLayer content): the <see cref="SequenceStyle" />
///     cosmetics plus the two export actions. Pure form - the owner handles file dialogs
///     and the actual writing. The tree is ExportDialog.snx.xml; the input bounds are set here.
/// </summary>
public sealed class ExportDialog
{
    public ExportDialog(UIContext context)
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Export Dialog/ExportDialog.snx.xml");
        Element = component.GetID<FlexPanel>("export-dialog");
        DividerEveryBars = component.GetID<NumericInput>("divider-every-bars");
        MigrateToStop = component.GetID<NumericInput>("migrate-to-stop");
        DividerOnSpeedChanges = component.GetID<Checkbox>("divider-on-speed-changes");
        TdwButton = component.GetID<Button>("tdw-button");
        WavButton = component.GetID<Button>("wav-button");
        CancelButton = component.GetID<Button>("cancel-button");

        DividerEveryBars.Min = 0;
        DividerEveryBars.Max = 1024;

        MigrateToStop.Value = new SequenceStyle().MigrateToStop;
        MigrateToStop.Min = 1;
        MigrateToStop.Max = 4096;
        MigrateToStop.AllowNull = true;

        // A checkbox builds its own label, so markup cannot put a class on it; the class and
        // the sheet are applied by hand here, after the tree's own style pass.
        DividerOnSpeedChanges.Label.Classes = ["muted-check-label"];
        DividerOnSpeedChanges.Label.ApplyStyleSheet(component.StyleSheet!);
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

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
}
