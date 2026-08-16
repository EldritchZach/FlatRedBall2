# MusicPitchManagerSpike — interactive AudioManager pitch tester for issue #799

Exercises the real public `AudioManager.PlayPitchableSong`/`MusicPitch`/`PauseSong`/`ResumeSong`/
`StopSong` surface against a real OGG file (`Content/Audio/song.ogg`, public-domain Wikimedia
sample) and a real audio device — unlike `diagnostics/MusicPitchSpike`, which proves
`DynamicSoundEffectInstance.Pitch` itself without going through `AudioManager` at all.

## Run

```
dotnet run
```

A window opens and the song loops immediately. Use the keyboard to drive it — nothing is automatic,
so listen for yourself:

- **Up / +** — raise `MusicPitch` by 0.1 (toward +1 = one octave up / 2x speed)
- **Down / -** — lower `MusicPitch` by 0.1 (toward -1 = one octave down / 0.5x speed)
- **0** — reset `MusicPitch` to 0
- **Space** — pause / resume
- **R** — restart the song from the beginning
- **Esc** — quit

Current state prints to stdout on every change. Confirms the pipeline runs end-to-end with no
exceptions; a human still needs to listen for the audible pitch/speed change as `MusicPitch` moves.
