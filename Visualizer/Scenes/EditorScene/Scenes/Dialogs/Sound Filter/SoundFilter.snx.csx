using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;

var ctx = As<EditorInterface>(Context);

ctx.SoundFilterModal = Component.GetID<ModalLayer>("sound-filter-modal");
ctx.SoundFilterTray = Component.GetID<ScrollView>("sound-filter-tray");
ctx.SoundFilterCount = Component.GetID<Label>("sound-filter-count");
ctx.SoundFilterInput = Component.GetID<TextInput>("sound-filter-input");
ctx.SoundFilterClear = Component.GetID<Button>("sound-filter-clear");
ctx.SoundFilterDone = Component.GetID<Button>("sound-filter-done");

// Every way out commits - Done, Enter, the ×, Esc and the backdrop: clicking outside
// shouldn't discard picks, so nothing here promises a discard either.
ctx.SoundFilterModal.OnDismissRequested = _ => ctx.CommitAndCloseSoundFilter();
ctx.SoundFilterModal.OnConfirmRequested = _ => ctx.CommitAndCloseSoundFilter();
Component.GetID<Button>("close-button").OnClick = _ => ctx.CommitAndCloseSoundFilter();
ctx.SoundFilterDone.OnClick = _ => ctx.CommitAndCloseSoundFilter();
