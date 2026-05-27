using Sundex.Components.Labels;
using Sundex.Components.Panels;

var context = As<DrumMaster>(Context);

Component.GetID<Button>("play-button").OnClick += _ => context.StartPlayback();
Component.GetID<Button>("back-button").OnClick += _ => context.Game.SceneManager.TransitionTo("home");
Component.GetID<Button>("add-lane-button").OnClick += _ => {
    var list = Component.GetID<FlexPanel>("taiko-lane-list");
    var taikoLane = Sundex.CreateElement("taiko-lane-configuration");
    list.AddChild(taikoLane);
    list.InvalidateLayout();
};