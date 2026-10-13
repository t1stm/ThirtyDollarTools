using System.Globalization;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;

namespace Sundex.Components.File_Selector;

/// <summary>
///     A file open/save dialog body. Size it at the use site (e.g. inside a ModalLayer).
///     Open mode: click a file (<see cref="SelectedFile" />), confirm with the confirm button or
///     a double-click. The keyboard does the same (<see cref="HandleKeyDown" />), and clicking the
///     path, or Ctrl+L, makes it a field to type a path into. Save mode (a <paramref name="saveFileName" /> was given): a name field picks
///     the name inside <see cref="CurrentPath" />; clicking a file copies its name.
///     <see cref="SelectedPath" /> is the resulting full path for either mode.
///     <para>
///         The tree is built here, its look comes from the host's sheet through these classes:
///         <c>dialog-header</c>, <c>dialog-title</c>, <c>info-chip</c>, <c>info-chip-label</c>,
///         <c>icon-button</c>, <c>icon-button-icon</c>, <c>file-location</c>,
///         <c>file-location-gap</c>, <c>file-location-rule</c>, <c>file-crumbs</c>,
///         <c>file-crumb</c>, <c>file-crumb-link</c>, <c>file-path-input</c>,
///         <c>file-path-input-invalid</c>, <c>file-crumb-icon</c>,
///         <c>file-crumb-label</c>, <c>file-crumb-label-current</c>, <c>file-crumb-chevron</c>,
///         <c>file-list</c>, <c>file-row</c>, <c>file-row-idle</c>, <c>file-row-selected</c>,
///         <c>file-row-icon</c>, <c>file-row-folder</c>, <c>file-row-name</c>, <c>file-row-date</c>,
///         <c>file-list-empty</c>, <c>file-list-empty-label</c>, <c>file-list-note</c>,
///         <c>file-list-note-label</c>, <c>file-overwrite</c>, <c>file-overwrite-icon</c>,
///         <c>file-overwrite-label</c>, <c>file-footer</c>, <c>field-label</c>,
///         <c>file-name-input</c> and <c>file-ext-label</c>. <see cref="CancelButton" /> and
///         <see cref="ConfirmButton" /> carry none: the host styles them like its other dialogs.
///     </para>
/// </summary>
public sealed class FileSelection : FlexPanel
{
    private readonly Breadcrumbs _crumbs;
    private readonly string _confirmLabel;
    private readonly FlexPanel _footer;
    private readonly ScrollView _list;
    private readonly FlexPanel _location;
    private readonly TextInput? _nameInput;
    private readonly FlexPanel _overwrite;
    private readonly Label _overwriteLabel;
    private readonly TextInput _pathInput;
    private readonly List<FileRow> _rows = [];

    /// <summary>The keyboard's row in <see cref="_rows" />; -1 for none.</summary>
    private int _cursor = -1;

    /// <summary>A typed path that leads nowhere keeps the field open (see <see cref="CommitPath" />).</summary>
    private bool _keepEditingPath;

    /// <summary>A row to scroll into view once the list has been laid out with it.</summary>
    private FileRow? _reveal;

