using EditorScene.State;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Views;

namespace EditorScene.Scenes.Layout;

/// <summary>
///     The automation sections - one or several notes', and a track's - and the keyframe
///     blocks inside them.
/// </summary>
public partial class InspectorPanel
{
    /// <summary>
    ///     The automation of one note or several. None of them has one: "+ Add automation"
    ///     gives each its own manager - never shared, matching <see cref="Note.Duplicate" />.
    ///     Otherwise the form edits every automation in the selection at once: a field they
    ///     agree on shows its value, one they differ on renders empty, and an edit writes that
    ///     one field to all of them, leaving the rest of each as it was. Every commit is one
    ///     undo step across all the notes (<see cref="EditorState.EditNoteAutomations" />).
    /// </summary>
    private void AutomationSection(IReadOnlyList<Note> notes)
    {
        _form.Header("Automation");

        void Commit(object field, Action edit)
        {
            _state.EditNoteAutomations(notes, field, edit);
        }

        // A cut has no value or sound for an automation to expand against (see Note.IsCut).
        var missing = notes.Where(n => n is { Automation: null, IsCut: false }).ToList();
        var automations = notes.Select(n => n.Automation).OfType<AudioKeyframeManager>().ToList();
        if (automations.Count > 0) KeyframeBlocks("Automation", "", automations, Commit);

        _form.Section = "Automation";
        if (missing.Count > 0)
            _form.ActionRow("+ Add automation",
                automations.Count == 0 ? "+ Add automation" : $"+ Add automation to the other {missing.Count}",
                () =>
                {
                    Commit(new object(), () =>
                    {
                        foreach (var note in missing) note.Automation = new AudioKeyframeManager();
                    });
                    RebuildWhenIdle();
                });

        if (automations.Count > 0)
            _form.ActionRow("Remove automation", () =>
            {
                Commit(new object(), () =>
                {
                    foreach (var note in notes) note.Automation = null;
                });
                RebuildWhenIdle();
            });
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

                KeyframeBlocks(section, $"{section} ", [entry.Keyframes], (_, edit) => _state.Edit(edit));

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
    ///     keyframe - shared by note and track automation, and by any number of automations at
    ///     once: a row shows a value only where they all agree, and writes to all of them.
    ///     Keyframes are generated from Gap/End/AutomationOffset, so there is nothing to add or
    ///     remove by hand. <paramref name="keyframeHeaderPrefix" /> disambiguates the keyframe
    ///     cards' field keys when several automations are on screen at once (empty for a note's
    ///     automation, so its field keys are unchanged: "Keyframe 1.Value"). Their headings
    ///     leave it out: each card already sits inside its automation's own, and "Track
    ///     Automation 1 All keyframes (× 3)" ran past the 300 px panel.
    ///     <paramref name="commit" /> applies one row's edit; the row comes along so typing
    ///     into one field can be a single undo step.
    /// </summary>
    private void KeyframeBlocks(string section, string keyframeHeaderPrefix,
        IReadOnlyList<AudioKeyframeManager> automations, Action<object, Action> commit)
    {
        var first = automations[0];

        // One undo subject per row, fresh on every build.
        Action<Action> Row()
        {
            var row = new object();
            return edit => commit(row, edit);
        }

        Action<Action> RebuildingRow()
        {
            var field = Row();
            return edit =>
            {
                field(edit);
                RebuildWhenIdle();
            };
        }

        void Each(Action<AudioKeyframeManager> apply)
        {
            foreach (var automation in automations) apply(automation);
        }

        _form.Section = section;
        var timing = Row();
        _form.CheckRow("Gaps in seconds", () => automations.All(a => a.Timing == KeyframeTiming.Time),
            timeMode => timing(() => Each(a => a.Timing = timeMode ? KeyframeTiming.Time : KeyframeTiming.Step)));
        // Gap and End resize the keyframe list, so their cards appear and disappear.
        var gap = RebuildingRow();
        _form.NumberRow("Gap", () => first.Gap, v => gap(() => Each(a => a.Gap = (float)v!.Value)), 0, 4096, 0.5,
            mixed: () => !AllEqual(automations, a => a.Gap));
        var end = RebuildingRow();
        _form.NumberRow("End", () => first.End, v => end(() => Each(a => a.End = (float)v!.Value)), 0, 4096,
            mixed: () => !AllEqual(automations, a => a.End));
        // "Grid offset", not "Automation offset": the label column is 84 px and the longer
        // name clips. It still reads apart from the per-keyframe sound Offset below.
        var offset = RebuildingRow();
        _form.NumberRow("Grid offset", () => first.AutomationOffset,
            v => offset(() => Each(a => a.AutomationOffset = (float)v!.Value)), -4096, 4096, 0.5,
            mixed: () => !AllEqual(automations, a => a.AutomationOffset));
        var cut = RebuildingRow();
        var cutAtEnd = RebuildingRow();
        _form.CheckRow("Cut", () => automations.All(a => a.Cut), on => cut(() => Each(a => a.Cut = on)),
            ("Cut at end", () => automations.All(a => a.CutAtEnd), on => cutAtEnd(() => Each(a => a.CutAtEnd = on))));

        // The keyframe list can be hundreds long, so one card edits them all: the template
        // every auto-created keyframe copies, fanned out to the ones that still match it.
        // Only keyframes given settings of their own get a card.
        void EditTemplates(Action<AudioKeyframe> apply)
        {
            Each(a =>
            {
                var followers = a.Keyframes.Where(k => k.ValueEquals(a.Template)).ToList();
                apply(a.Template);
                foreach (var keyframe in followers) apply(keyframe);
            });
        }

        // Nothing to shape until a note has a length to hold keyframes.
        if (automations.Any(a => a.Keyframes.Count > 0))
            _form.Card("inspector-card-keyframe", () =>
            {
                _form.Header(AllEqual(automations, a => a.Keyframes.Count)
                    ? $"All keyframes  (× {first.Keyframes.Count})"
                    : "All keyframes");
                _form.Section = $"{keyframeHeaderPrefix}All keyframes";
                // Read through the managers, never a captured keyframe: an undo restores a
                // manager in place but hands it fresh keyframes (CopyFrom).
                KeyframeFields(() => [.. automations.Select(a => a.Template)], Row, EditTemplates);
                // Auto offset splices onto the instance it replaces, which only works if that
                // instance is cut - so ticking it turns the automation's cut on and keeps it.
                var auto = RebuildingRow();
                _form.CheckRow("Auto offset", () => automations.All(a => a.Template.AutoOffset), on => auto(() =>
                {
                    EditTemplates(k => k.AutoOffset = on);
                    if (on) Each(a => a.Cut = true);
                }));
            });

        // A keyframe card edits the same slot of every automation, so the slots have to line
        // up: only when they all hold as many keyframes.
        if (!AllEqual(automations, a => a.Keyframes.Count)) return;

        for (var i = 0; i < first.Keyframes.Count; i++)
        {
            var index = i;
            if (automations.All(a => a.Keyframes[index].ValueEquals(a.Template))) continue; // follows the card above

            _form.Card("inspector-card-keyframe", () =>
            {
                _form.Header($"Keyframe {index + 1}  (edited)");
                _form.Section = $"{keyframeHeaderPrefix}Keyframe {index + 1}";

                var position = Row();
                _form.NumberRow("Position", () => PositionOf(first, index),
                    v => position(() => Each(a => At(a, index).Position = (float?)v)), 0, 4096, 0.25, true,
                    () => !AllEqual(automations, a => PositionOf(a, index)));
                KeyframeFields(() => [.. automations.Select(a => At(a, index))], Row,
                    apply => Each(a => apply(At(a, index))));
                var auto = RebuildingRow();
                _form.CheckRow("Auto offset", () => automations.All(a => At(a, index).AutoOffset), on => auto(() =>
                    Each(a =>
                    {
                        At(a, index).AutoOffset = on;
                        if (on) a.Cut = true;
                    })));
                var reset = RebuildingRow();
                _form.ActionRow("reset", () => reset(() => Each(a =>
                {
                    var keyframe = At(a, index);
                    var template = a.Template.Clone(false);
                    keyframe.Position = null;
                    keyframe.Value = template.Value;
                    keyframe.Volume = template.Volume;
                    keyframe.Pan = template.Pan;
                    keyframe.Offset = template.Offset;
                    keyframe.AutoOffset = template.AutoOffset;
                })));
            });
        }

        _form.Section = section;
        // Typing a keyframe's own values needs a card, and only an edited keyframe has one -
        // this pins the first slot that still follows the template everywhere, the way
        // dragging its marker would.
        var pin = RebuildingRow();
        if (first.Keyframes.Count > 0)
            _form.ActionRow("+ Edit a single keyframe...", () => pin(() =>
            {
                for (var i = 0; i < first.Keyframes.Count; i++)
                {
                    var index = i;
                    if (!automations.All(a => a.Keyframes[index].ValueEquals(a.Template))) continue;
                    Each(a => a.Keyframes[index].Position = a.PositionOf(index));
                    break;
                }
            }));
        return;

        // A card's rows read their slot on every sync, never a captured keyframe (see the
        // template card). Past the end only while a rebuild waits on a focused field (see
        // Sync), where any value will do.
        static AudioKeyframe At(AudioKeyframeManager automation, int index)
        {
            return index < automation.Keyframes.Count ? automation.Keyframes[index] : automation.Template;
        }

        static float? PositionOf(AudioKeyframeManager automation, int index)
        {
            return index < automation.Keyframes.Count
                ? automation.Keyframes[index].Position ?? automation.PositionOf(index)
                : null;
        }
    }

    /// <summary>
    ///     The four relative-change rows shared by the "All keyframes" card and an edited
    ///     keyframe's own. <paramref name="keyframes" /> are the ones shown - one per automation
    ///     being edited - and <paramref name="apply" /> makes one change to all of them (the
    ///     templates and their followers, or one slot). <paramref name="field" /> hands each
    ///     row its undo subject.
    /// </summary>
    private void KeyframeFields(Func<IReadOnlyList<AudioKeyframe>> keyframes, Func<Action<Action>> field,
        Action<Action<AudioKeyframe>> apply)
    {
        Row("Value", k => k.Value, (k, m) => k.Value = m);
        Row("Volume", k => k.Volume, (k, m) => k.Volume = m);
        Row("Pan", k => k.Pan, (k, m) => k.Pan = m);
        Row("Offset", k => k.Offset, (k, m) => k.Offset = m);
        return;

        void Row(string label, Func<AudioKeyframe, Modifier> of, Action<AudioKeyframe, Modifier> set)
        {
            var commit = field();
            _form.ModifierRow(label,
                () => AllEqual(keyframes(), k => of(k).Amount) ? of(keyframes()[0]).Amount : null,
                () => AllEqual(keyframes(), k => of(k).Kind) ? of(keyframes()[0]).Kind == ModifierKind.Multiply : null,
                amount => commit(() => apply(k => set(k, of(k) with { Amount = amount }))),
                multiply => commit(() => apply(k => set(k, of(k) with
                {
                    Kind = multiply ? ModifierKind.Multiply : ModifierKind.Add
                }))));
        }
    }
}
