using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;

var context = As<SettingsInterface>(Context);
var component = context.Component;

context.RootPanel = component.Element as Panel ?? throw new Exception("Root panel not found");
context.SettingsList = component.GetID<ScrollView>("settings-list");
context.StripView = component.GetID<ScrollView>("strip-view");
context.Strip = component.GetID<FlexPanel>("strip");

component.GetID<Button>("back-button").OnClick = _ => context.OnBack();
