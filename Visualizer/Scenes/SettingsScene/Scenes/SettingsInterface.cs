using System.Reflection;
using JetBrains.Annotations;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using Sundex.Markup.Attributes;
using VisualizerScene.Settings;
using CultureInfo = System.Globalization.CultureInfo;

namespace SettingsScene.Scenes;

/// <summary>
///     One row of the settings screen. The control is chosen from the property's type;
///     <see cref="Min" />, <see cref="Max" /> and <see cref="Step" /> are only read for the
///     numeric ones.
/// </summary>
public sealed record SettingRow(
    string Property,
    string Name,
    string Description,
    double Min = 0,
    double Max = 1,
    double Step = 1);

public class SettingsInterface
{
    /// <summary>
    ///     What the screen shows, in the order it shows it. Written out rather than
    ///     reflected over <see cref="VisualizerSettings" /> so each setting can carry a name
    ///     a reader recognises and a line saying what it does - "EventMargin" on its own is
    ///     the field name, not an explanation - and so state that isn't a setting stays off
    ///     the screen. A new property is a deliberate addition here; the test suite fails
    ///     until it is either listed or named in <see cref="HiddenProperties" />.
    /// </summary>
    public static readonly (string Title, SettingRow[] Rows)[] Sections =
    [
        ("P L A Y F I E L D", [
            new SettingRow(nameof(VisualizerSettings.EventSize), "Event size",
                "Pixels across, per emoji.", 16, 256),
            new SettingRow(nameof(VisualizerSettings.EventMargin), "Event margin",
                "Gap between emoji.", 0, 128),
            new SettingRow(nameof(VisualizerSettings.LineAmount), "Line amount",
                "Emoji per line before it wraps.", 1, 64),
            new SettingRow(nameof(VisualizerSettings.ScrollSpeed), "Scroll speed",
                "How fast the playfield moves while a cover plays.", 0.5, 100, 0.5),
            new SettingRow(nameof(VisualizerSettings.Greeting), "Greeting",
                "Text shown above the playfield.")
        ]),
        ("W I N D O W   &   A U D I O", [
            new SettingRow(nameof(VisualizerSettings.AutomaticScaling), "Automatic scaling",
                "Size the playfield to the window."),
            new SettingRow(nameof(VisualizerSettings.UseVsync), "Use VSync",
                "Match the frame rate to your display."),
            new SettingRow(nameof(VisualizerSettings.TransparentFramebuffer), "Transparent framebuffer",
                "Let the desktop show through the window.\nTakes effect on the next launch."),
            new SettingRow(nameof(VisualizerSettings.AudioBackend), "Audio backend",
                "Leave empty to pick one automatically.")
        ]),
        ("U P D A T E S", [
            new SettingRow(nameof(VisualizerSettings.CheckForUpdates), "Check for updates",
                "Look for a new release on startup."),
            new SettingRow(nameof(VisualizerSettings.UpdateIncludePrereleases), "Include prereleases",
                "Offer release candidates as well."),
            new SettingRow(nameof(VisualizerSettings.UpdateIncludeNightlies), "Include nightlies",
                "Offer nightly builds too. Expect breakage.")
        ])
    ];

    /// <summary>The two shortcut sections, in the order they appear under the settings above.</summary>
    public static readonly (string Title, BindScene Scene)[] KeybindSections =
    [
        ("V I S U A L I Z E R   S H O R T C U T S", BindScene.Visualizer),
        ("E D I T O R   S H O R T C U T S", BindScene.Editor)
    ];

    /// <summary>
    ///     Properties of <see cref="VisualizerSettings" /> that are state rather than
    ///     settings, and so have no row: the loader flips UpdateCheckAsked itself once it
    ///     has asked about update checking, and nobody would know what to do with it, and
    ///     Keybinds is one opaque string that the shortcut sections below present properly.
    /// </summary>
    public static readonly string[] HiddenProperties =
        [nameof(VisualizerSettings.UpdateCheckAsked), nameof(VisualizerSettings.Keybinds)];

    /// <summary>The tile hues of the preview line, cycled across it.</summary>
    private static readonly string[] TileHues = ["tile-blue", "tile-orange", "tile-green"];

    private readonly VisualizerSettings _settings;

    private bool _stripDirty;

