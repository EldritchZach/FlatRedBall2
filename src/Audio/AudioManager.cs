using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using NVorbis;
using XnaTitleContainer = Microsoft.Xna.Framework.TitleContainer;

namespace FlatRedBall2.Audio;

/// <summary>
/// Manages sound effect playback and background music. Updated each frame by the engine.
/// </summary>
public class AudioManager
{
    private readonly List<(SoundEffect Effect, SoundEffectInstance Instance)> _activeSounds = new();
    private readonly HashSet<SoundEffect> _playedThisFrame = new();

    private float _soundVolume = 1f;
    private float _musicVolume = 1f;
    private float _musicPitch = 0f;
    private bool _soundEnabled = true;
    private bool _musicEnabled = true;

    private IMusicBackend? _musicBackend;

    // ── Sound effects ──────────────────────────────────────────────────────────

    /// <summary>
    /// Plays a sound effect. If the same <paramref name="soundEffect"/> was already played this
    /// frame, the call is silently ignored (per-frame dedup). Cross-frame stacking is allowed.
    /// Has no effect when <see cref="SoundEnabled"/> is <c>false</c> or <see cref="ConcurrentSounds"/>
    /// has reached <see cref="MaxConcurrentSounds"/>.
    /// </summary>
    public void Play(SoundEffect soundEffect, float volume = 1f, float pitch = 0f, float pan = 0f)
    {
        if (!_soundEnabled) return;
        if (_playedThisFrame.Contains(soundEffect)) return;
        if (_activeSounds.Count >= MaxConcurrentSounds) return;

        var instance = soundEffect.CreateInstance();
        instance.Volume = System.Math.Clamp(volume * _soundVolume, 0f, 1f);
        instance.Pitch = System.Math.Clamp(pitch, -1f, 1f);
        instance.Pan = System.Math.Clamp(pan, -1f, 1f);
        instance.Play();

        _activeSounds.Add((soundEffect, instance));
        _playedThisFrame.Add(soundEffect);
    }

    /// <summary>Returns <c>true</c> if any tracked instance of <paramref name="soundEffect"/> is currently playing.</summary>
    public bool IsPlaying(SoundEffect soundEffect)
    {
        foreach (var (effect, instance) in _activeSounds)
        {
            if (effect == soundEffect && instance.State == SoundState.Playing)
                return true;
        }
        return false;
    }

    // ── Music ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// The song that is currently queued (may be paused or stopped). Always <c>null</c> while a
    /// <see cref="PlayPitchableSong"/> track is active, since that path bypasses <see cref="Song"/>.
    /// </summary>
    public Song? CurrentSong => (_musicBackend as MediaPlayerMusicBackend)?.CurrentSong;

    /// <summary>
    /// Plays <paramref name="song"/> as the background track.
    /// Replaces any active song, playlist, or <see cref="PlayPitchableSong"/> track.
    /// Has no effect on volume when <see cref="MusicEnabled"/> is <c>false</c> — the song will
    /// begin playing but will immediately be paused by the <see cref="MusicEnabled"/> setter logic;
    /// use <see cref="MusicEnabled"/> to re-enable.
    /// </summary>
    public void PlaySong(Song song, bool loop = true)
    {
        SetMusicBackend(new MediaPlayerMusicBackend(song, loop));
    }

    /// <summary>
    /// Plays <paramref name="songs"/> in sequence, looping the playlist from the beginning after the last track.
    /// Replaces any active song or <see cref="PlayPitchableSong"/> track.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="songs"/> is empty.</exception>
    public void PlayPlaylist(params Song[] songs)
    {
        if (songs.Length == 0)
            throw new ArgumentException("Playlist must contain at least one song.", nameof(songs));

        SetMusicBackend(new MediaPlayerMusicBackend(songs));
    }

    /// <summary>
    /// Plays a raw OGG file directly (bypassing <see cref="Song"/>) through a pitchable streaming
    /// backend, so <see cref="MusicPitch"/> has an audible effect. Replaces any active song,
    /// playlist, or previous pitchable track. OGG-only, matching <see cref="Song.FromUri"/>'s
    /// OGG-only convention.
    /// </summary>
    public void PlayPitchableSong(string oggFilePath, bool loop = true)
    {
        var stream = XnaTitleContainer.OpenStream(oggFilePath);
        var reader = new VorbisReader(stream, true);
        SetMusicBackend(new StreamingOggMusicBackend(reader, loop));
    }

