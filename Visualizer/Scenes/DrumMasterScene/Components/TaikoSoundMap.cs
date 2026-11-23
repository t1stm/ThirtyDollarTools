using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Engine.Input;

namespace DrumMasterScene.Components;

public class TaikoSoundMap
{
    private Dictionary<Keys, string> KeyToSoundMap { get; } = [];
    private Dictionary<string, Keys> SoundToKeyMap { get; } = [];

    public void Bind(Keys key, string sound)
    {
        KeyToSoundMap[key] = sound;
        SoundToKeyMap[sound] = key;
    }

    public string? GetPressedSound(KeyboardInput keyboardState)
    {
        foreach (var (key, sound) in KeyToSoundMap)
            if (keyboardState.IsKeyPressed(key))
                return sound;

        return null;
    }

    public bool Has(string sound)
    {
        return SoundToKeyMap.ContainsKey(sound);
    }
}