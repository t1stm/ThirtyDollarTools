using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Atlases;
using Shared.Renderer.Planes;
using Sundex.Core;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Engine.Renderer.Enums;
using ThirtyDollarConverter.Objects;
using VisualizerScene.Objects;
using Sundex.Engine.Text;
using VisualizerScene;
using VisualizerScene.Objects.Playfield;
using VisualizerScene.Objects.Playfield.Batch.Chunks;
using VisualizerScene.Objects.Playfield.Batch.Objects;
using VisualizerScene.Objects.Sound_Values;

namespace DrumMasterScene.Components;

public class TaikoLane : IDisposable
{
    private class TaikoChunk(TextBuffer textBuffer, StackCollection stacks) : IDisposable
    {
        public TextBuffer TextBuffer { get; } = textBuffer;
        public StackCollection Stacks { get; } = stacks;
        public List<SoundRenderable> Renderables { get; } = [];
        public List<TaikoSound> Sounds { get; } = [];
        public RenderStack<BackgroundBlip>? BackgroundBlips { get; set; }

        public void Dispose()
        {
            TextBuffer.Dispose();
            Stacks.Dispose();
            BackgroundBlips?.Dispose();
        }
    }

    private readonly List<TaikoChunk> _chunks = [];
    public TaikoTimedSounds TimedSounds { get; }
    public TaikoSoundMap SoundMap { get; }
    public SeekableStopwatch Stopwatch { get; }
    private readonly VisualizerFonts _fonts;

    public Vector2 LanePosition
    {
        get;
        set
        {
            field = value;
            UpdatePosition();
        }
    }

    public Vector2 LaneScale
    {
        get;
        set
        {
            field = value;
            UpdatePosition();
        }
    }

    // _hitBox.Scale.X = this && _hitBox.Scale.Y = this
    public float HitBoxSize
    {
        get;
        set
        {
            field = value;
            UpdatePosition();
        }
    } = 100;

    // LanePosition.X + this = _hitBox.Position.X / _hitBox.Position.Y = centered to (Lane.Y + Lane.Height) / 2f
    public float HitBoxOffset
    {
        get;
        set
        {
            field = value;
            UpdatePosition();
        }
    } = 450;

    // controls whether the sounds will automatically play at the right time, if false the user will have to press them.
    public bool AutoPlay { get; set; }

    private readonly ColoredPlane _lane = new()
    {
        Color = (0, 0, 0, 0.8f)
    };

    private readonly ColoredPlane _hitBox = new()
    {
        Color = (1, 0, 0, 0.25f)
    };

    public List<double> AccuracyScores { get; } = [];
    public int MissCount { get; set; }
    public float HitWindowMs { get; set; } = 100f;

    public TaikoLane(Placement[] placement,
        SeekableStopwatch stopwatch,
        TaikoSoundMap soundMap,
        AtlasStore atlasStore,
        VisualizerFonts fonts,
        PlayfieldSizing sizing)
    {
        _fonts = fonts;
        TimedSounds = new TaikoTimedSounds(placement, soundMap);
        SoundMap = soundMap;
        Stopwatch = stopwatch;

        const int chunkSize = 512;
        TaikoChunk? currentChunk = null;
        RenderableFactory? factory = null;

        for (var i = 0; i < TimedSounds.Sounds.Count; i++)
        {
            if (i % chunkSize == 0)
            {
                var textBuffer = new TextBuffer(fonts.LatoBoldProvider, fonts.DeleteQueue);
                textBuffer.Resize(chunkSize * 32 * 3);
                currentChunk = new TaikoChunk(textBuffer, new StackCollection());
                _chunks.Add(currentChunk);
                factory = new RenderableFactory(atlasStore);
            }

            var sound = TimedSounds.Sounds[i];
            var renderable = factory!.CookUp(sound.Event);

            if (sound.Event is { SoundEvent: "!bg" })
            {
                renderable.Value = new BackgroundEventValue(sound.Event.Value, factory, currentChunk!.TextBuffer,
                    sizing.ValueFontSize);
            }
            else
            {
                RenderableFactory.AssignTextBuffers(renderable, sound.Event, currentChunk!.TextBuffer, sizing, 1f);
            }

            var originalScale = renderable.Scale;
            var aspectRatio = originalScale.X / originalScale.Y;

            renderable.Scale = aspectRatio > 1
                ? (HitBoxSize, HitBoxSize / aspectRatio, 1)
                : (HitBoxSize * aspectRatio, HitBoxSize, 1);

            renderable.Color = Vector4.Zero;
            
            renderable.Value?.Position = new Vector3(-10000, -10000, 0);
            renderable.Volume?.Position = new Vector3(-10000, -10000, 0);
            renderable.Pan?.Position = new Vector3(-10000, -10000, 0);
            
            currentChunk.Renderables.Add(renderable);
            currentChunk.Sounds.Add(sound);

            if (i % chunkSize != chunkSize - 1 && i != TimedSounds.Sounds.Count - 1) continue;
            
            currentChunk.Stacks.AnimatedStacks = factory.AnimatedAtlases;
            currentChunk.Stacks.StaticStacks = factory.StaticAtlases;
            currentChunk.BackgroundBlips = factory.BackgroundBlips;
        }
    }

