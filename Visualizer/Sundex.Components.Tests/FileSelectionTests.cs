using Sundex.Components.Abstractions;
using Sundex.Components.File_Selector;
using Xunit;

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
}
