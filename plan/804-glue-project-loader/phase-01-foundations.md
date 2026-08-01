# Phase 1 — Foundations + Screens/Entities Skeleton

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Not started |
| **Depends on** | Nothing — this is the base of the epic |
| **Blocks** | Every other phase (2–14) |
| **Suggested branch** | `804-phase-1-glue-loader-foundations` |

---

## 1. The issue

Today Glue generates C# that FRB1 compiles. Issue #804 flips that relationship: **FRB2 reads the
JSON project files Glue already produces and builds Screens/Entities from them directly, at
runtime, via reflection — no codegen step.**

Phase 1 delivers the foundation everything else stands on: read the files, resolve the element
graph, decode the value-bag primitives, and boot the start-up Screen. It deliberately produces
*empty* Screens and Entities — no `NamedObjects` are instantiated (Phase 2), no `CustomVariables`
are applied (Phase 3), no files are loaded (Phase 4). "It loaded and the right Screen is active"
is the whole bar.

### Ground rules inherited from the epic

- **Latest `FileVersion` only.** No replication of `GluxVersions` gating history. Projects must be
  opened and re-saved in current Glue first. FRB1's current `LatestVersion` is **67**
  (`GluxVersions.NineSliceHasTilingMiddleSections`, March 15 2026) — see
  `FRBDK/Glue/GlueCommon/SaveClasses/GlueProjectSave.cs:213`.
- **Fully data-driven, no required user base class.** Loaded elements are generic objects;
  `CustomVariables` are a reflected property bag. Behavior hooks in via external registration, not
  subclassing.
- **No `CustomClasses` support.** Explicitly out of scope for the whole epic.
- **Package boundary: core.** The loader lives in `src/FlatRedBall2.csproj`, not a separate opt-in
  package — consistent with Gum/Tiled integration.
- **Test fixtures are vendored.** Commit a snapshot of representative FRB1 sample files into this
  repo rather than reading live from the sibling `FlatRedBall` checkout.

### Where FRB1 lives

`C:\git\flatredball` — a sibling checkout, not a submodule. Reference files for this phase:

| What | Path (relative to the FRB1 repo) |
|---|---|
| Project save shape | `FRBDK/Glue/GlueCommon/SaveClasses/GlueProjectSave.cs` |
| Element base | `FRBDK/Glue/GlueCommon/SaveClasses/GlueElement.cs` |
| Screen / Entity | `FRBDK/Glue/GlueCommon/SaveClasses/ScreenSave.cs`, `EntitySave.cs` |
| Object model | `FRBDK/Glue/GlueCommon/SaveClasses/NamedObjectSave.cs` |
| Value bag | `FRBDK/Glue/GlueCommon/SaveClasses/PropertySave.cs` |
| **The load path to mirror** | `FRBDK/Glue/Glue/Extensions/GlueProjectSaveExtensions.cs:514` (`Load`) and `:567` (`LoadReferencedScreensAndEntities`) |
| Version enum / skill | `FRBDK/Glue/.claude/skills/gluj-versions/SKILL.md` |

---

## 2. Scope

### In scope

1. JSON read pipeline for `.gluj` / `.glsj` / `.glej` on a FRB2-side POCO mirror.
2. `ScreenReferences` / `EntityReferences` resolution → load the matching element files.
3. `PropertySave` value decoding (the `GetValue<T>` equivalent), including raw-int enums.
4. Type-mapping table: Glue type strings → FRB2 types, with a decided fail-fast/skip policy.
5. `StartUpScreen` boots into FRB2's screen system as an empty `GlueScreen`.
6. Vendored test fixtures + the `tests/FlatRedBall2.Tests/Glue/` convention.

### Out of scope (later phases, do not creep)

- Instantiating `NamedObjects` (Phase 2) — Phase 1 only *parses and retains* them.
- Applying `CustomVariables` / `InstructionSaves` (Phase 3).
- Loading any referenced file, `.gumx` included (Phases 4–5).
- Inheritance merge (Phase 6) — `BaseScreen`/`BaseEntity` are parsed and retained, not resolved.
- States (Phase 7), factories (Phase 8), collision (Phase 9), TMX (Phase 10), movement (11–12),
  display settings (Phase 13), the name-based navigation API (Phase 14).
