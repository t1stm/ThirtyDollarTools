using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarConverter.Tests;

public class AudioMixerTests
{
    [Fact]
    public void Sum_AddsNewTracks()
    {
        // Arrange
        const int length = 10;
        var data1 = AudioData<float>.WithLength(1, length);
        var mixer1 = new AudioMixer(data1, AudioLayout.AudioMono);

        var data2 = AudioData<float>.WithLength(1, length);
        for (var i = 0; i < length; i++) data2.Samples[0][i] = 0.5f;
        var mixer2 = new AudioMixer(AudioData<float>.WithLength(1, length), AudioLayout.AudioMono);
        mixer2.AddTrack("new_track", data2, AudioLayout.AudioMono);

        // Act
        mixer1.Sum(mixer2);

        // Assert
        Assert.True(mixer1.HasTrack("new_track", AudioLayout.AudioMono));
        var track = mixer1.GetTrack("new_track", AudioLayout.AudioMono);
        for (var i = 0; i < length; i++)
        {
            Assert.Equal(0.5f, track.Samples[0][i], 1e-6f);
        }
    }
}
