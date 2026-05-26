using Sundex.Components.Labels;
using Sundex.Components.Panels;

var context = As<SettingsInterface>(Context);

context.RootPanel = Component.Element as Panel ?? throw new Exception("Root panel not found");
context.SettingsList = Component.RegisteredIDs["settings-list"] as FlexPanel ?? throw new Exception("Settings list not found");

var backButton = Component.RegisteredIDs["back-button"] as Button ?? throw new Exception("Back button not found");
backButton.OnClick = _ => context.OnBack();
