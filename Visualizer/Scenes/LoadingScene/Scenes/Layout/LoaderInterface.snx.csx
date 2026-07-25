using Sundex.Components.Bars;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

var context = As<LoaderInterface>(Context);

context.RootPanel = context.Component.Element as Panel ?? throw new Exception("Root panel not found");
context.ProgressBar = context.Component.RegisteredIDs["loader-progress"] as ProgressBar ??
                      throw new Exception("Progress bar not found");
context.Label = context.Component.RegisteredIDs["loader-label"] as Label ?? throw new Exception("Label not found");

var button = context.Component.RegisteredIDs["start-button"] as Button ?? throw new Exception("Button not found");
button.OnClick = _ => context.OnClickAction();