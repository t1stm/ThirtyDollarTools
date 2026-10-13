using Sundex.Components.Labels;
using Sundex.Components.Panels;

// Handles only - the bar has no controls of its own. HintBarController drives the text
// (Show) and the gutter width (SetView).
var ctx = As<EditorInterface>(Context);

ctx.HintBar = Component.GetID<FlexPanel>("hint-bar");
ctx.HintGutter = Component.GetID<Panel>("hint-gutter");
ctx.HintLines = Component.GetID<FlexPanel>("hint-lines");
ctx.HintLabel = Component.GetID<Label>("hint-label");
