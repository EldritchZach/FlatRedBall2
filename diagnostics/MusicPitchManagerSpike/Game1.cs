using System;
using FlatRedBall2.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MusicPitchManagerSpike;

// Interactive manual-test tool for issue #799 — exercises the real public AudioManager surface
// (PlayPitchableSong / MusicPitch / PauseSong / ResumeSong / StopSong) end-to-end against a real
// OGG file and a real audio device, driven by the keyboard so a human can freely explore pitch
// values instead of watching a fixed auto-cycle. See diagnostics/MusicPitchSpike for the lower-level
// spike that proves DynamicSoundEffectInstance.Pitch itself, without going through AudioManager.
public class Game1 : Game
{
    private const float PitchStep = 0.1f;

    private readonly GraphicsDeviceManager _graphics;
    private AudioManager _audio = null!;
    private KeyboardState _previousKeyboard;
    private bool _paused;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _audio = new AudioManager();
        _audio.PlayPitchableSong("Content/Audio/song.ogg");
        _previousKeyboard = Keyboard.GetState();
        PrintControls();
        PrintState();
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

        if (WasPressed(keyboard, Keys.Up) || WasPressed(keyboard, Keys.OemPlus) || WasPressed(keyboard, Keys.Add))
        {
            _audio.MusicPitch = Math.Clamp(_audio.MusicPitch + PitchStep, -1f, 1f);
            PrintState();
        }
        else if (WasPressed(keyboard, Keys.Down) || WasPressed(keyboard, Keys.OemMinus) || WasPressed(keyboard, Keys.Subtract))
        {
            _audio.MusicPitch = Math.Clamp(_audio.MusicPitch - PitchStep, -1f, 1f);
            PrintState();
        }
        else if (WasPressed(keyboard, Keys.D0) || WasPressed(keyboard, Keys.NumPad0))
        {
            _audio.MusicPitch = 0f;
            PrintState();
        }
        else if (WasPressed(keyboard, Keys.Space))
        {
            _paused = !_paused;
            if (_paused) _audio.PauseSong();
            else _audio.ResumeSong();
            PrintState();
        }
        else if (WasPressed(keyboard, Keys.R))
        {
            _audio.StopSong();
            _audio.PlayPitchableSong("Content/Audio/song.ogg");
            _paused = false;
            PrintState();
        }
        else if (WasPressed(keyboard, Keys.Escape))
        {
            Exit();
        }

        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        base.Draw(gameTime);
    }

    private bool WasPressed(KeyboardState current, Keys key) =>
        current.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    private static void PrintControls()
    {
        Console.WriteLine("[MusicPitchManagerSpike] Controls:");
        Console.WriteLine("  Up / +   raise MusicPitch by 0.1");
        Console.WriteLine("  Down / - lower MusicPitch by 0.1");
        Console.WriteLine("  0        reset MusicPitch to 0");
        Console.WriteLine("  Space    pause / resume");
        Console.WriteLine("  R        restart song from the beginning");
        Console.WriteLine("  Esc      quit");
    }

    private void PrintState() =>
        Console.WriteLine($"[MusicPitchManagerSpike] MusicPitch={_audio.MusicPitch:F1} paused={_paused}");
}