    /// <param name="context">The current UI context.</param>
    /// <param name="saveFileName">
    ///     Non-null switches to save mode and prefills the name field. A trailing
    ///     <paramref name="extensionFilter" /> is left out of the field; it is shown beside it.
    /// </param>
    /// <param name="extensionFilter">
    ///     Only files with this extension (e.g. ".tdwproj") are listed; in save mode a
    ///     typed name missing it gets it appended. Directories always show.
    /// </param>
    /// <param name="confirmLabel">Text on the confirm button, e.g. "Save" or "Export".</param>
    /// <param name="directory">
    ///     Where the dialog opens. Null, or a directory that no longer exists, opens the user's
    ///     home directory instead.
    /// </param>
    /// <param name="title">The dialog's heading, e.g. "Open project".</param>
    public FileSelection(UIContext context, string? saveFileName = null, string? extensionFilter = null,
        string confirmLabel = "Select", string? directory = null, string title = "Open file") :
        base(context)
    {
        Direction = LayoutDirection.Vertical;
        ExtensionFilter = extensionFilter;
        _confirmLabel = confirmLabel;
        CurrentPath = Directory.Exists(directory)
            ? Path.GetFullPath(directory)
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var header = new FlexPanel(context) { Classes = ["dialog-header"] };
        header.Children = [new Label(context, title) { Classes = ["dialog-title"] }];
        if (extensionFilter != null)
            header.Children.Add(new FlexPanel(context)
            {
                Classes = ["info-chip"],
                Children = [new Label(context, extensionFilter) { Classes = ["info-chip-label"] }]
            });
        header.Children.AddRange([Spacer(context), IconButton(context, Icons.X, () => OnCancel?.Invoke(this))]);

        // Clicking the path anywhere but on a folder above the current one edits it as text.
        _crumbs = new Breadcrumbs(context, NavigateTo)
        {
            Classes = ["file-crumbs"],
            Cursor = CursorType.Text,
            OnClick = _ => EditPath()
        };
        _pathInput = new TextInput(context)
        {
            Classes = ["file-path-input"],
            OnCommit = _ => CommitPath(),
            OnValueChanged = input => input.SetClass("file-path-input-invalid", false),
            OnBlur = _ => EndPathEdit()
        };
        _location = new FlexPanel(context)
        {
            Classes = ["file-location"],
            Children =
            [
                IconButton(context, Icons.ArrowUp, NavigateUp),
                new FlexPanel(context)
                {
                    Classes = ["file-location-gap"],
                    Children = [new Panel(context) { Classes = ["file-location-rule"] }]
                },
                _crumbs
            ]
        };

        _list = new ScrollView(context) { Classes = ["file-list"] };

        _overwriteLabel = new Label(context, "") { Classes = ["file-overwrite-label"] };
        _overwrite = new FlexPanel(context)
        {
            Classes = ["file-overwrite"],
            Children = [new Label(context, Icons.TriangleAlert, Icons.Font) { Classes = ["file-overwrite-icon"] }, _overwriteLabel]
        };

        CancelButton = new Button(context, "Cancel") { OnClick = _ => OnCancel?.Invoke(this) };
        ConfirmButton = new Button(context, confirmLabel) { OnClick = _ => Confirm() };

        _footer = new FlexPanel(context) { Classes = ["file-footer"] };
        if (saveFileName != null)
        {
            if (extensionFilter != null && saveFileName.EndsWith(extensionFilter, StringComparison.OrdinalIgnoreCase))
                saveFileName = saveFileName[..^extensionFilter.Length];

            _nameInput = new TextInput(context, saveFileName)
            {
                Classes = ["file-name-input"],
                // Enter in the field is Enter on the dialog: a focused field takes the key itself.
                OnCommit = _ => Confirm(),
                OnValueChanged = _ => RefreshOverwrite()
            };
            _footer.Children.Add(new Label(context, "Name") { Classes = ["field-label"] });
            _footer.Children.Add(_nameInput);
            if (extensionFilter != null)
                _footer.Children.Add(new Label(context, extensionFilter) { Classes = ["file-ext-label"] });
        }

        _footer.Children.AddRange([Spacer(context), CancelButton, ConfirmButton]);

        Children = [header, _location, _list, _footer];
        RefreshFiles();
    }

    public string CurrentPath { get; private set; }

    /// <summary>The full path of the last clicked file; cleared when the directory changes.</summary>
    public string? SelectedFile { get; private set; }

    public string? ExtensionFilter { get; }

    /// <summary>
    ///     The confirmed full path: in save mode CurrentPath + the typed name (with
    ///     <see cref="ExtensionFilter" /> appended when missing), otherwise
    ///     <see cref="SelectedFile" />. Null while nothing usable is chosen.
    /// </summary>
    public string? SelectedPath
    {
        get
        {
            if (_nameInput == null) return SelectedFile;

            var name = _nameInput.Value.Trim();
            if (name.Length == 0) return null;
            if (ExtensionFilter != null && !name.EndsWith(ExtensionFilter, StringComparison.OrdinalIgnoreCase))
                name += ExtensionFilter;
            return Path.Combine(CurrentPath, name);
        }
    }