    /// <summary>Pauses the current song at its current position. No-op if nothing is playing.</summary>
    public void PauseSong() => _musicBackend?.Pause();

    /// <summary>
    /// Resumes the current song from its paused position.
    /// Has no effect if <see cref="MusicEnabled"/> is <c>false</c> or no song is loaded.
    /// </summary>
    public void ResumeSong()
    {
        if (_musicEnabled)
            _musicBackend?.Resume();
    }

    /// <summary>Stops the current song, playlist, or pitchable track and releases its resources.</summary>
    public void StopSong()
    {
        _musicBackend?.Stop();
        _musicBackend = null;
    }

    // ── Volume / enable ────────────────────────────────────────────────────────

    /// <summary>Master volume for sound effects in the range [0, 1]. Default is 1.</summary>
    public float SoundVolume
    {
        get => _soundVolume;
        set => _soundVolume = System.Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Master volume for music in the range [0, 1]. Default is 1.
    /// Takes effect immediately on whichever music backend is currently active.
    /// </summary>
    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = System.Math.Clamp(value, 0f, 1f);
            if (_musicBackend != null)
                _musicBackend.Volume = _musicVolume;
        }
    }

    /// <summary>
    /// Playback pitch for the currently active <see cref="PlayPitchableSong"/> track, in the range
    /// [-1, 1] (XNA semantics: +/-1 = one octave up/down = 2x/0.5x rate). Takes effect immediately.
    /// Setting this while a <see cref="PlaySong"/>/<see cref="PlayPlaylist"/> (MediaPlayer-backed)
    /// track is active is a silent no-op — <see cref="Song"/>/<see cref="MediaPlayer"/> expose no
    /// pitch control on any backend.
    /// </summary>
    public float MusicPitch
    {
        get => _musicPitch;
        set
        {
            _musicPitch = System.Math.Clamp(value, -1f, 1f);
            if (_musicBackend != null)
                _musicBackend.Pitch = _musicPitch;
        }
    }

    /// <summary>
    /// When <c>false</c>, calls to <see cref="Play"/> are silently ignored.
    /// Existing playing instances finish naturally; they are not stopped retroactively.
    /// </summary>
    public bool SoundEnabled
    {
        get => _soundEnabled;
        set => _soundEnabled = value;
    }

    /// <summary>
    /// When set to <c>false</c>, pauses the current song immediately.
    /// When set back to <c>true</c>, resumes the current song (if one is loaded).
    /// </summary>
    public bool MusicEnabled
    {
        get => _musicEnabled;
        set
        {
            _musicEnabled = value;
            if (!value)
                _musicBackend?.Pause();
            else
                _musicBackend?.Resume();
        }
    }

    // ── Limits / diagnostics ───────────────────────────────────────────────────

    /// <summary>Maximum number of concurrently tracked sound effect instances. Default is 32.</summary>
    public int MaxConcurrentSounds { get; set; } = 32;

    /// <summary>Number of currently tracked active sound effect instances.</summary>
    public int ConcurrentSounds => _activeSounds.Count;

    // ── Testing seam ───────────────────────────────────────────────────────────

    /// <summary>
    /// Test-only seam for injecting a fake <see cref="IMusicBackend"/> so routing (volume, pitch,
    /// pause/resume/stop) can be asserted without a real audio device.
    /// </summary>
    internal IMusicBackend? MusicBackendForTesting
    {
        get => _musicBackend;
        set => _musicBackend = value;
    }

    // ── Internal update ────────────────────────────────────────────────────────

    /// <summary>
    /// Called once per frame by <see cref="FlatRedBallService"/>. Clears the per-frame dedup set
    /// and disposes any sound effect instances that have finished playing.
    /// </summary>
    internal void Update()
    {
        _playedThisFrame.Clear();

        for (int i = _activeSounds.Count - 1; i >= 0; i--)
        {
            var (_, instance) = _activeSounds[i];
            if (instance.State == SoundState.Stopped)
            {
                instance.Dispose();
                _activeSounds.RemoveAt(i);
            }
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void SetMusicBackend(IMusicBackend backend)
    {
        _musicBackend?.Stop();
        _musicBackend = backend;
        _musicBackend.Volume = _musicVolume;
        _musicBackend.Pitch = _musicPitch;
        _musicBackend.Play();

        if (!_musicEnabled)
            _musicBackend.Pause();
    }
}
