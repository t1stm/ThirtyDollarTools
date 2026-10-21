using System.Diagnostics;
using EditorScene.Scenes.Components;
using EditorScene.State;
using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Markup;
using VisualizerScene.Settings;

namespace EditorScene.Scenes.Layout;

/// <summary>
///     Drives the editor's top bar. The tree lives in Scenes/Layout/Editor Header/EditorHeader.snx.xml
///     and its clicks are wired by that document's logic; this type keeps what the bar shows in
///     step with the project:
///     <list type="bullet">
///         <item>the project name, which opens the project menu, and the BPM chip;</item>
///         <item>Save - "Save As" until the project has a file - with its chip lit amber while there are unsaved changes;</item>
///         <item>New and Open, which stand at the far end only while the project is new and untouched.</item>
///     </list>
///     A button that appears or is relabelled drops into place: 5 px and from transparent over
///     0.2 s, the design canvas's entrance.
/// </summary>
internal sealed class EditorHeaderController
{
    private const float EnterSeconds = 0.2f;
    private const float EnterDrop = 5;
    private static readonly CubicBezier EnterCurve = new(0.2f, 0.8f, 0.2f, 1f);

    private readonly Label _bpm;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Slot _fileSlot;
    private readonly Label _name;
    private readonly Button _save;
    private readonly Slot _saveSlot;
    private readonly EditorState _state;

    /// <summary>What the bar last showed; null until the first <see cref="Update" />.</summary>
    private (bool Linked, bool Pristine, bool Dirty)? _shown;

    /// <param name="header">The built header document, whose ids this resolves.</param>
    /// <param name="state">The editor's state, whose file and edits the bar follows.</param>
    /// <param name="tools">Laid over the bar; <see cref="EditorInterface" /> places it at the grid's centre.</param>
    public EditorHeaderController(SundexComponent header, EditorState state, ToolSwitch tools)
    {
        _state = state;
        _name = header.GetID<Label>("project-name");
        _bpm = header.GetID<Label>("project-bpm");
        _save = Chip(header.GetID<Button>("save-button"), Bind.EditorSave);
        var newButton = Chip(header.GetID<Button>("new-button"), Bind.EditorNew);
        var openButton = Chip(header.GetID<Button>("open-button"), Bind.EditorOpen);
        _saveSlot = new Slot(header.GetID<Panel>("save-slot"), _save);
        _fileSlot = new Slot(header.GetID<Panel>("file-slot"), header.GetID<FlexPanel>("file-actions"));
        _fileSlot.Fit();

        // The chevron says the name opens a menu. A markup button holds one label.
        var menu = header.GetID<Button>("project-menu-button");
        menu.Children =
            [menu.Label, new Label(menu.Context, Icons.ChevronDown, Icons.Font) { Classes = ["header-menu-chevron"] }];

        header.GetID<Panel>("editor-header").AddChild(tools);

        // Never unsubscribed, like the hint bar's: the editor lives as long as the process.
        Keybinds.Changed += () =>
        {
            KeyChip.SetKey(_save, Key(Bind.EditorSave));
            KeyChip.SetKey(newButton, Key(Bind.EditorNew));
            KeyChip.SetKey(openButton, Key(Bind.EditorOpen));
            _saveSlot.Fit();
            _fileSlot.Fit();
        };
    }

    private float Now => (float)_clock.Elapsed.TotalSeconds;

    private static string Key(Bind bind)
    {
        return Keybinds.Get(bind).ToString();
    }

    /// <summary>The key in front, then header-key-button after key-button for the bar's tighter padding.</summary>
    private static Button Chip(Button button, Bind bind)
    {
        KeyChip.Add(button, Key(bind)).SetClass("header-key-button", true);
        return button;
    }

    public void RefreshProject(string name, string bpm)
    {
        _name.SetTextContents(name);
        _bpm.SetTextContents(bpm);
    }

    /// <summary>
    ///     Per frame, after the update pass and before layout. Polled rather than pushed: a load
    ///     names the file only after the change notification it fires, and a save fires none.
    /// </summary>
    public void Update()
    {
        var now = Now;
        var linked = _state.ProjectPath != null;
        var shown = (Linked: linked, Pristine: !linked && !_state.Dirty, _state.Dirty);
        if (shown != _shown)
        {
            // The first pass sets the bar up as it is; only later changes play the entrance.
            var first = _shown is null;
            if (first || shown.Linked != _shown!.Value.Linked)
            {
                _save.Label.SetTextContents(linked ? "Save" : "Save As");
                _saveSlot.Fit();
                if (!first) _saveSlot.Enter(now);
            }

            if (first || shown.Pristine != _shown!.Value.Pristine)
            {
                _fileSlot.Panel.Visible = shown.Pristine;
                if (!first && shown.Pristine) _fileSlot.Enter(now);
            }

            KeyChip.SetLit(_save, shown.Dirty);
            _shown = shown;
        }

        _saveSlot.Animate(now);
        _fileSlot.Animate(now);
    }

    /// <summary>
    ///     A plain panel holding one header element, so the entrance can lift the element off its
    ///     row: the bar places the slot, and a plain panel leaves its child wherever its Y says.
    ///     The slot has to be told its size - a plain panel doesn't measure its content.
    /// </summary>
    private sealed class Slot(Panel panel, Panel content)
    {
        private readonly ElementAlpha _alpha = new();
        private readonly Transition _progress = new(1);
        private bool _moving;

        public Panel Panel => panel;

        public void Fit()
        {
            panel.Width = content.Measure(float.MaxValue, panel.Height.Value).width;
        }

        public void Enter(float now)
        {
            _progress.Snap(0);
            _progress.Set(1, now, EnterSeconds, EnterCurve);
            _moving = true;
        }

        public void Animate(float now)
        {
            var moving = _progress.Running(now);
            if (!moving && !_moving) return;
            _moving = moving;

            var progress = _progress.Value(now);
            content.Y = -EnterDrop * (1 - progress);
            // Every moving frame: a hover or the chip lighting up restyles the button and puts
            // the styled alpha back.
            _alpha.Apply(content, progress);
        }
    }
}
