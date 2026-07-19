using Sundex.Components.Labels;
using Sundex.Components.Panels;

var context = As<EditorInterface>(Context);

context.RootPanel = Component.Element as Panel ?? throw new Exception("Root panel not found");

var backButton = Component.RegisteredIDs["back-button"] as Button ?? throw new Exception("Back button not found");
backButton.OnClick = _ => context.RequestBack();
