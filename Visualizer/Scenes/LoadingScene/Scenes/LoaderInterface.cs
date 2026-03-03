using Sundex.Components.Abstractions;
using Sundex.Components.Bars;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using LoadingScene.Reports;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Sunder.Markup;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Engine.Renderer.Cameras;

namespace LoadingScene.Scenes;

public class LoaderInterface
{
    public LoaderInterface(UIContext context, Camera camera, Action action)
    {
        var sundexContext = new SundexContext<LoaderInterface>(this, context);
        var componentSource = context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = "Scenes/Layout/LoaderInterface.snx.xml",
            }
        });
        
        var component = sundexContext.NewComponent(componentSource.Value);
        
        RootPanel = component.Element as Panel ?? throw new Exception("Root panel not found");
        /*
            Ideally the code down below will be in the logic block in the future and will be called with RunLogic?.Invoke().
            The only thing this class will have is actions that will be called / registered by the logic block.
         */
        ProgressBar = component.RegisteredIDs["loader-progress"] as ProgressBar ?? throw new Exception("Progress bar not found");
        Label = component.RegisteredIDs["loader-label"] as Label ?? throw new Exception("Label not found");
        
        var button = component.RegisteredIDs["start-button"] as Button ?? throw new Exception("Button not found");
        button.OnClick = _ => action();
        
        component.RunLogic?.Invoke();
        
        // queue to render each time context.Render() is called
        RootPanel.DrawTo(context);
    }
    
    public Panel RootPanel { get; }
    public ProgressBar ProgressBar { get; set; }
    public Label Label { get; set; }

    public void Resize()
    {
        RootPanel.InvalidateCoordinates();
        RootPanel.Layout();
    }

    public void Update(IProgressReport progressReport, UIContext context, MouseState mouseState, Vector2 scale)
    {
        RootPanel.Update(context);
        RootPanel.Test(mouseState, scale);
        Label.SetTextContents(progressReport.Message);
        ProgressBar.Progress = (float)progressReport.Percentage;
    }

    public void Render(DollarStoreCamera camera, UIContext context)
    {
        _ = camera;
        context.Render();
    }
}