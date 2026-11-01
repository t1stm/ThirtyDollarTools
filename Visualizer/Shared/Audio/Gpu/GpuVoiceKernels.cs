using OpenTK.Graphics.OpenGL;
using Sundex.Engine;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Voices;

namespace Shared.Audio.Gpu;

/// <summary>
///     The GPU half of the voice mixer: two compute programs and the buffers they read, on the
///     app's own GL context, so every call belongs to the render thread. One block is mixed in
///     two passes. Pass 1 gives each workgroup a <see cref="Tile" /> of the block's samples and a
///     chunk of its sounding voices, and writes that chunk's sum; pass 2 adds the chunks up in
///     index order, so a block comes out the same from run to run.
///     <para>
///         Each voice is mixed as <see cref="VoiceList" />'s <c>MixRange</c> does it - the same
///         terms in the same order, the cut's fade, one read of a mono sound - but the sum is
///         split into chunks, so the output matches <see cref="VoiceMixer.MixAll" /> only to
///         within rounding.
///     </para>
/// </summary>
public sealed unsafe class GpuVoiceKernels : IDisposable
{
    /// <summary>Samples per pass-1 workgroup, one per invocation.</summary>
    private const int Tile = 256;

    private const uint SamplesBinding = 0, SoundsBinding = 1, VoicesBinding = 2, ActiveBinding = 3,
        PartialsBinding = 4, OutputBinding = 5;

    private const string MixSource = """
        #version 430 core
        layout(local_size_x = 256) in;

        // VoiceList's Voice, field for field: 5 ints, then 4 floats, 36 bytes
        struct Voice { int start; int end; int cutAt; int sourceOffset; int sound; float ll; float rr; float lr; float rl; };

        layout(std430, binding = 0) readonly buffer SampleData { float sampleData[]; };
        layout(std430, binding = 1) readonly buffer SoundTable { ivec2 soundTable[]; }; // left, right offsets
        layout(std430, binding = 2) readonly buffer VoiceData { Voice voiceData[]; };
        layout(std430, binding = 3) readonly buffer ActiveIndex { int activeIndex[]; };
        layout(std430, binding = 4) writeonly buffer PartialSums { vec2 partialSums[]; };

        uniform int blockStart;
        uniform int frames;
        uniform int activeCount;
        uniform int chunkVoices;
        uniform int chunkOffset;
        uniform int fadeLength;
        uniform int stride;

        shared ivec4 groupRange[256]; // start, end, cutAt, mono
        shared ivec2 groupBase[256];  // where output sample 0 would read, left and right
        shared vec4 groupGain[256];   // ll, rr, lr, rl
        shared vec2 groupMono[256];   // ll + lr, rr + rl: a mono sound's single pass

        void main() {
            int lane = int(gl_LocalInvocationID.x);
            int t = int(gl_WorkGroupID.x) * 256 + lane;
            int chunk = chunkOffset + int(gl_WorkGroupID.y);
            int first = chunk * chunkVoices;
            int last = min(first + chunkVoices, activeCount);
            int at = blockStart + t;
            bool inBlock = t < frames;

            precise float left = 0.0;
            precise float right = 0.0;

            for (int base = first; base < last; base += 256) {
                int n = min(256, last - base);
                barrier();
                if (lane < n) {
                    Voice v = voiceData[activeIndex[base + lane]];
                    ivec2 sound = soundTable[v.sound];
                    int shift = v.sourceOffset - v.start;
                    groupRange[lane] = ivec4(v.start, v.end, v.cutAt, sound.x == sound.y ? 1 : 0);
                    groupBase[lane] = ivec2(sound.x + shift, sound.y + shift);
                    groupGain[lane] = vec4(v.ll, v.rr, v.lr, v.rl);
                    groupMono[lane] = vec2(v.ll + v.lr, v.rr + v.rl);
                }
                barrier();

                if (inBlock) {
                    for (int k = 0; k < n; k++) {
                        ivec4 range = groupRange[k];
                        if (at < range.x || at >= range.y) continue;

                        ivec2 b = groupBase[k];
                        if (at < range.z) {
                            if (range.w != 0) {
                                float s = sampleData[b.x + at];
                                vec2 g = groupMono[k];
                                left += s * g.x;
                                right += s * g.y;
                            } else {
                                // one source channel into both outputs, then the other
                                float sl = sampleData[b.x + at];
                                float sr = sampleData[b.y + at];
                                vec4 g = groupGain[k];
                                left += sl * g.x;
                                right += sl * g.w;
                                left += sr * g.z;
                                right += sr * g.y;
                            }
                        } else {
                            precise float fade = float(range.z + fadeLength - at) / float(fadeLength);
                            float sl = sampleData[b.x + at];
                            float sr = sampleData[b.y + at];
                            vec4 g = groupGain[k];
                            left += sl * g.x * fade;
                            left += sr * g.z * fade;
                            right += sr * g.y * fade;
                            right += sl * g.w * fade;
                        }
                    }
                }
            }

            if (inBlock) partialSums[chunk * stride + t] = vec2(left, right);
        }
        """;

