using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Views;

namespace EditorScene.Scenes.Layout;

/// <summary>
///     The automation sections - a note's, several notes' at once, and a track's - and the
///     keyframe blocks inside them.
/// </summary>
public partial class InspectorPanel
{
    /// <summary>
    ///     Uniform means all null, or all non-null and structurally equal
    ///     (<see cref="AudioKeyframeManager.ValueEquals" />). All-null offers "+ Add
    ///     automation", giving each note its own manager instance - never shared, matching
    ///     <see cref="Note.Duplicate" />. Uniform renders the full form bound to the primary
    ///     note and clones it out to the rest on every commit. Mixed shows one disabled
    ///     info row.
    /// </summary>
    private void MultiAutomationSection(IReadOnlyList<Note> notes, Note primary)
    {
        _form.Header("Automation");

        if (notes.All(n => n.Automation == null))
        {
            _form.ActionRow("+ Add automation", () => EditAndRebuild(() =>
            {
                foreach (var note in notes)
                    if (!note.IsCut)
                        note.Automation = new AudioKeyframeManager();
            }));
            return;
        }

        if (notes.Any(n => n.Automation == null) ||
            !notes.All(n => n.Automation!.ValueEquals(primary.Automation!)))
        {
            _form.InfoRow("Automation", () => "mixed - select notes with matching automation to edit");
            return;
        }

        KeyframeBlocks("Automation", "", primary.Automation!, () => FanOutAutomation(notes, primary));
        _form.Section = "Automation";
        _form.ActionRow("Remove automation", () => EditAndRebuild(() =>
        {
            foreach (var note in notes) note.Automation = null;
        }));
    }

    private static void FanOutAutomation(IReadOnlyList<Note> notes, Note primary)
    {
        foreach (var note in notes)
            if (note != primary && !note.IsCut)
                note.Automation = primary.Automation!.Clone();
    }

    /// <summary>
    ///     Form for <see cref="Note.Automation" />: each keyframe fires one generated event,
    ///     its gap after the previous one, modifying the previous result. Structural edits
    ///     (add/remove) rebuild; field edits sync like every other row.
    /// </summary>
    private void AutomationSection(Note note)
    {
        _form.Header("Automation");
        if (note.Automation is not { } automation)
        {
            _form.ActionRow("+ Add automation",
                () => EditAndRebuild(() => note.Automation = new AudioKeyframeManager()));
            return;
        }

        KeyframeBlocks("Automation", "", automation);
        _form.Section = "Automation";
        _form.ActionRow("Remove automation", () => EditAndRebuild(() => note.Automation = null));
    }

    /// <summary>
    ///     The same form for a whole track: any number of automations, each with its own
    ///     sound filter (null = every sound), instead of one note's single nullable
    ///     <see cref="Note.Automation" />. Shares <see cref="KeyframeBlocks" /> with
    ///     <see cref="AutomationSection" /> for the gap/repeats/keyframe rows.
    /// </summary>
    private void TrackAutomationSection(ProjectTrack track)
    {
        _form.Header("Track Automation");

        _form.NumberRow("Transpose", () => track.Transpose, v => track.Transpose = (float?)v,
            -TrackEditorView.MaxValue, TrackEditorView.MaxValue, 0.1, true);

        for (var i = 0; i < track.TrackAutomations.Count; i++)
        {
            var entry = track.TrackAutomations[i];
            var section = $"Track Automation {i + 1}";

            _form.Card("inspector-card-entry", () =>
            {
                _form.Header(section);

                _form.CheckRow("All sounds", () => entry.Sounds is null,
                    allSounds => EditAndRebuild(() => entry.Sounds = allSounds ? null : []));
                if (entry.Sounds is { } sounds)
                    _form.ActionRow("Sounds", $"Sounds: {sounds.Count} selected",
                        () => OnEditTrackAutomationSounds?.Invoke(entry));

                KeyframeBlocks(section, $"{section} ", entry.Keyframes);

                _form.Section = section;
                _form.ActionRow("Remove", () => EditAndRebuild(() => track.RemoveTrackAutomation(entry)));
            });
        }

        _form.Section = "Track Automation";
        _form.ActionRow("+ Add automation",
            () => EditAndRebuild(() => track.AddTrackAutomation(new AudioKeyframeManager())));
    }

