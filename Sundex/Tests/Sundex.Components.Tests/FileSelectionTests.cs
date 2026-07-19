using Sundex.Components.File_Selector;

namespace Sundex.Components.Tests;

public class FileSelectionTests
{
    [Fact]
    public void TestFileSelectionInitialization()
    {
        var context = new TestUIContext();
        var fileSelection = new FileSelection(context);

        Assert.NotNull(fileSelection.CurrentPath);
        Assert.NotEmpty(fileSelection.Children);
    }

    [Fact]
    public void TestFileSelectionPathChange()
    {
        var context = new TestUIContext();
        var fileSelection = new FileSelection(context);
        var initialPath = fileSelection.CurrentPath;

        // Note: NavigateUp and NavigateTo use Task.Run, so they are asynchronous.

        Assert.Equal(initialPath, fileSelection.CurrentPath);
    }

    [Fact]
    public void OpenMode_SelectFile_ExposesThePath_AndNotifies()
    {
        var context = new TestUIContext();
        var selection = new FileSelection(context);
        FileSelection? notified = null;
        selection.OnSelectFile = s => notified = s;

        selection.SelectFile("/tmp/song.tdwproj");

        Assert.Equal("/tmp/song.tdwproj", selection.SelectedFile);
        Assert.Equal("/tmp/song.tdwproj", selection.SelectedPath);
        Assert.Same(selection, notified);
    }

    [Fact]
    public void SaveMode_TypedName_ResolvesInsideCurrentPath_AndAppendsTheExtension()
    {
        var context = new TestUIContext();
        var selection = new FileSelection(context, "song", ".tdwproj");

        Assert.Equal(Path.Combine(selection.CurrentPath, "song.tdwproj"), selection.SelectedPath);
    }

    [Fact]
    public void SaveMode_ClickingAFile_CopiesItsName()
    {
        var context = new TestUIContext();
        var selection = new FileSelection(context, "", ".tdw");

        Assert.Null(selection.SelectedPath); // empty name = nothing chosen yet

        selection.SelectFile("/somewhere/else/export.tdw");

        Assert.Equal(Path.Combine(selection.CurrentPath, "export.tdw"), selection.SelectedPath);
    }
}
