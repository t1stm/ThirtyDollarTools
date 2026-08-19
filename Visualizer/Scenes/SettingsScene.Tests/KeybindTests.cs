using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using VisualizerScene.Settings;

namespace SettingsScene.Tests;

/// <summary>
///     The shortcut table is a static registry parsed out of one settings string, so the
///     things that can silently go wrong are all textual: a binding that doesn't survive the
///     round trip, a serialised line that corrupts the settings file, or a modifier
///     comparison loose enough that Ctrl+Alt+Z fires Undo.
/// </summary>
public class KeybindTests : IDisposable
{
    /// <summary>
    ///     The registry is static and every test writes to it, so each one starts from a
    ///     settings object of its own. (The suite already runs sequentially - see
    ///     SettingsInterfaceMarkupTests.)
    /// </summary>
    private readonly VisualizerSettings _settings = new();

    public KeybindTests()
    {
        Keybinds.Attach(_settings);
    }

    public void Dispose()
    {
        Keybinds.Attach(new VisualizerSettings());
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void EveryDefault_SurvivesTheRoundTrip()
    {
        foreach (var info in Keybinds.All)
        {
            Assert.True(Keybind.TryParse(info.Default.ToString(), out var parsed),
                $"{info.Id} writes \"{info.Default}\", which doesn't read back");
            Assert.Equal(info.Default, parsed);
        }
    }

    [Theory]
    [InlineData("Ctrl+Shift+Z")]
    [InlineData("Shift+Ctrl+Z")]
    [InlineData("shift+ctrl+z")]
    public void ModifierOrderAndCase_DoNotMatter(string text)
    {
        Assert.True(Keybind.TryParse(text, out var bind));
        Assert.Equal(new Keybind(Keys.Z, KeyModifiers.Control | KeyModifiers.Shift), bind);
    }

    /// <summary>A file hand-edited on one platform has to read on the other.</summary>
    [Theory]
    [InlineData("Cmd+Z")]
    [InlineData("Super+Z")]
    [InlineData("Win+Z")]
    [InlineData("Meta+Z")]
    public void EverySpellingOfSuper_ParsesToTheSameModifier(string text)
    {
        Assert.True(Keybind.TryParse(text, out var bind));
        Assert.Equal(new Keybind(Keys.Z, KeyModifiers.Super), bind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Hyper+Z")]
    [InlineData("Ctrl+NotAKey")]
    [InlineData("Ctrl+7")]
    public void Garbage_IsRejectedRatherThanGuessedAt(string text)
    {
        Assert.False(Keybind.TryParse(text, out _));
    }

    /// <summary>
    ///     The whole point of comparing the modifier set rather than testing flags: Ctrl+Z and
    ///     Ctrl+Shift+Z are two bindings, and only one of them fires.
    /// </summary>
    [Fact]
    public void Matches_RejectsASupersetOfModifiers()
    {
        var undo = new Keybind(Keys.Z, KeyModifiers.Control);

        Assert.True(undo.Matches(Event(Keys.Z, KeyModifiers.Control)));
        Assert.False(undo.Matches(Event(Keys.Z, KeyModifiers.Control | KeyModifiers.Alt)));
        Assert.False(undo.Matches(Event(Keys.Z, KeyModifiers.Control | KeyModifiers.Shift)));
        Assert.False(undo.Matches(Event(Keys.Y, KeyModifiers.Control)));
    }

    /// <summary>GLFW reports the lock keys in the same bitfield; Caps on must not break every shortcut.</summary>
    [Fact]
    public void Matches_IgnoresTheLockKeys()
    {
        var undo = new Keybind(Keys.Z, KeyModifiers.Control);
        const KeyModifiers locks = (KeyModifiers)0x0010 | (KeyModifiers)0x0020;

        Assert.True(undo.Matches(Event(Keys.Z, KeyModifiers.Control | locks)));

        var plain = new Keybind(Keys.Space, 0);
        Assert.True(plain.Matches(Event(Keys.Space, locks)));
    }

    /// <summary>
    ///     Asserted against Keybinds.Primary rather than a hardcoded platform, so it passes on
    ///     CI and on a Mac and still fails if the mapping is inverted.
    /// </summary>
    [Fact]
    public void Defaults_UseThePlatformPrimaryModifier()
    {
        Assert.Equal(OperatingSystem.IsMacOS() ? KeyModifiers.Super : KeyModifiers.Control, Keybinds.Primary);
        Assert.Equal(OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl", Keybinds.PrimaryName);

        Assert.Equal(new Keybind(Keys.Z, Keybinds.Primary), Keybinds.Get(Bind.EditorUndo));
        Assert.Equal(new Keybind(Keys.A, Keybinds.Primary), Keybinds.Get(Bind.EditorSelectAll));
    }

    [Fact]
    public void Rebind_WritesTheSettingAndAFreshAttachReadsItBack()
    {
        Keybinds.Rebind(Bind.EditorUndo, new Keybind(Keys.F1, 0));

        Assert.Contains("EditorUndo:F1", _settings.Keybinds);
        Assert.Equal(new Keybind(Keys.F1, 0), Keybinds.Get(Bind.EditorUndo));

        // A second registry lifetime over the same string: what a restart does.
        Keybinds.Attach(new VisualizerSettings { Keybinds = _settings.Keybinds });
        Assert.Equal(new Keybind(Keys.F1, 0), Keybinds.Get(Bind.EditorUndo));
    }

    /// <summary>Only overrides are serialised, so an untouched install never gets the line at all.</summary>
    [Fact]
    public void UntouchedBindings_AreNotSerialised()
    {
        Assert.Equal("", _settings.Keybinds);

        Keybinds.Rebind(Bind.EditorUndo, new Keybind(Keys.F1, 0));
        Keybinds.Rebind(Bind.EditorUndo, Keybinds.Info(Bind.EditorUndo).Default);

        Assert.Equal("", _settings.Keybinds);
    }

    /// <summary>
    ///     The two things that would corrupt the line-based settings file: a newline, and the
    ///     " # " that its parser reads as a trailing comment.
    /// </summary>
    [Fact]
    public void TheSerialisedLine_CannotCorruptTheSettingsFile()
    {
        foreach (var info in Keybinds.All)
            Keybinds.Rebind(info.Id, new Keybind(Keys.F1, KeyModifiers.Control | KeyModifiers.Shift));

        var line = Keybinds.Serialize();

        Assert.DoesNotContain("\n", line);
        Assert.DoesNotContain("\r", line);
        Assert.DoesNotContain(" # ", line);
        Assert.DoesNotContain("=", line);
    }

    [Fact]
    public void UnknownIdsAndGarbage_AreDroppedWithoutThrowing()
    {
        Keybinds.Deserialize("NotABind:Ctrl+Q;EditorUndo:Hyper+Q;;junk;EditorRedo:Ctrl+Q;EditorCut:");

        Assert.Equal(Keybinds.Info(Bind.EditorUndo).Default, Keybinds.Get(Bind.EditorUndo));
        Assert.Equal(Keybinds.Info(Bind.EditorCut).Default, Keybinds.Get(Bind.EditorCut));
        Assert.Equal(new Keybind(Keys.Q, KeyModifiers.Control), Keybinds.Get(Bind.EditorRedo));
    }

    [Fact]
    public void Match_FindsTheActionForTheSceneAndOnlyThatScene()
    {
        Assert.Equal(Bind.EditorUndo, Keybinds.Match(Event(Keys.Z, Keybinds.Primary), BindScene.Editor));
        Assert.Null(Keybinds.Match(Event(Keys.Z, Keybinds.Primary), BindScene.Visualizer));

        // Space is play/pause on both screens - cross-scene duplicates are expected.
        Assert.Equal(Bind.EditorPlayPause, Keybinds.Match(Event(Keys.Space, 0), BindScene.Editor));
        Assert.Equal(Bind.VisualizerPlayPause, Keybinds.Match(Event(Keys.Space, 0), BindScene.Visualizer));
    }

    [Fact]
    public void Conflict_ReportsTheSameSceneAndIgnoresTheOther()
    {
        Assert.Equal(Bind.EditorUndo, Keybinds.Conflict(Bind.EditorCut, new Keybind(Keys.Z, Keybinds.Primary),
            BindScene.Editor));

        // The editor's Space is not a conflict for the visualizer's.
        Assert.Null(Keybinds.Conflict(Bind.VisualizerPlayPause, new Keybind(Keys.Space, 0), BindScene.Visualizer));
    }

    /// <summary>
    ///     The hot-change contract: a rebind announces itself once, and the new binding is
    ///     readable from inside the handler. Dropping the guard in Write makes this fire twice.
    /// </summary>
    [Fact]
    public void Changed_FiresOncePerRebindAndOncePerReset()
    {
        var fired = 0;
        Keybind seen = default;

        void Handler()
        {
            fired++;
            seen = Keybinds.Get(Bind.EditorUndo);
        }

        Keybinds.Changed += Handler;
        try
        {
            Keybinds.Rebind(Bind.EditorUndo, new Keybind(Keys.F1, 0));
            Assert.Equal(1, fired);
            Assert.Equal(new Keybind(Keys.F1, 0), seen);

            Keybinds.ResetToDefaults(BindScene.Editor);
            Assert.Equal(2, fired);
            Assert.Equal(Keybinds.Info(Bind.EditorUndo).Default, seen);
        }
        finally
        {
            Keybinds.Changed -= Handler;
        }
    }

    /// <summary>A reset leaves the other screen's rebindings alone.</summary>
    [Fact]
    public void ResetToDefaults_OnlyTouchesItsOwnScene()
    {
        Keybinds.Rebind(Bind.EditorUndo, new Keybind(Keys.F1, 0));
        Keybinds.Rebind(Bind.VisualizerRestart, new Keybind(Keys.F2, 0));

        Keybinds.ResetToDefaults(BindScene.Editor);

        Assert.Equal(Keybinds.Info(Bind.EditorUndo).Default, Keybinds.Get(Bind.EditorUndo));
        Assert.Equal(new Keybind(Keys.F2, 0), Keybinds.Get(Bind.VisualizerRestart));
    }

    private static KeyboardKeyEventArgs Event(Keys key, KeyModifiers modifiers)
    {
        return new KeyboardKeyEventArgs(key, 0, modifiers, false);
    }
}