    public void Dispose()
    {
        foreach (var chunk in _chunks) chunk.Dispose();
        GC.SuppressFinalize(this);
    }

    private int _lastActiveIndex;
    private long _lastTime;

    public void Update()
    {
        var currentTime = Stopwatch.ElapsedMilliseconds;
        var hitBoxX = LanePosition.X + HitBoxOffset;
        var centerY = LanePosition.Y + LaneScale.Y / 2f;

        if (_lastTime > currentTime)
        {
            _lastActiveIndex = 0;
            foreach (var chunk in _chunks)
            {
                foreach (var sound in chunk.Sounds)
                {
                    sound.IsHit = false;
                    sound.HitTime = null;
                }
            }

            AccuracyScores.Clear();
            MissCount = 0;
        }

        _lastTime = currentTime;

        // Fast skip to _lastActiveIndex
        var totalProcessed = _lastActiveIndex;
        var skipChunks = 0;
        var skipInChunk = 0;

        {
            var temp = totalProcessed;
            foreach (var chunk in _chunks)
            {
                if (temp >= chunk.Sounds.Count)
                {
                    temp -= chunk.Sounds.Count;
                    skipChunks++;
                }
                else
                {
                    skipInChunk = temp;
                    break;
                }
            }
        }

        var stopSkipping = false;
        for (var chunkIdx = skipChunks; chunkIdx < _chunks.Count; chunkIdx++)
        {
            var chunk = _chunks[chunkIdx];
            for (var i = skipInChunk; i < chunk.Sounds.Count; i++)
            {
                var sound = chunk.Sounds[i];
                if (!sound.IsHit)
                {
                    stopSkipping = true;
                    break;
                }

                // If it was a miss (no HitTime) or hit long ago, we can skip
                var canSkip = (!sound.HitTime.HasValue && sound.Timestamp < currentTime - 1000) ||
                              (sound.HitTime.HasValue && currentTime - sound.HitTime.Value > 2000);

                if (canSkip) totalProcessed++;
                else
                {
                    stopSkipping = true;
                    break;
                }
            }
            skipInChunk = 0; // Reset for subsequent chunks
            if (stopSkipping) break;
        }

        _lastActiveIndex = totalProcessed;

        for (var chunkIdx = skipChunks; chunkIdx < _chunks.Count; chunkIdx++)
        {
            var chunk = _chunks[chunkIdx];
            for (var i = skipInChunk; i < chunk.Sounds.Count; i++)
            {
                skipInChunk = 0; // Only use for the first chunk we actually process
                var sound = chunk.Sounds[i];
                var renderable = chunk.Renderables[i];

                var timeDiff = sound.Timestamp - currentTime;

                // Optimization: skip sounds far in the future
                if (timeDiff > 5000)
                {
                    if (renderable.Color != Vector4.Zero)
                    {
                        renderable.Color = Vector4.Zero;
                        renderable.Value?.Position = new Vector3(-10000, -10000, 0);
                        renderable.Volume?.Position = new Vector3(-10000, -10000, 0);
                        renderable.Pan?.Position = new Vector3(-10000, -10000, 0);
                        renderable.UpdateModel(false);
                    }

                    continue;
                }

                // Miss detection
                if (timeDiff < -HitWindowMs && !sound.IsHit)
                {
                    sound.IsHit = true; // Mark as processed
                    MissCount++;
                }

                if (AutoPlay && !sound.IsHit && sound.Timestamp <= currentTime)
                {
                    RegisterHit(sound, chunkIdx, i, 0);
                }

                // Horizontal position calculation
                var x = hitBoxX + (float)(timeDiff * sound.ScrollSpeed);
                var y = centerY;

                // Vertical bounce calculation
                if (sound.HitTime.HasValue)
                {
                    var t = (float)(currentTime - sound.HitTime.Value) / 1000f; // time in seconds
                    // Fly up and out. 
                    // v0_y = -800, g = 1500
                    y += -800 * t + 750 * t * t;
                }

                // Visibility check
                var isOffScreen = x < LanePosition.X - HitBoxSize || x > LanePosition.X + LaneScale.X + HitBoxSize;

                if (isOffScreen)
                {
                    if (renderable.Color == Vector4.Zero) continue;
                    renderable.Color = Vector4.Zero;
                    renderable.Value?.Position = new Vector3(-10000, -10000, 0);
                    renderable.Volume?.Position = new Vector3(-10000, -10000, 0);
                    renderable.Pan?.Position = new Vector3(-10000, -10000, 0);
                    renderable.UpdateModel(false);
                }
                else
                {
                    // Hit notes are always visible until off-screen.
                    // Missed notes disappear after they pass the hit window if they didn't have a HitTime.
                    var shouldBeVisible = !sound.IsHit || sound.HitTime.HasValue || (timeDiff >= -HitWindowMs);

                    if (shouldBeVisible)
                    {
                        renderable.SetPosition(new Vector3(x, y, 0), PositionAlign.Center);
                        renderable.Color = Vector4.One;
                        renderable.UpdateModel(false);
                        
                        var box_position = renderable.Position.Xy;
                        var box_scale = renderable.Scale.Xy;
                        
                        var bottom_center = box_position + (box_scale.X / 2f, box_scale.Y);
                        var top_right = box_position + (box_scale.X + 6f, 0f);

                        renderable.Value?.PositionAlign = PositionAlign.Center;
                        renderable.Value?.Position = (bottom_center.X, bottom_center.Y - 1f, 0.5f);

                        renderable.Volume?.PositionAlign = PositionAlign.Top | PositionAlign.Right;
                        renderable.Volume?.Position = (top_right.X, top_right.Y, 0.5f);

                        renderable.Pan?.PositionAlign = PositionAlign.Top | PositionAlign.Left;
                        renderable.Pan?.Position = (box_position.X, box_position.Y, 0.5f);
                        renderable.Update();
                    }
                    else
                    {
                        if (renderable.Color == Vector4.Zero) continue;
                        
                        renderable.Color = Vector4.Zero;
                        renderable.Value?.Position = new Vector3(-10000, -10000, 0);
                        renderable.Volume?.Position = new Vector3(-10000, -10000, 0);
                        renderable.Pan?.Position = new Vector3(-10000, -10000, 0);
                        renderable.UpdateModel(false);
                    }
                }
            }
            
            // If we are deep into the future and no sounds were visible in this chunk,
            // we might be able to break early. But some sounds might have different scroll speeds.
            // However, scroll speeds are usually positive and similar.
            // For safety, let's only break if the first sound of the chunk is very far in the future.
            if (chunk.Sounds.Count > 0 && chunk.Sounds[0].Timestamp - currentTime > 10000)
            {
                break;
            }
        }
    }

