# MusicPitchManagerSpike — AudioManager wiring check for issue #799

Exercises the real public `AudioManager.PlayPitchableSong`/`MusicPitch`/`StopSong` surface against
a real OGG file (`Content/Audio/song.ogg`, public-domain Wikimedia sample) and a real audio device
— unlike `diagnostics/MusicPitchSpike`, which proves `DynamicSoundEffectInstance.Pitch` itself
without going through `AudioManager` at all.

## Run

```
dotnet run
```

A window opens and closes itself after ~12s, cycling `MusicPitch` through `0, +1, -1, 0` (3s each)
while printing state to stdout. Confirms the pipeline runs end-to-end with no exceptions; does
**not** auto-verify pitch is audible — a human needs to listen for the octave-up/octave-down
effect during phases 1 and 2.
