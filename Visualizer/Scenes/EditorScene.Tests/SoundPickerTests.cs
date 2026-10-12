using Serilog;
using Shared.Atlases;
using Sundex.Engine.Asset_Management;
using ThirtyDollarConverter.Parser;
using EditorScene.Scenes.Dialogs;

namespace EditorScene.Tests;

/// <summary>
///     The atlas store has no images in tests, so no icon is ever built; what the grid offers
///     is read from <see cref="SoundPicker.AvailableSounds" />, which the icons are built from.
/// </summary>
public class SoundPickerTests
{
    private static SoundPicker NewPicker(EditorTestContext ctx, bool availableOnly = false)
    {
        var atlases = new AtlasStore((AssetProvider)ctx.AssetProvider, new LoggerConfiguration().CreateLogger());
        var picker = new SoundPicker(ctx, atlases) { MultiSelect = true, AvailableOnly = availableOnly };
        picker.Fill([
            Sound("kick", "Kick drum", "Generic", "percussion"),
            Sound("clap"),
            Sound("noteblock_harp", "Harp", "Minecraft", "note"),
            Sound("noteblock_bell", null, "Minecraft", "note")
        ]);
        return picker;
    }

    private static Sound Sound(string id, string? name = null, string? source = null, params string[] tags) =>
        new() { Id = id, Name = name, Source = source, Tags = tags };

    [Fact]
    public void AddInstance_Duplicating_CopiesTheTuningAndLandsRightAfterTheSource()
    {
        var picker = NewPicker(new EditorTestContext());
        picker.SetSelected(["kick", "clap"]);
        picker.Instances[0].Value = -5;

        var duplicate = picker.AddInstance("kick", picker.Instances[0]);

        Assert.Equal(["kick", "kick", "clap"], picker.Instances.Select(instance => instance.Sound));
        Assert.Equal(-5, duplicate.Value);
        Assert.NotSame(picker.Instances[0], duplicate);
        Assert.Equal(["kick", "clap"], picker.Selected); // names dedupe
    }

    [Fact]
    public void RemoveInstance_DropsOnlyThatCopy()
    {
        var picker = NewPicker(new EditorTestContext());
        picker.SetSelected(["kick"]);
        var duplicate = picker.AddInstance("kick", picker.Instances[0]);
        duplicate.Value = -12;

        picker.RemoveInstance(duplicate);

        var remaining = Assert.Single(picker.Instances);
        Assert.Equal("kick", remaining.Sound);
        Assert.Equal(0, remaining.Value);
    }

    [Fact]
    public void AvailableOnly_OffersEverySoundNotPickedYet()
    {
        var picker = NewPicker(new EditorTestContext(), true);

        picker.SetSelected(["clap"]);

        Assert.Equal(["kick", "noteblock_harp", "noteblock_bell"], picker.AvailableSounds);
        Assert.Empty(picker.Children); // no Selected section, and no icons without atlases
    }

    [Theory]
    [InlineData("noteblock", new[] { "noteblock_harp", "noteblock_bell" })]
    [InlineData("  BELL ", new[] { "noteblock_bell" })] // trimmed, any case
    [InlineData("", new[] { "kick", "noteblock_harp", "noteblock_bell" })]
    [InlineData("zzz", new string[0])]
    [InlineData("percussion", new[] { "kick" })] // a tag
    [InlineData("minecraft", new[] { "noteblock_harp", "noteblock_bell" })] // the source
    [InlineData("drum", new[] { "kick" })] // the display name, not in the ID
    public void Filter_HidesSoundsWithNoMatchingText(string filter, string[] expected)
    {
        var picker = NewPicker(new EditorTestContext(), true);
        picker.SetSelected(["clap"]);

        picker.Filter(filter);

        Assert.Equal(expected, picker.AvailableSounds);
    }
}
