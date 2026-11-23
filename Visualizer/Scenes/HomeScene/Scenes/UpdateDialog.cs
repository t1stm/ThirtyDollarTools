using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Updates;
using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Markdown;
using Sundex.Components.Panels;
using Sundex.Markup;

namespace HomeScene.Scenes;

/// <summary>
///     What clicking Home's update note opens: the notes of every release since this build, and
///     the way to the download page. The owner mounts <see cref="Modal" /> on its root; everything
///     that closes it takes it off again, and the same tree goes back up on the next open.
/// </summary>
public sealed class UpdateDialog
{
    /// <summary>As many tiles as fit between the build card's two tags. The line under them has the real count.</summary>
    private const int MaxLitTiles = 20;

    public UpdateDialog(UIContext context, IReadOnlyList<GitHubRelease> releases, VersionInfo build, string downloadPage)
    {
        new Markup.UpdateDialog(new SundexContext<UpdateDialog>(context, this)).Create();

        var newest = releases[0];
        Title.SetTextContents(ReleaseNotes.Title(newest));
        Subtitle.SetTextContents(ReleaseNotes.Subtitle(newest));

        var header = (FlexPanel)CloseButton.Parent!;
        // Labels size themselves to their text (their Width is auto): measure them.
        TitleClip.Width = Math.Min(Title.Measure(float.MaxValue, float.MaxValue).width,
            Frame.Width.Value - 2 * Frame.Padding - Subtitle.Measure(float.MaxValue, float.MaxValue).width -
            CloseButton.Width.Value - 3 * header.Spacing);

        YourTag.SetTextContents(build.Tag);
        NewestTag.SetTextContents(newest.TagName);
        Gain.SetTextContents(ReleaseNotes.Gain(releases, build));

        Tiles.AddChild(new Panel(context) { Classes = ["update-tile"] });
        for (var i = 0; i < Math.Min(releases.Count, MaxLitTiles); i++)
            Tiles.AddChild(new Panel(context) { Classes = ["update-tile", "update-tile-lit"] });

        if (releases.All(release => string.IsNullOrWhiteSpace(release.Body)))
        {
            Frame.RemoveChild(Notes);
            EmptyLabel.SetTextContents(ReleaseNotes.NoNotes);
        }
        else
        {
            Frame.RemoveChild(Empty);
            Frame.SetClass("update-dialog-notes", true);
            Notes.Markdown = ReleaseNotes.Combine(releases);
        }

        var later = KeyChip.Add(Later, "Esc");
        var open = KeyChip.Add(Open, "Enter");
        ReleasePage.Children =
        [
            new Label(context, Icons.ExternalLink, Icons.Font) { Classes = ["update-release-icon"] },
            ReleasePage.Label
        ];

        Modal = new ModalLayer(context)
        {
            AutoFocusContent = false,
            OnDismissRequested = _ => Close(),
            OnConfirmRequested = _ => Link.Open(downloadPage),
            OnKeyRequested = HandleKey
        };
        Modal.AddChild(Frame);

        CloseButton.OnClick = _ => Close();
        later.OnClick = _ => Close();
        open.OnClick = _ => Link.Open(downloadPage);
        // The address comes from the response, so it has to prove it is a web page before the shell sees it.
        ReleasePage.OnClick = _ =>
        {
            if (MarkdownParser.TryWebUrl(newest.HtmlUrl, out var url)) Link.Open(url);
        };
    }

    public ModalLayer Modal { get; }

    public bool IsOpen => Modal.Parent != null;

    /// <summary>The notes, which the keys and the wheel scroll.</summary>
    public MarkdownView Notes { get; set; } = null!;

    public FlexPanel Frame { get; set; } = null!;
    public FlexPanel TitleClip { get; set; } = null!;
    public ClippedLabel Title { get; set; } = null!;
    public Label Subtitle { get; set; } = null!;
    public Button CloseButton { get; set; } = null!;
    public Label YourTag { get; set; } = null!;
    public FlexPanel Tiles { get; set; } = null!;
    public Label Gain { get; set; } = null!;
    public Label NewestTag { get; set; } = null!;
    public FlexPanel Empty { get; set; } = null!;
    public Label EmptyLabel { get; set; } = null!;
    public Button ReleasePage { get; set; } = null!;
    public Button Later { get; set; } = null!;
    public Button Open { get; set; } = null!;

    private void Close()
    {
        (Modal.Parent as Panel)?.RemoveChild(Modal);
    }

    private bool HandleKey(KeyboardKeyEventArgs e)
    {
        switch (e.Key)
        {
            case Keys.Up:
                Notes.ScrollY -= Notes.WheelSpeed;
                return true;
            case Keys.Down:
                Notes.ScrollY += Notes.WheelSpeed;
                return true;
            case Keys.PageUp:
                Notes.ScrollY -= Notes.Computed.Height;
                return true;
            case Keys.PageDown:
                Notes.ScrollY += Notes.Computed.Height;
                return true;
            case Keys.Home:
                Notes.ScrollY = 0;
                return true;
            case Keys.End:
                Notes.ScrollY = Notes.MaxScroll;
                return true;
            default:
                return false;
        }
    }
}
