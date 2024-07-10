namespace ThirtyDollarVisualizer.Objects;

public struct PlayfieldLine(int max_size)
{
    public Memory<SoundRenderable?> Sounds = new SoundRenderable?[max_size];
    public int Count = 0;

    public void Render(DollarStoreCamera temporary_camera)
    {
        var span = Sounds.Span;
        for (var i = 0; i < Count; i++)
        {
            var sound = span[i];
            sound?.Render(temporary_camera);
        }
    }
}