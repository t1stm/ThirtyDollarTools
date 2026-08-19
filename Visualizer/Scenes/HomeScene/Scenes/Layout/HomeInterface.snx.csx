using Sundex.Components.Labels;
using Sundex.Components.Panels;

var context = As<HomeInterface>(Context);
var component = context.Component;

context.RootPanel = component.Element as Panel ?? throw new Exception("Root panel not found");

context.Band = component.GetID<Panel>("band");
context.Moai = component.GetID<Image>("moai");
context.Playhead = component.GetID<Panel>("playhead");
context.VersionLabel = component.GetID<Label>("version-label");
context.UpdateLabel = component.GetID<Label>("update-label");

// The whole cell is the target, not a button inside it - the row is the affordance.
component.GetID<Panel>("cell-visualizer").OnClick = _ => context.OnVisualizer();
component.GetID<Panel>("cell-drum-master").OnClick = _ => context.OnDrumMaster();
component.GetID<Panel>("cell-editor").OnClick = _ => context.OnEditor();
component.GetID<Button>("settings-button").OnClick = _ => context.OnSettings();

// Eight steps to a cell: one bar. Built here rather than in the markup because the sweep
// wants them as one ordered list across all three cells, and twenty-four identical tags
// would say nothing the loop doesn't.
foreach (var (rowID, hue) in new[]
         {
             ("steps-visualizer", "step-blue"),
             ("steps-drum-master", "step-orange"),
             ("steps-editor", "step-green")
         })
{
    var row = component.GetID<FlexPanel>(rowID);
    for (var i = 0; i < HomeInterface.StepsPerCell; i++)
    {
        var step = new Panel(context.UI) { Classes = ["step", hue] };
        row.AddChild(step);
        context.Steps.Add(step);
    }
}
