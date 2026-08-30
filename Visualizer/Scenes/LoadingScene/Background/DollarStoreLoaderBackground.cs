using OpenTK.Mathematics;
using Shared;
using Shared.Atlases;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Renderer.Shaders;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace LoadingScene.Background;

[PreloadGraphicsContext]
public class DollarStoreLoaderBackground(DeleteQueue deleteQueue) : IGamePreloadable
{
    private const int SoundSize = 32;
    private static readonly Vector4 SoundColorMultiply = new(1f, 1f, 1f, 0.4f);

    private readonly List<AnimatedTexture> _animatedSoundData = [];
    private readonly Dictionary<FramedAtlas, RenderStack<SoundData>> _animatedSounds = [];

    private readonly DollarStoreCamera _camera = new((0, 0, 0), (1920, 1080));
    private readonly Random _random = new();
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly List<StaticTexture> _staticSoundData = [];

    private readonly Dictionary<StaticSoundAtlas, RenderStack<StaticSound>> _staticSounds = [];

    /// <summary>How far into the exit the field is, 0-1. See <see cref="Drain" />.</summary>
    private float _drain;

    /// <summary>True once the sprites have been re-aimed - the bounce is off from then on.</summary>
    private bool _draining;

    public static Shader AnimatedShader { get; private set; } = null!;
    public static Shader StaticShader { get; private set; } = null!;

    public required AtlasStore AtlasStore { get; init; }

    public static void Preload(AssetProvider assetProvider)
    {
        AnimatedShader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/Playfield/Chunk/Animated");
        StaticShader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/Playfield/Chunk/Static");
    }

    private float RandomFloat(float min, float max)
    {
        return (float)_random.NextDouble() * (max - min) + min;
    }