- `CustomClasses`, `Events`/`EventResponseSave`, `SyncedProjects`, `PerformanceSettingsSave`,
  `ResolutionPresets`, historical `FileVersion` support.

---

## 3. Features and stories

### F1 — Load a Glue project from disk (or from anywhere)

> As an engine developer, I point `GlueProjectLoader` at a `.gluj` path and get back a fully
> populated object graph with every referenced Screen and Entity resolved, so that later phases
> have something to walk.

> As a web/WASM consumer, the loader never calls `System.IO.File` directly, so the same code runs
> in the browser where there is no filesystem.

### F2 — Read values out of the Glue value bag without guessing

> As an engine developer, I call one helper to pull a typed value out of a `Properties` list and
> get the right CLR type back, so that I am not writing `is long ? (int)` casts at every call site.

### F3 — Know what failed, and keep going

> As an engine developer working through a 14-phase epic, an unmapped type or a missing element
> file gives me a diagnostic on the load result rather than an exception that hides the other
> ninety percent of the project that loaded fine.

### F4 — Boot the start-up Screen

> As a game developer, I call one method with a `.gluj` path and the game starts on the Screen that
> Glue's `StartUpScreen` names, so that "does the loader work?" is answerable by running the game.

### F5 — A repeatable fixture convention

> As a future contributor starting Phase 2, there is already a vendored sample project under
> `tests/` and an established folder convention, so I add a fixture rather than inventing a scheme.

---

## 4. Proposed resolution (high level)

### Reader

Mirror FRB1's `Load` flow exactly, because the file layout is defined by that code:

1. Deserialize `<name>.gluj` into `GlueProjectSave`.
2. For each `ScreenReferences[i].Name`, read `<glujDirectory>/<Name>.glsj` → `ScreenSave`.
3. For each `EntityReferences[i].Name`, read `<glujDirectory>/<Name>.glej` → `EntitySave`.
4. Populate `Screens` / `Entities`; clear the reference lists.

`Name` is a project-relative, **backslash-separated** path with no extension
(`"Screens\\MenuScreen"`), so the file is `<glujDir>/Screens/MenuScreen.glsj`.

### Serializer choice: `System.Text.Json` with source generation

FRB2 has no Newtonsoft dependency and already uses STJ with source-generated contexts —
`src/Movement/TopDownConfig.cs:40` and `src/Movement/PlatformerConfig.cs:43` are the pattern to
copy. Source generation is not optional: `src/FlatRedBall2.csproj` sets `IsAotCompatible=true`, and
reflection-based STJ would break that.

Two of the epic's stated landmines soften under STJ, and the doc should say so rather than carry
Newtonsoft-shaped worry forward:

- **Boxed `long`/`double`.** Newtonsoft boxes `object`-typed values as `long`/`double` even for
  int/float fields, which is why FRB1 needs the cast ladder in
  `PropertySaveListExtensions.GetValue<T>` (`PropertySave.cs:72`). STJ deserializes `object` to
  `JsonElement` instead. **Type the mirror's `Value` as `JsonElement`** and the ambiguity becomes an
  explicit, testable decode step rather than a silent cast.
- **Raw-int enums.** STJ converts an int to an enum natively for enum-typed properties, so
  `"SourceType": 2` binds correctly with no custom converter. The int→enum work is only needed
  inside the value bag, where the target type is not known statically.

### Namespace and file layout

