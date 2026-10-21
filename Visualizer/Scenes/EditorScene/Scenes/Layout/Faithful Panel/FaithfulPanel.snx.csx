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

ctx.AdoptFollowButton(Component.GetID<Button>("faithful-follow"));
