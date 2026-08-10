using Sundex.Components.Bars;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

var context = As<LoaderInterface>(Context);

context.RootPanel = context.Component.Element as Panel ?? throw new Exception("Root panel not found");
context.ProgressBar = context.Component.RegisteredIDs["loader-progress"] as ProgressBar ??
                      throw new Exception("Progress bar not found");
context.Label = context.Component.RegisteredIDs["loader-label"] as Label ?? throw new Exception("Label not found");

var button = context.Component.RegisteredIDs["start-button"] as Button ?? throw new Exception("Button not found");
button.OnClick = _ => context.OnClickAction();

// The update prompt's handles - Loader.cs wires its two buttons, since what they do is
// settings work rather than interface work.
context.MainView = context.Component.GetID<FlexPanel>("main-view");
context.UpdatePrompt = context.Component.GetID<FlexPanel>("update-prompt");
context.IncludePrereleases = context.Component.GetID<Checkbox>("include-prereleases");
context.IncludeNightlies = context.Component.GetID<Checkbox>("include-nightlies");
context.UpdateDecline = context.Component.GetID<Button>("update-decline");
context.UpdateOptIn = context.Component.GetID<Button>("update-opt-in");
