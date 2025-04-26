using OpenTK.Mathematics;
using SixLabors.Fonts;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.UI.Components.File_Selector;

public sealed class FileSelection : Panel
{
    private readonly FlexPanel _mainLayout;
    private readonly FlexPanel _filesSection;
    private readonly Label _currentPathLabel;

    public string CurrentPath { get; private set; } = Directory.GetCurrentDirectory();
    public Action? OnSelectFile { get; set; }
    public Action? OnChangeDirectory { get; set; }
    
    public Action? OnCancel { get; set; }
    public Action? OnSelect { get; set; }

    public FileSelection() : this(0, 0, 600, 400)
    {
    }

    public FileSelection(float x, float y, float width, float height) : base(x, y, width, height)
    {
        var top_section = new FlexPanel(0, 0, 0, 30)
        {
            Direction = LayoutDirection.Horizontal,
            Padding = 5,
            Spacing = 10,
            VerticalAlign = Align.Center,
            AutoWidth = true,
            Background = new ColoredPlane(new Vector4(0.15f, 0.15f, 0.15f, 1.0f)),
            Children =
            [
                new Label("↑ Up")
                {
                    FontSizePx = 12,
                    UpdateCursorOnHover = true,
                    OnClick = _ => NavigateUp(),
                    FontStyle = FontStyle.Bold
                },
                _currentPathLabel = new Label(CurrentPath, LabelMode.CachedDynamic)
                {
                    FontSizePx = 12,
                    AutoWidth = true,
                    FontStyle = FontStyle.Bold
                }
            ]
        };
        
        _filesSection = new FlexPanel(0, 0, 0, 0)
        {
            Direction = LayoutDirection.Vertical,
            Padding = 5,
            Spacing = 10,
            AutoWidth = true,
            AutoHeight = true,
            Background = new ColoredPlane(new Vector4(0.1f, 0.1f, 0.1f, 1.0f))
        };

        var bottom_section = new FlexPanel(0, 0, 0, 30)
        {
            Direction = LayoutDirection.Horizontal,
            HorizontalAlign = Align.End,
            VerticalAlign = Align.Center,
            Padding = 5,
            Spacing = 10,
            AutoWidth = true,
            Background = new ColoredPlane(new Vector4(0.15f, 0.15f, 0.15f, 1.0f)),
            Children = [
                new Button("Select")
                {
                    FontSizePx = 14,
                    UpdateCursorOnHover = true,
                    OnClick = _ => OnSelect?.Invoke()
                },
                new Button("Cancel")
                {
                    FontSizePx = 14,
                    UpdateCursorOnHover = true,
                    OnClick = _ => OnCancel?.Invoke()
                }
            ]
        };

        _mainLayout = new FlexPanel(0, 0, width, height)
        {
            Direction = LayoutDirection.Vertical,
            Padding = 10,
            Spacing = 10,
            Background = new ColoredPlane(new Vector4(0.2f, 0.2f, 0.2f, 1.0f)),
            Children = [top_section, _filesSection, bottom_section]
        };

        AddChild(_mainLayout);
        RefreshFiles();
        Layout();
    }

    private void NavigateUp()
    {
        var directory = new DirectoryInfo(CurrentPath);
        if (directory.Parent == null) return;

        CurrentPath = directory.Parent.FullName;
        RefreshFiles();
        OnChangeDirectory?.Invoke();
    }

    private void NavigateTo(string path)
    {
        if (!Directory.Exists(path)) return;
        CurrentPath = path;
        RefreshFiles();
        OnChangeDirectory?.Invoke();
    }

    private void UpdateCurrentPathLabel()
    {
        const int max_separators = 5;
        var split = CurrentPath.Split(Path.DirectorySeparatorChar).TakeLast(max_separators);
        _currentPathLabel.SetTextContents(string.Join(Path.DirectorySeparatorChar, split));
    }
    
    private void RefreshFiles()
    {
        UpdateCurrentPathLabel();
        _filesSection.Children.Clear();

        try
        {
            var directories = Directory.GetDirectories(CurrentPath);
            foreach (var directory in directories)
            {
                var dirInfo = new DirectoryInfo(directory);
                if ((dirInfo.Attributes & FileAttributes.Hidden) != 0) continue;
                
                var dirLabel = new Label($"📁 {dirInfo.Name}", LabelMode.CachedDynamic)
                {
                    FontSizePx = 14,
                    UpdateCursorOnHover = true,
                    OnClick = _ => NavigateTo(directory)
                };

                _filesSection.AddChild(dirLabel);
            }
            
            var files = Directory.GetFiles(CurrentPath);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var fileLabel = new Label($"📄 {fileInfo.Name}", LabelMode.CachedDynamic)
                {
                    FontSizePx = 14,
                    UpdateCursorOnHover = true,
                    OnClick = _ => { OnSelectFile?.Invoke(); }
                };

                _filesSection.AddChild(fileLabel);
            }
        }
        catch (Exception ex)
        {
            var errorLabel = new Label($"Error: {ex.Message}", LabelMode.CachedDynamic)
            {
                FontSizePx = 14
            };
            _filesSection.AddChild(errorLabel);
        }

        _filesSection.Layout();
        _mainLayout.Layout();
    }

    protected override void DrawSelf(UIContext context)
    {
        // renders children only
    }
}