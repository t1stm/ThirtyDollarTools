// The transport block's sheet. The rules themselves stay in Panels.snx.ss next to the
// rest of the track column's chrome - this only pulls in the same vocabulary the root
// sheet does, so the component's tree is styled while it is being built rather than
// only by the root's later cascade over the imported subtree.
//
// Both routes work today: ProgressBar takes its two planes as constructor arguments,
// but ProgressBar.ApplyStyleValue also rebuilds them from a ColorValue afterwards. The
// sheet is here so the component stands on its own - a usage site that isn't the editor
// root would otherwise get an unstyled transport.
import "Scenes/Styles/Controls.snx.ss";
import "Scenes/Styles/Panels.snx.ss";
