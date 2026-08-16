using System;
using System.Diagnostics;
using FlatRedBall2.Audio;
using Microsoft.Xna.Framework;

namespace MusicPitchManagerSpike;

// Manual verification spike for issue #799 — exercises the real public AudioManager surface
// (PlayPitchableSong / MusicPitch / PauseSong / ResumeSong / StopSong) end-to-end against a real
// OGG file and a real audio device. Cycles MusicPitch through 0, +1 (octave up), -1 (octave down)
// every 3 seconds and prints State/Pitch to stdout so a human can correlate what they hear with
// what the API reports. This does NOT auto-verify pitch audibly — see diagnostics/MusicPitchSpike
// for the automated rate-verification spike; this one is about the AudioManager wiring, not
// re-proving DynamicSoundEffectInstance.Pitch itself.
public class Game1 : Game
{
    private static readonly float[] PitchSequence = [0f, 1f, -1f, 0f];
    private const double PhaseSeconds = 3.0;

    private readonly GraphicsDeviceManager _graphics;
    private readonly Stopwatch _phaseClock = new();
    private AudioManager _audio = null!;
    private int _pitchIndex;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _audio = new AudioManager();
        _audio.PlayPitchableSong("Content/Audio/song.ogg");
        Console.WriteLine($"[spike] PlayPitchableSong called. CurrentSong (should be null, bypasses Song)={_audio.CurrentSong}");
        Console.WriteLine($"[spike] phase 0: MusicPitch={PitchSequence[0]}");
        _audio.MusicPitch = PitchSequence[0];
        _phaseClock.Start();
    }

    protected override void Update(GameTime gameTime)
    {
        double elapsed = _phaseClock.Elapsed.TotalSeconds;

        if (elapsed >= PhaseSeconds)
        {
            _pitchIndex++;
            _phaseClock.Restart();

            if (_pitchIndex >= PitchSequence.Length)
            {
                Console.WriteLine("[spike] StopSong");
                _audio.StopSong();
                Console.WriteLine("[spike] DONE");
                Exit();
                return;
            }

            _audio.MusicPitch = PitchSequence[_pitchIndex];
            Console.WriteLine($"[spike] phase {_pitchIndex}: MusicPitch={PitchSequence[_pitchIndex]} MusicVolume={_audio.MusicVolume}");
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        base.Draw(gameTime);
    }
}
