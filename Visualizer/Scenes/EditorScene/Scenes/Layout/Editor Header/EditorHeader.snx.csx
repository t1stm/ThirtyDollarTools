using Sundex.Components.Labels;

// Component is this document's own id map, not the root's. Context is the shared
// EditorInterface every component in the tree wires itself into.
var ctx = As<EditorInterface>(Context);

// EditorHeaderController takes the rest of the handles from here: what the bar shows
// follows the project, which only the interface's state knows.
ctx.Header = Component;

// The dialog host these go through only exists after the root is drawn, which is after
// this runs - the lambdas resolve it at click time instead.
var menu = Component.GetID<Button>("project-menu-button");
menu.OnClick = _ => ctx.ShowProjectMenu(menu);
Component.GetID<Button>("save-button").OnClick = _ => ctx.SaveProject();
Component.GetID<Button>("new-button").OnClick = _ => ctx.NewProject();
Component.GetID<Button>("open-button").OnClick = _ => ctx.ShowLoadDialog();