```
src/Glue/
  GlueProjectLoader.cs        entry point + the read seam
  GlueLoadResult.cs           loaded project + diagnostics
  GlueLoadDiagnostic.cs       severity, element name, message
  GlueTypeMap.cs              Glue type string -> FRB2 Type
  GlueScreen.cs               Screen subclass built from a ScreenSave (skeleton this phase)
  GlueEntity.cs               Entity subclass built from an EntitySave (skeleton this phase)
  PropertySaveExtensions.cs   GetValue<T> over JsonElement
  GlueJsonContext.cs          [JsonSerializable] source-gen context
  Model/                      POCO mirror: GlueProjectSave, ScreenSave, EntitySave, GlueElement,
                              NamedObjectSave, CustomVariable, ReferencedFileSave, PropertySave,
                              InstructionSave, StateSave, StateSaveCategory,
                              GlueElementFileReference, DisplaySettings
```

**Keep FRB1's type names verbatim** in `Model/` (`GlueProjectSave`, not `GlueProjectData`). The
JSON property names are fixed by FRB1 anyway, and matching names make the two codebases
cross-referenceable during a 14-phase port. `namespace FlatRedBall2.Glue.Model` prevents collision.

The mirror is **trimmed**: only fields Phases 1–14 actually consume. Editor-only metadata
(`IsHiddenInTreeView`, `Bookmarks`, `PluginData`, `Tags`) is omitted. STJ ignores unknown JSON
members by default, so omission is safe.

### The read seam

Follow the existing precedent in `src/Tiled/TileMap.cs` — `TileMap.TmxLoader` is a static
injectable delegate specifically so tests never touch disk and WASM can route through
`TitleContainer` (see the comment in `tests/FlatRedBall2.Tests/Tiled/TileMapLoadingTests.cs:9`).
Give `GlueProjectLoader` the same shape: a `Func<string, string> TextLoader` defaulting to
`File.ReadAllText`, and a `Func<string, bool> FileExists`.

### Failure policy: collect, don't throw

`GlueProjectLoader.Load(path)` returns a `GlueLoadResult` carrying the project plus a diagnostic
list. Missing element files, unmapped type strings, and unparseable elements each add a diagnostic
and continue. A `GlueLoadOptions.Strict` flag throws on the first `Error` for callers who want
fail-fast.

Rationale: during a 14-phase incremental build, most of a project will reference things the loader
cannot handle *yet*. A hard failure on the first unknown type makes the loader untestable against
real fixtures until Phase 14. FRB1 itself already tolerates missing/corrupt element files
(`GlueProjectSaveExtensions.cs:574`, `:584`) — this is the same posture with better reporting.

### Boot

`GlueProjectLoader.Start(FlatRedBallService, glujPath)` loads, resolves `StartUpScreen` against the
loaded `Screens`, and calls the service's existing start path with a `GlueScreen` configured from
that `ScreenSave`. `GlueScreen` this phase is a `Screen` subclass holding its `ScreenSave` and
nothing else — later phases fill in `CustomInitialize`.

---

## 5. Landmines (verified against FRB1 source and real sample files)

1. **`.gluj` and element files use different Newtonsoft settings.**
   `.gluj` writes with `NullValueHandling.Ignore` + `DefaultValueHandling.IgnoreAndPopulate`
   (`GlueProjectSaveExtensions.cs:127`); `.glsj`/`.glej` write with `DefaultValueHandling.Ignore`
   only (`:434`). Element files are read back with **no settings at all** (`:578`, `:599`).

2. **Absent ≠ `false`.** Because element files are written with `DefaultValueHandling.Ignore`, any
   member equal to its default is *omitted*. `NamedObjectSave` sets its true-by-default members in
   the **constructor** — `Instantiate`, `AddToManagers`, `IncludeInICollidable`,
   `IncludeInIClickable`, `CallActivity`, `GenerateTimedEmit` (`NamedObjectSave.cs:828`). A naive
   POCO with `bool Instantiate { get; set; }` reads back `false` for every object in every real
   project. **The mirror must reproduce every constructor default.** STJ calls the parameterless
   constructor, so replicating them there works identically to Newtonsoft.
   - Counter-example to keep straight: `AttachToContainer` is *deliberately* not defaulted in the
     constructor (see the comment at `NamedObjectSave.cs:838`) and is written explicitly — confirm
     against `Samples/Beefball/Beefball/Entities/PlayerBall.glej`, where it appears as an explicit
     `"AttachToContainer": true`.

