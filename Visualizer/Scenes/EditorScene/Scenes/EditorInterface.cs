using JetBrains.Annotations;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using Sundex.Markup.Attributes;

namespace EditorScene.Scenes;

public class EditorInterface
{
    public EditorInterface(UIContext context, Action back)
    {
        OnBack = back;

        var sundexContext = new SundexContext(context);
        var componentSource = context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo { Location = "Scenes/Layout/EditorInterface.snx.xml" }
        });

        Component = sundexContext.NewComponent(componentSource.Value);
        sundexContext.RunLogicAndVerify(Component, () => RootPanel);

        RootPanel.DrawTo(context);
        RootPanel.InvalidateCoordinates();
        RootPanel.Layout();
    }

    public Action OnBack { get; }
    [UsedImplicitly] public SundexComponent Component { get; }
    [SetFromLogic] public Panel RootPanel { get; set; } = null!;

    public void Resize()
    {
        RootPanel.InvalidateCoordinates();
        RootPanel.Layout();
    }

    public void Update(UIContext context)
    {
        RootPanel.Update(context);
        RootPanel.Layout();
    }

    public void MouseEvent(MouseState mouseState, Vector2 scale)
    {
        RootPanel.Test(mouseState, scale);
    }
}
