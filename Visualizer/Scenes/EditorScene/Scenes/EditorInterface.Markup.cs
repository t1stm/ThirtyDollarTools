using Sundex.Components.Bars;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using Sundex.Markup;

namespace EditorScene.Scenes;

public partial class EditorInterface
{
    private void BindLayout()
    {
        SundexComponent Child(string name) => (SundexComponent)Component.Children.Single(child => child.Name == name);

        Header = Child("editor-header");
        var menu = Header.GetID<Button>("project-menu-button");
        menu.OnClick = _ => ShowProjectMenu(menu);
        Header.GetID<Button>("save-button").OnClick = _ => SaveProject();
        Header.GetID<Button>("new-button").OnClick = _ => NewProject();
        Header.GetID<Button>("open-button").OnClick = _ => ShowLoadDialog();

        var transport = Child("transport-section");
        TransportProgress = transport.GetID<ProgressBar>("transport-progress");
        TransportElapsed = transport.GetID<Label>("transport-elapsed");
        TransportTotal = transport.GetID<Label>("transport-total");
        TransportPlay = transport.GetID<Button>("transport-play");
        TransportPlay.OnClick = _ => Playback.PlayPause();
        transport.GetID<Button>("transport-stop").OnClick = _ => Playback.Stop();
        transport.GetID<Button>("transport-back").OnClick = _ => RequestBack();

        var hint = Child("hint-bar");
        HintBar = hint.GetID<FlexPanel>("hint-bar");
        HintGutter = hint.GetID<Panel>("hint-gutter");
        HintLines = hint.GetID<FlexPanel>("hint-lines");
        HintLabel = hint.GetID<Label>("hint-label");

        ArrangementPanel = Child("arrangement-panel").GetID<FlexPanel>("arrangement-panel");

        var trackEditor = Child("track-editor-panel");
        TrackEditorPanel = trackEditor.GetID<FlexPanel>("track-editor-panel");
        OpenedTrackName = trackEditor.GetID<TextInput>("opened-track-name");
        InstrumentButton = trackEditor.GetID<Button>("instrument-button");
        InstrumentButton.OnClick = _ => OpenInstrumentSelector();
        OpenedTrackName.OnValueChanged = RenameOpenedTrack;
        trackEditor.GetID<Button>("back-to-arrangement").OnClick = _ => State.CloseTrack();

        var faithful = Child("faithful-panel");
        FaithfulPanel = faithful.GetID<FlexPanel>("faithful-panel");
        FaithfulBody = faithful.GetID<FlexPanel>("faithful-body");
        FaithfulTrackName = faithful.GetID<TextInput>("opened-track-name");
        FaithfulTrackName.OnValueChanged = RenameOpenedTrack;
        faithful.GetID<Button>("back-to-arrangement").OnClick = _ => State.CloseTrack();
        AdoptFollowButton(faithful.GetID<Button>("faithful-follow"));

        var inspector = Child("inspector-shell");
        InspectorPanelElement = inspector.GetID<Panel>("inspector-panel");
        InspectorRows = inspector.GetID<ScrollView>("inspector-rows");
        InspectorStatusBar = inspector.GetID<ProgressBar>("inspector-status-bar");
        InspectorStatusLabel = inspector.GetID<Label>("inspector-status-label");

        var filter = Child("sound-filter");
        SoundFilterModal = filter.GetID<ModalLayer>("sound-filter-modal");
        SoundFilterTray = filter.GetID<ScrollView>("sound-filter-tray");
        SoundFilterCount = filter.GetID<Label>("sound-filter-count");
        SoundFilterInput = filter.GetID<TextInput>("sound-filter-input");
        SoundFilterClear = filter.GetID<Button>("sound-filter-clear");
        SoundFilterDone = filter.GetID<Button>("sound-filter-done");

        // Done, Enter, Escape, the close button, and the backdrop all commit the picks.
        SoundFilterModal.OnDismissRequested = _ => CommitAndCloseSoundFilter();
        SoundFilterModal.OnConfirmRequested = _ => CommitAndCloseSoundFilter();
        filter.GetID<Button>("close-button").OnClick = _ => CommitAndCloseSoundFilter();
        SoundFilterDone.OnClick = _ => CommitAndCloseSoundFilter();
    }

    private void RenameOpenedTrack(TextInput input)
    {
        if (State.OpenedTrack is { } track) State.RenameTrack(track, input.Value);
    }
}
