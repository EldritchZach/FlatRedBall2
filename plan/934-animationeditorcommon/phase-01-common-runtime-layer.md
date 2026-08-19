# Phase 1 — Generic runtime layer in AnimationEditorCommon

Tracking issue: [vchelaru/FlatRedBall2#934](https://github.com/vchelaru/FlatRedBall2/issues/934)

## Problem

`AnimationChain.MonoGame`/`AnimationChain.KNI` (the standalone .achx runtime NuGet packages, no
FlatRedBall2 engine dependency) duplicate the .achx POCO/Save layer that already exists, pure C#,
as `FlatRedBall2.Animation.Content` (`src/Animation.Content/`) — except their copy bakes in
`Microsoft.Xna.Framework` types directly. Their runtime playback types (`AnimationFrame`,
`AnimationChain`, `AnimationChainList`, `AnimationPlayer`) also hardcode `Texture2D`/`Rectangle`
even where the logic (timing, seeking, name lookup, reload-merge) never touches a texture. Net
result: none of this is usable outside MonoGame/KNI (e.g. raylib via raylib-cs), and a hypothetical
third renderer would have to hand-copy ~450 lines instead of referencing a package.

## Proposed resolution

Grow `src/Animation.Content` into `AnimationEditorCommon` — it is already "pure C#, zero
MonoGame/KNI dependency by design," already published standalone, and already consumed by both the
main engine (`src/FlatRedBall2.csproj`) and `AnimationEditor.Core`, so this is a rename-and-extend,
not a new project starting from zero.

Two-tier generic base for the texture-bearing runtime types, so only the texture handle itself is
renderer-specific:

```csharp
// renderer-agnostic — everything except the texture handle itself
public abstract class AnimationFrameBase
{
    public string TextureName = string.Empty;
    public TimeSpan FrameLength;
    public bool FlipHorizontal, FlipVertical, FlipDiagonal;
    public float RelativeX, RelativeY;
    public int? Red, Green, Blue, Alpha;
    public ColorOperation? ColorOperation;
    public List<AnimationShapeFrame> Shapes { get; } = new();
    public PixelRectangle? SourceRectangle;
}

// only the texture handle varies by renderer
public abstract class AnimationFrameBase<TTexture> : AnimationFrameBase
{
    public TTexture? Texture;
}
```

`AnimationChain<TFrame>`, `AnimationChainList<TFrame>`, `AnimationPlayer<TFrame>` (all constrained
`where TFrame : AnimationFrameBase`, so they never see `TTexture` at all) move down into Common too
— this is where the actual reusable logic lives: timing/seek arithmetic, name-indexed lookup, and
the `TryReloadFrom` merge-in-place semantics.

MonoGame/KNI then collapse to a one-line closure per platform:

```csharp
public class AnimationFrame : AnimationFrameBase<Texture2D> { }
```

Consumer code stays functionally identical (`player.Play("Run")`, `player.Update(...)`,
`frame.Texture`); explicit type declarations gain a generic argument
(`AnimationPlayer<AnimationFrame>` instead of bare `AnimationPlayer`), `var`-typed code is
unaffected. `AchxLoader`/`SpriteBatchExtensions`/`AddColorEffect` stay MonoGame/KNI-side (GPU
upload, drawing) — those never move to Common.

**Out of scope for this phase (and not yet decided for later):** the main engine's own
`src/Animation/AnimationFrame.cs` / `AnimationChainList.cs`, which back `Sprite.CurrentAnimation` —
a third, independently-shaped copy of this same data, converted via
`AnimationChainListSaveExtensions`. It already consumes `Animation.Content` for the POCO layer, so
renaming that project doesn't break it, but pointing `Sprite`'s own animation system at the new
generic runtime layer is a separate, higher-risk change (touches `Sprite.Draw`, hot-reload, shape
reconciliation) and isn't part of this issue.

**Deferred to Phase 2:** migrating `AnimationChain.MonoGame`/`AnimationChain.KNI` to actually consume
this layer (delete their duplicated `Content/` POCOs, generic-close their `Runtime/*` types, update
`AchxLoader`/`SpriteBatchExtensions` for the `PixelRectangle` → `Rectangle` conversion). Phase 1 adds
the layer and proves it in isolation; Phase 2 is the migration once Phase 1 is stable. Do not start
Phase 2 work under this phase's checkboxes.

## Steps

### Rename `Animation.Content` → `AnimationEditorCommon`

- [x] Rename `src/Animation.Content/` → `src/AnimationEditorCommon/`, project file →
      `AnimationEditorCommon.csproj`, `PackageId` → `FlatRedBall2.AnimationEditorCommon`. Update the
      project's own header comment (currently describes it as POCO-only) to reflect the broader
      scope.
- [x] Update namespace `FlatRedBall2.Animation.Content` → `FlatRedBall2.AnimationEditorCommon` for
      the existing Save/POCO types (or a `.Content` sub-namespace if that reads better in practice —
      use judgement, keep it consistent).
- [x] Update every reference: `src/FlatRedBall2.csproj` ProjectReference path,
      `tools/AnimationEditorAvalonia/src/AnimationEditor.Core/AnimationEditor.Core.csproj`
      ProjectReference path, `src/Animation/Content/AnimationChainListSaveExtensions.cs` `using`,
      any other `using FlatRedBall2.Animation.Content;` in `src/`, `tests/FlatRedBall2.Tests/`, and
      `tools/AnimationEditorAvalonia/`.
- [x] Update `.github/workflows/publish.yml` for the renamed package id (keep it published in
      lockstep with `FlatRedBall2.MonoGame`/`FlatRedBall2.Kni`, same as today).
- [x] `dotnet build src/FlatRedBall2.csproj` and the AnimationEditor solution both succeed; full
      `dotnet test tests/FlatRedBall2.Tests/` and `AnimationEditor.App.Tests`/`Core.Tests` stay green
      with no behavior change (pure rename — no new failing test expected here, since nothing about
      engine behavior changed yet).

### Add the generic runtime layer

- [x] `PixelRectangle` struct (`X`, `Y`, `Width`, `Height`, all `int`) — new file in
      `AnimationEditorCommon`.
- [x] `AnimationFrameBase` (non-generic, all the pure data listed above) — new file, failing test
      first per `engine-tdd` (even though this project isn't `src/FlatRedBall2.csproj` itself, the
      repo-wide test-first rule in `CLAUDE.md` still applies: a failing test ahead of the type,
      covering at minimum construction + default values).
- [x] `AnimationFrameBase<TTexture> : AnimationFrameBase` — adds `Texture`. Test: a closed generic
      over a stand-in type (e.g. `object` or a test-only fake texture) round-trips assignment.
- [x] `AnimationChain<TFrame> : List<TFrame> where TFrame : AnimationFrameBase` — `Name`,
      `TotalLength`. Port the existing test coverage shape from
      `tests/AnimationChain.MonoGame.Tests/` (adjusted for the generic signature) rather than
      inventing new cases from scratch.
- [x] `AnimationChainList<TFrame> : List<AnimationChain<TFrame>> where TFrame : AnimationFrameBase`
      — `Name`, string indexer, `GetOwnedShapeNames()` (port from the engine's
      `src/Animation/AnimationChainList.cs`, which already has this and the standalone package's
      copy doesn't — worth carrying over since it's directly reusable), `TryReloadFrom` merge
      semantics generic over a frame-factory delegate instead of `Func<string, Texture2D?>`.
- [x] `AnimationPlayer<TFrame> where TFrame : AnimationFrameBase` — port
      `src/AnimationChain.MonoGame/AnimationPlayer.cs` almost verbatim (it's already 100% texture-free);
      the only change is the class/type-parameter declaration. Reuse
      `tests/AnimationChain.MonoGame.Tests/AnimationPlayerTests.cs` as the starting point, generic-closed
      over a minimal test frame type.
- [x] Do **not** wire `AnimationChain.MonoGame`/`AnimationChain.KNI` to any of this yet — that's
      Phase 2. This phase's new types should build and test entirely standalone within
      `AnimationEditorCommon` and its own test project.

### New test project

- [x] `tests/AnimationEditorCommon.Tests/` (new), referencing only `AnimationEditorCommon` — proves
      the whole generic layer compiles and runs with zero MonoGame/KNI reference in the test project
      itself, which is the actual claim being made ("usable outside MonoGame/KNI").

## Manual test

Not needed — this phase is pure library code with unit coverage; no rendering or UI surface changes.
