using Microsoft.Xna.Framework.Media;

namespace FlatRedBall2.Audio;

/// <summary>
/// Common surface for the two music playback strategies <see cref="AudioManager"/> can drive:
/// the MediaPlayer-backed <see cref="Song"/> path and the streaming OGG/pitchable path.
/// Lets <see cref="AudioManager"/>'s volume/pause/resume/stop logic stay single-surface instead
/// of duplicating per backend.
/// </summary>
internal interface IMusicBackend
{
    /// <summary>Volume in the range [0, 1].</summary>
    float Volume { get; set; }

    /// <summary>
    /// Playback pitch in the range [-1, 1] (XNA semantics: +/-1 = one octave up/down).
    /// Backends that cannot support pitch (e.g. <see cref="MediaPlayerMusicBackend"/>) treat
    /// the setter as a no-op and the getter always returns 0.
    /// </summary>
    float Pitch { get; set; }

    /// <summary>Whether playback restarts from the beginning after reaching the end.</summary>
    bool IsRepeating { get; set; }

    /// <summary>Current playback state.</summary>
    MediaState State { get; }

    /// <summary>Starts or restarts playback from the beginning.</summary>
    void Play();

    /// <summary>Pauses playback at the current position.</summary>
    void Pause();

    /// <summary>Resumes playback from the paused position.</summary>
    void Resume();

    /// <summary>Stops playback and releases any resources the backend holds.</summary>
    void Stop();
}