    public SettingsInterface(UIContext context, VisualizerSettings settings, Action back)
    {
        UI = context;
        OnBack = back;
        _settings = settings;

        var sundexContext = new SundexContext(context);
        var componentSource = context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo { Location = "Scenes/Layout/SettingsInterface.snx.xml" }
        });

        Component = sundexContext.NewComponent(componentSource.Value);
        sundexContext.RunLogicAndVerify(Component, () => RootPanel, () => SettingsList, () => Strip);

        foreach (var (title, rows) in Sections)
            SettingsList.AddChild(BuildSection(title, rows));

        foreach (var (title, scene) in KeybindSections)
            SettingsList.AddChild(BuildKeybindSection(title, scene));

        // Nothing about the tiles depends on a measured width any more, so the preview is
        // right on the first frame rather than appearing after one.
        RebuildStrip();

        RootPanel.DrawTo(context);
    }

    /// <summary>The context the rows and preview tiles are built against.</summary>
    public UIContext UI { get; }

    public Action OnBack { get; }
    [UsedImplicitly] public SundexComponent Component { get; }
    [SetFromLogic] public Panel RootPanel { get; set; } = null!;
    [SetFromLogic] public ScrollView SettingsList { get; set; } = null!;
    [SetFromLogic] public ScrollView StripView { get; set; } = null!;
    [SetFromLogic] public FlexPanel Strip { get; set; } = null!;

    public void Resize()
    {
        RootPanel.InvalidateCoordinates();
        RootPanel.Layout();
    }

    public void Update(UIContext context)
    {
        // Before the layout pass: a slider moved since the last frame changes how many
        // tiles there are and how big they are, and the flex wraps them from there.
        if (_stripDirty) RebuildStrip();

        RootPanel.Update(context);
        RootPanel.Layout();
    }

    public void MouseEvent(MouseState mouseState, Vector2 scale)
    {
        RootPanel.Test(mouseState, scale);
    }

    /// <summary>
    ///     Redraws the preview from the three geometry settings: LineAmount tiles, each
    ///     EventSize across, EventMargin apart. Everything is the playfield's own pixel
    ///     size - a line too wide for the bed wraps, the way the playfield itself wraps,
    ///     and the bed's auto height plus the list's scrolling absorb the rest. Nothing
    ///     here is scaled to fit, so raising the event size raises what you see by the
    ///     same amount.
    /// </summary>
    private void RebuildStrip()
    {
        _stripDirty = false;

        var count = Math.Clamp(_settings.LineAmount, 1, 512);
        var tile = Math.Max(1f, _settings.EventSize);

        while (Strip.Children.Count > count) Strip.RemoveChild(Strip.Children[^1]);
        while (Strip.Children.Count < count)
            Strip.AddChild(new Panel(UI)
            {
                Classes = ["tile", TileHues[Strip.Children.Count % TileHues.Length]]
            });

        // One gap value for both axes: the wrapped rows sit the same distance apart as
        // the tiles in a row, which is what the margin means on the playfield too.
        Strip.Spacing = Math.Max(0f, _settings.EventMargin);
        foreach (var child in Strip.Children)
        {
            child.Width = tile;
            child.Height = tile;
        }
    }

    private FlexPanel BuildSection(string title, SettingRow[] rows)
    {
        var header = new FlexPanel(UI)
        {
            Classes = ["section-header"],
            Children =
            [
                new Label(UI, title) { Classes = ["section-title"] },
                new Panel(UI) { Classes = ["rule"] }
            ]
        };

        return new FlexPanel(UI)
        {
            Classes = ["section"],
            Children = [header, .. rows.Select(BuildRow)]
        };
    }

    /// <summary>
    ///     The shortcuts, which don't fit <see cref="SettingRow" />: their backing store is one
    ///     opaque string, so the rows are walked off the bind table instead of reflected off a
    ///     property. Everything but the button itself reuses the classes above, so the two
    ///     kinds of section read as one page.
    /// </summary>
    private FlexPanel BuildKeybindSection(string title, BindScene scene)
    {
        var header = new FlexPanel(UI)
        {
            Classes = ["section-header"],
            Children =
            [
                new Label(UI, title) { Classes = ["section-title"] },
                new Panel(UI) { Classes = ["rule"] }
            ]
        };

        // A capture UI without a way out can genuinely strand someone who binds Escape to
        // something; this is that way out.
        var reset = new Button(UI, "Reset shortcuts")
        {
            Classes = ["reset-shortcuts"],
            OnClick = _ => Keybinds.ResetToDefaults(scene)
        };

        return new FlexPanel(UI)
        {
            Classes = ["section"],
            Children =
            [
                header,
                .. Keybinds.All.Where(info => info.Scene == scene).Select(BuildKeybindRow),
                new FlexPanel(UI) { Classes = ["row"], Children = [reset] }
            ]
        };
    }

    private FlexPanel BuildKeybindRow(BindInfo info)
    {
        var text = new FlexPanel(UI)
        {
            Classes = ["row-text"],
            Children =
            [
                new Label(UI, info.Name) { Classes = ["setting-name"] },
                new Label(UI, info.Description) { Classes = ["setting-desc"] }
            ]
        };

        return new FlexPanel(UI)
        {
            Classes = ["row"],
            Children =
            [
                text,
                new FlexPanel(UI) { Classes = ["control"], Children = [new KeybindButton(UI, info)] }
            ]
        };
    }

    private FlexPanel BuildRow(SettingRow row)
    {
        var property = typeof(VisualizerSettings).GetProperty(row.Property)
                       ?? throw new InvalidOperationException($"No setting named \"{row.Property}\"");

        var text = new FlexPanel(UI)
        {
            Classes = ["row-text"],
            Children =
            [
                new Label(UI, row.Name) { Classes = ["setting-name"] },
                new Label(UI, row.Description) { Classes = ["setting-desc"] }
            ]
        };

        return new FlexPanel(UI)
        {
            Classes = ["row"],
            Children = [text, BuildControl(row, property)]
        };
    }

    private FlexPanel BuildControl(SettingRow row, PropertyInfo property)
    {
        var isBool = property.PropertyType == typeof(bool);
        var isInt = property.PropertyType == typeof(int);
        var isFloat = property.PropertyType == typeof(float);

        return new FlexPanel(UI)
        {
            Classes = ["control"],
            Children = isBool
                ? [BuildCheckbox(property)]
                : isInt || isFloat
                    ? BuildSlider(row, property, isInt)
                    : [BuildTextInput(property)]
        };
    }

    private Checkbox BuildCheckbox(PropertyInfo property)
    {
        // No label on the box: the row's name column already names it, and a second copy
        // beside the tick would be the same word twice.
        var box = new Checkbox(UI, "", (bool)(property.GetValue(_settings) ?? false))
        {
            OnCheckedChanged = b => property.SetValue(_settings, b.Checked)
        };

        Bind(property, () => box.Checked = (bool)(property.GetValue(_settings) ?? false));
        return box;
    }

    /// <summary>
    ///     Writes the setting back into its control whenever anything changes it, so a screen
    ///     built before the value moved - this one is built during the boot, before the first
    ///     run's setup has been answered - shows what the object actually holds rather than
    ///     what it held at construction. Writing a control's own value back into it is a
    ///     no-op: all three of them drop a set that doesn't change anything, so this can't
    ///     bounce between the control and the setting.
    /// </summary>
    private void Bind(PropertyInfo property, Action refresh)
    {
        _settings.Changed += name =>
        {
            if (name == property.Name) refresh();
        };
    }

    private List<UIElement> BuildSlider(SettingRow row, PropertyInfo property, bool isInt)
    {
        var current = Convert.ToDouble(property.GetValue(_settings));
        var readout = new Label(UI, Format(current)) { Classes = ["value-label"] };

        var slider = new Slider(UI)
        {
            Classes = ["setting-slider"],
            Min = row.Min,
            Max = row.Max,
            Step = row.Step,
            Value = current,
            OnValueChanged = s =>
            {
                // The (object) cast is load-bearing: without it the ternary unifies to float
                // and SetValue throws on the int properties.
                property.SetValue(_settings, isInt ? (object)(int)Math.Round(s.Value) : (float)s.Value);
                readout.SetTextContents(Format(s.Value));
                _stripDirty = true;
            }
        };

        Bind(property, () =>
        {
            slider.Value = Convert.ToDouble(property.GetValue(_settings));
            readout.SetTextContents(Format(slider.Value));
            _stripDirty = true;
        });

        return [slider, new FlexPanel(UI) { Classes = ["value"], Children = [readout] }];

        string Format(double value)
        {
            return value.ToString(isInt ? "0" : "0.##", CultureInfo.InvariantCulture);
        }
    }

    private TextInput BuildTextInput(PropertyInfo property)
    {
        var input = new TextInput(UI, property.GetValue(_settings) as string ?? "")
        {
            Width = 380f,
            FontSizePx = 14f
        };

        // Clearing a nullable string setting means "unset", not "empty string" - AudioBackend
        // picks its default from null, and "" would pin it to a backend that doesn't exist.
        var nullable = new NullabilityInfoContext().Create(property).WriteState == NullabilityState.Nullable;
        input.OnValueChanged = i => property.SetValue(_settings, nullable && i.Value.Length == 0 ? null : i.Value);

        Bind(property, () => input.Value = property.GetValue(_settings) as string ?? "");
        return input;
    }
}
