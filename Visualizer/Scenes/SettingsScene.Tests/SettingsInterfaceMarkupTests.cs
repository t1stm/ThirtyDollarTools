using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using SettingsScene.Scenes;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Tests;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using VisualizerScene.Settings;

// UIContext stores its providers in static fields (see Sundex.Components.Tests),
// so this suite must also run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SettingsScene.Tests;

/// <summary>Headless UIContext over the SettingsScene assembly's embedded assets.</summary>
public class SettingsTestContext : UIContext
{
    [SetsRequiredMembers]
    public SettingsTestContext()
    {
        InjectForTesting(
            new AssetProvider(new LoggerConfiguration().CreateLogger(),
                [typeof(SettingsInterface).Assembly, Assembly.GetExecutingAssembly()], new GLInfo()),
            new MockFontProvider(),
            new MockTextProvider());
        Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1600, 900));
    }
}

/// <summary>
///     Headless checks for the settings screen. The app is a GLFW window that can't be
///     screenshotted in this environment, so a stylesheet typo, a renamed id, or a setting
///     that quietly stopped appearing would otherwise only surface on launch.
/// </summary>
public class SettingsInterfaceMarkupTests
{
    private readonly SettingsTestContext _context = new();

    private string LoadMarkup()
    {
        return _context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = "Scenes/Layout/SettingsInterface.snx.xml",
                Storage = StorageLocation.Assembly
            }
        }).Value;
    }

    /// <summary>Every id the logic block reaches for, plus the click target.</summary>
    [Theory]
    [InlineData("stage")]
    [InlineData("settings-list")]
    [InlineData("strip-view")]
    [InlineData("strip")]
    [InlineData("back-button")]
    public void SettingsMarkup_RegistersEveryWiredId(string id)
    {
        var component = new SundexContext(_context).NewComponent(LoadMarkup());
        Assert.True(component.RegisteredIDs.ContainsKey(id), $"markup lost id \"{id}\"");
    }

    /// <summary>
    ///     The classes SettingsInterface applies by name while building the sections, rows
    ///     and preview tiles. A class no rule defines is silent: the element keeps its bare
    ///     framework defaults and renders wrong.
    /// </summary>
    [Theory]
    [InlineData("section")]
    [InlineData("section-header")]
    [InlineData("section-title")]
    [InlineData("rule")]
    [InlineData("row")]
    [InlineData("row-text")]
    [InlineData("setting-name")]
    [InlineData("setting-desc")]
    [InlineData("control")]
    [InlineData("value")]
    [InlineData("value-label")]
    [InlineData("setting-slider")]
    [InlineData("choice-button")]
    [InlineData("tile")]
    [InlineData("tile-blue")]
    [InlineData("tile-orange")]
    [InlineData("tile-green")]
    [InlineData("keybind-button")]
    [InlineData("keybind-button-capturing")]
    [InlineData("keybind-button-conflict")]
    [InlineData("reset-shortcuts")]
    public void StylesheetDefines_TheClassesSetFromCode(string cls)
    {
        var component = new SundexContext(_context).NewComponent(LoadMarkup());
        Assert.True(component.StyleSheet?.Classes.ContainsKey(cls), $"stylesheet lost class \"{cls}\"");
    }

    /// <summary>
    ///     The screen lists its settings by hand, so a property added to VisualizerSettings
    ///     would otherwise just never show up. Adding one means giving it a row - or saying
    ///     here that it is state, not a setting.
    /// </summary>
    [Fact]
    public void EverySetting_HasARowOrIsExplicitlyHidden()
    {
        var shown = SettingsInterface.Sections
            .SelectMany(section => section.Rows)
            .Select(row => row.Property)
            .Concat(SettingsInterface.HiddenProperties)
            .ToHashSet();

        var missing = typeof(VisualizerSettings).GetProperties()
            .Select(property => property.Name)
            .Where(name => !shown.Contains(name))
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    ///     Same reason as the settings above: the shortcut rows are walked off a hand-written
    ///     table, so an action added to it would otherwise just never appear on the screen.
    /// </summary>
    [Fact]
    public void EveryBind_HasARow()
    {
        var ui = NewInterface(new VisualizerSettings());
        var shown = Descendants(ui.RootPanel).OfType<KeybindButton>().Select(button => button.Id).ToHashSet();

        Assert.All(Enum.GetValues<Bind>(), bind => Assert.Contains(bind, shown));

        // The rows are built in code and only meet the sheet when they are parented; the
        // class has to beat the sheet's generic `component button` rule or every binding
        // sits in a 104px box the combos don't fit.
        Assert.All(Descendants(ui.RootPanel).OfType<KeybindButton>(),
            button => Assert.Equal(240, button.Computed.Width, 0.5f));
    }

    /// <summary>
    ///     A rebind made anywhere else - the other row on this screen, or the Reset shortcuts
    ///     button - reaches every button rather than only the one that was clicked. Without
    ///     the Keybinds.Changed subscription, Reset shortcuts visibly does nothing until the
    ///     scene is rebuilt, and the screen is built once during the boot preload.
    /// </summary>
    [Fact]
    public void RebindingElsewhere_UpdatesTheButtons()
    {
        Keybinds.Attach(new VisualizerSettings());
        var ui = NewInterface(new VisualizerSettings());
        var buttons = Descendants(ui.RootPanel).OfType<KeybindButton>().ToArray();
        var undo = buttons.Single(button => button.Id == Bind.EditorUndo);

        Keybinds.Rebind(Bind.EditorUndo, new Keybind(Keys.F1, 0));

        ui.Update(_context);
        ui.Update(_context);
        // Label keeps a fixed-length buffer and pads a shorter replacement with NULs.
        Assert.Equal("F1", Text(undo));

        Keybinds.ResetToDefaults(BindScene.Editor);

        ui.Update(_context);
        ui.Update(_context);
        Assert.All(buttons.Where(button => Keybinds.Info(button.Id).Scene == BindScene.Editor),
            button => Assert.Equal(Keybinds.Info(button.Id).Default.ToString(), Text(button)));
    }

    /// <summary>
    ///     The whole screen, wired the way the scene wires it: the preview is the point of
    ///     the thing, and what it draws is what the visualizer will draw.
    /// </summary>
    [Fact]
    public void PreviewLine_DrawsTheDefaultsAtTheirRealSize()
    {
        var settings = new VisualizerSettings();
        var ui = NewInterface(settings);

        Assert.Equal(settings.LineAmount, ui.Strip.Children.Count);
        Assert.Equal(settings.EventSize, ui.Strip.Children[0].Computed.Width, 0.5f);
        Assert.Equal(settings.EventMargin, ui.Strip.Spacing, 0.5f);
    }

    /// <summary>
    ///     A line too wide for the bed wraps onto further rows at full size instead of being
    ///     scaled down to fit, so raising the event size visibly raises the tile size rather
    ///     than being absorbed by the shrink.
    /// </summary>
    [Fact]
    public void PreviewLine_WrapsInsteadOfShrinkingWhenItDoesNotFit()
    {
        var settings = new VisualizerSettings() { LineAmount = 64, EventSize = 256 };
        var ui = NewInterface(settings);

        Assert.Equal(64, ui.Strip.Children.Count);
        Assert.All(ui.Strip.Children, tile => Assert.Equal(256, tile.Computed.Width, 0.5f));

        // The line is far wider than the bed, so it has to have wrapped: the bed is taller
        // than one tile and no tile hangs off the right edge.
        Assert.True(ui.Strip.Computed.Height > 256 + 2 * ui.Strip.Padding,
            $"the bed is {ui.Strip.Computed.Height}, expected several rows");
        Assert.All(ui.Strip.Children, tile =>
            Assert.True(tile.Computed.AbsoluteX + 256 <= ui.Strip.Computed.AbsoluteX + ui.Strip.Computed.Width + 0.5f,
                "a tile overflows the bed to the right"));
    }

    /// <summary>
    ///     Every setting reaches the preview at its own scale: the tiles are the event size,
    ///     the gaps are the event margin, and there is one tile per event on a line.
    /// </summary>
    [Theory]
    [InlineData(16, 4, 3)]
    [InlineData(64, 12, 16)]
    [InlineData(256, 128, 1)]
    public void PreviewLine_DrawsEverySettingAtItsRealSize(int eventSize, int margin, int lineAmount)
    {
        var settings = new VisualizerSettings()
            { EventSize = eventSize, EventMargin = margin, LineAmount = lineAmount };
        var ui = NewInterface(settings);

        Assert.Equal(lineAmount, ui.Strip.Children.Count);
        Assert.Equal(eventSize, ui.Strip.Children[0].Computed.Width, 0.5f);
        Assert.Equal(eventSize, ui.Strip.Children[0].Computed.Height, 0.5f);
        Assert.Equal(margin, ui.Strip.Spacing, 0.5f);
    }

    /// <summary>
    ///     The band is the same height whatever the settings are: sizing it to the tiles would
    ///     shift the whole page under the pointer while a slider is being dragged.
    /// </summary>
    [Fact]
    public void PreviewBand_KeepsItsHeightWhateverTheSettingsAre()
    {
        var smallest = NewInterface(new VisualizerSettings() { LineAmount = 1, EventSize = 16 });
        var largest = NewInterface(new VisualizerSettings() { LineAmount = 64, EventSize = 256 });

        Assert.Equal(300, smallest.StripView.Computed.Height, 0.5f);
        Assert.Equal(300, largest.StripView.Computed.Height, 0.5f);

        // And what doesn't fit that height is reachable rather than clipped away.
        Assert.True(largest.Strip.Computed.Height > 300, "the line fits, so this asserts nothing");
        Assert.True(largest.StripView.MaxScroll > 0, "the band has nothing to scroll");
        Assert.Equal(0, smallest.StripView.MaxScroll);
    }

    /// <summary>
    ///     Moving a slider doesn't move anything under it: the rows below the band stay put
    ///     while the tiles inside it change size.
    /// </summary>
    [Fact]
    public void MovingASlider_DoesNotShiftTheRowsBelowThePreview()
    {
        var ui = NewInterface(new VisualizerSettings());
        var eventSize = Descendants(ui.RootPanel).OfType<Slider>().Single(slider => slider.Max == 256);
        var before = eventSize.Computed.AbsoluteY;

        eventSize.Value = 256;
        ui.Update(_context);
        ui.Update(_context);

        Assert.Equal(256, ui.Strip.Children[0].Computed.Width, 0.5f);
        Assert.Equal(before, eventSize.Computed.AbsoluteY, 0.5f);
    }

    /// <summary>
    ///     Dragging the line amount slider writes the setting and reshapes the preview -
    ///     the connection the screen is built around.
    /// </summary>
    [Fact]
    public void MovingASlider_WritesTheSettingAndReshapesThePreview()
    {
        var settings = new VisualizerSettings();
        var ui = NewInterface(settings);

        // Picked by range rather than position: LineAmount is the only setting that tops out at 64.
        var lineAmount = Descendants(ui.RootPanel).OfType<Slider>().Single(slider => slider.Max == 64);

        // The rows are built in code and only meet the sheet when they are parented; an
        // unstyled slider is a 0x0 element you can neither see nor grab.
        Assert.Equal(240, lineAmount.Computed.Width, 0.5f);

        lineAmount.Value = 8;

        ui.Update(_context);
        ui.Update(_context);

        Assert.Equal(8, settings.LineAmount);
        Assert.Equal(8, ui.Strip.Children.Count);
    }

    /// <summary>
    ///     A setting written somewhere else - the first run's setup answers the update
    ///     question after this screen has been built - shows up on the control rather than
    ///     leaving it on the value it was built with.
    /// </summary>
    [Fact]
    public void ChangingASettingElsewhere_UpdatesTheControls()
    {
        // Both defaulted-on bools off to start with, so every box starts unticked.
        var settings = new VisualizerSettings { AutomaticScaling = false, UseVsync = false };
        var ui = NewInterface(settings);

        var checkboxes = Descendants(ui.RootPanel).OfType<Checkbox>().ToArray();
        var lineAmount = Descendants(ui.RootPanel).OfType<Slider>().Single(slider => slider.Max == 64);
        var greeting = Descendants(ui.RootPanel).OfType<TextInput>().First();

        Assert.All(checkboxes, box => Assert.False(box.Checked));

        // Every bool at once: which box belongs to which setting is the screen's own order,
        // and this is about the values arriving at all.
        settings.CheckForUpdates = true;
        settings.UpdateIncludePrereleases = true;
        settings.UpdateIncludeNightlies = true;
        settings.AutomaticScaling = true;
        settings.UseVsync = true;
        settings.TransparentFramebuffer = true;
        settings.LineAmount = 8;
        settings.Greeting = "HELLO";

        ui.Update(_context);
        ui.Update(_context);

        Assert.All(checkboxes, box => Assert.True(box.Checked));
        Assert.Equal(8, lineAmount.Value);
        Assert.Equal("HELLO", greeting.Value);
        Assert.Equal(8, ui.Strip.Children.Count);

        // The write-back doesn't undo the setting: the control writing its own value into
        // the setting it just came from is what a bounce between the two would look like.
        Assert.True(settings.CheckForUpdates);
        Assert.Equal(8, settings.LineAmount);
    }

    /// <summary>
    ///     Descriptions fit the 280px text column they are written into. Labels don't wrap,
    ///     so a line too long for the column keeps going and slides under the control on its
    ///     right - invisible in code, obvious on screen. Counted in characters rather than
    ///     measured, because the headless font provider is a mock and its advance widths are
    ///     not the real font's. 49 is the longest line that renders correctly today (the
    ///     scroll-speed row); break a longer one over two with \n, as the
    ///     transparent-framebuffer row does.
    /// </summary>
    [Fact]
    public void EveryDescription_FitsItsColumn()
    {
        var lines = SettingsInterface.Sections.SelectMany(section => section.Rows)
            .Select(row => (Name: row.Property, row.Description))
            .Concat(Keybinds.All.Select(info => (Name: info.Id.ToString(), info.Description)))
            .SelectMany(entry => entry.Description.Split('\n').Select(line => (entry.Name, Line: line)))
            .ToArray();

        Assert.All(lines, entry =>
            Assert.True(entry.Line.Length <= 49,
                $"{entry.Name}'s \"{entry.Line}\" is {entry.Line.Length} characters, the column fits 49"));
    }

    /// <summary>
    ///     The capture button's branches, driven headlessly: what commits, what is ignored,
    ///     and the two ways out. A capture UI that can be talked into an unusable state is
    ///     how someone loses their shortcuts for good.
    /// </summary>
    [Fact]
    public void KeybindButton_CapturesRefusesAndResets()
    {
        Keybinds.Attach(new VisualizerSettings());
        var ui = NewInterface(new VisualizerSettings());
        var undo = Descendants(ui.RootPanel).OfType<KeybindButton>().Single(b => b.Id == Bind.EditorUndo);

        _context.Focus(undo);
        Assert.Equal("Press a key...", Text(undo));

        // Holding Ctrl before the real key must not commit "Ctrl" on its own.
        Assert.True(undo.HandleKeyDown(Key(Keys.LeftControl, KeyModifiers.Control)));
        Assert.Equal(Keybinds.Info(Bind.EditorUndo).Default, Keybinds.Get(Bind.EditorUndo));

        // Refuse, don't steal: Ctrl+C is Copy's, and Undo doesn't take it.
        Assert.True(undo.HandleKeyDown(Key(Keys.C, Keybinds.Primary)));
        Assert.Equal(Keybinds.Info(Bind.EditorUndo).Default, Keybinds.Get(Bind.EditorUndo));
        Assert.Contains("Already used by", Text(undo));

        Assert.True(undo.HandleKeyDown(Key(Keys.F1, 0)));
        Assert.Equal(new Keybind(Keys.F1, 0), Keybinds.Get(Bind.EditorUndo));
        Assert.Equal("F1", Text(undo));

        // Delete is itself a bind, so it resets rather than being captured - otherwise a bad
        // binding has no way out.
        _context.Focus(undo);
        Assert.True(undo.HandleKeyDown(Key(Keys.Delete, 0)));
        Assert.Equal(Keybinds.Info(Bind.EditorUndo).Default, Keybinds.Get(Bind.EditorUndo));

        // Escape is left unhandled, which is what UIContext blurs on.
        _context.Focus(undo);
        Assert.False(undo.HandleKeyDown(Key(Keys.Escape, 0)));
    }

    /// <summary>
    ///     The picker's menu offers every resampler, picking one sticks, and each name the
    ///     list offers builds a different resampler. A name that fell out of
    ///     <see cref="Resamplers.Create" />'s switch would silently land on the default and
    ///     the setting would look like it did nothing.
    /// </summary>
    [Fact]
    public void ResamplerPicker_OffersEveryNameAndEachOneBuilds()
    {
        var settings = new VisualizerSettings();
        var ui = NewInterface(settings);
        var picker = Descendants(ui.RootPanel).OfType<Button>()
            .Single(button => button.Classes.Contains("choice-button"));

        Assert.Equal(Resamplers.Hermite, settings.Resampler);

        var built = new List<Type>();
        foreach (var expected in Resamplers.Names)
        {
            picker.OnClick?.Invoke(picker);
            var menu = ui.RootPanel.Children.OfType<DropdownMenu>().Single();
            var item = menu.Menu.Children.OfType<DropdownItem>()
                .Single(i => i.Value.ToString().TrimEnd('\0') == expected);

            item.OnClick?.Invoke(item);
            ui.Update(_context);

            Assert.Equal(expected, settings.Resampler);
            Assert.Equal(expected, picker.Value.ToString().TrimEnd('\0'));
            // Picking closes the menu, so the next click opens a fresh one rather than
            // stacking a second layer on the root.
            Assert.Empty(ui.RootPanel.Children.OfType<DropdownMenu>());

            built.Add(Resamplers.Create(settings).GetType());
        }

        // No two names share a resampler - the default fallback would show up as a duplicate.
        Assert.Equal(Resamplers.Names.Length, built.Distinct().Count());
    }

    /// <summary>The sinc parameters reach the resampler rather than only the settings file.</summary>
    [Fact]
    public void ResamplerParameters_ReachTheBuiltResampler()
    {
        var settings = new VisualizerSettings { Resampler = Resamplers.SincHann };
        var ui = NewInterface(settings);

        // Picked by range, as the sliders above are: nothing else tops out at 2048 or 192.
        Descendants(ui.RootPanel).OfType<Slider>().Single(slider => slider.Max == 192).Value = 16;
        Descendants(ui.RootPanel).OfType<Slider>().Single(slider => slider.Max == 2048).Value = 128;
        ui.Update(_context);

        Assert.Equal(16, settings.SincFilterSize);
        Assert.Equal(128, settings.SincPrecision);

        // The parameters are private to the resampler, so this asserts on what they change:
        // a 16-tap filter and a 64-tap one don't resample the same ramp identically.
        var ramp = Enumerable.Range(0, 512).Select(i => (float)Math.Sin(i * 0.1)).ToArray();
        var narrow = Resamplers.Create(settings).Resample(ramp, 48000, 44100);

        settings.SincFilterSize = 64;
        settings.SincPrecision = 512;
        var wide = Resamplers.Create(settings).Resample(ramp, 48000, 44100);

        Assert.NotEqual(narrow, wide);
    }

    private static KeyboardKeyEventArgs Key(Keys key, KeyModifiers modifiers)
    {
        return new KeyboardKeyEventArgs(key, 0, modifiers, false);
    }

    private static string Text(KeybindButton button)
    {
        return button.Value.ToString().TrimEnd('\0');
    }

    /// <summary>Two passes: the first lays the tree out, the second places the tiles it built.</summary>
    private SettingsInterface NewInterface(VisualizerSettings settings)
    {
        var ui = new SettingsInterface(_context, settings, () => { });
        ui.Update(_context);
        ui.Update(_context);

        Assert.True(ui.Strip.Computed.Width > 0, "the preview bed resolved to no width");
        return ui;
    }

    private static IEnumerable<UIElement> Descendants(UIElement root)
    {
        if (root is not Panel panel) yield break;
        foreach (var child in panel.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
