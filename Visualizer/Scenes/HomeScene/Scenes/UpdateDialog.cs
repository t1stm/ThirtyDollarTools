using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Updates;
using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Markdown;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management.Extensions;
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
        var sundex = new SundexContext(context);
        sundex.RegisterElementFactory("clipped-label", ui => new ClippedLabel(ui, ""));
        var component = sundex.NewComponent(context.AssetProvider.LoadString("Scenes/Layout/UpdateDialog.snx.xml"));

        var newest = releases[0];
        var frame = component.GetID<FlexPanel>("update-dialog");
        var title = component.GetID<ClippedLabel>("update-title");
        var subtitle = component.GetID<Label>("update-subtitle");
        var close = component.GetID<Button>("close-button");
        title.SetTextContents(ReleaseNotes.Title(newest));
        subtitle.SetTextContents(ReleaseNotes.Subtitle(newest));

        var header = (FlexPanel)close.Parent!;
        component.GetID<FlexPanel>("update-title-clip").Width = Math.Min(title.Width.Value,
            frame.Width.Value - 2 * frame.Padding - subtitle.Width.Value - close.Width.Value - 3 * header.Spacing);

        component.GetID<Label>("update-your-tag").SetTextContents(build.Tag);
        component.GetID<Label>("update-newest-tag").SetTextContents(newest.TagName);
        component.GetID<Label>("update-gain").SetTextContents(ReleaseNotes.Gain(releases, build));

        var tiles = component.GetID<FlexPanel>("update-tiles");
        tiles.AddChild(new Panel(context) { Classes = ["update-tile"] });
        for (var i = 0; i < Math.Min(releases.Count, MaxLitTiles); i++)
            tiles.AddChild(new Panel(context) { Classes = ["update-tile", "update-tile-lit"] });

        Notes = component.GetID<MarkdownView>("update-notes");
        var empty = component.GetID<FlexPanel>("update-empty");
        if (releases.All(release => string.IsNullOrWhiteSpace(release.Body)))
        {
            frame.RemoveChild(Notes);
            component.GetID<Label>("update-empty-label").SetTextContents(ReleaseNotes.NoNotes);
        }
        else
        {
            frame.RemoveChild(empty);
            frame.SetClass("update-dialog-notes", true);
            Notes.Markdown = ReleaseNotes.Combine(releases);
        }

        var releasePage = component.GetID<Button>("update-release-page");
        var later = KeyChip.Add(component.GetID<Button>("update-later"), "Esc");
        var open = KeyChip.Add(component.GetID<Button>("update-open"), "Enter");
        releasePage.Children =
        [
            new Label(context, Icons.ExternalLink, Icons.Font) { Classes = ["update-release-icon"] },
            releasePage.Label
        ];

        Modal = new ModalLayer(context)
        {
            OnDismissRequested = _ => Close(),
            OnConfirmRequested = _ => Link.Open(downloadPage),
            OnKeyRequested = HandleKey
        };
        Modal.AddChild(frame);

        close.OnClick = _ => Close();
        later.OnClick = _ => Close();
        open.OnClick = _ => Link.Open(downloadPage);
        // The address comes from the response, so it has to prove it is a web page before the shell sees it.
        releasePage.OnClick = _ =>
        {
            if (MarkdownParser.TryWebUrl(newest.HtmlUrl, out var url)) Link.Open(url);
        };
    }

    public ModalLayer Modal { get; }

    public bool IsOpen => Modal.Parent != null;

    /// <summary>The notes, which the keys and the wheel scroll.</summary>
    public MarkdownView Notes { get; }

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