    /// <summary>Whether confirming would write over a file that is already there (save mode only).</summary>
    public bool Overwrites => _nameInput != null && SelectedPath is { } path && File.Exists(path);

    public Button CancelButton { get; }

    /// <summary>The confirm action. Its label turns to "Replace" while <see cref="Overwrites" />.</summary>
    public Button ConfirmButton { get; }

    public Action<FileSelection>? OnSelectFile { get; set; }
    public Action<FileSelection>? OnChangeDirectory { get; set; }

    public Action<FileSelection>? OnCancel { get; set; }
    public Action<FileSelection>? OnSelect { get; set; }

    /// <summary>What the confirm button does; also a double-click on a file and Enter in the name field.</summary>
    public void Confirm()
    {
        OnSelect?.Invoke(this);
    }

    /// <summary>Marks a file as chosen - what clicking a row does.</summary>
    public void SelectFile(string path)
    {
        SelectedFile = path;
        _nameInput?.Value = ExtensionFilter != null && path.EndsWith(ExtensionFilter, StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(path)
            : Path.GetFileName(path);

        Highlight(_rows.FindIndex(row => row.Path == path));
        RefreshOverwrite();
        OnSelectFile?.Invoke(this);
    }

    /// <summary>
    ///     The list's keys, for when no field has focus (a ModalLayer passes them on): Up/Down,
    ///     Home/End and PageUp/PageDown move through the rows, picking a file as they land on it;
    ///     Enter opens the folder on the highlighted row or confirms; Backspace or Alt+Up opens
    ///     the folder above; Ctrl+L edits the path.
    /// </summary>
    public override bool HandleKeyDown(KeyboardKeyEventArgs e)
    {
        var page = Math.Max(1, (int)(_list.Computed.Height / RowPitch) - 1);
        switch (e.Key)
        {
            case Keys.Up when e.Alt:
            case Keys.Backspace when !e.IsRepeat:
                NavigateUp();
                return true;
            case Keys.Up:
                MoveCursor(_cursor < 0 ? 0 : _cursor - 1);
                return true;
            case Keys.Down:
                MoveCursor(_cursor + 1);
                return true;
            case Keys.PageUp:
                MoveCursor(_cursor - page);
                return true;
            case Keys.PageDown:
                MoveCursor(_cursor + page);
                return true;
            case Keys.Home:
                MoveCursor(0);
                return true;
            case Keys.End:
                MoveCursor(_rows.Count - 1);
                return true;
            case Keys.Enter or Keys.KeyPadEnter when !e.IsRepeat:
                if (_cursor >= 0 && _rows[_cursor].IsDirectory) NavigateTo(_rows[_cursor].Path);
                else Confirm();
                return true;
            case Keys.L when e.Control || e.Command:
                EditPath();
                return true;
            default:
                return false;
        }
    }

    /// <summary>A row's height and the gap under it: what one step of the list scrolls.</summary>
    private const float RowPitch = 36;

    /// <summary>Moves the keyboard's row: a file is picked as it is landed on, a folder only marked.</summary>
    private void MoveCursor(int index)
    {
        if (_rows.Count == 0) return;
        var row = _rows[Math.Clamp(index, 0, _rows.Count - 1)];
        if (row.IsDirectory)
        {
            SelectedFile = null; // Open must not pick a file the keyboard has moved off
            Highlight(_rows.IndexOf(row));
        }
        else
        {
            SelectFile(row.Path);
        }

        _reveal = row;
        InvalidateLayout();
    }

    private void Highlight(int index)
    {
        _cursor = index;
        for (var i = 0; i < _rows.Count; i++) _rows[i].Selected = i == index;
    }

    private void NavigateUp()
    {
        var directory = new DirectoryInfo(CurrentPath);
        if (directory.Parent == null) return;

        // Coming back up, the folder just left is where the keyboard picks up.
        var left = CurrentPath;
        NavigateTo(directory.Parent.FullName);
        if (_rows.Find(row => row.Path == left) is not { } row) return;
        Highlight(_rows.IndexOf(row));
        _reveal = row;
    }

    /// <summary>Swaps the crumbs for a field holding the path, selected, to type over.</summary>
    private void EditPath()
    {
        if (_location.Children.Contains(_pathInput)) return;
        _pathInput.Value = CurrentPath;
        _location.RemoveChild(_crumbs);
        _location.AddChild(_pathInput);
        Context.Focus(_pathInput);
        _pathInput.SelectAll();
    }

    /// <summary>
    ///     Goes where the typed path leads: a folder opens, a file opens its folder and is picked.
    ///     "~" is the home directory and a relative path starts from the current folder. A path
    ///     to nothing turns the text red and keeps the field open.
    /// </summary>
    private void CommitPath()
    {
        var text = _pathInput.Value.Trim();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (text == "~" || text.StartsWith("~/")) text = home + text[1..];

        string path;
        try
        {
            path = Path.GetFullPath(text, CurrentPath);
        }
        catch (Exception)
        {
            path = "";
        }

        if (Directory.Exists(path))
        {
            NavigateTo(path);
        }
        else if (File.Exists(path) && Path.GetDirectoryName(path) is { } folder)
        {
            NavigateTo(folder);
            if (ExtensionFilter == null || path.EndsWith(ExtensionFilter, StringComparison.OrdinalIgnoreCase))
            {
                SelectFile(path);
                _reveal = _rows.ElementAtOrDefault(_cursor);
            }
        }
        else
        {
            _pathInput.SetClass("file-path-input-invalid", true);
            // The field blurs itself right after this; EndPathEdit takes focus back.
            _keepEditingPath = true;
        }
    }

    /// <summary>Puts the crumbs back once the field loses focus (Enter, Escape, a click elsewhere).</summary>
    private void EndPathEdit()
    {
        if (_keepEditingPath)
        {
            _keepEditingPath = false;
            Context.Focus(_pathInput);
            return;
        }

        if (!_location.Children.Contains(_pathInput)) return;
        _location.RemoveChild(_pathInput);
        _location.AddChild(_crumbs);
    }

    protected override void DoLayout()
    {
        base.DoLayout();
        if (_reveal is not { } row) return;
        _reveal = null;

        // Measured rather than read off the laid-out rows: right after a folder opens, the list
        // hasn't placed its new rows yet.
        float Height(UIElement child) => child.Measure(_list.Computed.Width, _list.Computed.Height).height;
        float top = 0;
        foreach (var child in _list.Children)
        {
            if (child == row) break;
            top += Height(child) + _list.Spacing;
        }

        var bottom = top + Height(row);
        if (top < _list.ScrollY) _list.ScrollY = top;
        else if (bottom > _list.ScrollY + _list.Computed.Height) _list.ScrollY = bottom - _list.Computed.Height;

        // Scrolling marks the list for layout, but this panel is still mid-layout and takes no
        // notice, so the list would stay marked with nothing above it to reach it, and later
        // changes in it (the highlight) wouldn't show. Lay it out now instead.
        _list.Layout();
    }

    private void NavigateTo(string path)
    {
        if (!Directory.Exists(path)) return;
        CurrentPath = path;
        SelectedFile = null; // a pick from another directory must not survive navigation
        RefreshFiles();
        OnChangeDirectory?.Invoke(this);
    }

    /// <summary>Shows or hides the overwrite notice and names the confirm action after it.</summary>
    private void RefreshOverwrite()
    {
        var overwrites = Overwrites;
        ConfirmButton.Value = overwrites ? "Replace" : _confirmLabel;
        if (overwrites)
        {
            // "Save" reads "Saving", "Export" "Exporting".
            var verb = _confirmLabel.EndsWith('e') ? _confirmLabel[..^1] + "ing" : _confirmLabel + "ing";
            _overwriteLabel.SetTextContents($"“{Path.GetFileName(SelectedPath)}” is already here. {verb} replaces it.");
        }

        if (overwrites == Children.Contains(_overwrite)) return;
        if (overwrites) Children = [.. Children.Where(child => child != _footer), _overwrite, _footer];
        else RemoveChild(_overwrite);
    }

    private void RefreshFiles()
    {
        _crumbs.Show(CrumbsFor(CurrentPath));
        var rows = new List<UIElement>();

        try
        {
            var directories = new DirectoryInfo(CurrentPath).GetDirectories()
                .Where(info => (info.Attributes & FileAttributes.Hidden) == 0)
                .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var files = new DirectoryInfo(CurrentPath).GetFiles()
                .Where(info => ExtensionFilter == null ||
                               info.Name.EndsWith(ExtensionFilter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (directories.Length == 0 && files.Length == 0)
            {
                rows.Add(new FlexPanel(Context)
                {
                    Classes = ["file-list-empty"],
                    Children = [new Label(Context, "This folder is empty") { Classes = ["file-list-empty-label"] }]
                });
            }
            else
            {
                // The list has no padding of its own (a scroll view lays its rows out itself); this
                // and the list's spacing put the first row 4 below its top.
                rows.Add(new Panel(Context) { Height = 2 });
                rows.AddRange(directories.Select(info =>
                    new FileRow(Context, info.FullName, info.Name, null, true) { OnClick = _ => NavigateTo(info.FullName) }));
                rows.AddRange(files.Select(info =>
                    new FileRow(Context, info.FullName, info.Name, info.LastWriteTime, false)
                    {
                        OnClick = _ => SelectFile(info.FullName),
                        OnDoublePress = Confirm
                    }));
                if (files.Length == 0 && ExtensionFilter != null)
                    rows.Add(new FlexPanel(Context)
                    {
                        Classes = ["file-list-note"],
                        Children = [new Label(Context, $"No {ExtensionFilter} files here") { Classes = ["file-list-note-label"] }]
                    });
            }
        }
        catch (Exception ex)
        {
            rows.Add(new FlexPanel(Context)
            {
                Classes = ["file-list-note"],
                Children = [new Label(Context, $"Can't open this folder: {ex.Message}") { Classes = ["file-list-note-label"] }]
            });
        }

        foreach (var child in _list.Children) child.StopRendering();
        _list.Children = rows;
        _list.ScrollY = 0;
        _rows.Clear();
        _rows.AddRange(rows.OfType<FileRow>());
        _cursor = -1;
        RefreshOverwrite();
    }

    /// <summary>
    ///     The path as crumbs: under the home directory the first is "Home", elsewhere the root.
    /// </summary>
    internal static List<(string Label, string Path, bool Home)> CrumbsFor(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var crumbs = new List<(string, string, bool)>();
        string rest;
        string current;
        if (home.Length > 0 && (path == home || path.StartsWith(home + Path.DirectorySeparatorChar)))
        {
            crumbs.Add(("Home", home, true));
            current = home;
            rest = path[home.Length..];
        }
        else
        {
            var root = Path.GetPathRoot(path) ?? "/";
            crumbs.Add((root, root, false));
            current = root;
            rest = path[root.Length..];
        }

        foreach (var segment in rest.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            crumbs.Add((segment, current, false));
        }

        return crumbs;
    }

    private static Panel Spacer(UIContext context)
    {
        // A percent-width spacer soaks up the free space: there is no space-between align.
        return new Panel(context) { Width = LiteralOrComputable.Percent(100), Height = 1 };
    }

    private static Button IconButton(UIContext context, string icon, Action onClick)
    {
        var button = new Button(context, icon, font: Icons.Font) { Classes = ["icon-button"], OnClick = _ => onClick() };
        button.Label.Classes = ["icon-button-icon"];
        return button;
    }

    /// <summary>One folder or file: its icon, its name, and when a file was last written.</summary>
    private sealed class FileRow : FlexPanel
    {
        public FileRow(UIContext context, string path, string name, DateTime? modified, bool isDirectory) :
            base(context)
        {
            Path = path;
            IsDirectory = isDirectory;
            Classes = ["file-row", "file-row-idle"];
            UpdateCursorOnHover = true;

            var icon = isDirectory ? Icons.Folder : IconFor(name);
            Children =
            [
                new Label(context, icon, Icons.Font) { Classes = [isDirectory ? "file-row-folder" : "file-row-icon"] },
                // The name takes what the date leaves, and is cut off at its edge.
                new FlexPanel(context)
                {
                    Width = LiteralOrComputable.Percent(100),
                    VerticalAlign = Align.Center,
                    Children = [new ClippedLabel(context, name) { Classes = ["file-row-name"] }]
                },
                new Label(context, modified?.ToString("MMM d, HH:mm", CultureInfo.InvariantCulture) ?? "")
                    { Classes = ["file-row-date"] }
            ];
        }

        public string Path { get; }

        public bool IsDirectory { get; }

        public Action? OnDoublePress { get; init; }

        /// <summary>
        ///     Swaps the idle fill for the selected one. Two classes rather than a modifier: a hover
        ///     override is taken from the first class that has one, so a modifier listed after
        ///     file-row-idle could never replace its hover.
        /// </summary>
        public bool Selected
        {
            set
            {
                SetClass("file-row-idle", !value);
                SetClass("file-row-selected", value);
            }
        }

        public override bool HandleDoublePress(float x, float y)
        {
            if (OnDoublePress == null) return false;
            OnDoublePress();
            return true;
        }

        private static string IconFor(string name)
        {
            var extension = System.IO.Path.GetExtension(name).ToLowerInvariant();
            return extension switch
            {
                ".wav" or ".mp3" or ".ogg" or ".flac" => Icons.AudioWaveform,
                ".tdwproj" => Icons.FileMusic,
                _ => Icons.FileText
            };
        }
    }

    /// <summary>
    ///     The path as clickable crumbs. When they don't fit, the leading ones fold into one "…"
    ///     crumb that opens the deepest folded folder; the current folder always shows, cut off
    ///     at the edge if even it alone is too long.
    /// </summary>
    private sealed class Breadcrumbs(UIContext context, Action<string> navigate) : FlexPanel(context)
    {
        private Vector4i? _inheritedClip;
        private List<(string Label, string Path, bool Home)> _parts = [];
        private int _folded;

        public void Show(List<(string Label, string Path, bool Home)> parts)
        {
            _parts = parts;
            _folded = 0;
            Rebuild();
        }

        private void Rebuild()
        {
            var crumbs = new List<UIElement>();
            if (_folded > 0)
                crumbs.Add(Crumb("…", _parts[_folded - 1].Path, false, false));

            for (var i = _folded; i < _parts.Count; i++)
            {
                if (crumbs.Count > 0)
                    crumbs.Add(new Label(Context, Icons.ChevronRight, Icons.Font) { Classes = ["file-crumb-chevron"] });
                var (label, path, home) = _parts[i];
                crumbs.Add(Crumb(label, path, home, i == _parts.Count - 1));
            }

            foreach (var child in Children) child.StopRendering();
            Children = crumbs;
        }

        private FlexPanel Crumb(string text, string path, bool home, bool current)
        {
            var crumb = new FlexPanel(Context) { Classes = current ? ["file-crumb"] : ["file-crumb", "file-crumb-link"] };
            if (home) crumb.Children.Add(new Label(Context, Icons.House, Icons.Font) { Classes = ["file-crumb-icon"] });
            crumb.Children.Add(new Label(Context, text)
                { Classes = current ? ["file-crumb-label", "file-crumb-label-current"] : ["file-crumb-label"] });
            if (!current)
            {
                crumb.UpdateCursorOnHover = true;
                crumb.OnClick = _ => navigate(path);
            }

            return crumb;
        }

        private float Needed()
        {
            var width = Math.Max(0, Children.Count - 1) * Spacing;
            foreach (var child in Children) width += child.Measure(float.MaxValue, Computed.Height).width;
            return width;
        }

        protected override void DoLayout()
        {
            base.DoLayout();
            while (_folded < _parts.Count - 1 && Needed() > Computed.Width)
            {
                _folded++;
                Rebuild();
                base.DoLayout();
            }

            ApplyClip(_inheritedClip);
        }

        public override void ApplyClip(Vector4i? clip)
        {
            _inheritedClip = clip;
            var x = (int)Computed.AbsoluteX;
            var y = (int)Computed.AbsoluteY;
            base.ApplyClip(IntersectClip(new Vector4i(x, y, x + (int)Computed.Width, y + (int)Computed.Height), clip));
        }
    }
}
