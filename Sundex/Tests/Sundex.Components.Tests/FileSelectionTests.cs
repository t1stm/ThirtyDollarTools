using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.File_Selector;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;

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
    public void OpensInTheGivenDirectory_OrHomeWhenThereIsNone()
    {
        var context = new TestUIContext();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var given = Directory.CreateTempSubdirectory().FullName;

        try
        {
            Assert.Equal(given, new FileSelection(context, directory: given).CurrentPath);
            Assert.Equal(home, new FileSelection(context).CurrentPath);
            Assert.Equal(home, new FileSelection(context, directory: Path.Combine(given, "gone")).CurrentPath);
        }
        finally
        {
            Directory.Delete(given);
        }
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

    [Fact]
    public void SaveMode_LeavesTheExtensionOutOfTheNameField()
    {
        var selection = new FileSelection(new TestUIContext(), "song.tdwproj", ".tdwproj");
        var field = Descendants(selection).OfType<TextInput>().Single();

        Assert.Equal("song", field.Value.ToString());
        Assert.Equal(Path.Combine(selection.CurrentPath, "song.tdwproj"), selection.SelectedPath);
    }

    [Fact]
    public void SaveMode_ANameThatIsAlreadyThere_TurnsTheConfirmIntoReplace()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "taken.tdw"), "");
            var selection = new FileSelection(new TestUIContext(), "fresh", ".tdw", "Export", folder);
            Assert.False(selection.Overwrites);
            Assert.Equal("Export", selection.ConfirmButton.Value.ToString());

            selection.SelectFile(Path.Combine(folder, "taken.tdw"));

            Assert.True(selection.Overwrites);
            Assert.Equal("Replace", selection.ConfirmButton.Value.ToString());
            Assert.Contains(Descendants(selection).OfType<Label>(),
                label => label.Value.ToString() == "“taken.tdw” is already here. Exporting replaces it.");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void DoublePressingAFile_Confirms()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "song.tdwproj"), "");
            var selection = new FileSelection(new TestUIContext(), extensionFilter: ".tdwproj", directory: folder);
            var confirmed = 0;
            selection.OnSelect = _ => confirmed++;

            var row = Descendants(selection).OfType<Label>().Single(label => label.Value.ToString() == "song.tdwproj")
                .Parent!.Parent!;
            row.OnClick!(row);
            Assert.True(row.HandleDoublePress(0, 0));

            Assert.Equal(1, confirmed);
            Assert.Equal(Path.Combine(folder, "song.tdwproj"), selection.SelectedPath);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Crumbs_StartAtHomeUnderIt_AndAtTheRootElsewhere()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var under = FileSelection.CrumbsFor(Path.Combine(home, "Music", "Covers"));
        Assert.Equal(["Home", "Music", "Covers"], under.Select(crumb => crumb.Label));
        Assert.True(under[0].Home);
        Assert.Equal(Path.Combine(home, "Music"), under[1].Path);

        var outside = FileSelection.CrumbsFor(Path.Combine(Path.GetTempPath(), "x"));
        Assert.False(outside[0].Home);
        Assert.Equal(Path.GetPathRoot(Path.GetTempPath()), outside[0].Path);
    }

    private static KeyboardKeyEventArgs Key(Keys key, KeyModifiers modifiers = 0) => new(key, 0, modifiers, false);

    /// <summary>A folder holding <c>a/</c>, <c>b/</c> and <c>song.tdwproj</c>.</summary>
    private static string Folder()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        Directory.CreateDirectory(Path.Combine(folder, "a"));
        Directory.CreateDirectory(Path.Combine(folder, "b"));
        File.WriteAllText(Path.Combine(folder, "song.tdwproj"), "");
        return folder;
    }

    [Fact]
    public void ArrowKeys_MarkFolders_AndPickFilesAsTheyLandOnThem()
    {
        var folder = Folder();
        try
        {
            var selection = new FileSelection(new TestUIContext(), extensionFilter: ".tdwproj", directory: folder);

            Assert.True(selection.HandleKeyDown(Key(Keys.Down))); // a/
            Assert.Null(selection.SelectedFile);
            selection.HandleKeyDown(Key(Keys.End)); // song.tdwproj
            Assert.Equal(Path.Combine(folder, "song.tdwproj"), selection.SelectedFile);
            selection.HandleKeyDown(Key(Keys.Up)); // b/: Open mustn't still pick the file
            Assert.Null(selection.SelectedPath);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Enter_OpensTheMarkedFolder_AndBackspaceComesBackToIt()
    {
        var folder = Folder();
        try
        {
            var selection = new FileSelection(new TestUIContext(), extensionFilter: ".tdwproj", directory: folder);
            var confirmed = 0;
            selection.OnSelect = _ => confirmed++;

            selection.HandleKeyDown(Key(Keys.Down));
            selection.HandleKeyDown(Key(Keys.Down)); // b/
            selection.HandleKeyDown(Key(Keys.Enter));
            Assert.Equal(Path.Combine(folder, "b"), selection.CurrentPath);
            Assert.Equal(0, confirmed);

            selection.HandleKeyDown(Key(Keys.Backspace));
            Assert.Equal(folder, selection.CurrentPath);

            // Back on b/, so one more step down lands on the file, and Enter confirms it.
            selection.HandleKeyDown(Key(Keys.Down));
            selection.HandleKeyDown(Key(Keys.Enter));
            Assert.Equal(1, confirmed);
            Assert.Equal(Path.Combine(folder, "song.tdwproj"), selection.SelectedPath);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void MovingPastTheBottom_ScrollsTheRowIntoView_AndLeavesTheListLaidOut()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            for (var i = 0; i < 30; i++) Directory.CreateDirectory(Path.Combine(folder, $"d{i:00}"));
            var selection = new FileSelection(new TestUIContext(), directory: folder) { Width = 640, Height = 472 };
            selection.Layout();
            var list = Descendants(selection).OfType<ScrollView>().Single();
            list.Height = 300; // unstyled, so the list has no height of its own
            selection.InvalidateLayout();
            selection.Layout();

            selection.HandleKeyDown(Key(Keys.End));
            selection.Layout();

            Assert.Equal(list.MaxScroll, list.ScrollY);
            // Scrolling inside the dialog's own layout once left the list marked for a layout
            // nothing would run, so the highlight on later rows never showed.
            Assert.False(list.NeedsLayout);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void TypingAPath_OpensIt_AndAPathToNothingStaysInTheField()
    {
        var folder = Folder();
        try
        {
            var context = new TestUIContext();
            var selection = new FileSelection(context, extensionFilter: ".tdwproj", directory: folder);

            Assert.True(selection.HandleKeyDown(Key(Keys.L, KeyModifiers.Control)));
            var field = Descendants(selection).OfType<TextInput>().Single();
            Assert.Same(field, context.FocusedElement);
            Assert.Equal(folder, field.Value);

            field.Value = Path.Combine(folder, "nowhere");
            field.OnCommit!(field);
            context.Blur(); // what the field does after its commit
            Assert.Equal(folder, selection.CurrentPath);
            Assert.Same(field, context.FocusedElement);
            Assert.Contains("file-path-input-invalid", field.Classes);

            field.Value = "a"; // relative to the current folder
            field.OnCommit!(field);
            context.Blur();
            Assert.Equal(Path.Combine(folder, "a"), selection.CurrentPath);
            Assert.Empty(Descendants(selection).OfType<TextInput>()); // the crumbs are back
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void TypingAFilesPath_OpensItsFolder_AndPicksIt()
    {
        var folder = Folder();
        try
        {
            var context = new TestUIContext();
            var selection = new FileSelection(context, extensionFilter: ".tdwproj");
            selection.HandleKeyDown(Key(Keys.L, KeyModifiers.Control));
            var field = Descendants(selection).OfType<TextInput>().Single();

            field.Value = Path.Combine(folder, "song.tdwproj");
            field.OnCommit!(field);

            Assert.Equal(folder, selection.CurrentPath);
            Assert.Equal(Path.Combine(folder, "song.tdwproj"), selection.SelectedPath);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private static IEnumerable<UIElement> Descendants(UIElement element)
    {
        yield return element;
        if (element is not Panel panel) yield break;
        foreach (var child in panel.Children)
        foreach (var descendant in Descendants(child))
            yield return descendant;
    }
}
