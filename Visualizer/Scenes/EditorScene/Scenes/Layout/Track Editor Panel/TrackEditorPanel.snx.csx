using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

var ctx = As<EditorInterface>(Context);

ctx.TrackEditorPanel = Component.GetID<FlexPanel>("track-editor-panel");
ctx.OpenedTrackName = Component.GetID<TextInput>("opened-track-name");
ctx.InstrumentButton = Component.GetID<Button>("instrument-button");

ctx.OpenedTrackName.OnValueChanged = input =>
{
    if (ctx.State.OpenedTrack is { } track) ctx.State.RenameTrack(track, input.Value);
};
Component.GetID<Button>("back-to-arrangement").OnClick = _ => ctx.State.CloseTrack();

// The instrument workflow is only constructed after the root is drawn, which is after
// this runs - the lambda resolves it at click time.
ctx.InstrumentButton.OnClick = _ => ctx.OpenInstrumentSelector();

ctx.AdoptToolButton(Component.GetID<Button>("editor-tool-draw"), EditorTool.Draw);
ctx.AdoptToolButton(Component.GetID<Button>("editor-tool-select"), EditorTool.Select);