    /// <summary>
    ///     Timing, the note's length and retrigger grid, the cut flags, and one card per
    ///     keyframe - shared by note and track automation. Keyframes are generated from
    ///     Gap/End/AutomationOffset, so there is nothing to add or remove by hand.
    ///     <paramref name="keyframeHeaderPrefix" /> disambiguates the keyframe cards' field keys
    ///     when several automations are on screen at once (empty for the single per-note
    ///     automation, so its field keys are unchanged: "Keyframe 1.Value"). Their headings
    ///     leave it out: each card already sits inside its automation's own, and "Track
    ///     Automation 1 All keyframes (× 3)" ran past the 300 px panel.
    ///     <paramref name="afterEdit" />, when given, runs after every commit (field or
    ///     structural) - the multi-note form's clone-fan-out hook (see
    ///     <see cref="MultiAutomationSection" />); null for every other caller.
    /// </summary>
    private void KeyframeBlocks(string section, string keyframeHeaderPrefix, AudioKeyframeManager automation,
        Action? afterEdit = null)
    {
        void Commit(Action edit)
        {
            _state.Edit(edit);
            afterEdit?.Invoke();
        }

        _form.Section = section;
        _form.CheckRow("Gaps in seconds", () => automation.Timing == KeyframeTiming.Time,
            timeMode =>
            {
                _state.Edit(() => automation.Timing = timeMode ? KeyframeTiming.Time : KeyframeTiming.Step);
                afterEdit?.Invoke();
            });
        // Gap and End resize the keyframe list, so their cards appear and disappear.
        _form.NumberRow("Gap", () => automation.Gap, v => EditAndRebuild(() =>
        {
            automation.Gap = (float)v!.Value;
            afterEdit?.Invoke();
        }), 0, 4096, 0.5);
        _form.NumberRow("End", () => automation.End, v => EditAndRebuild(() =>
        {
            automation.End = (float)v!.Value;
            afterEdit?.Invoke();
        }), 0, 4096);
        // "Grid offset", not "Automation offset": the label column is 84 px and the longer
        // name clips. It still reads apart from the per-keyframe sound Offset below.
        _form.NumberRow("Grid offset", () => automation.AutomationOffset, v => EditAndRebuild(() =>
        {
            automation.AutomationOffset = (float)v!.Value;
            afterEdit?.Invoke();
        }), -4096, 4096, 0.5);
        _form.CheckRow("Cut", () => automation.Cut, cut => EditAndRebuild(() =>
        {
            automation.Cut = cut;
            afterEdit?.Invoke();
        }), ("Cut at end", () => automation.CutAtEnd, v => EditAndRebuild(() =>
        {
            automation.CutAtEnd = v;
            afterEdit?.Invoke();
        })));

        // The keyframe list can be hundreds long, so one card edits them all: the template
        // every auto-created keyframe copies, fanned out to the ones that still match it.
        // Only keyframes given settings of their own get a card.
        void EditTemplate(Action<AudioKeyframe> apply)
        {
            var followers = automation.Keyframes.Where(k => k.ValueEquals(automation.Template)).ToList();
            _state.Edit(() =>
            {
                apply(automation.Template);
                foreach (var keyframe in followers) apply(keyframe);
            });
            afterEdit?.Invoke();
        }

        // Nothing to shape until the note has a length to hold keyframes.
        if (automation.Keyframes.Count > 0)
            _form.Card("inspector-card-keyframe", () =>
        {
            _form.Header($"All keyframes  (× {automation.Keyframes.Count})");
            _form.Section = $"{keyframeHeaderPrefix}All keyframes";
            KeyframeFields(automation.Template, EditTemplate, () => EditTemplate(k => k.Position = null));
            // Auto offset splices onto the instance it replaces, which only works if that
            // instance is cut - so ticking it turns the automation's cut on and keeps it.
            _form.CheckRow("Auto offset", () => automation.Template.AutoOffset, auto => EditAndRebuild(() =>
            {
                EditTemplate(k => k.AutoOffset = auto);
                if (auto) automation.Cut = true;
            }));
        });

        for (var i = 0; i < automation.Keyframes.Count; i++)
        {
            var keyframe = automation.Keyframes[i];
            if (keyframe.ValueEquals(automation.Template)) continue; // follows the card above

            var index = i;
            _form.Card("inspector-card-keyframe", () =>
            {
                _form.Header($"Keyframe {index + 1}  (edited)");
                _form.Section = $"{keyframeHeaderPrefix}Keyframe {index + 1}";
                _form.NumberRow("Position", () => keyframe.Position ?? automation.PositionOf(index),
                    v => Commit(() => keyframe.Position = (float?)v), 0, 4096, 0.25, true);
                KeyframeFields(keyframe, apply => Commit(() => apply(keyframe)), null);
                _form.CheckRow("Auto offset", () => keyframe.AutoOffset, auto => EditAndRebuild(() =>
                {
                    keyframe.AutoOffset = auto;
                    if (auto) automation.Cut = true;
                    afterEdit?.Invoke();
                }));
                _form.ActionRow("reset", () => EditAndRebuild(() =>
                {
                    var template = automation.Template.Clone(false);
                    keyframe.Position = null;
                    keyframe.Value = template.Value;
                    keyframe.Volume = template.Volume;
                    keyframe.Pan = template.Pan;
                    keyframe.Offset = template.Offset;
                    keyframe.AutoOffset = template.AutoOffset;
                    afterEdit?.Invoke();
                }));
            });
        }

        _form.Section = section;
        // Typing a keyframe's own values needs a card, and only an edited keyframe has one -
        // this pins the first one that still follows the template, the way dragging its
        // marker would.
        if (automation.Keyframes.Count > 0)
            _form.ActionRow("+ Edit a single keyframe...", () => EditAndRebuild(() =>
            {
                for (var i = 0; i < automation.Keyframes.Count; i++)
                {
                    if (!automation.Keyframes[i].ValueEquals(automation.Template)) continue;
                    automation.Keyframes[i].Position = automation.PositionOf(i);
                    break;
                }

                afterEdit?.Invoke();
            }));
    }

    /// <summary>
    ///     The four relative-change rows shared by the "All keyframes" card and an edited
    ///     keyframe's own. <paramref name="edit" /> applies one change - to the template and
    ///     its followers, or to the single keyframe.
    /// </summary>
    private void KeyframeFields(AudioKeyframe keyframe, Action<Action<AudioKeyframe>> edit, Action? resetPosition)
    {
        _ = resetPosition;
        _form.ModifierRow("Value", () => keyframe.Value, m => edit(k => k.Value = m));
        _form.ModifierRow("Volume", () => keyframe.Volume, m => edit(k => k.Volume = m));
        _form.ModifierRow("Pan", () => keyframe.Pan, m => edit(k => k.Pan = m));
        _form.ModifierRow("Offset", () => keyframe.Offset, m => edit(k => k.Offset = m));
    }
}