    public void AddSound(string sound)
    {
        _semaphore.Wait();
        try
        {
            if (AtlasStore.AnimatedSounds.TryGetValue(sound, out var framedAtlas))
            {
                HandleAnimated(framedAtlas);
                return;
            }

            if (!AtlasStore.StaticSounds.TryGetValue(sound, out var staticAtlas)) return;
            HandleStatic(sound, staticAtlas);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void HandleStatic(string sound, StaticSoundAtlas staticAtlas)
    {
        if (!_staticSounds.TryGetValue(staticAtlas, out var renderBuffer))
            _staticSounds.Add(staticAtlas, renderBuffer = new RenderStack<StaticSound>(deleteQueue)
            {
                Shader = StaticShader
            });

        var rect = staticAtlas.GetSoundUV(sound);
        var staticSound = new StaticSound
        {
            Data = new SoundData
            {
                Model = Matrix4.Identity * Matrix4.CreateScale(SoundSize, SoundSize, 1) *
                        Matrix4.CreateTranslation(RandomFloat(0, _camera.Width), RandomFloat(0, _camera.Height), 0),
                RGBA = SoundColorMultiply
            },
            TextureUV = QuadUV.FromRectangle(rect, staticAtlas.Width, staticAtlas.Height)
        };

        renderBuffer.List.Add(staticSound);
        var reference = renderBuffer.List.GetReferenceAt(renderBuffer.List.Count - 1);
        _staticSoundData.Add(new StaticTexture(reference)
        {
            Velocity = new Vector2(RandomFloat(-1, 1), RandomFloat(-1, 1))
        });
    }

    private void HandleAnimated(FramedAtlas framedAtlas)
    {
        if (!_animatedSounds.TryGetValue(framedAtlas, out var renderBuffer))
            _animatedSounds.Add(framedAtlas, renderBuffer = new RenderStack<SoundData>(deleteQueue)
            {
                Shader = AnimatedShader
            });

        var soundData = new SoundData
        {
            Model = Matrix4.Identity * Matrix4.CreateScale(SoundSize, SoundSize, 1) *
                    Matrix4.CreateTranslation(RandomFloat(0, _camera.Width), RandomFloat(0, _camera.Height), 0),
            RGBA = SoundColorMultiply
        };

        renderBuffer.List.Add(soundData);
        var reference = renderBuffer.List.GetReferenceAt(renderBuffer.List.Count - 1);

        _animatedSoundData.Add(new AnimatedTexture(reference)
        {
            Velocity = new Vector2(RandomFloat(-1, 1), RandomFloat(-1, 1))
        });
    }

    /// <summary>
    ///     Drives the field's exit: the sprites stop bouncing off the edges, scatter upward
    ///     and fade out as the home screen comes up underneath them. Progress runs 0 to 1, and
    ///     the first call above zero re-aims them from the drift they are already carrying.
    /// </summary>
    public void Drain(float progress)
    {
        _drain = progress;
        if (_draining || progress <= 0f) return;
        _draining = true;

        _semaphore.Wait();
        try
        {
            // Lateral drift is kept, damped, so the scatter reads as the same field leaving.
            // Y is negative because the loader's camera has its origin at the top edge.
            //
            // Speed is per frame rather than per second, matching the drift it replaces, and
            // sized for roughly half a screen of travel over the exit - fast enough to still
            // be moving when the rest of the transition ends.
            foreach (var sound in _animatedSoundData)
                sound.Velocity = new Vector2(sound.Velocity.X * 0.4f, -RandomFloat(4, 7));

            foreach (var sound in _staticSoundData)
                sound.Velocity = new Vector2(sound.Velocity.X * 0.4f, -RandomFloat(4, 7));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Resize(int x, int y)
    {
        _camera.Viewport = (x, y);
    }

    public void Update()
    {
        _semaphore.Wait();
        try
        {
            var alpha = SoundColorMultiply.W * (1f - _drain);

            foreach (var sound in _animatedSoundData)
            {
                var oldValue = sound.Reference.Value;

                var x = oldValue.Model.M41;
                var y = oldValue.Model.M42;
                var w = x + SoundSize;
                var h = y + SoundSize;

                if (!_draining)
                    sound.Velocity = GetNewVelocity(sound.Velocity, x, y, w, h, _camera.Viewport);
                else oldValue.RGBA.W = alpha;

                oldValue.Model *= Matrix4.CreateTranslation(sound.Velocity.X, sound.Velocity.Y, 0);
                sound.Reference.Value = oldValue;
            }

            foreach (var sound in _staticSoundData)
            {
                var oldValue = sound.Reference.Value;

                var x = oldValue.Data.Model.M41;
                var y = oldValue.Data.Model.M42;
                var w = x + SoundSize;
                var h = y + SoundSize;

                if (!_draining)
                    sound.Velocity = GetNewVelocity(sound.Velocity, x, y, w, h, _camera.Viewport);
                else oldValue.Data.RGBA.W = alpha;

                oldValue.Data.Model *= Matrix4.CreateTranslation(sound.Velocity.X, sound.Velocity.Y, 0);
                sound.Reference.Value = oldValue;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static Vector2 GetNewVelocity(Vector2 velocity, float x, float y, float w, float h, Vector2i viewport)
    {
        if ((x < 0 && velocity.X < 0) || (w > viewport.X && velocity.X > 0)) velocity.X *= -1;

        if ((y < 0 && velocity.Y < 0) || (h > viewport.Y && velocity.Y > 0)) velocity.Y *= -1;

        return velocity;
    }

    public void Render()
    {
        _semaphore.Wait();
        try
        {
            AtlasStore.Update();
            _camera.UpdateMatrix();

            foreach (var (atlas, soundBuffer) in _animatedSounds)
            {
                atlas.Bind();
                AnimatedShader.Use();
                atlas.SetUniforms(AnimatedShader);
                soundBuffer.Render(_camera);
            }

            foreach (var (atlas, renderStack) in _staticSounds)
            {
                atlas.Bind();
                renderStack.Render(_camera);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}