using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Base_Objects.Settings;
using ThirtyDollarVisualizer.Helpers.Decoders;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Playfield;

namespace ThirtyDollarVisualizer.Scenes.Application;

public class PlayfieldContainer(
    PlayfieldSettings settings,
    SequencePlayer sequencePlayer,
    Vector2i initialViewport,
    Vector3? initialCameraPosition = null) : IDisposable
{
    private static readonly DollarStoreCamera OffscreenCamera = new((-1, -1, -1), (0, 0));
    private readonly CancellationTokenSource _tokenSource = new();
    private TimedEvents _currentEvents;

    private Playfield[] _playfields = [];
    private bool _playfieldsUpdated;

    private CancellationToken Token => _tokenSource.Token;

    public DollarStoreCamera Camera { get; set; } =
        new(initialCameraPosition ?? (0, -300, 0), initialViewport, settings.ScrollSpeed);

    public DollarStoreCamera StaticCamera { get; set; } = new(Vector3.Zero, initialViewport, settings.ScrollSpeed);
    public BackgroundPlane BackgroundPlane { get; } = new(settings.BackgroundColor);
    public FlashOverlayPlane FlashOverlayPlane { get; } = new(Vector4.One);
    public CameraFollowMode CameraFollowMode { get; set; } = CameraFollowMode.TDWLike;
    public double SequenceVolume { get; private set; } = 100;
    public float LastBPM { get; private set; } = 300;
    public int CurrentSequence { get; private set; }

    public void Dispose()
    {
        foreach (var playfield in _playfields)
            playfield.Dispose();
        _playfields = [];

        _tokenSource.Cancel();
        _tokenSource.Dispose();
        GC.SuppressFinalize(this);
    }

    public void ChangeFromTimedEvents(TimedEvents events)
    {
        _currentEvents = events;
        var sequences_count = events.Sequences.Length;
        var playfields = new Playfield[sequences_count];

        _ = Task.Run(() =>
        {
            foreach (var placement in events.Placement.Where(p =>
                         p.Event is { SoundEvent: "!divider", Value: > 0 and <= 9 }
                             or BookmarkEvent { Value: >= 0 and <= 9 }))
            {
                if (Token.IsCancellationRequested) return;
                var time = placement.Index * 1000f / events.TimingSampleRate;
                var idx = (int)placement.Event.Value;

                sequencePlayer.SetBookmarkTo(idx, (long)time);
            }
        }, Token);

        for (var index = 0; index < events.Sequences.Length; index++)
        {
            var sequence = events.Sequences[index];

            playfields[index] = new Playfield(settings);
            playfields[index].UpdateSounds(sequence);
        }

        // dispose old playfields
        foreach (var playfield in _playfields.AsSpan()) playfield.Dispose();

        _playfields = playfields;
        _playfieldsUpdated = true;
        
        RegisterSequencePlayerEvents(sequencePlayer);
    }

    public void Reset()
    {
        CurrentSequence = 0;
        LastBPM = 300;
        SequenceVolume = 100;
    }

    public void Update(double deltaTime)
    {
        BackgroundPlane.Update();
        FlashOverlayPlane.Update();
        Camera.Update(deltaTime);
        StaticCamera.Update(deltaTime);

        if (!_playfieldsUpdated) return;

        foreach (var playfield in _playfields)
            playfield.Render(OffscreenCamera, 0, 0);
        _playfieldsUpdated = false;
    }

    public void Render(double renderDelta)
    {
        BackgroundPlane.Render(StaticCamera);

        if (_playfields.Length > 0)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(CurrentSequence, _playfields.Length);

            var currentPlayfield = _playfields.AsSpan()[CurrentSequence];
            currentPlayfield.Render(Camera, Camera.GetRenderScale(), renderDelta);
        }

        FlashOverlayPlane.Render(StaticCamera);
    }

    public void Resize(Vector2i viewport)
    {
        var width = viewport.X;
        var height = viewport.Y;

        Camera.Viewport = StaticCamera.Viewport = (width, height);
        Camera.UpdateMatrix();
        StaticCamera.UpdateMatrix();
    }

    /// <summary>
    ///     Call this method when there is a change to the objects of a given sequence.
    /// </summary>
    /// <param name="sequenceIndex">The changed sequence's index.</param>
    private void HandleSequenceChange(int sequenceIndex)
    {
        if (_currentEvents.Placement.Length <= 1) return;
        var old_sequence = CurrentSequence;
        CurrentSequence = sequenceIndex;

        if (old_sequence != CurrentSequence)
        {
            // reset debugging values to their default ones
            SequenceVolume = 100d;
            LastBPM = 300;
        }

        if (old_sequence >= CurrentSequence) return;
        Camera.SetPosition((0, -300, 0));

        // values for the loop below
        var current_placement = sequencePlayer.PlacementIndex;
        var current_index = _currentEvents.Placement[current_placement].Index;
        var max_index_change = _currentEvents.TimingSampleRate / 2; // 500 ms
        var max_index = current_index + (ulong)max_index_change;

        // checks if there is a color event in the next 500ms
        for (var i = current_placement; i < _currentEvents.Placement.Length; i++)
        {
            var index = Math.Clamp(i, 0, _currentEvents.Placement.Length - 1);
            var placement = _currentEvents.Placement[index];
            if (placement.Index > max_index) break;
            if (placement.Event.SoundEvent is "!bg") return;
        }

        // changes the background color to the default one if there isn't a color event in the next 500ms
        BackgroundPlane.TransitionToColor(settings.BackgroundColor, 0.33f);
    }

    private void RegisterSequencePlayerEvents(SequencePlayer player)
    {
        player.SubscribeSequenceChange(HandleSequenceChange);
        player.SubscribeActionToEvent(string.Empty, NormalSubscription);
        player.SubscribeActionToEvent("!speed", SpeedEventHandler);
        player.SubscribeActionToEvent("!bg", BackgroundEventHandler);
        player.SubscribeActionToEvent("!flash", FlashEventHandler);
        player.SubscribeActionToEvent("!pulse", PulseEventHandler);
        player.SubscribeActionToEvent("!loopmany", LoopManyEventHandler);
        player.SubscribeActionToEvent("!stop", StopEventHandler);
        player.SubscribeActionToEvent("!volume", VolumeEventHandler);
    }

    private SoundRenderable? GetRenderable(Placement placement, int sequenceIndex)
    {
        if (sequenceIndex >= _playfields.Length) return null;

        var playfields = _playfields.AsSpan();
        var objects = CollectionsMarshal.AsSpan(playfields[sequenceIndex].Renderables);

        var len = objects.Length;
        var placement_idx = (int)placement.SequenceIndex;
        var element = placement_idx >= len || placement_idx < 0 ? null : objects[placement_idx];

        return element;
    }

    private void CameraBoundsCheck(Placement placement, int sequenceIndex)
    {
        var element = GetRenderable(placement, sequenceIndex);
        if (element == null) return;

        var position = element.Position + element.Translation;
        var scale = element.Scale;

        switch (CameraFollowMode)
        {
            case var follow_mode when follow_mode.HasFlag(CameraFollowMode.TDWLike):
            {
                float margin = settings.PlayfieldSizing.SoundSize;

                if (!Camera.IsOutsideOfCameraView(position, scale, margin) &&
                    placement.Event.SoundEvent is not "!divider") break;

                var pos = new Vector3(0, position.Y - margin, 0f);

                if (CameraFollowMode.HasFlag(CameraFollowMode.NoAnimation))
                {
                    Camera.SetPosition(pos);
                    return;
                }

                Camera.ScrollTo(pos);
                return;
            }

            case var follow_mode when follow_mode.HasFlag(CameraFollowMode.CurrentLine):
            {
                var pos = position * Vector3.UnitY - Vector3.UnitY * (Camera.Height / 2f);

                if (CameraFollowMode.HasFlag(CameraFollowMode.NoAnimation))
                {
                    Camera.SetPosition(pos);
                    return;
                }

                Camera.ScrollTo(pos);
                return;
            }
        }
    }

    private void NormalSubscription(Placement placement, int index)
    {
        var element = GetRenderable(placement, index);
        if (element == null) return;
        CameraBoundsCheck(placement, index);

        if ((placement.Event.SoundEvent?.StartsWith('!') ?? false) || placement.Event is ICustomActionEvent)
        {
            element.Fade();
            element.Expand();
        }
        else if (placement.Event is not ICustomActionEvent)
        {
            element.Bounce();
        }
    }

    private void SpeedEventHandler(Placement placement, int index)
    {
        var val = (float)placement.Event.Value;

        LastBPM = placement.Event.ValueScale switch
        {
            ValueScale.None => val,
            ValueScale.Add => LastBPM + val,
            ValueScale.Times => LastBPM * val,
            ValueScale.Divide => LastBPM / val,

            _ => LastBPM
        };
    }

    private void BackgroundEventHandler(Placement placement, int index)
    {
        var (color, seconds) = BackgroundParser.ParseFromDouble(placement.Event.Value);
        BackgroundPlane.TransitionToColor(color, seconds);
    }

    private void FlashEventHandler(Placement placement, int index)
    {
        FlashOverlayPlane.Flash();
    }

    private void PulseEventHandler(Placement placement, int index)
    {
        var parsed_value = (long)placement.Event.Value;
        var repeats = (byte)parsed_value;
        float frequency = (short)(parsed_value >> 8);

        var computed_frequency = frequency * 1000f / (LastBPM / 60);
        Camera.Pulse(repeats, computed_frequency);
        StaticCamera.Pulse(repeats, computed_frequency);
    }

    private void LoopManyEventHandler(Placement placement, int sequenceIndex)
    {
        if (sequenceIndex >= _playfields.Length) return;

        var element = GetRenderable(placement, sequenceIndex);
        element?.SetValue(placement.Event, ValueChangeWrapMode.RemoveTexture);
    }

    private void StopEventHandler(Placement placement, int sequenceIndex)
    {
        if (sequenceIndex >= _playfields.Length) return;

        var element = GetRenderable(placement, sequenceIndex);
        element?.SetValue(placement.Event, ValueChangeWrapMode.ResetToDefault);
    }

    private void VolumeEventHandler(Placement placement, int index)
    {
        SequenceVolume = placement.Event.WorkingVolume;
    }

    public void ResetAllAnimations()
    {
        var index = 0;
        foreach (var placement in _currentEvents.Placement)
        {
            var renderable = GetRenderable(placement, index);
            renderable?.ResetAnimations();

            if (placement.Event is EndEvent) index++;
        }
    }
}