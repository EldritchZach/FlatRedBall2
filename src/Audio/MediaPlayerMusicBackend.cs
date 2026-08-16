using System;
using Microsoft.Xna.Framework.Media;

namespace FlatRedBall2.Audio;

/// <summary>
/// <see cref="IMusicBackend"/> wrapping the static <see cref="MediaPlayer"/>/<see cref="Song"/>
/// API. Owns playlist advancement (subscribes to <see cref="MediaPlayer.MediaStateChanged"/> and
/// moves to the next track when the current one stops). <see cref="Pitch"/> is a no-op — neither
/// <see cref="Song"/> nor <see cref="MediaPlayer"/> exposes playback-rate control.
/// </summary>
internal sealed class MediaPlayerMusicBackend : IMusicBackend
{
    private readonly Song[] _songs;
    private int _index;

    internal MediaPlayerMusicBackend(Song song, bool loop)
    {
        _songs = [song];
        IsRepeating = loop;
    }

    internal MediaPlayerMusicBackend(Song[] playlist)
    {
        _songs = playlist;
    }

    /// <summary>The track currently playing (or about to resume) from the playlist.</summary>
    internal Song CurrentSong => _songs[_index];

    public float Volume
    {
        get => MediaPlayer.Volume;
        set => MediaPlayer.Volume = value;
    }

    public float Pitch
    {
        get => 0f;
        set { }
    }

    public bool IsRepeating
    {
        get => MediaPlayer.IsRepeating;
        set => MediaPlayer.IsRepeating = value;
    }

    public MediaState State => MediaPlayer.State;

    public void Play()
    {
        MediaPlayer.MediaStateChanged += OnMediaStateChanged;
        MediaPlayer.Play(CurrentSong);
    }

    public void Pause() => MediaPlayer.Pause();

    public void Resume() => MediaPlayer.Resume();

    public void Stop()
    {
        MediaPlayer.MediaStateChanged -= OnMediaStateChanged;
        MediaPlayer.Stop();
    }

    private void OnMediaStateChanged(object? sender, EventArgs e)
    {
        // Single-song looping is handled natively by MediaPlayer.IsRepeating; only a multi-track
        // playlist needs manual advancement here.
        if (_songs.Length <= 1) return;
        if (MediaPlayer.State != MediaState.Stopped) return;

        _index = (_index + 1) % _songs.Length;
        MediaPlayer.Play(CurrentSong);
    }
}
