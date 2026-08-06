using Sundex.Components.Labels;
using Sundex.Components.Panels;

var ctx = As<EditorInterface>(Context);

ctx.ArrangementPanel = Component.GetID<FlexPanel>("arrangement-panel");

// Each grid mode owns its own Draw/Select pair; AdoptToolButton joins them to the one
// State.OnToolChanged sweep, so both bars stay in step.
ctx.AdoptToolButton(Component.GetID<Button>("arrangement-tool-draw"), EditorTool.Draw);
ctx.AdoptToolButton(Component.GetID<Button>("arrangement-tool-select"), EditorTool.Select);