    private const string ReduceSource = """
        #version 430 core
        layout(local_size_x = 256) in;

        layout(std430, binding = 4) readonly buffer PartialSums { vec2 partialSums[]; };
        layout(std430, binding = 5) writeonly buffer MixOutput { vec2 mixOutput[]; }; // interleaved L, R

        uniform int frames;
        uniform int chunks;
        uniform int stride;
        uniform int outputOffset;

        void main() {
            int t = int(gl_GlobalInvocationID.x);
            if (t >= frames) return;

            precise vec2 sum = vec2(0.0);
            for (int c = 0; c < chunks; c++) sum += partialSums[c * stride + t];
            mixOutput[outputOffset + t] = sum;
        }
        """;

    private readonly int _mix, _reduce;
    private readonly int _samples, _sounds, _voices, _active, _partials, _output;
    private readonly int _uBlockStart, _uFrames, _uActiveCount, _uChunkVoices, _uChunkOffset, _uFadeLength, _uStride;
    private readonly int _rFrames, _rChunks, _rStride, _rOutputOffset;

    /// <summary>Where each uploaded sound's channels start in the samples buffer, in floats.</summary>
    private readonly Dictionary<AudioData<float>, (int Left, int Right)> _soundOffsets =
        new(ReferenceEqualityComparer.Instance);

    private long _activeBytes, _partialsBytes, _outputBytes;
    private int[] _table = [];
    private int _outputFrames;

    public GpuVoiceKernels(int chunkVoices = 1024)
    {
        ChunkVoices = chunkVoices;
        MaxBufferBytes = GL.GetInteger64((GetPName)All.MaxShaderStorageBlockSize);

        _mix = Link(MixSource);
        _reduce = Link(ReduceSource);
        _uBlockStart = GL.GetUniformLocation(_mix, "blockStart");
        _uFrames = GL.GetUniformLocation(_mix, "frames");
        _uActiveCount = GL.GetUniformLocation(_mix, "activeCount");
        _uChunkVoices = GL.GetUniformLocation(_mix, "chunkVoices");
        _uChunkOffset = GL.GetUniformLocation(_mix, "chunkOffset");
        _uFadeLength = GL.GetUniformLocation(_mix, "fadeLength");
        _uStride = GL.GetUniformLocation(_mix, "stride");
        _rFrames = GL.GetUniformLocation(_reduce, "frames");
        _rChunks = GL.GetUniformLocation(_reduce, "chunks");
        _rStride = GL.GetUniformLocation(_reduce, "stride");
        _rOutputOffset = GL.GetUniformLocation(_reduce, "outputOffset");

        // every binding gets storage up front, so a dispatch never sees an empty buffer
        _samples = Create(16);
        _sounds = Create(16);
        _voices = Create(16);
        _active = Create(16);
        _partials = Create(16);
        _output = Create(16);
    }

