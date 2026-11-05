using HomeScene.Scenes;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Updates;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Markdown;
using Sundex.Components.Panels;

namespace HomeScene.Tests;

/// <summary>The dialog's document against the real sheets, mounted the way Home mounts it.</summary>
public class UpdateDialogTests
{
    private static readonly DateTimeOffset Date = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
    private static readonly VersionInfo Build = new("v2.0.0-rc5", null, false, false, Date.AddDays(-72));

    private readonly HomeTestContext _context = new();

    private static GitHubRelease Release(string tag, string? body, int age = 0)
    {
        return new GitHubRelease(tag, $"https://example.invalid/{tag}", false, false, Date.AddDays(-age),
            $"Name of {tag}", body);
    }

    private (UpdateDialog Dialog, HomeInterface Home) Mounted(params GitHubRelease[] releases)
    {
        var home = new HomeInterface(_context, () => { }, () => { }, () => { }, () => { });
        var dialog = new UpdateDialog(_context, releases, Build, "https://example.invalid/download");
        home.RootPanel.AddChild(dialog.Modal);
        home.RootPanel.Layout();
        return (dialog, home);
    }

    private static IEnumerable<UIElement> Descendants(UIElement element)
    {
        yield return element;
        if (element is not Panel panel) yield break;
        foreach (var descendant in panel.Children.SelectMany(Descendants)) yield return descendant;
    }

    private static Button ButtonLabelled(UIElement root, string text)
    {
        return Descendants(root).OfType<Button>().Single(button => button.Label.Value.TrimEnd('\0').SequenceEqual(text));
    }

    private static KeyboardKeyEventArgs Key(Keys key)
    {
        return new KeyboardKeyEventArgs(key, 0, default, false);
    }

    [Fact]
    public void Frame_FitsTheMinimumWindow()
    {
        var (dialog, _) = Mounted(Release("v2", "notes"));
        var frame = dialog.Modal.Children[0];

        Assert.Equal(680, frame.Computed.Width);
        Assert.Equal(640, frame.Computed.Height);
    }

    [Fact]
    public void Header_NamesTheNewestRelease()
    {
        var (dialog, _) = Mounted(Release("v3", "new"), Release("v2", "old", 5));
        var labels = Descendants(dialog.Modal).OfType<Label>().Select(label => label.Value.TrimEnd('\0').ToString()).ToList();

        Assert.Contains("Name of v3", labels);
        Assert.Contains("v3 · 21 Oct 2026", labels);
        Assert.Contains("v2.0.0-rc5", labels);
        Assert.Contains("2 releases, 72 days newer", labels);
    }

    [Fact]
    public void BuildCard_OneDimTileAndOneLitPerRelease()
    {
        var (dialog, _) = Mounted(Release("v3", "new"), Release("v2", "old", 5));
        var tiles = Descendants(dialog.Modal).Where(element => element.Classes.Contains("update-tile")).ToList();

        Assert.Equal(3, tiles.Count);
        Assert.Equal(2, tiles.Count(tile => tile.Classes.Contains("update-tile-lit")));
    }

    [Fact]
    public void BuildCard_ManyReleasesStayInsideTheCard()
    {
        var releases = Enumerable.Range(0, 30).Select(i => Release($"v{i}", "n", i)).ToArray();
        var (dialog, _) = Mounted(releases);

        var tiles = Descendants(dialog.Modal).Single(element => element.ID == "update-tiles");
        Assert.True(tiles.Computed.Width <= tiles.Parent!.Computed.Width);
    }

    [Fact]
    public void Notes_OnlyOlderReleasesGetAHead()
    {
        var (dialog, _) = Mounted(Release("v3", "new"), Release("v2", "old", 5));
        var labels = Descendants(dialog.Notes).OfType<Label>().Select(label => label.Value.TrimEnd('\0').ToString()).ToList();

        Assert.Contains("Name of v2", labels);
        Assert.DoesNotContain("Name of v3", labels);
    }

    [Fact]
    public void Notes_TakeTheirStylesFromTheRootsSheet()
    {
        var (dialog, _) = Mounted(Release("v2", "plain words"));
        var paragraph = Assert.IsType<Label>(dialog.Notes.Children[0]);

        Assert.Equal(14, paragraph.FontSizePx.Value);
        Assert.True(paragraph.WrapWidth > 0);
    }

