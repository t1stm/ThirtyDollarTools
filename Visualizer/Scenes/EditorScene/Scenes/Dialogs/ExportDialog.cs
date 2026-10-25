using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>The two things the export dialog can write.</summary>
public enum ExportFormat
{
    Tdw,
    Wav
}

/// <summary>
///     The export form (ModalLayer content): pick a format (a click, or 1 and 2), then its
///     options - the <see cref="SequenceStyle" /> cosmetics for a .tdw, what goes into the file for
///     a .wav - and one export action named after the format. Pure form - the owner handles file
///     dialogs and the actual writing. The tree is ExportDialog.snx.xml; the input bounds and the
///     format swap are here.
/// </summary>
public sealed class ExportDialog
{
    private readonly FlexPanel _options;
    private readonly FlexPanel _tdwCard;
    private readonly FlexPanel _tdwOptions;
    private readonly FlexPanel _wavCard;
    private readonly FlexPanel _wavOptions;
    private readonly Label _wavLength;
    private readonly Label _wavTracks;

    public ExportDialog(UIContext context)
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Export Dialog/ExportDialog.snx.xml");
        Element = component.GetID<FlexPanel>("export-dialog");
        ProjectLabel = component.GetID<Label>("project-label");
        DividerEveryBars = component.GetID<NumericInput>("divider-every-bars");
        MigrateToStop = component.GetID<NumericInput>("migrate-to-stop");
        DividerOnSpeedChanges = component.GetID<Checkbox>("divider-on-speed-changes");
        ExportButton = component.GetID<Button>("export-button");
        CancelButton = component.GetID<Button>("cancel-button");
        CloseButton = component.GetID<Button>("close-button");
        WaveNote = component.GetID<FlexPanel>("wave-note");
        _wavLength = component.GetID<Label>("wav-length");
        _wavTracks = component.GetID<Label>("wav-tracks");
        _options = component.GetID<FlexPanel>("export-options");
        _tdwOptions = component.GetID<FlexPanel>("tdw-options");
        _wavOptions = component.GetID<FlexPanel>("wav-options");
        _tdwCard = component.GetID<FlexPanel>("tdw-card");
        _wavCard = component.GetID<FlexPanel>("wav-card");

        DividerEveryBars.Min = 0;
        DividerEveryBars.Max = 1024;
        DividerEveryBars.Unit = "bars";

        MigrateToStop.Value = new SequenceStyle().MigrateToStop;
        MigrateToStop.Min = 1;
        MigrateToStop.Max = 4096;
        MigrateToStop.AllowNull = true;
        MigrateToStop.Unit = "pauses";

        // A checkbox builds its own label, so markup cannot put a class on it; the class and
        // the sheet are applied by hand here, after the tree's own style pass.
        DividerOnSpeedChanges.Label.Classes = ["muted-check-label"];
        DividerOnSpeedChanges.Label.ApplyStyleSheet(component.StyleSheet!);

        KeyChip.Add(CancelButton, "Esc");
        KeyChip.Add(ExportButton, "Enter");

        _tdwCard.UpdateCursorOnHover = true;
        _wavCard.UpdateCursorOnHover = true;
        _tdwCard.OnClick = _ => Format = ExportFormat.Tdw;
        _wavCard.OnClick = _ => Format = ExportFormat.Wav;
        _options.RemoveChild(_wavOptions);
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    /// <summary>Beside the title: the project being exported.</summary>
    public Label ProjectLabel { get; }

    public NumericInput DividerEveryBars { get; }
    public Checkbox DividerOnSpeedChanges { get; }
    public NumericInput MigrateToStop { get; }

    /// <summary>Exports in <see cref="Format" />; its label names it.</summary>
    public Button ExportButton { get; }

    public Button CancelButton { get; }
    public Button CloseButton { get; }

    /// <summary>
    ///     The "not exported" note for wave reference tracks. The owner removes it when the
    ///     project holds none, so it is never a line of text about something that isn't there.
    /// </summary>
    public FlexPanel WaveNote { get; }

    /// <summary>The chosen format: picks its card, shows its options and names the export action.</summary>
    public ExportFormat Format
    {
        get;
        set
        {
            if (field == value) return;
            field = value;

            var tdw = value == ExportFormat.Tdw;
            _tdwCard.SetClass("export-card-idle", !tdw);
            _tdwCard.SetClass("export-card-selected", tdw);
            _wavCard.SetClass("export-card-idle", tdw);
            _wavCard.SetClass("export-card-selected", !tdw);

            _options.RemoveChild(tdw ? _wavOptions : _tdwOptions);
            _options.AddChild(tdw ? _tdwOptions : _wavOptions);
            ExportButton.Value = tdw ? "Export .tdw…" : "Export .wav…";
        }
    } = ExportFormat.Tdw;

    /// <summary>The audio's track count, for the .wav options.</summary>
    public void SetTracks(int count)
    {
        _wavTracks.SetTextContents(count == 1 ? "1, alone in the file" : $"{count}, mixed into one file");
    }

    /// <summary>
    ///     The audio's length once measured (<see cref="EditorPlayback.ExportLengthMs" />), or null
    ///     when the measuring failed; "Measuring…" until then.
    /// </summary>
    public void SetLength(long? ms)
    {
        _wavLength.SetTextContents(ms is { } t ? $"{t / 60000}:{t / 1000 % 60:00}.{t % 1000 / 100}" : "Couldn't be measured");
    }

    /// <summary>1 and 2 pick a format. Only reaches the dialog while no field has focus.</summary>
    /// <returns>True for a key the dialog used.</returns>
    public bool HandleKey(KeyboardKeyEventArgs e)
    {
        if (e.Control || e.Alt || e.Command) return false;
        ExportFormat? format = e.Key switch
        {
            Keys.D1 or Keys.KeyPad1 => ExportFormat.Tdw,
            Keys.D2 or Keys.KeyPad2 => ExportFormat.Wav,
            _ => null
        };
        if (format is not { } picked) return false;
        Format = picked;
        return true;
    }

    /// <summary>The style the form currently describes.</summary>
    public SequenceStyle Style => new()
    {
        DividerEveryBars = (int?)DividerEveryBars.Value,
        DividerOnSpeedChanges = DividerOnSpeedChanges.Checked,
        MigrateToStop = (int?)MigrateToStop.Value
    };
}
