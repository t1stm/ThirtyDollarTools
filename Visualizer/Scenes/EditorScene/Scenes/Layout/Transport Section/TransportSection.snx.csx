using Sundex.Components.Bars;
using Sundex.Components.Labels;

// Component is this document's own id map, not the root's - the root registers only the
// ids in EditorInterface.snx.xml, and <transport-section/> is one opaque tag to it.
// Context is the shared EditorInterface every component in the tree wires itself into.
var ctx = As<EditorInterface>(Context);

ctx.TransportProgress = Component.GetID<ProgressBar>("transport-progress");
ctx.TransportElapsed = Component.GetID<Label>("transport-elapsed");
ctx.TransportTotal = Component.GetID<Label>("transport-total");
ctx.TransportPlay = Component.GetID<Button>("transport-play");

// Playback is assigned before this runs, but the handlers read it per click anyway.
ctx.TransportPlay.OnClick = _ => ctx.Playback.PlayPause();
Component.GetID<Button>("transport-stop").OnClick = _ => ctx.Playback.Stop();
Component.GetID<Button>("transport-back").OnClick = _ => ctx.RequestBack();