    [Fact]
    public void Buttons_KeepTheirOwnWidth()
    {
        // Home's own button rule is on its one button, so it can't reach these.
        var (dialog, _) = Mounted(Release("v2", "notes"));
        var open = ButtonLabelled(dialog.Modal, "Open download page");

        Assert.NotEqual(104, open.Computed.Width);
        Assert.Equal(34, open.Computed.Height);
    }

    [Fact]
    public void ReleasePage_HasItsIconBeforeTheLabel()
    {
        var (dialog, _) = Mounted(Release("v2", "notes"));
        var button = ButtonLabelled(dialog.Modal, "Release page");

        Assert.Equal(2, button.Children.Count);
        Assert.Same(button.Label, button.Children[1]);
    }

    [Fact]
    public void Later_Closes()
    {
        var (dialog, home) = Mounted(Release("v2", "notes"));
        var later = ButtonLabelled(dialog.Modal, "Later");

        later.OnClick!(later);

        Assert.False(dialog.IsOpen);
        Assert.DoesNotContain(dialog.Modal, home.RootPanel.Children);
    }

    [Fact]
    public void Backdrop_Closes()
    {
        var (dialog, _) = Mounted(Release("v2", "notes"));

        dialog.Modal.OnDismissRequested!(dialog.Modal);

        Assert.False(dialog.IsOpen);
    }

    [Fact]
    public void Reopening_KeepsTheScrollAndFetchesNothing()
    {
        var body = string.Join("\n\n", Enumerable.Repeat("para", 200)) + "\n\n![a](http://127.0.0.1:1/a.png)";
        var (dialog, home) = Mounted(Release("v2", body));
        home.RootPanel.Layout();

        var image = Descendants(dialog.Notes).OfType<Sundex.Components.Panels.Image>().Single();
        dialog.Notes.ScrollY = 500;
        dialog.Modal.OnDismissRequested!(dialog.Modal);

        home.RootPanel.AddChild(dialog.Modal);
        home.RootPanel.Layout();

        Assert.Equal(500, dialog.Notes.ScrollY);
        Assert.Equal(1, image.LoadCount);
    }

    [Fact]
    public void Keys_ScrollTheNotes()
    {
        var body = string.Join("\n\n", Enumerable.Repeat("para", 200));
        var (dialog, home) = Mounted(Release("v2", body));
        home.RootPanel.Layout();
        var notes = dialog.Notes;

        Assert.True(notes.MaxScroll > 0);

        Assert.True(dialog.Modal.HandleKeyDown(Key(Keys.Down)));
        Assert.Equal(notes.WheelSpeed, notes.ScrollY);

        Assert.True(dialog.Modal.HandleKeyDown(Key(Keys.Up)));
        Assert.Equal(0, notes.ScrollY);

        Assert.True(dialog.Modal.HandleKeyDown(Key(Keys.PageDown)));
        Assert.Equal(notes.Computed.Height, notes.ScrollY);

        Assert.True(dialog.Modal.HandleKeyDown(Key(Keys.End)));
        Assert.Equal(notes.MaxScroll, notes.ScrollY);

        Assert.True(dialog.Modal.HandleKeyDown(Key(Keys.Home)));
        Assert.Equal(0, notes.ScrollY);

        Assert.False(dialog.Modal.HandleKeyDown(Key(Keys.A)));
    }

    [Fact]
    public void NoNotes_ShrinksTheFrameAroundOneLine()
    {
        var (dialog, _) = Mounted(Release("v2", " "));
        var frame = dialog.Modal.Children[0];

        Assert.Empty(Descendants(frame).OfType<MarkdownView>());
        Assert.Contains(Descendants(frame).OfType<Label>(), label => label.Value.TrimEnd('\0').SequenceEqual(ReleaseNotes.NoNotes));
        Assert.True(frame.Computed.Height < 640);
    }

    [Fact]
    public void OneReleaseOfSeveralWithNoNotes_SaysSoInPlace()
    {
        var (dialog, _) = Mounted(Release("v3", ""), Release("v2", "old", 5));

        Assert.Contains(Descendants(dialog.Notes).OfType<Label>(), label => label.Value.TrimEnd('\0').SequenceEqual(ReleaseNotes.NoNotes));
    }
}