    /// <summary>Voices per pass-1 chunk: fewer means more workgroups and more partial sums to add up.</summary>
    public int ChunkVoices { get; }

    /// <summary>The largest buffer a shader may read; a list whose voices or sounds don't fit stays on the CPU.</summary>
    public long MaxBufferBytes { get; }

    /// <summary>The list the buffers hold, retained by the kernels until the next one.</summary>
    public VoiceList? List { get; private set; }

    /// <summary>How many of <see cref="List" />'s voices are on the GPU, from index 0 - see <see cref="UploadVoices" />.</summary>
    public int UploadedVoices { get; private set; }

    /// <summary>Size of the samples buffer: every channel of every uploaded sound.</summary>
    public long SampleBytes { get; private set; }

    public void Dispose()
    {
        List?.Dispose();
        List = null;
        GL.DeleteProgram(_mix);
        GL.DeleteProgram(_reduce);
        foreach (var buffer in (ReadOnlySpan<int>)[_samples, _sounds, _voices, _active, _partials, _output])
            GL.DeleteBuffer(buffer);
    }

    /// <summary>Whether the context can run the kernels at all.</summary>
    public static bool IsSupported(GLInfo info)
    {
        return info.SupportsComputeShaders;
    }

    public int Chunks(int activeCount)
    {
        return (activeCount + ChunkVoices - 1) / ChunkVoices;
    }

    /// <summary>
    ///     Points the buffers at <paramref name="list" />: uploads its sound table, its sounds if any
    ///     are new, and allocates room for its voices, which <see cref="UploadVoices" /> fills.
    ///     Returns false, holding nothing, when the list is too big for a shader to read.
    /// </summary>
    public bool SetList(VoiceList list)
    {
        if (ReferenceEquals(list, List)) return true;

        List?.Dispose();
        List = null;
        UploadedVoices = 0;

        var voice_bytes = (long)list.Count * sizeof(Voice);
        if (voice_bytes > MaxBufferBytes) return false;

        var sounds = list.Sounds;
        var fresh = false;
        foreach (var sound in sounds) fresh |= !_soundOffsets.ContainsKey(sound);
        if (fresh && !UploadSounds(sounds)) return false;

        if (_table.Length < 2 * sounds.Length) _table = new int[2 * sounds.Length];
        for (var i = 0; i < sounds.Length; i++)
        {
            var (left, right) = _soundOffsets[sounds[i]];
            _table[2 * i] = left;
            _table[2 * i + 1] = right;
        }

        fixed (int* table = _table)
        {
            Allocate(_sounds, Math.Max(16, (long)sounds.Length * 2 * sizeof(int)), table, BufferUsage.StaticDraw);
        }

        Allocate(_voices, Math.Max(16, voice_bytes), null, BufferUsage.DynamicDraw);
        list.Retain();
        List = list;
        return true;
    }

    /// <summary>
    ///     Uploads the next voices of <see cref="List" />, at most <paramref name="maxBytes" /> of
    ///     them, so a big list spreads over frames. Returns true once all of them are up.
    /// </summary>
    public bool UploadVoices(long maxBytes)
    {
        var list = List;
        if (list == null) return true;

        var voices = list.Voices;
        var count = (int)Math.Min(voices.Length - UploadedVoices, Math.Max(1, maxBytes / sizeof(Voice)));
        if (count > 0)
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _voices);
            fixed (Voice* data = voices.Slice(UploadedVoices, count))
            {
                GL.BufferSubData(BufferTarget.ShaderStorageBuffer, (IntPtr)((long)UploadedVoices * sizeof(Voice)),
                    count * sizeof(Voice), data);
            }

