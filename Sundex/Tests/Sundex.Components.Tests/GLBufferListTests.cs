using Sundex.Engine.Renderer.Data_Buffers;
using Sundex.Engine.Renderer.Queues;

namespace Sundex.Components.Tests;

/// <summary>
///     The tracked references a <see cref="GLBufferList{TDataType}" /> hands out have to
///     survive their neighbours shifting - EditorScene's sound picker releases one per icon,
///     long after the insertions and removals that moved them, so a reference must still point
///     at its own slot after every shift.
///     Lives here rather than in an engine test project of its own: this is the nearest
///     project that already links the engine.
/// </summary>
public class GLBufferListTests
{
    private static GLBufferList<int> Filled(int count)
    {
        var list = new GLBufferList<int>(new DeleteQueue(), count);
        for (var index = 0; index < count; index++) list.Add(index * 10);
        return list;
    }

    /// <param name="backToFront">
    ///     Which order the reference map ends up holding them in - the two shift directions
    ///     each fall over on one of the two, so a test picks the one that bites.
    /// </param>
    private static TrackedBufferReference<int>[] References(GLBufferList<int> list, bool backToFront)
    {
        var references = new TrackedBufferReference<int>[list.Count];
        for (var position = 0; position < list.Count; position++)
        {
            var index = backToFront ? list.Count - 1 - position : position;
            references[index] = list.GetReferenceAt(index);
        }

        return references;
    }

    [Fact]
    public void ReleasingEveryReferenceAfterARemoval_EmptiesTheList()
    {
        var list = Filled(5);
        var references = References(list, true);

        list.RemoveAt(0);
        for (var index = 1; index < references.Length; index++) list.Remove(references[index]);

        Assert.Empty(list);
    }

    [Fact]
    public void ReleasingEveryReferenceAfterAnInsertion_LeavesOnlyTheInsertedItem()
    {
        var list = Filled(5);
        var references = References(list, false);

        list.Insert(0, -1);
        foreach (var reference in references) list.Remove(reference);

        Assert.Equal([-1], list);
    }
}
