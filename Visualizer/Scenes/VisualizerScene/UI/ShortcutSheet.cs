using System.Diagnostics;
using JetBrains.Annotations;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using Sundex.Markup.Attributes;
using VisualizerScene.Settings;

namespace VisualizerScene.UI;

/// <summary>
///     The Visualizer's shortcuts, drawn as a line of playfield tiles. With nothing loaded
///     it is the screen's content. While a cover plays it is a card over the playfield,
///     hidden until <see cref="Bind.VisualizerShowShortcuts" /> opens it.
/// </summary>
public class ShortcutSheet
{
    /// <summary>The player bar's fade speed, so the two move alike.</summary>
    private const float FadeSpeed = 5f;

    /// <summary>How long a tile stays lit after its key is let go.</summary>
    private const double LitSeconds = 0.25;

    private readonly ElementAlpha _alpha = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<Tile> _tiles = [];

    private bool? _idle;

    private Tile _scrollTile = null!;
    private Tile _zoomTile = null!;

    public ShortcutSheet(UIContext context)
    {
        UI = context;

        var sundexContext = new SundexContext(context);
        var source = context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo { Location = "UI/Layout/ShortcutSheet.snx.xml" }
        });

        Component = sundexContext.NewComponent(source.Value);
        sundexContext.RunLogicAndVerify(Component,
            () => RootPanel,
            () => Header,
            () => Groups,
            () => Footnote);

        BuildGroups();
        Refresh();
        Keybinds.Changed += Refresh;

        RootPanel.DrawTo(context);
    }

    public UIContext UI { get; }

    [UsedImplicitly] public SundexComponent Component { get; }

    [SetFromLogic] public FlexPanel RootPanel { get; set; } = null!;
    [SetFromLogic] public FlexPanel Header { get; set; } = null!;
    [SetFromLogic] public FlexPanel Groups { get; set; } = null!;
    [SetFromLogic] public Label Footnote { get; set; } = null!;

    public float CurrentAlpha { get; private set; }

    /// <summary>Whether the card is open over a playing cover. Always false while idle, where the sheet is simply shown.</summary>
    public bool Open { get; private set; }

    public void Toggle()
    {
        if (_idle == false) Open = !Open;
    }

    /// <summary>Closes the card. False if it was already closed, so Back can go on to leave the scene.</summary>
    public bool Close()
    {
        if (!Open) return false;
        Open = false;
        return true;
    }

    private void BuildGroups()
    {
        var primary = Keybinds.Primary;
        var fullscreen = OperatingSystem.IsMacOS()
            ? new Keybind(Keys.F, KeyModifiers.Control | KeyModifiers.Super)
            : new Keybind(Keys.Enter, KeyModifiers.Alt);

        _scrollTile = new Tile("Scroll", () => default, _ => false, "Scroll");
        _zoomTile = new Tile("Zoom", () => new Keybind(Keys.Unknown, primary), _ => false, "Scroll");

        // Curated rather than every entry in Keybinds.All: the step-size modifiers and the
        // quiet play/pause are in Settings, and a sheet of everything is too long to read.
        AddGroup("PLAYBACK",
            Bound(Bind.VisualizerPlayPause, "Play / pause"),
            Bound(Bind.VisualizerRestart, "Restart"),
            Bound(Bind.VisualizerRestartPaused, "Restart paused"),
            // Not exact, like Visualizer.Keyboard: Shift and Ctrl scale the step here.
            Bound(Bind.VisualizerSeekBack, "Back 1 s", false),
            Bound(Bind.VisualizerSeekForward, "Ahead 1 s", false),
            Bound(Bind.VisualizerVolumeUp, "Louder"),
            Bound(Bind.VisualizerVolumeDown, "Quieter"));

        AddGroup("SEQUENCES",
            Bound(Bind.VisualizerPreviousSequence, "Previous"),
            Bound(Bind.VisualizerNextSequence, "Next"),
            Digits("Go to mark", 0),
            Digits("Set mark", primary),
            Digits("Clear mark", primary | KeyModifiers.Shift),
            Bound(Bind.VisualizerReloadSequences, "Reload files"));

        AddGroup("VIEW",
            Bound(Bind.VisualizerCycleCamera, "Camera mode"),
            Bound(Bind.VisualizerTogglePlayerBar, "Player bar"),
            _scrollTile,
            _zoomTile,
            new Tile("Fullscreen", () => fullscreen, state => fullscreen.IsDown(state)),
            Bound(Bind.VisualizerToggleDebug, "Debug info"),
            Bound(Bind.VisualizerShowShortcuts, "Shortcuts"),
            Bound(Bind.VisualizerBack, "Home"));
    }

    private static Tile Bound(Bind bind, string caption, bool exact = true)
    {
        return new Tile(caption, () => Keybinds.Get(bind), state => Keybinds.Get(bind).IsDown(state, exact));
    }

    /// <summary>Bookmarks are fixed to "modifier + digit" (see Visualizer.Keyboard), so one tile covers ten keys.</summary>
    private static Tile Digits(string caption, KeyModifiers modifiers)
    {
        return new Tile(caption, () => new Keybind(Keys.D0, modifiers), state =>
        {
            for (var key = Keys.D0; key <= Keys.D9; key++)
                if (new Keybind(key, modifiers).IsDown(state))
                    return true;
            return false;
        }, "0–9");
    }

    private void AddGroup(string name, params Tile[] tiles)
    {
        var row = new FlexPanel(UI)
        {
            Classes = ["group"],
            Children =
            [
                new FlexPanel(UI)
                {
                    Classes = ["group-slot"],
                    Children = [new Label(UI, string.Join(' ', name.ToCharArray())) { Classes = ["group-name"] }]
                }
            ]
        };

        foreach (var tile in tiles)
        {
            tile.Build(UI);
            row.AddChild(tile.Column);
            _tiles.Add(tile);
        }

        Groups.AddChild(row);
    }

    /// <summary>Rewrites every tile's face, and the footnote naming the sheet's key, from the current bindings. Runs on <see cref="Keybinds.Changed" />.</summary>
    private void Refresh()
    {
        foreach (var tile in _tiles) tile.Refresh();
        Footnote.Value = $"While a cover plays, press {Keybinds.Get(Bind.VisualizerShowShortcuts)} to see these again.";
    }

    public void Resize()
    {
        RootPanel.InvalidateCoordinates();
        RootPanel.Layout();
    }

    /// <summary>Marks the tiles whose keys are held. Called from the scene's keyboard pass, which only runs while a key is down.</summary>
    public void TrackKeys(KeyboardState state)
    {
        var now = _clock.Elapsed.TotalSeconds;
        foreach (var tile in _tiles)
            if (tile.IsDown(state))
                tile.LastDown = now;
    }

    public void TrackScroll(bool zoom)
    {
        (zoom ? _zoomTile : _scrollTile).LastDown = _clock.Elapsed.TotalSeconds;
    }

    /// <param name="idle">Nothing is loaded, so the sheet is the screen's content rather than a hidden overlay.</param>
    public void Update(UIContext context, bool idle)
    {
        if (_idle != idle)
        {
            _idle = idle;
            // A cover starting shouldn't open under a card left open over the last one.
            Open = false;
            RootPanel.SetClass("card", !idle);

            // Taken out of the tree rather than only hidden: a flex lays out its invisible
            // children too, which left their height as a gap at the top of the card.
            if (!idle)
            {
                Header.Visible = false;
                Footnote.Visible = false;
                RootPanel.Children = [Groups];
            }
            else
            {
                RootPanel.Children = [Header, Groups, Footnote];
                Header.Visible = true;
                Footnote.Visible = true;
            }
        }

        var now = _clock.Elapsed.TotalSeconds;
        foreach (var tile in _tiles) tile.SetLit(now - tile.LastDown < LitSeconds);

        RootPanel.Update(context);
        RootPanel.Layout();
    }

    /// <summary>Applied every frame, after <see cref="Update" />: SetClass re-runs the stylesheet, which puts the styled alpha back.</summary>
    public void UpdateAlpha(float deltaTime, bool idle)
    {
        var target = idle || Open ? 1f : 0f;
        var step = FadeSpeed * deltaTime;
        CurrentAlpha = CurrentAlpha < target
            ? Math.Min(CurrentAlpha + step, target)
            : Math.Max(CurrentAlpha - step, target);

        _alpha.Apply(RootPanel, CurrentAlpha);
    }

    /// <summary>One key: a tile with the key on its face and the modifier in its corner, over a caption.</summary>
    private sealed class Tile(
        string caption,
        Func<Keybind> binding,
        Func<KeyboardState, bool> isDown,
        string? face = null)
    {
        private Label _face = null!;
        private bool _lit;
        private Label _modifier = null!;
        private Panel _tile = null!;

        public FlexPanel Column { get; private set; } = null!;
        public double LastDown { get; set; } = double.NegativeInfinity;

        public bool IsDown(KeyboardState state)
        {
            return isDown(state);
        }

        public void Build(UIContext ui)
        {
            _face = new Label(ui, " ") { Classes = ["face"] };
            _modifier = new Label(ui, " ") { Classes = ["modifier"] };
            _tile = new Panel(ui)
            {
                Classes = ["tile"],
                Children =
                [
                    new FlexPanel(ui) { Classes = ["tile-face-slot"], Children = [_face] },
                    new FlexPanel(ui) { Classes = ["tile-corner"], Children = [_modifier] }
                ]
            };

            Column = new FlexPanel(ui)
            {
                Classes = ["column"],
                Children = [_tile, new Label(ui, caption) { Classes = ["caption"] }]
            };
        }

        public void Refresh()
        {
            var bind = binding();
            var text = face ?? FaceOf(bind.Key);
            _face.Value = text;
            _face.SetClass("face-word", text.Length > 2);
            _modifier.Value = ModifiersOf(bind.Modifiers);
        }

        public void SetLit(bool lit)
        {
            if (_lit == lit) return;
            _lit = lit;
            _tile.SetClass("lit", lit);
            _face.SetClass("lit-text", lit);
            _modifier.SetClass("lit-text", lit);
        }

        private static string FaceOf(Keys key)
        {
            return key switch
            {
                Keys.Escape => "Esc",
                Keys.PageUp => "PgUp",
                Keys.PageDown => "PgDn",
                Keys.Left => "←",
                Keys.Right => "→",
                Keys.Up => "↑",
                Keys.Down => "↓",
                Keys.Equal => "=",
                Keys.Minus => "−",
                >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
                _ => key.ToString()
            };
        }

        private static string ModifiersOf(KeyModifiers modifiers)
        {
            var parts = new List<string>(4);
            if (modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(KeyModifiers.Super)) parts.Add(OperatingSystem.IsMacOS() ? "Cmd" : "Super");
            return parts.Count == 0 ? " " : string.Join(' ', parts);
        }
    }
}
