# Glue + FRB2 unification — status

Making the Glue editor a first-class editor for FRB2 games, rather than an FRB1-only tool whose
output FRB2 happens to be able to read.

**Most of the remaining work is in the Glue repo, not this one.** This file exists so the FRB2 side
can be picked back up without re-deriving what state it is in.

## The end state

Glue saves a project; the running FRB2 game shows the change. No codegen, no rebuild, no separate
FRB1 C# project. FRB2 reads `.gluj`/`.glsj`/`.glej` as data at runtime — that half is done (see
`plan/804-glue-project-loader/`, all 14 phases implemented).

## Current project layout

Glue's FRB2 mode writes everything it authors into one folder inside the game's content tree:

```
<game>/Content/FrbEditor/    <- .gluj, Screens/*.glsj, Entities/*.glej, GlueSettings/, and every
                               file the project references
```

Referenced files resolve **directly** against that folder — there is no `Content` segment in
between, because the project and its content are one self-contained tree. That is what lets a game
copy a single directory to the build output, and lets a user delete it to remove the editor's
footprint entirely. One `.csproj` glob does the copy.

`samples/GlueLoaderScratch` is the live test bed. It is deliberately near-empty — a real project's
worth of content is not the point, the round trip is.

Keep `EngineInitSettings.GlueProjectFile` **relative**. `GlueContentSource` resolves through
`TitleContainer`, which throws on a rooted path, so an absolute `.gluj` loads and then fails every
asset it references.

## Works today (FRB2 side)

- **Loading.** `EngineInitSettings.GlueProjectFile` → `FlatRedBallService.GlueProject` →
  `Start<GlueScreen>`. Resolves against `OutputContentRoot`, so the working directory does not matter.
- **Hot reload.** `GlueScreen` watches the `.gluj`'s own directory, copies changes to the build
  output, reparses, and restarts onto the same screen from the new data. Covers element files, the
  project file, new files with no output copy yet, and assets under the project's `Content/`. Gum
  files are left to Gum's in-place pipeline. See the `glue-project-loading` skill.
- **Shape defaults matching the editor** — opaque white, outline only, 1px stroke.

## Open — Glue side

- Editor UI for FRB2 mode generally: which FRB1 concepts are hidden, which map to FRB2 equivalents.
- `GenerateGlueControlManagerCode` is off and `EmbedGameInGameTab` is on in the scratch project's
  `CompilerSettings.json`. Game-in-tab needs a transport between editor and running game; FRB1 used
  GlueControl's generated manager, which FRB2 has no equivalent of. Decide whether FRB2 reuses that
  wire protocol, uses the engine's own automation mode (`automation-mode` skill), or something else.
- Object placement/drag in the editor writing back to `.glsj` while the game is running — the
  round trip hot reload is built to serve.

## Open — FRB2 side

- **Discrepancy sweep is ongoing.** Each one is a commit on the issue #829 branch. Method: run
  `samples/GlueLoaderScratch`, compare against what the Glue editor shows, fix the engine default
  rather than the deserializer whenever the `.gluj` is silent about the value.
- **Deletion is not handled.** Removing an element in Glue leaves the stale copy in the build
  output. Harmless today because the reload reads the `.gluj`'s element list, but a rename that
  leaves both files behind has not been exercised.
- **PNG dimension changes across a reload.** The engine-level texture cache is not cleared on a Glue
  reload, so a resized texture keeps the old dimensions until the game restarts.
- Not wired from FRB1: pooling (`PooledByFactory`), `SortAxis` partitioning, `CustomClasses`,
  `Events`. `plan/804-glue-project-loader/` records why for each.

## Pointers

| What | Where |
|---|---|
| Loader design, phase by phase | `plan/804-glue-project-loader/` |
| Using the loader | `glue-project-loading` skill |
| The watch/copy machinery underneath | `content-hot-reload` skill |
| Live test bed | `samples/GlueLoaderScratch` |
| Discrepancy sweep | issue #829 |
