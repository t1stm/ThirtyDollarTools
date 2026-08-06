// Styling for the two canvas views - the arrangement and the note editor - kept next
// to ArrangementView.cs/TrackEditorView.cs because nothing else uses it.
//
// Only the clip block's box is here. The grids themselves are painted from a LineBatch
// and a pool of reassigned planes, not from styled components, so there is no element
// for a selector to hit - those colors are constants in EditorPalette.cs.

// One track's clip on an arrangement lane. Its fill is code-owned - the view tints it
// per selection state every layout - so only the box and its label are here.
class clip-block {
    padding = 6;
}

class clip-label {
    font-size = 13;
}
