using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

var ctx = As<EditorInterface>(Context);

ctx.FaithfulPanel = Component.GetID<FlexPanel>("faithful-panel");
ctx.FaithfulBody = Component.GetID<FlexPanel>("faithful-body");
ctx.FaithfulTrackName = Component.GetID<TextInput>("opened-track-name");

ctx.FaithfulTrackName.OnValueChanged = input =>
{
    if (ctx.State.OpenedTrack is { } track) ctx.State.RenameTrack(track, input.Value);
};
Component.GetID<Button>("back-to-arrangement").OnClick = _ => ctx.State.CloseTrack();

// Same Draw/Select pair the two grid panels carry, on the one shared EditorState.ActiveTool.
ctx.AdoptToolButton(Component.GetID<Button>("faithful-tool-draw"), EditorTool.Draw);
ctx.AdoptToolButton(Component.GetID<Button>("faithful-tool-select"), EditorTool.Select);
ctx.AdoptFollowButton(Component.GetID<Button>("faithful-follow"));
