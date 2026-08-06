// Imports only. Every selector this panel's markup uses - grid-panel, tool-bar,
// tool-button, grid-body, grid-view, spacer - is shared vocabulary the note editor's bar
// wants too, so the rules stay in Scenes/Styles rather than being split per component.
// Pulling them in here is what styles the tree while it is built, instead of leaving it
// to the root's later cascade; a usage site that isn't the editor root gets a styled
// panel either way.
import "Scenes/Styles/Controls.snx.ss";
import "Scenes/Styles/Panels.snx.ss";
