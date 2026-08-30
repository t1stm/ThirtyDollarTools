namespace EditorScene;

/// <summary>
///     Per-channel mute/solo state for the session; never saved with the project.
/// </summary>
public sealed class MuteSolo
{
    private readonly HashSet<int> _muted = [];
    private readonly HashSet<int> _soloed = [];

    public bool AnySoloed => _soloed.Count > 0;

    public void ToggleMute(int channel)
    {
        if (!_muted.Add(channel)) _muted.Remove(channel);
    }

    public void ToggleSolo(int channel)
    {
        if (!_soloed.Add(channel)) _soloed.Remove(channel);
    }

    public bool IsMuted(int channel)
    {
        return _muted.Contains(channel);
    }

    public bool IsSoloed(int channel)
    {
        return _soloed.Contains(channel);
    }

    /// <summary>Any soloed channel silences the rest; with nothing soloed, every unmuted channel sounds.</summary>
    public bool IsChannelAudible(int channel)
    {
        return _soloed.Count > 0 ? _soloed.Contains(channel) : !_muted.Contains(channel);
    }

    public void Clear()
    {
        _muted.Clear();
        _soloed.Clear();
    }
}