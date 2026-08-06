using Sundex.Components.Labels;

// Component is this document's own id map, not the root's. Context is the shared
// EditorInterface every component in the tree wires itself into.
var ctx = As<EditorInterface>(Context);

ctx.ProjectName = Component.GetID<Label>("project-name");
ctx.ProjectBpm = Component.GetID<Label>("project-bpm");

// The dialog host these three go through only exists after the root is drawn, which is
// after this runs - the lambdas resolve it at click time instead.
Component.GetID<Button>("load-button").OnClick = _ => ctx.ShowLoadDialog();
Component.GetID<Button>("save-button").OnClick = _ => ctx.SaveProject();
Component.GetID<Button>("export-button").OnClick = _ => ctx.ShowExportDialog();
