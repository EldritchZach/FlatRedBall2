using System;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using NVorbis;

namespace FlatRedBall2.Audio;

/// <summary>
/// <see cref="IMusicBackend"/> that streams a raw OGG Vorbis file through a
/// <see cref="DynamicSoundEffectInstance"/> instead of <see cref="MediaPlayer"/>, so <see cref="Pitch"/>
/// is real (backed by the SFX pitch pipeline) rather than a no-op.
/// </summary>
internal sealed class StreamingOggMusicBackend : IMusicBackend
{
    // ~92.9ms of audio per submitted buffer at 44100Hz mono; matches the chunk size validated by
    // the diagnostics/MusicPitchSpike feasibility spike for issue #799.
    private const int ChunkSizeInFloats = 4096;

    private readonly VorbisReader _reader;
    private readonly DynamicSoundEffectInstance _instance;
    private readonly float[] _floatBuffer = new float[ChunkSizeInFloats];
    private bool _reachedEndOfStream;

    internal StreamingOggMusicBackend(VorbisReader reader, bool loop)
    {
        _reader = reader;
        IsRepeating = loop;

        var channels = reader.Channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo;
        _instance = new DynamicSoundEffectInstance(reader.SampleRate, channels);
        _instance.BufferNeeded += OnBufferNeeded;
    }

    public float Volume
    {
        get => _instance.Volume;
        set => _instance.Volume = value;
    }

    public float Pitch
    {
        get => _instance.Pitch;
        set => _instance.Pitch = value;
    }

    public bool IsRepeating { get; set; }

    public MediaState State => _instance.State switch
    {
        SoundState.Playing => MediaState.Playing,
        SoundState.Paused => MediaState.Paused,
        _ => MediaState.Stopped,
    };

    public void Play()
    {
        // Pre-buffer a few chunks before Play() to avoid an initial underrun, matching the
        // feasibility spike's approach.
        SubmitNextChunk();
        SubmitNextChunk();
        SubmitNextChunk();
        _instance.Play();
    }

    public void Pause() => _instance.Pause();

    public void Resume() => _instance.Resume();

    public void Stop()
    {
        _instance.BufferNeeded -= OnBufferNeeded;
        _instance.Stop();
        _instance.Dispose();
        _reader.Dispose();
    }

    private void OnBufferNeeded(object? sender, EventArgs e) => SubmitNextChunk();

    private void SubmitNextChunk()
    {
        if (_reachedEndOfStream) return;

        int samplesRead = _reader.ReadSamples(_floatBuffer, 0, _floatBuffer.Length);
        if (samplesRead == 0)
        {
            if (!IsRepeating)
            {
                _reachedEndOfStream = true;
                return;
            }

            _reader.SeekTo(TimeSpan.Zero);
            samplesRead = _reader.ReadSamples(_floatBuffer, 0, _floatBuffer.Length);
            if (samplesRead == 0) return;
        }

        var pcm = AudioSampleConversion.FloatSamplesToPcm16(_floatBuffer, samplesRead);
        _instance.SubmitBuffer(pcm);
    }
}
