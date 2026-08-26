using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;

namespace Sundex.Components.File_Selector;

/// <summary>
///     A file open/save dialog body. Size it at the use site (e.g. inside a ModalLayer).
///     Open mode: click a file (<see cref="SelectedFile" />), confirm with Select.
///     Save mode (a <paramref name="saveFileName" /> was given): a filename input picks
///     the name inside <see cref="CurrentPath" />; clicking a file copies its name.
///     <see cref="SelectedPath" /> is the resulting full path for either mode.
/// </summary>
public sealed class FileSelection : Panel
{
    private static readonly Vector4 InputColor = new(0.15f, 0.16f, 0.21f, 1f);
    private static readonly Vector4 ConfirmColor = new(0.30f, 0.42f, 0.80f, 1f);
    private static readonly Vector4 CancelColor = new(0.2f, 0.204f, 0.29f, 1f);

    private readonly Label _currentPathLabel;
    private readonly FlexPanel _filesSection;
    private readonly TextInput? _nameInput;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly FlexPanel _topSection;

    /// <param name="context">The current UI context.</param>
    /// <param name="saveFileName">Non-null switches to save mode and prefills the filename input.</param>
    /// <param name="extensionFilter">
    ///     Only files with this extension (e.g. ".tdwproj") are listed; in save mode a
    ///     typed name missing it gets it appended. Directories always show.
    /// </param>
    /// <param name="confirmLabel">Text on the confirm button, e.g. "Save" or "Export".</param>
    public FileSelection(UIContext context, string? saveFileName = null, string? extensionFilter = null,
        string confirmLabel = "Select") :
        base(context)
    {
        ExtensionFilter = extensionFilter;

        _topSection = new FlexPanel(context)
        {
            Direction = LayoutDirection.Horizontal,
            Width = LiteralOrComputable.Percent(100),
            Height = 30,
            Padding = 5,
            Spacing = 10,
            VerticalAlign = Align.Center,
            Background = new ColoredPlane
            {
                Color = (0.15f, 0.15f, 0.15f, 1.0f)
            },
            Children =
            [
                new Label(Context, "↑ Up")
                {
                    FontSizePx = 12,
                    UpdateCursorOnHover = true,
                    OnClick = _ => NavigateUp()
                },
                _currentPathLabel = new Label(Context, CurrentPath)
                {
                    FontSizePx = 12
                }
            ]
        };

        _filesSection = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = LiteralOrComputable.Percent(100),
            Padding = 5,
            Spacing = 10,
            Background = new ColoredPlane
            {
                Color = (0.1f, 0.1f, 0.1f, 1.0f)
            }
        };

