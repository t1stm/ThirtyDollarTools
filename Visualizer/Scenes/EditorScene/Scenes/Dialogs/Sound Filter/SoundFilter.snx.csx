using Sundex.Components.Labels;
using Sundex.Components.Panels;

var ctx = As<EditorInterface>(Context);

ctx.SoundFilterModal = Component.GetID<ModalLayer>("sound-filter-modal");

// Dismissing via the backdrop commits too - clicking outside shouldn't discard picks.
ctx.SoundFilterModal.OnDismissRequested = _ => ctx.CommitAndCloseSoundFilter();
Component.GetID<Button>("sound-filter-done").OnClick = _ => ctx.CommitAndCloseSoundFilter();
