namespace ThirtyDollarVisualizer.Objects.Playfield;

public struct PlayfieldLine(int maxSize)
{
    public Memory<SoundRenderable> Sounds = new SoundRenderable[maxSize];
    public int Count = 0;

    public void Render(DollarStoreCamera temporaryCamera)
    {
        var span = Sounds.Span;
        for (var i = 0; i < Count; i++)
        {
            var sound = span[i];
            sound.Render(temporaryCamera);
        }
    }
}