3. **`[DefaultValue(true)]` disagrees with FRB1's own read path.** `AddToManagers`,
   `IncludeInICollidable`, `IncludeInIClickable` and `CallActivity` are annotated `[DefaultValue(true)]`
   (used on write to omit them) but element files load with default settings, so FRB1 relies purely
   on the constructor to restore them. The annotation and the reader agree *by coincidence*, not by
   construction. **This is a candidate FRB1 bug to log** — the epic explicitly welcomes these.

4. **Backslash-separated element names.** `"Screens\\MenuScreen"` must be normalized before being
   joined to a path, or every non-Windows target fails. FRB2 targets WASM and Linux; this is not
   theoretical.

5. **Case sensitivity.** `GluxVersions.CaseSensitiveLoading = 55` exists because this bit FRB1.
   Decide and test the FRB2 matching rule explicitly rather than inheriting the host filesystem's.

6. **`PropertySave` carries a `Type` string that is not a CLR type name.** Real values observed in
   `ChickenClicker.gluj` include `"int"`, `"Boolean"`, `"String"`, `"SourceType"` — a mix of C#
   keywords, CLR simple names, and Glue enum names. Some entries omit `Type` entirely (see
   `IncludeFormsInComponents` in that file). The decode helper must be driven by the *requested*
   `T`, exactly as `GetValue<T>` is, not by the `Type` string.

7. **Same-named members live in two places.** `NamedObjectSave.SourceType` exists both as a real
   property (`"SourceType": 2`) **and** as a `Properties` entry
   (`{"Name":"SourceType","Value":2,"Type":"SourceType"}`) in the same object — see
   `PlayerBall.glej:166-186`. Some FRB1 properties are backed by the bag via
   `Properties.GetValue<T>(nameof(X))` (e.g. `AssociateWithFactory`, `NamedObjectSave.cs:683`).
   Decide per member which one is authoritative; do not assume the strongly-typed one wins.

---

## 6. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D1 | Fail-fast vs skip-with-warning for unmapped types | **Skip + diagnostic**, with an opt-in `Strict` mode. Reasoning in §4. |
| D2 | Mirror POCO names | **Keep FRB1 names verbatim** under `FlatRedBall2.Glue.Model`. |
| D3 | Fixture location | **`tests/FlatRedBall2.Tests/Glue/Fixtures/<ProjectName>/`**, copied to output via `<None ... CopyToOutputDirectory="PreserveNewest" />` — matches the existing `Animation\Content\Corpus\**` rows in `tests/FlatRedBall2.Tests/FlatRedBall2.Tests.csproj:24`. |
| D4 | Which sample to vendor first | **`Samples/ChickenClicker`** — 111-line `.gluj`, three 15-line `.glsj`, no entities. Smallest thing that exercises the full reference-resolution path. Add `Samples/Beefball` (2 entities with `NamedObjects`, `CustomVariables`, `StateCategoryList`) as the richer second fixture. |
| D5 | Case-sensitivity rule for element-name lookup | **Case-insensitive match with a diagnostic on case mismatch.** Glue authored these on Windows; a silent miss on Linux is the worse failure. Confirm this does not mask a genuinely missing file. |
| D6 | `FileVersion` enforcement | **Warn below 67, do not block.** A hard version gate makes every fixture re-save a prerequisite for running a single test. Revisit if version drift causes real misreads. |

---

## 7. Tasks

Repo rule: **a failing test comes first, or the commit body explains why one was not feasible.**
Load the `engine-tdd` skill before touching `src/`. Each group below is roughly one commit.

### 7.1 — Fixtures and conventions