        var files_scroll = new ScrollView(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100)
        };
        files_scroll.AddChild(_filesSection);

        if (saveFileName != null)
            _nameInput = new TextInput(context, saveFileName)
            {
                Width = 220,
                FontSizePx = 14,
                BorderRadius = 4,
                Background = new ColoredPlane { Color = InputColor }
            };

        var bottom_section = new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 44,
            Direction = LayoutDirection.Horizontal,
            VerticalAlign = Align.Center,
            Padding = 5,
            Spacing = 10,
            Background = new ColoredPlane
            {
                Color = (0.15f, 0.15f, 0.15f, 1.0f)
            }
        };
        if (_nameInput != null) bottom_section.AddChild(_nameInput);
        // Percent-width spacer soaks up the free space so the name input sits flush left
        // and the actions flush right - this framework has no space-between align.
        bottom_section.AddChild(new Panel(context) { Width = LiteralOrComputable.Percent(100) });
        bottom_section.AddChild(new Button(Context, "Cancel", new ColoredPlane { Color = CancelColor })
        {
            FontSizePx = 14,
            BorderRadius = 6,
            OnClick = _ => OnCancel?.Invoke(this)
        });
        bottom_section.AddChild(new Button(Context, confirmLabel, new ColoredPlane { Color = ConfirmColor })
        {
            FontSizePx = 14,
            BorderRadius = 6,
            OnClick = _ => OnSelect?.Invoke(this)
        });

        var main_layout = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100),
            Padding = 10,
            Spacing = 10,
            Background = new ColoredPlane
            {
                Color = (0.2f, 0.2f, 0.2f, 1.0f)
            },
            Children = [_topSection, files_scroll, bottom_section]
        };

        AddChild(main_layout);
        RefreshFiles();
    }

    public string CurrentPath { get; private set; } = Directory.GetCurrentDirectory();

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

    public Action<FileSelection>? OnSelectFile { get; set; }
    public Action<FileSelection>? OnChangeDirectory { get; set; }

    public Action<FileSelection>? OnCancel { get; set; }
    public Action<FileSelection>? OnSelect { get; set; }

    /// <summary>Marks a file as chosen - what clicking a row does.</summary>
    public void SelectFile(string path)
    {
        SelectedFile = path;
        _nameInput?.Value = Path.GetFileName(path);
        OnSelectFile?.Invoke(this);
    }

    private void NavigateUp()
    {
        var directory = new DirectoryInfo(CurrentPath);
        if (directory.Parent == null) return;

        NavigateTo(directory.Parent.FullName);
    }

    private void NavigateTo(string path)
    {
        if (!Directory.Exists(path)) return;
        CurrentPath = path;
        SelectedFile = null; // a pick from another directory must not survive navigation
        RefreshFiles();
        OnChangeDirectory?.Invoke(this);
    }

    private void UpdateCurrentPathLabel()
    {
        _currentPathLabel.SetTextContents(CurrentPath);
    }

    private void RefreshFiles()
    {
        UpdateCurrentPathLabel();
        var list = new List<UIElement>();

        try
        {
            var directories = Directory.GetDirectories(CurrentPath);
            list.AddRange(from directory in directories
                let dirInfo = new DirectoryInfo(directory)
                where (dirInfo.Attributes & FileAttributes.Hidden) == 0
                orderby dirInfo.Name
                select new Label(Context, $"{dirInfo.Name}/") // TODO emojis don't render with new label system
                {
                    FontSizePx = 14, UpdateCursorOnHover = true, OnClick = _ => NavigateTo(directory),
                    // After FontSizePx, which remeasures the label to its own text: a row has
                    // to be clickable across the list, not only on the glyphs of its name.
                    Width = LiteralOrComputable.Percent(100)
                });

            var files = Directory.GetFiles(CurrentPath)
                .Where(file => ExtensionFilter == null ||
                               file.EndsWith(ExtensionFilter, StringComparison.OrdinalIgnoreCase))
                .Order();
            list.AddRange(files.Select(file => new FileInfo(file)).Select(fileInfo =>
                new Label(Context, $"📄 {fileInfo.Name}")
                {
                    FontSizePx = 14,
                    UpdateCursorOnHover = true,
                    OnClick = _ => SelectFile(fileInfo.FullName),
                    Width = LiteralOrComputable.Percent(100)
                }));
        }
        catch (Exception ex)
        {
            var errorLabel = new Label(Context, $"Error: {ex.Message}")
            {
                FontSizePx = 14
            };
            list.Add(errorLabel);
        }

        _semaphore.Wait();
        foreach (var child in _filesSection.Children)
            if (child is Label label)
                label.StopRendering();

        _filesSection.Children = list;
        _semaphore.Release();
    }

    protected override void DrawSelf(UIContext context)
    {
        _semaphore.Wait();
        _semaphore.Release();
        // renders children only
    }

    protected override void DoLayout()
    {
        base.DoLayout();

        // A long CurrentPath auto-sizes past the header's bounds otherwise (Labels don't
        // wrap/truncate) - clip it to the header like ScrollView/TextInput self-clip.
        var x = (int)_topSection.Computed.AbsoluteX;
        var y = (int)_topSection.Computed.AbsoluteY;
        _topSection.ApplyClip(new Vector4i(x, y, x + (int)_topSection.Computed.Width,
            y + (int)_topSection.Computed.Height));
    }
}