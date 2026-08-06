using Sundex.Components.Labels;
using Sundex.Components.Panels;

// Handles only - the bar has no controls of its own. EditorInterface drives the text
// (SetHint) and the gutter width (AlignHintToGrid).
var ctx = As<EditorInterface>(Context);

ctx.HintBar = Component.GetID<FlexPanel>("hint-bar");
ctx.HintGutter = Component.GetID<Panel>("hint-gutter");
ctx.HintLabel = Component.GetID<Label>("hint-label");
