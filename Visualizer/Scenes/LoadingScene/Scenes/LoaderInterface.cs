using JetBrains.Annotations;
using LoadingScene.Reports;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Markup;
using Sundex.Markup.Attributes;
using Sundex.Components.Abstractions;
using Sundex.Components.Bars;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;

namespace LoadingScene.Scenes;

public class LoaderInterface
{
    public LoaderInterface(UIContext context, Action action)
    {
        OnClickAction = action;
        
        var sundexContext = new SundexContext(context);
        var componentSource = context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = "Scenes/Layout/LoaderInterface.snx.xml"
            }
        });
        
        Component = sundexContext.NewComponent(componentSource.Value);
        Component.RunLogic?.Invoke(this);

        sundexContext.RunLogicAndVerify(Component, () => RootPanel, () => ProgressBar, () => Label);
        RootPanel.DrawTo(context);
    }

    public Action OnClickAction { get; }

    [UsedImplicitly]
    public SundexComponent Component { get; }
    
    [SetFromLogic]
    public Panel RootPanel { get; set; } = null!;
    
    [SetFromLogic]
    public ProgressBar ProgressBar { get; set; } = null!;
    
    [SetFromLogic]
    public Label Label { get; set; } = null!;

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
        RootPanel.Layout();
    }
}