- [ ] Create `tests/FlatRedBall2.Tests/Glue/` (mirrors `src/Glue/`, per `.claude/code-style.md` §Test Organization).
- [ ] Vendor `Samples/ChickenClicker` Glue files from the FRB1 checkout into
      `tests/FlatRedBall2.Tests/Glue/Fixtures/ChickenClicker/` — `ChickenClicker.gluj` plus
      `Screens/GameScreen.glsj`, `Screens/MenuScreen.glsj`, `Screens/OptionsScreen.glsj`.
- [ ] Vendor `Samples/Beefball` Glue files into `Fixtures/Beefball/` — `.gluj`, `Screens/GameScreen.glsj`,
      and the four `.glej` files (`PlayerBall`, `Puck`, `Goal`, `ScoreHud`).
- [ ] Add a `Fixtures/README.md` recording the source repo, sample path, and sync date, so a future
      re-sync knows what it is re-syncing from.
- [ ] Add the `<None Include="Glue\Fixtures\**\*" CopyToOutputDirectory="PreserveNewest" />` row to
      `tests/FlatRedBall2.Tests/FlatRedBall2.Tests.csproj`.

### 7.2 — POCO mirror

- [ ] Failing test: deserializing the ChickenClicker `.gluj` fixture yields `FileVersion == 42`,
      `StartUpScreen == "Screens\\MenuScreen"`, and three `ScreenReferences`.
- [ ] Add `src/Glue/Model/` POCOs for `GlueProjectSave`, `GlueElementFileReference`, `GlueElement`,
      `ScreenSave`, `EntitySave`, `PropertySave`, `InstructionSave`, `NamedObjectSave`,
      `CustomVariable`, `ReferencedFileSave`, `StateSave`, `StateSaveCategory`, `DisplaySettings`.
- [ ] Type `PropertySave.Value` and `InstructionSave.Value` as `JsonElement` (see §4).
- [ ] Add `src/Glue/GlueJsonContext.cs` with `[JsonSerializable]` entries for every root shape and
      `PropertyNameCaseInsensitive = true`, mirroring `src/Movement/TopDownConfig.cs:40`.
- [ ] Failing test: a `NamedObjectSave` deserialized from JSON that omits `Instantiate` reports
      `Instantiate == true` (landmine §5.2).
- [ ] Reproduce every `NamedObjectSave` constructor default from `NamedObjectSave.cs:828`, and
      confirm `AttachToContainer` is *not* among them.
- [ ] Verify the build stays AOT-clean: `dotnet build src/FlatRedBall2.csproj` emits no
      `IL2026`/`IL3050` trim or AOT warnings from the new code.

### 7.3 — Value-bag decoding

- [ ] Failing test: `GetValue<int>` on `{"Value": 1}` returns `1`; `GetValue<float>` on
      `{"Value": 16.0}` returns `16f`; `GetValue<bool>` on `{"Value": true}` returns `true`;
      `GetValue<string>` on `{"Value": "White"}` returns `"White"`.
- [ ] Failing test: `GetValue<SourceType>` on `{"Value": 2}` returns the enum member for `2`
      (raw-int enum landmine, §5.6).
- [ ] Failing test: `GetValue<T>` for a name not present returns `default(T)` and does not throw —
      matching `PropertySaveListExtensions.GetValue<T>` (`PropertySave.cs:120`).
- [ ] Implement `src/Glue/PropertySaveExtensions.cs` over `JsonElement`, covering
      `int`/`float`/`bool`/`string`/enum and their nullable forms.
- [ ] Failing test: nullable requests (`GetValue<int?>`) on an absent name return `null`, not `0`.

### 7.4 — Reference resolution and the read seam

- [ ] Failing test: `GlueProjectLoader` routes every read through its injectable `TextLoader`
      delegate and never touches `System.IO` (mirrors `TileMapLoadingTests.cs:15`).
- [ ] Failing test: loading the ChickenClicker fixture populates `Screens.Count == 3` and clears
      `ScreenReferences`, matching `LoadReferencedScreensAndEntities` (`GlueProjectSaveExtensions.cs:567`).
- [ ] Failing test: `"Screens\\MenuScreen"` resolves to `Screens/MenuScreen.glsj` with forward
      slashes on non-Windows (landmine §5.4).
