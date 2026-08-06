// Every dialog document's sheet. The rules themselves stay in Scenes/Styles/Dialogs.snx.ss
// next to the rest of the shared vocabulary - this only pulls in what the dialog markup
// names, so each dialog is styled while it is built rather than by a host's later cascade.
// A dialog has no host: it is mounted into a bare ModalLayer, so this is its only styling.
import "Scenes/Styles/Controls.snx.ss";
import "Scenes/Styles/Dialogs.snx.ss";
