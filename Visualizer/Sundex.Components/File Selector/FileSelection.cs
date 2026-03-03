using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace Sundex.Components.File_Selector;

public sealed class FileSelection : Panel
{
    private readonly Label _currentPathLabel;
    private readonly FlexPanel _filesSection;
    private readonly FlexPanel _mainLayout;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public FileSelection(UIContext context) : base(context)
    {
        var top_section = new FlexPanel(context)
        {
            Direction = LayoutDirection.Horizontal,
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
                    FontSizePx = 12,
                    UpdateCursorOnHover = true
                }
            ]
        };

        _filesSection = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Padding = 5,
            Spacing = 10,
            Background = new ColoredPlane
            {
                Color = (0.1f, 0.1f, 0.1f, 1.0f)
            },
            ScrollOnOverflow = true
        };

        var bottom_section = new FlexPanel(context)
        {
            Height = 34,
            Direction = LayoutDirection.Horizontal,
            VerticalAlign = Align.Center,
            HorizontalAlign = Align.End,
            Padding = 5,
            Spacing = 10,
            Background = new ColoredPlane
            {
                Color = (0.15f, 0.15f, 0.15f, 1.0f)
            },
            Children =
            [
                new Button(Context, "Select")
                {
                    FontSizePx = 14,
                    UpdateCursorOnHover = true,
                    OnClick = _ => OnSelect?.Invoke(this)
                },
                new Button(Context, "Cancel")
                {
                    FontSizePx = 14,
                    UpdateCursorOnHover = true,
                    OnClick = _ => OnCancel?.Invoke(this)
                }
            ]
        };

        _mainLayout = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Padding = 10,
            Spacing = 10,
            Background = new ColoredPlane
            {
                Color = (0.2f, 0.2f, 0.2f, 1.0f)
            },
            Children = [top_section, _filesSection, bottom_section]
        };

        var window = new WindowFrame(context)
        {
            Child = _mainLayout,
            Resizable = true
        };

        AddChild(window);
        Task.Run(RefreshFiles);
    }

    public string CurrentPath { get; private set; } = Directory.GetCurrentDirectory();
    public Action<FileSelection>? OnSelectFile { get; set; }
    public Action<FileSelection>? OnChangeDirectory { get; set; }

    public Action<FileSelection>? OnCancel { get; set; }
    public Action<FileSelection>? OnSelect { get; set; }

    private void NavigateUp()
    {
        var directory = new DirectoryInfo(CurrentPath);
        if (directory.Parent == null) return;

        CurrentPath = directory.Parent.FullName;
        Task.Run(RefreshFiles);
        OnChangeDirectory?.Invoke(this);
    }

    private void NavigateTo(string path)
    {
        if (!Directory.Exists(path)) return;
        CurrentPath = path;
        Task.Run(RefreshFiles);
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
                select new Label(Context, $"📁 {dirInfo.Name}")
                    { FontSizePx = 14, UpdateCursorOnHover = true, OnClick = _ => NavigateTo(directory) });

            var files = Directory.GetFiles(CurrentPath);
            list.AddRange(files.Select(file => new FileInfo(file)).Select(fileInfo =>
                new Label(Context, $"📄 {fileInfo.Name}")
                {
                    FontSizePx = 14, UpdateCursorOnHover = true, OnClick = _ => { OnSelectFile?.Invoke(this); }
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
        _filesSection.Children = list;
        _semaphore.Release();
    }

    protected override void DrawSelf(UIContext context)
    {
        _semaphore.Wait();
        _semaphore.Release();
        // renders children only
    }
}