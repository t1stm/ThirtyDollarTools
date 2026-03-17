using JetBrains.Annotations;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Markup;
using Sundex.Markup.Attributes;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;

namespace HomeScene.Scenes;

public class HomeInterface
{
    public HomeInterface(UIContext context, Action visualizer, Action drumMaster, Action editor, Action settings)
    {
        var sundexContext = new SundexContext(context);
        var componentSource = context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = "Scenes/Layout/HomeInterface.snx.xml"
            }
        });

        OnVisualizer = visualizer;
        OnEditor = editor;
        OnDrumMaster = drumMaster;
        OnSettings = settings;

        Component = sundexContext.NewComponent(componentSource.Value);
        sundexContext.RunLogicAndVerify(Component, () => RootPanel);
        RootPanel.DrawTo(context);
    }

    public Action OnVisualizer { get; }
    public Action OnEditor { get; }
    public Action OnDrumMaster { get; }
    public Action OnSettings { get; }

    [UsedImplicitly]
    public SundexComponent Component { get; }
    
    [SetFromLogic]
    public Panel RootPanel { get; set; } = null!;

    public void Resize()
    {
        RootPanel.InvalidateCoordinates();
        RootPanel.Layout();
    }

    public void Update(UIContext context, MouseState mouseState, Vector2 scale)
    {
        RootPanel.Update(context);
        RootPanel.Test(mouseState, scale);
        RootPanel.Layout();
    }
}
