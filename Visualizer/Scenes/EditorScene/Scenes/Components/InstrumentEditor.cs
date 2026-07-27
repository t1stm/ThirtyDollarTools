using OpenTK.Mathematics;
using Shared.Atlases;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;
using ThirtyDollarParser;

namespace EditorScene.Scenes.Components;

/// <summary>
///     Create-or-edit form for one instrument: a name field and a multi-select sound picker
///     (the same picker construction as the track-automation sound filter), with per-sound
///     value/volume/pan adjustment turned on (see <see cref="SoundPicker.ShowAdjustments" />).
///     Pure form - the owner loads/commits the name and <see cref="SoundPicker.Instances" />,
///     and shows/hides the modal.
/// </summary>
public sealed class InstrumentEditor : FlexPanel
{
    // The accent blue used for selection/clips elsewhere in EditorScene (#4c6bcc) -
    // reused here so code-built buttons (which get no stylesheet) still read as buttons.
    private static readonly Vector4 ButtonColor = new(0.30f, 0.42f, 0.80f, 1f);

    public InstrumentEditor(UIContext context, AtlasStore store) : base(context)
    {
        Direction = LayoutDirection.Vertical;
        Width = 640;
        Padding = 10;
        Spacing = 8;
        Background = new ColoredPlane { Color = EditorPalette.Panel };

        PreviewButton = new Button(context, "Preview", new ColoredPlane { Color = ButtonColor })
        {
            FontSizePx = 14,
            BorderRadius = 8
        };

        NameInput = new TextInput(context, "")
        {
            Width = 300,
            FontSizePx = 15f,
            BorderRadius = 6,
            Background = new ColoredPlane { Color = EditorPalette.InputBackground }
        };
        // Percent-width spacer soaks up the free space so Preview lands flush against
        // the right edge - this framework has no space-between align.
        var nameRowSpacer = new Panel(context) { Width = LiteralOrComputable.Percent(100) };
        var nameRow = new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Spacing = 8,
            VerticalAlign = Align.Center,
            Children = [new Label(context, "Name: ") { FontSizePx = 15f }, NameInput, nameRowSpacer, PreviewButton]
        };

        SoundsPicker = new SoundPicker(context, store)
        {
            Width = LiteralOrComputable.Percent(100),
            MultiSelect = true,
            ShowAdjustments = true
        };
        var soundsList = new ScrollView(context) { Width = LiteralOrComputable.Percent(100), Height = 380 };
        soundsList.AddChild(SoundsPicker);

        DoneButton = new Button(context, "Done", new ColoredPlane { Color = ButtonColor })
        {
            FontSizePx = 14,
            BorderRadius = 8
        };
        var doneRow = new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            HorizontalAlign = Align.End,
            Children = [DoneButton]
        };

        AddChild(nameRow);
        AddChild(soundsList);
        AddChild(doneRow);
    }

    public TextInput NameInput { get; }
    public Button PreviewButton { get; }
    public SoundPicker SoundsPicker { get; }
    public Button DoneButton { get; }

    /// <summary>Fills the sound grid on first use - lazily, same guard as the other pickers.</summary>
    public void EnsureSounds(IEnumerable<Sound> sounds)
    {
        if (SoundsPicker.HasSounds) return;
        SoundsPicker.Fill(sounds);
    }

    /// <summary>Pre-loads the form; call with an empty name/selection to start a fresh instrument.</summary>
    public void Load(string name, IEnumerable<InstrumentSound> sounds)
    {
        NameInput.Value = name;
        SoundsPicker.SetInstances(sounds);
    }
}
