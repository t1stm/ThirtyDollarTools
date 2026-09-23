using Sundex.Components.Labels;
using Sundex.Components.Panels;

var ctx = As<ShortcutSheet>(Context);

ctx.RootPanel = Component.Element as FlexPanel ?? throw new Exception("sheet-root not found");
ctx.Header    = Component.GetID<FlexPanel>("header");
ctx.Groups    = Component.GetID<FlexPanel>("groups");
ctx.Footnote  = Component.GetID<Label>("footnote");