    private void RegisterHit(TaikoSound sound, int chunkIndex, int index, double offset)
    {
        sound.IsHit = true;
        sound.HitTime = Stopwatch.ElapsedMilliseconds;
        AccuracyScores.Add(offset);
        // Trigger hit animation if available
        _chunks[chunkIndex].Renderables[index].Bounce();
    }

    public void Keyboard(KeyboardState state)
    {
        if (AutoPlay) return;

        var pressedSound = SoundMap.GetPressedSound(state);
        if (pressedSound == null) return;

        var currentTime = Stopwatch.ElapsedMilliseconds;

        // Find the closest unhit note of the same type within the hit window
        var bestChunkIdx = -1;
        var bestIdx = -1;
        var bestDiff = double.MaxValue;

        var startSearchIdx = _lastActiveIndex;
        var skipChunks = 0;
        var skipInChunk = 0;
        
        {
            var temp = startSearchIdx;
            for (var i = 0; i < _chunks.Count; i++)
            {
                if (temp >= _chunks[i].Sounds.Count)
                {
                    temp -= _chunks[i].Sounds.Count;
                    skipChunks++;
                }
                else
                {
                    skipInChunk = temp;
                    break;
                }
            }
        }

        for (var chunkIdx = skipChunks; chunkIdx < _chunks.Count; chunkIdx++)
        {
            var chunk = _chunks[chunkIdx];
            var foundInRange = false;
            for (var i = skipInChunk; i < chunk.Sounds.Count; i++)
            {
                var sound = chunk.Sounds[i];
                var timeDiff = sound.Timestamp - currentTime;
                
                // If this sound is too far in the future, we can stop searching this and further chunks
                if (timeDiff > HitWindowMs)
                {
                    foundInRange = true; // Not really in range, but to break outer loop
                    break; 
                }

                if (sound.IsHit || sound.Sound != pressedSound) continue;

                if (Math.Abs(timeDiff) <= HitWindowMs)
                {
                    if (Math.Abs(timeDiff) < bestDiff)
                    {
                        bestDiff = Math.Abs(timeDiff);
                        bestChunkIdx = chunkIdx;
                        bestIdx = i;
                    }
                }
            }
            skipInChunk = 0;
            if (foundInRange) break;
        }

        if (bestIdx == -1) return;

        var bestSound = _chunks[bestChunkIdx].Sounds[bestIdx];
        var offset = bestSound.Timestamp - currentTime;
        RegisterHit(bestSound, bestChunkIdx, bestIdx, offset);
    }

