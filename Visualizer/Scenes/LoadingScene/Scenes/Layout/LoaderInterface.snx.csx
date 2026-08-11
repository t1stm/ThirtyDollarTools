using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

// Handles only stay resolved here. What the buttons do is wired in LoaderInterface, which
// owns the order of the setup panes and the motion between them.
var context = As<LoaderInterface>(Context);

context.RootPanel = context.Component.Element as Panel ?? throw new Exception("Root panel not found");
context.Strip = context.Component.GetID<Panel>("strip");
context.Ticks = context.Component.GetID<FlexPanel>("ticks");

context.Status = context.Component.GetID<Panel>("status");
context.StatusMessage = context.Component.GetID<Label>("status-message");
context.StatusDetail = context.Component.GetID<Label>("status-detail");
context.StatusPercent = context.Component.GetID<Label>("status-percent");

context.PaneWelcome = context.Component.GetID<Panel>("pane-welcome");
context.PaneUpdates = context.Component.GetID<Panel>("pane-updates");
context.PaneSounds = context.Component.GetID<Panel>("pane-sounds");

context.IncludePrereleases = context.Component.GetID<Checkbox>("include-prereleases");
context.IncludeNightlies = context.Component.GetID<Checkbox>("include-nightlies");

context.WelcomeNext = context.Component.GetID<Button>("welcome-next");
context.UpdatesDecline = context.Component.GetID<Button>("updates-decline");
context.UpdatesAccept = context.Component.GetID<Button>("updates-accept");
context.SoundsStart = context.Component.GetID<Button>("sounds-start");