- [ ] Failing test: a `ScreenReferences` entry whose file is absent produces one `Warning`
      diagnostic and leaves the other screens loaded.
- [ ] Implement `GlueProjectLoader.Load`, `GlueLoadResult`, `GlueLoadDiagnostic`, `GlueLoadOptions`.
- [ ] Failing test: `GlueLoadOptions.Strict` throws on the first `Error` diagnostic.
- [ ] Failing test: loading the Beefball fixture populates four entities and, for `PlayerBall`,
      retains two `NamedObjects` and ten `CustomVariables` un-applied (proving Phase 1 parses
      without instantiating).
- [ ] Failing test: a `.gluj` with `FileVersion` below 67 loads and emits one `Info` diagnostic (D6).

### 7.5 — Type mapping

- [ ] Failing test: `GlueTypeMap` maps `"FlatRedBall.Math.Geometry.Circle"` to FRB2's `Circle`
      (the string appears verbatim in `PlayerBall.glej:165`).
- [ ] Failing test: an unmapped type string returns no type and yields one `Warning` diagnostic
      naming both the element and the type string (D1).
- [ ] Implement `src/Glue/GlueTypeMap.cs` covering the Phase 2 target set:
      `Sprite`, `AxisAlignedRectangle`, `Circle`, `Polygon`, `ShapeCollection`, `Text`.
- [ ] Make the map extensible — later phases add rows without editing a `switch`.

### 7.6 — Boot into FRB2's screen system

- [ ] Failing test: `GlueScreen` constructed from a `ScreenSave` exposes that save and its `Name`.
- [ ] Failing test: resolving `StartUpScreen` picks the `ScreenSave` whose `Name` matches, and an
      unresolvable `StartUpScreen` yields an `Error` diagnostic rather than a `NullReferenceException`.
- [ ] Implement `src/Glue/GlueScreen.cs` and `src/Glue/GlueEntity.cs` as skeletons — hold the save,
      no `NamedObject` construction.
- [ ] Implement the boot entry point that hands the resolved `GlueScreen` to
      `FlatRedBallService` (`src/FlatRedBallService.cs:465` is the existing `Start<T>` path; a
      non-generic seam is needed because every loaded screen shares one CLR type — this is the
      Phase 14 API in embryo, so keep it `internal` for now rather than committing to public shape).
- [ ] Manual check: point the boot entry point at the vendored ChickenClicker fixture and confirm
      the game starts on `MenuScreen` with an empty screen and no exception.

### 7.7 — Documentation and wrap-up

- [ ] XML docs on every public type in `src/Glue/` — CS1591 is a tracked metric (see the comment in
      `src/FlatRedBall2.csproj`); do not add to the count.
- [ ] Log any FRB1 bug found (landmine §5.3 is already a candidate) as an issue on the FRB1 repo.
- [ ] Update this document's checkboxes and flip its **Status** row.
- [ ] Update the Phase 1 row in [`plan/plan.md`](../plan.md).
- [ ] Decide whether a `glue-project-loading` skill is warranted yet, or whether it should wait
      until Phase 2 gives it enough surface to be worth the context budget. Consult
      `skill-creator` before writing one.

---

## 8. Definition of done

- [ ] `dotnet build src/FlatRedBall2.csproj` succeeds with no new warnings and no AOT/trim warnings.
- [ ] `dotnet test tests/FlatRedBall2.Tests/` passes.
- [ ] Both vendored fixtures load with zero `Error` diagnostics.
- [ ] ChickenClicker boots to `MenuScreen` from its `.gluj` with no hand-written screen class.
- [ ] `Beefball`'s `PlayerBall` `.glej` round-trips into POCOs with `NamedObjects`,
      `CustomVariables`, and `StateCategoryList` all populated but un-applied.
- [ ] No Newtonsoft.Json reference was added anywhere.
- [ ] Every open decision in §6 is either implemented as recommended or amended in place with the
      reason it changed.