            UploadedVoices += count;
        }

        return UploadedVoices >= voices.Length;
    }

    /// <summary>Sizes the output ring: <paramref name="slots" /> blocks of up to <paramref name="frames" />, interleaved.</summary>
    public void EnsureOutput(int slots, int frames)
    {
        _outputFrames = Math.Max(_outputFrames, frames);
        Grow(_output, ref _outputBytes, (long)slots * _outputFrames * 2 * sizeof(float), BufferUsage.StreamRead);
    }

    /// <summary>
    ///     Uploads one block's sounding voices - indices into <see cref="List" />, ascending, every one
    ///     below <see cref="UploadedVoices" /> - and sizes the partial sums for them. Pass 1 and 2 of
    ///     this block go before the next block's <see cref="BeginBlock" />.
    /// </summary>
    public void BeginBlock(ReadOnlySpan<int> active, int frames)
    {
        Grow(_partials, ref _partialsBytes, (long)Math.Max(1, Chunks(active.Length)) * Stride(frames) * 2 * sizeof(float),
            BufferUsage.DynamicCopy);
        if (active.IsEmpty) return;

        var bytes = (long)active.Length * sizeof(int);
        Grow(_active, ref _activeBytes, bytes, BufferUsage.StreamDraw);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _active);
        fixed (int* data = active)
        {
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, (IntPtr)bytes, data);
        }
    }

    /// <summary>Pass 1 over chunks <c>[firstChunk, firstChunk + chunkCount)</c> of the block <see cref="BeginBlock" /> uploaded.</summary>
    public void MixChunks(int blockStart, int frames, int activeCount, int firstChunk, int chunkCount)
    {
        if (chunkCount <= 0) return;

        Bind();
        GL.UseProgram(_mix);
        GL.Uniform1i(_uBlockStart, blockStart);
        GL.Uniform1i(_uFrames, frames);
        GL.Uniform1i(_uActiveCount, activeCount);
        GL.Uniform1i(_uChunkVoices, ChunkVoices);
        GL.Uniform1i(_uChunkOffset, firstChunk);
        GL.Uniform1i(_uFadeLength, List?.FadeLength ?? 1);
        GL.Uniform1i(_uStride, Stride(frames));
        GL.DispatchCompute((uint)(Stride(frames) / Tile), (uint)chunkCount, 1);
        GL.MemoryBarrier(MemoryBarrierMask.ShaderStorageBarrierBit);
    }

    /// <summary>Pass 2: adds up all <paramref name="chunks" /> of the block into output slot <paramref name="slot" />.</summary>
    public void Reduce(int frames, int chunks, int slot)
    {
        Bind();
        GL.UseProgram(_reduce);
        GL.Uniform1i(_rFrames, frames);
        GL.Uniform1i(_rChunks, chunks);
        GL.Uniform1i(_rStride, Stride(frames));
        GL.Uniform1i(_rOutputOffset, slot * _outputFrames);
        GL.DispatchCompute((uint)(Stride(frames) / Tile), 1, 1);
        GL.MemoryBarrier(MemoryBarrierMask.ShaderStorageBarrierBit | MemoryBarrierMask.BufferUpdateBarrierBit);
    }

    /// <summary>Copies output slot <paramref name="slot" /> into <paramref name="interleaved" />; call once its fence has signalled, or it stalls.</summary>
    public void ReadOutput(int slot, Span<float> interleaved)
    {
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _output);
        fixed (float* data = interleaved)
        {
            GL.GetBufferSubData(BufferTarget.ShaderStorageBuffer, (IntPtr)((long)slot * _outputFrames * 2 * sizeof(float)),
                (IntPtr)((long)interleaved.Length * sizeof(float)), data);
        }
    }

    private static int Stride(int frames)
    {
        return (frames + Tile - 1) / Tile * Tile;
    }

    private void Bind()
    {
        GL.BindBufferBase(BufferTarget.ShaderStorageBuffer, SamplesBinding, _samples);
        GL.BindBufferBase(BufferTarget.ShaderStorageBuffer, SoundsBinding, _sounds);
        GL.BindBufferBase(BufferTarget.ShaderStorageBuffer, VoicesBinding, _voices);
        GL.BindBufferBase(BufferTarget.ShaderStorageBuffer, ActiveBinding, _active);
        GL.BindBufferBase(BufferTarget.ShaderStorageBuffer, PartialsBinding, _partials);
        GL.BindBufferBase(BufferTarget.ShaderStorageBuffer, OutputBinding, _output);
    }

    /// <summary>
    ///     Replaces the samples buffer with <paramref name="sounds" />: the left channel of each, and
    ///     the right of a stereo source - a mono one points both sides at its one copy, as
    ///     <c>MixRange</c> reads it.
    /// </summary>
    private bool UploadSounds(ReadOnlySpan<AudioData<float>> sounds)
    {
        long floats = 0;
        foreach (var sound in sounds)
            floats += Stereo(sound) ? 2L * sound.Samples[0].Length : sound.Samples[0].Length;
        if (floats * sizeof(float) > MaxBufferBytes || floats > int.MaxValue) return false;

        _soundOffsets.Clear();
        SampleBytes = floats * sizeof(float);
        Allocate(_samples, Math.Max(16, SampleBytes), null, BufferUsage.DynamicDraw);

        var at = 0;
        foreach (var sound in sounds)
        {
            if (_soundOffsets.ContainsKey(sound)) continue;

            var left = at;
            at = Put(sound.Samples[0], at);
            var right = Stereo(sound) ? at : left;
            if (Stereo(sound)) at = Put(sound.Samples[1], at);
            _soundOffsets.Add(sound, (left, right));
        }

        return true;

        static bool Stereo(AudioData<float> sound)
        {
            return sound.Samples.Length > 1 && sound.SourceChannels > 1;
        }

        int Put(float[] channel, int offset)
        {
            fixed (float* data = channel)
            {
                GL.BufferSubData(BufferTarget.ShaderStorageBuffer, (IntPtr)((long)offset * sizeof(float)),
                    (IntPtr)((long)channel.Length * sizeof(float)), data);
            }

            return offset + channel.Length;
        }
    }

    private static int Create(long bytes)
    {
        var buffer = GL.GenBuffer();
        Allocate(buffer, bytes, null, BufferUsage.DynamicDraw);
        return buffer;
    }

    /// <summary>Leaves <paramref name="buffer" /> bound to the generic SSBO target, so a following BufferSubData lands in it.</summary>
    private static void Allocate(int buffer, long bytes, void* data, BufferUsage usage)
    {
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, buffer);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, (IntPtr)bytes, data, usage);
    }

    private static void Grow(int buffer, ref long capacity, long bytes, BufferUsage usage)
    {
        if (bytes <= capacity) return;

        capacity = Math.Max(bytes, capacity + capacity / 2);
        Allocate(buffer, capacity, null, usage);
    }

    private static int Link(string source)
    {
        var shader = GL.CreateShader(ShaderType.ComputeShader);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShaderi(shader, ShaderParameterName.CompileStatus, out var compiled);
        if (compiled == 0)
        {
            GL.GetShaderInfoLog(shader, out var log);
            GL.DeleteShader(shader);
            throw new InvalidOperationException($"Voice mixer kernel failed to compile: {log}");
        }

        var program = GL.CreateProgram();
        GL.AttachShader(program, shader);
        GL.LinkProgram(program);
        GL.DetachShader(program, shader);
        GL.DeleteShader(shader);
        GL.GetProgrami(program, ProgramProperty.LinkStatus, out var linked);
        if (linked != 0) return program;

        GL.GetProgramInfoLog(program, out var error);
        GL.DeleteProgram(program);
        throw new InvalidOperationException($"Voice mixer kernel failed to link: {error}");
    }
}