    public void Render(DollarStoreCamera camera)
    {
        _lane.Render(camera);
        _hitBox.Render(camera);
        foreach (var chunk in _chunks)
        {
            // Simple culling for chunks
            var firstSound = chunk.Sounds[0];
            var lastSound = chunk.Sounds[^1];
            var currentTime = Stopwatch.ElapsedMilliseconds;

            var hitBoxX = LanePosition.X + HitBoxOffset;
            var firstX = hitBoxX + (float)(firstSound.Timestamp - currentTime) * firstSound.ScrollSpeed;
            var lastX = hitBoxX + (float)(lastSound.Timestamp - currentTime) * lastSound.ScrollSpeed;

            // Sort them just in case scroll speed or timestamps are weird
            var minX = Math.Min(firstX, lastX) - HitBoxSize;
            var maxX = Math.Max(firstX, lastX) + HitBoxSize;

            if (maxX < LanePosition.X || minX > LanePosition.X + LaneScale.X)
                continue;

            chunk.Stacks.Render(camera);
            chunk.BackgroundBlips?.Render(camera);
            chunk.TextBuffer.Render(camera);
        }
    }

    private void UpdatePosition()
    {
        _lane.Position = new Vector3(LanePosition.X, LanePosition.Y, 0);
        _lane.Scale = new Vector3(LaneScale.X, LaneScale.Y, 1);
        _hitBox.Scale = new Vector3(HitBoxSize, HitBoxSize, 1);

        _hitBox.SetPosition(new Vector3(LanePosition.X + HitBoxOffset, LanePosition.Y + LaneScale.Y / 2f, 0),
            PositionAlign.Center);
    }
}