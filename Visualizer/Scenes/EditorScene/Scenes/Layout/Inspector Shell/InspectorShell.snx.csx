using Sundex.Components.Bars;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;

var ctx = As<EditorInterface>(Context);

ctx.InspectorPanelElement = Component.GetID<Panel>("inspector-panel");
ctx.InspectorRows = Component.GetID<ScrollView>("inspector-rows");
ctx.InspectorStatusBar = Component.GetID<ProgressBar>("inspector-status-bar");
ctx.InspectorStatusLabel = Component.GetID<Label>("inspector-status-label");
