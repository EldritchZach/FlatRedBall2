using FlatRedBall2.Audio;
using Microsoft.Xna.Framework.Media;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Audio;

public class AudioManagerTests
{
    private class FakeMusicBackend : IMusicBackend
    {
        public float Volume { get; set; }
        public float Pitch { get; set; }
        public bool IsRepeating { get; set; }
        public MediaState State { get; set; } = MediaState.Stopped;
        public int PauseCallCount { get; private set; }
        public int ResumeCallCount { get; private set; }
        public int StopCallCount { get; private set; }

        public void Play() => State = MediaState.Playing;
        public void Pause() { PauseCallCount++; State = MediaState.Paused; }
        public void Resume() { ResumeCallCount++; State = MediaState.Playing; }
        public void Stop() { StopCallCount++; State = MediaState.Stopped; }
    }

    [Fact]
    public void MusicPitch_BackendInjected_SetsValueOnBackend()
    {
        var audioManager = new AudioManager();
        var backend = new FakeMusicBackend();
        audioManager.MusicBackendForTesting = backend;

        audioManager.MusicPitch = 0.5f;

        backend.Pitch.ShouldBe(0.5f);
    }

    [Fact]
    public void MusicVolume_BackendInjected_SetsValueOnBackend()
    {
        var audioManager = new AudioManager();
        var backend = new FakeMusicBackend();
        audioManager.MusicBackendForTesting = backend;

        audioManager.MusicVolume = 0.25f;

        backend.Volume.ShouldBe(0.25f);
    }

    [Fact]
    public void PauseSong_BackendInjected_CallsPauseOnBackend()
    {
        var audioManager = new AudioManager();
        var backend = new FakeMusicBackend();
        audioManager.MusicBackendForTesting = backend;

        audioManager.PauseSong();

        backend.PauseCallCount.ShouldBe(1);
    }
}
