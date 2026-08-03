namespace EditorScene;

/// <summary>
///     Session-only mute/solo state, never saved. Lifted out of <see cref="EditorState" />.
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

    /// <summary>FL semantics: any solo wins; otherwise everything not muted sounds.</summary>
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