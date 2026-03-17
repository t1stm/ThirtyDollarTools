using Sundex.Components.Panels;
using Sundex.Components.Bars;
using Sundex.Components.Labels;

var context = As<HomeInterface>(Context);

context.RootPanel = context.Component.Element as Panel ?? throw new Exception("Root panel not found");

var visualizerButton = context.Component.RegisteredIDs["visualizer-button"] as Button ?? throw new Exception("Visualizer button not found");
visualizerButton.OnClick = _ => context.OnVisualizer();

var editorButton = context.Component.RegisteredIDs["editor-button"] as Button ?? throw new Exception("Editor button not found");
editorButton.OnClick = _ => context.OnEditor();

var drumMasterButton = context.Component.RegisteredIDs["drum-master-button"] as Button ?? throw new Exception("Drum Master button not found");
drumMasterButton.OnClick = _ => context.OnDrumMaster();

var settingsButton = context.Component.RegisteredIDs["settings-button"] as Button ?? throw new Exception("Settings button not found");
settingsButton.OnClick = _ => context.OnSettings();
