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
- **FRB1 is editable.** Both repos are checked out locally and both are ours. When the cheapest fix
  for a problem is a change on the FRB1/Glue side — re-saving a stale sample, fixing a corrupt
  committed file, correcting a serialization annotation — **make it there.** Do not contort the FRB2
  loader to accommodate a defect that should be fixed at the source. §5 tags each problem with the
  repo that owns the fix. The one hard constraint is that Glue keeps targeting FRB1 `.csproj`
  projects; this epic does not change what Glue *generates*.

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

## 5. Gotchas and problems — and how we tackle each

Everything below was verified against FRB1 source or real committed sample files, not inferred.
**Fix column** says which repo owns the resolution: `FRB2` = handle it in the loader, `FRB1` = fix
it at the source in the sibling `FlatRedBall` checkout (see the ground rule in §1), `Both` = needs a
change on each side.

| # | Problem | Severity | Fix |
|---|---|---|---|
| G1 | No FRB1 sample is at the required `FileVersion` | **Blocker** | FRB1 |
| G2 | `BeefballWeb.gluj` has a duplicate `FileVersion` key | High | FRB1 |
| G3 | Absent ≠ `false` — true-by-default members are omitted on write | **Blocker** | FRB2 |
| G4 | 31 semantically-important members are `[JsonIgnore]` bag-backed and never appear by name | **Blocker** | FRB2 |
| G5 | `[DefaultValue(true)]` disagrees with FRB1's own read path | Medium | FRB1 |
| G6 | `.gluj` and element files are written with different serializer settings | Medium | FRB2 |
| G7 | Element names are backslash-separated | High | FRB2 |
| G8 | Case sensitivity is unspecified | Medium | FRB2 |
| G9 | `PropertySave.Type` is not a CLR type name, and is sometimes absent | Medium | FRB2 |
| G10 | Same data lives in a typed property *and* the bag on the same object | Medium | FRB2 |
| G11 | Enum values are raw ints with no cross-repo compile-time link | High | Both |
| G12 | FRB1 silently swallows missing and corrupt element files | Low | FRB2 |
| G13 | An element's `Name` field can disagree with its file path | Low | FRB2 |
| G14 | Display/camera data is duplicated at the project root and in `DisplaySettings` | Low | FRB2 |
| G15 | `ShouldSerialize*` and `[JsonIgnore]` are Newtonsoft-only | Low | FRB2 |
| G16 | Real files contain shapes this epic excludes | Low | FRB2 |

---

### G1 — No FRB1 sample is at the required `FileVersion` · **Blocker** · fix in FRB1

`GlueProjectSave.LatestVersion` is **67** (`GlueProjectSave.cs:213`), and the epic's ground rule is
"latest `FileVersion` only." Every committed sample is below it:

| Version | Projects |
|---|---|
| 37 | `AdMobFrb` |
| 42 | `Beefball`, `BeefballKni`, `ChickenClicker` |
| 53 | `SkiaSampleProject` |
| 54 | `Tests/TestProjectDesktopNet6` |
| 55 | `BeefballWeb` |
| 60 | all six `Platformer/*Demo` projects |
| 61 | `FormsSampleProject` (the newest anything gets) |

The two fixtures the epic names — ChickenClicker and Beefball — are **25 versions stale**, and the
inheritance test case it calls out (`Platformer/*Demo/Screens/Level1.glsj`) is 7 behind. Taken
literally, the ground rule means there is currently nothing in FRB1 the loader is allowed to read.

**How we tackle it.** Open each fixture project in current Glue and re-save it, which is exactly the
migration the ground rule asks *users* to perform — so doing it ourselves both unblocks the work and
proves the migration path is real. Land the re-saved samples as a PR against the FRB1 repo, then
vendor from the re-saved output. Diff each before/after pair and record what version 42 → 67 actually
changed; that diff is free documentation of the schema delta and will inform every later phase.

Do **not** work around this by relaxing the version rule in code. If re-saving turns out to be
impractical for some project, that project simply is not a Phase 1 fixture.

---

### G2 — `BeefballWeb.gluj` has a duplicate `FileVersion` key · High · fix in FRB1

`Samples/BeefballWeb/BeefballWeb/BeefballWeb.gluj` lines 37–38:

```json
  "FileVersion": 42,
  "FileVersion": 55,
```

A committed, corrupt file — almost certainly a bad merge. Newtonsoft takes last-one-wins silently,
which is why nobody noticed.

**How we tackle it.** Fix the file in FRB1 (drop the stale `42`). On the FRB2 side, add a test
asserting what `System.Text.Json` does with a duplicate key so the behavior is pinned rather than
assumed — STJ is also last-one-wins for object members, but that is worth one test, not a guess.
Then treat this as evidence for a broader question: are there other hand-merged corruptions in the
sample set? A quick JSON well-formedness sweep across all `.gluj`/`.glsj`/`.glej` in FRB1 is cheap
and worth doing once.

---

### G3 — Absent ≠ `false` · **Blocker** · fix in FRB2

Element files are written with `DefaultValueHandling.Ignore` (`GlueProjectSaveExtensions.cs:434`),
so any member equal to its default is **omitted from disk**. `NamedObjectSave` restores its
true-by-default members in the **constructor** (`NamedObjectSave.cs:828`): `Instantiate`,
`AddToManagers`, `IncludeInICollidable`, `IncludeInIClickable`, `CallActivity`, `GenerateTimedEmit`.

A naive POCO with `bool Instantiate { get; set; }` therefore reads back `false` for **every object in
every real project** — and the failure is silent, producing an empty scene rather than an exception.

**How we tackle it.** Reproduce every constructor default in the mirror's parameterless constructor.
STJ calls it, so the behavior matches Newtonsoft exactly. Cover it with a test that deserializes JSON
which *omits* the member and asserts the default survived — a test asserting the default on a
`new NamedObjectSave()` would pass while the real bug shipped.

Keep the counter-example straight: `AttachToContainer` is *deliberately* left out of the constructor
(see the comment at `NamedObjectSave.cs:838`) and is written explicitly — `PlayerBall.glej:189` has
`"AttachToContainer": true`. Copying the constructor wholesale is right; extrapolating "all bools
default true" is wrong.

---

### G4 — Bag-backed members never appear by name · **Blocker** · fix in FRB2

A large share of the semantically important members are `[JsonIgnore]` accessors over the
`Properties` bag, so **they do not exist in the JSON under their own names at all**:

| Save class | Bag-backed getters |
|---|---|
| `EntitySave` | 9 |
| `NamedObjectSave` | 8 |
| `CustomVariable` | 7 |
| `ReferencedFileSave` | 5 |
| `ScreenSave` / `GlueElement` | 1 each |

`CustomVariable.Type` is the sharpest example (`CustomVariable.cs:62-69`): `[XmlIgnore]`,
`[JsonIgnore]`, `get => Properties.GetValue<string>("Type")`. A variable's **declared type — the
thing Phase 3 needs most — is a string inside a nested property bag**, not a field. Same story for
`Scope`, `OverridingPropertyType`, `TypeConverter`, `CreatesProperties`, and for
`NamedObjectSave.AssociateWithFactory` (`NamedObjectSave.cs:683`) and `EntitySave.InputDevice`.

**How we tackle it.** The mirror reproduces the bag-backed accessor pattern rather than only the
JSON-visible fields — the value bag is the primary storage, not a side-channel. This is also why §7.3
(the `GetValue<T>` helper) is a Phase 1 dependency of the model layer rather than a convenience: the
POCOs cannot expose their own properties without it. Build the helper first, then the POCOs on top.

---

### G5 — `[DefaultValue(true)]` disagrees with FRB1's own read path · Medium · fix in FRB1

`AddToManagers`, `IncludeInICollidable`, `IncludeInIClickable` and `CallActivity` are annotated
`[DefaultValue(true)]`, which Newtonsoft uses on *write* to omit them. But element files are read
back with **no settings at all** (`GlueProjectSaveExtensions.cs:578`, `:599`), so nothing consumes
the attribute on the way in — FRB1 relies purely on the constructor. The annotation and the reader
agree by coincidence, not by construction: change the constructor and the round-trip breaks silently.

**How we tackle it.** Log it against FRB1 with the round-trip test that demonstrates it. The epic
explicitly welcomes FRB1 bugs found this way. Low urgency — current behavior is correct by accident,
so this is fragility, not breakage. FRB2 does not wait on it: G3's approach (mirror the constructor)
is correct regardless of how FRB1 resolves this.

---

### G6 — `.gluj` and element files use different serializer settings · Medium · fix in FRB2

| File | Write settings | Read settings |
|---|---|---|
| `.gluj` | `NullValueHandling.Ignore` + `DefaultValueHandling.IgnoreAndPopulate` (`:127`) | `JsonConvert.DeserializeObject<GlueProjectSave>(text)`, no settings (`:532`) |
| `.glsj` / `.glej` | `DefaultValueHandling.Ignore` (`:434`) | no settings (`:578`, `:599`) |

So the two file kinds have genuinely different omission rules, and neither is read back with the
settings it was written with.

**How we tackle it.** Do not try to model "settings" in FRB2 — STJ has no equivalent knob and does
not need one. The only observable consequence is *which members get omitted*, and G3's approach
(constructor defaults) handles omission uniformly for both file kinds. Record the asymmetry here so
the next person does not rediscover it, and cover both file kinds in the round-trip tests rather than
assuming `.gluj` behavior generalizes to `.glsj`.

---

### G7 — Backslash-separated element names · High · fix in FRB2

`"Screens\\MenuScreen"` is a project-relative path with `\` separators and no extension. FRB2 targets
Linux and WASM, where `\` is a legal filename character rather than a separator — so an unnormalized
join does not throw, it looks for a file literally named `Screens\MenuScreen.glsj` and reports it
missing.

**How we tackle it.** Normalize `\` → `/` at the single point where a `Name` becomes a path, and test
it explicitly. One helper, one test, one place — resist scattering `Replace('\\','/')` through the
loader. Note that `Name` is also the element's identity for `StartUpScreen`, `BaseScreen`, and
`BaseEntity` lookups, where it must stay in its original backslash form: normalize for **paths only**,
never for identity comparisons.

---

### G8 — Case sensitivity is unspecified · Medium · fix in FRB2

`GluxVersions.CaseSensitiveLoading = 55` exists because this already bit FRB1 once. Glue authored
these names on Windows, so a project whose `.gluj` says `Screens\MenuScreen` and whose file is
`Screens/Menuscreen.glsj` works on Windows and fails on Linux and in the browser.

**How we tackle it.** Match case-insensitively and emit a diagnostic when the match required ignoring
case — the project loads everywhere, and the author still finds out. Take care that this does not
mask a genuinely missing file: a case-insensitive miss must still be a `Warning`, not silence.

---

### G9 — `PropertySave.Type` is not a CLR type name · Medium · fix in FRB2

Values observed in `ChickenClicker.gluj` alone: `"int"`, `"Boolean"`, `"String"`, `"SourceType"` — a
mix of C# keywords, CLR simple names, and Glue enum names. Some entries omit `Type` entirely
(`IncludeFormsInComponents`, `IncludeComponentToFormsAssociation`).

**How we tackle it.** Drive decoding from the **requested `T`**, exactly as
`PropertySaveListExtensions.GetValue<T>` does (`PropertySave.cs:72`) — never from the `Type` string.
Keep `Type` on the mirror for diagnostics and for Phase 3, but never let it steer a conversion. This
also means `Type` being absent is not an error condition.

---

### G10 — Same data in two places on the same object · Medium · fix in FRB2

`PlayerBall.glej:164-190` shows `SourceType` as both a real property (`"SourceType": 2`) **and** a
bag entry (`{"Name":"SourceType","Value":2,"Type":"SourceType"}`) on the same `NamedObjectSave`.

**How we tackle it.** Decide authority per member and write it down as you go — do not assume the
strongly-typed field wins. Where FRB1 declares the property as bag-backed (G4), the bag is
authoritative by definition and the mirror should expose only the accessor. Where both genuinely
exist, prefer the typed field and add a diagnostic when the two disagree; a disagreement is a
corruption signal worth surfacing, not a tie to break silently.

---

### G11 — Raw-int enums with no cross-repo compile-time link · High · fix in Both

Enums serialize as bare ints (`"SourceType": 2`) with no string converter. FRB2's mirrored enums must
therefore match FRB1's **numeric values** exactly — and nothing enforces that. If someone inserts a
member into `SourceType` in FRB1, every FRB2 project silently misreads every object of that type. No
compiler error, no test failure, just wrong behavior.

**How we tackle it.** Two parts. In FRB2: assign explicit numeric values to every mirrored enum
member (`FlatRedBallType = 2`, never bare ordinals) and add a test that pins each value, so a drift
shows up as a failing assert naming the member. In FRB1: add a comment at each mirrored enum
declaration noting that FRB2 mirrors its values and that members must be appended, never inserted or
reordered. That is the cheapest durable guard short of code-sharing the enums, which the epic's
package boundary rules out.

---

### G12 — FRB1 silently swallows missing and corrupt element files · Low · fix in FRB2

`LoadReferencedScreensAndEntities` skips a reference whose file is absent (`:574`) and skips a
`null` deserialization result (`:584`) with a comment explaining this is deliberate corruption
tolerance. The project loads with a screen quietly missing.

**How we tackle it.** Keep the tolerance, drop the silence — this is the direct motivation for
`GlueLoadResult` carrying diagnostics (§4). Same behavior, but the caller can see what was lost.

---

### G13 — Element `Name` can disagree with its file path · Low · fix in FRB2

`ElementReference.cs:109-114` carries a commented-out check for exactly this, with a note that it
"can cause errors at runtime" — so it is a real observed condition, not hypothetical.

**How we tackle it.** Compare the resolved reference name against the loaded element's `Name` and
emit a `Warning` on mismatch. Cheap, and it turns a class of confusing downstream failures into one
clear message at load time. Keep the file path authoritative for lookup.

---

### G14 — Display data duplicated at root and in `DisplaySettings` · Low · fix in FRB2

`ChickenClicker.gluj` carries `In2D`, `ResolutionWidth`, `ResolutionHeight`, `OrthogonalWidth`,
`OrthogonalHeight` at the root **and** a `DisplaySettings` block with its own `ResolutionWidth` /
`ResolutionHeight`. `GlueProjectSave.cs:220` labels the root copies "April 2017 - adding replacement
for these, eventually should get removed."

**How we tackle it.** Phase 1 parses both and applies neither — Phase 13 owns the mapping. Record
here that `DisplaySettings` is the newer, authoritative one so Phase 13 does not have to re-derive
that. Do not delete the root fields from the mirror; a real file still contains them, and Phase 13
may want to diagnose disagreement between the two.

---

### G15 — `ShouldSerialize*` and `[JsonIgnore]` are Newtonsoft-only · Low · fix in FRB2

The save classes are littered with `ShouldSerializeXxx()` methods and Newtonsoft `[JsonIgnore]`
attributes. STJ honors neither: it has no `ShouldSerialize` convention, and
`Newtonsoft.Json.JsonIgnoreAttribute` is a different type from
`System.Text.Json.Serialization.JsonIgnoreAttribute`.

**How we tackle it.** Irrelevant for Phase 1, which is read-only — flagged so it does not ambush
anyone later. **If write-back support is ever added, this becomes a blocker**, because FRB2 would
silently emit members FRB1 omits and produce `.gluj` diffs that churn on every save. Any future write
support needs its own design pass and a byte-comparison round-trip test against Glue's output.

---

### G16 — Real files contain shapes this epic excludes · Low · fix in FRB2

`ChickenClicker.gluj` has a populated `CustomClasses` array (`TileMapInfo`), which the epic
explicitly excludes. Real files also carry `SyncedProjects`, `PerformanceSettingsSave`,
`ResolutionPresets`, `Bookmarks`, and `PluginData`.

**How we tackle it.** Omit them from the mirror entirely — STJ ignores unknown JSON members by
default, so their presence costs nothing. "Out of scope" must mean the loader is *unbothered* by
them, not that it rejects files containing them. Add one test loading a fixture with a populated
`CustomClasses` and asserting a clean load, so nobody later mistakes tolerance for an oversight.

---

## 6. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D1 | Fail-fast vs skip-with-warning for unmapped types | **Skip + diagnostic**, with an opt-in `Strict` mode. Reasoning in §4. |
| D2 | Mirror POCO names | **Keep FRB1 names verbatim** under `FlatRedBall2.Glue.Model`. |
| D3 | Fixture location | **`tests/FlatRedBall2.Tests/Glue/Fixtures/<ProjectName>/`**, copied to output via `<None ... CopyToOutputDirectory="PreserveNewest" />` — matches the existing `Animation\Content\Corpus\**` rows in `tests/FlatRedBall2.Tests/FlatRedBall2.Tests.csproj:24`. |
| D4 | Which sample to vendor first | **`Samples/ChickenClicker`, re-saved to version 67 first** (G1) — 111-line `.gluj`, three 15-line `.glsj`, no entities. Smallest thing that exercises the full reference-resolution path. Add a re-saved `Samples/Beefball` (4 entities with `NamedObjects`, `CustomVariables`, `StateCategoryList`) as the richer second fixture. |
| D5 | Case-sensitivity rule for element-name lookup | **Case-insensitive match with a diagnostic on case mismatch** (G8). Glue authored these on Windows; a silent miss on Linux is the worse failure. Confirm this does not mask a genuinely missing file. |
| D6 | `FileVersion` enforcement | **Warn below 67, do not block** — but vendor only re-saved v67 fixtures, so the warning path is exercised by a synthetic file rather than by the real corpus. Blocking is tempting given the ground rule, but a hard gate turns any future FRB1 version bump into an instant total test failure with no diagnostic. Revisit if version drift causes real misreads. |
| D7 | Who re-saves the FRB1 samples, and does it land upstream? | **We do, as a PR to the FRB1 repo** (G1). Re-saving is the migration the ground rule already demands of users; doing it upstream means every FRB1 consumer benefits and FRB2 vendors from a clean source rather than a local scratch copy. Confirm with the FRB1 maintainer that bumping sample versions is welcome before opening the PR. |
| D8 | Authority when a typed field and a bag entry disagree | **Typed field wins, disagreement emits a diagnostic** (G10) — except where FRB1 declares the member bag-backed (G4), in which case the bag is authoritative by definition. |

---

## 7. Tasks

Repo rule: **a failing test comes first, or the commit body explains why one was not feasible.**
Load the `engine-tdd` skill before touching `src/`. Each group below is roughly one commit.

### 7.0 — FRB1-side work (do this first; G1 blocks everything else)

Lands in the sibling `FlatRedBall` repo, not this one. See D7 before opening the PR.

- [ ] Confirm with the FRB1 maintainer that bumping sample `FileVersion`s upstream is welcome.
- [ ] Fix the duplicate `"FileVersion"` key in `Samples/BeefballWeb/BeefballWeb/BeefballWeb.gluj`
      lines 37–38 (G2) — drop the stale `42`.
- [ ] Sweep every `.gluj`/`.glsj`/`.glej` in the FRB1 repo for JSON well-formedness and duplicate
      keys; fix what turns up (G2).
- [ ] Open `Samples/ChickenClicker` in current Glue and re-save it to `FileVersion` 67 (G1).
- [ ] Open `Samples/Beefball` in current Glue and re-save it to `FileVersion` 67 (G1).
- [ ] Record the version 42 → 67 diff for both projects in this document — it is the schema delta
      every later phase will need.
- [ ] Add a comment at each enum FRB2 will mirror, noting that FRB2 pins its numeric values and that
      members must be appended, never inserted or reordered (G11).
- [ ] File an FRB1 issue for the `[DefaultValue(true)]` / read-path mismatch (G5), with the
      round-trip test that demonstrates it.
- [ ] Open the FRB1 PR; do not start 7.1 until the re-saved samples exist.

### 7.1 — Fixtures and conventions

- [ ] Create `tests/FlatRedBall2.Tests/Glue/` (mirrors `src/Glue/`, per `.claude/code-style.md` §Test Organization).
- [ ] Vendor the **re-saved** `Samples/ChickenClicker` Glue files into
      `tests/FlatRedBall2.Tests/Glue/Fixtures/ChickenClicker/` — `ChickenClicker.gluj` plus
      `Screens/GameScreen.glsj`, `Screens/MenuScreen.glsj`, `Screens/OptionsScreen.glsj`.
- [ ] Vendor the **re-saved** `Samples/Beefball` Glue files into `Fixtures/Beefball/` — `.gluj`,
      `Screens/GameScreen.glsj`, and the four `.glej` files (`PlayerBall`, `Puck`, `Goal`, `ScoreHud`).
- [ ] Author a tiny hand-written `Fixtures/Synthetic/` project for the cases no real sample covers:
      a below-version `.gluj` (D6), a missing element reference (G12), a name/path mismatch (G13),
      and a case-mismatched reference (G8).
- [ ] Add a `Fixtures/README.md` recording the source repo, sample path, FRB1 commit, and sync date,
      so a future re-sync knows what it is re-syncing from.
- [ ] Add the `<None Include="Glue\Fixtures\**\*" CopyToOutputDirectory="PreserveNewest" />` row to
      `tests/FlatRedBall2.Tests/FlatRedBall2.Tests.csproj`.

### 7.2 — POCO mirror

- [ ] Failing test: deserializing the ChickenClicker `.gluj` fixture yields `FileVersion == 67`,
      `StartUpScreen == "Screens\\MenuScreen"`, and three `ScreenReferences`.
- [ ] Add `src/Glue/Model/` POCOs for `GlueProjectSave`, `GlueElementFileReference`, `GlueElement`,
      `ScreenSave`, `EntitySave`, `PropertySave`, `InstructionSave`, `NamedObjectSave`,
      `CustomVariable`, `ReferencedFileSave`, `StateSave`, `StateSaveCategory`, `DisplaySettings`.
- [ ] Type `PropertySave.Value` and `InstructionSave.Value` as `JsonElement` (see §4).
- [ ] Add `src/Glue/GlueJsonContext.cs` with `[JsonSerializable]` entries for every root shape and
      `PropertyNameCaseInsensitive = true`, mirroring `src/Movement/TopDownConfig.cs:40`.
- [ ] Failing test: a `NamedObjectSave` deserialized from JSON that **omits** `Instantiate` reports
      `Instantiate == true` (G3) — assert on deserialized JSON, not on `new NamedObjectSave()`.
- [ ] Reproduce every `NamedObjectSave` constructor default from `NamedObjectSave.cs:828`, and
      confirm `AttachToContainer` is *not* among them (G3).
- [ ] Expose the 31 bag-backed members as accessors over `Properties` rather than as fields (G4) —
      `CustomVariable.Type`, `Scope`, `OverridingPropertyType`, `TypeConverter`,
      `NamedObjectSave.AssociateWithFactory`, `EntitySave.InputDevice`, and the rest.
- [ ] Assign explicit numeric values to every mirrored enum member, and add a test pinning each one
      against the FRB1 value it mirrors (G11).
- [ ] Failing test: JSON with a duplicate object key resolves last-one-wins under STJ (G2).
- [ ] Failing test: a `.gluj` with a populated `CustomClasses` array loads cleanly with the member
      absent from the mirror (G16).
- [ ] Verify the build stays AOT-clean: `dotnet build src/FlatRedBall2.csproj` emits no
      `IL2026`/`IL3050` trim or AOT warnings from the new code.

### 7.3 — Value-bag decoding

Build this **before** the POCOs in 7.2 — the mirror's bag-backed accessors depend on it (G4).

- [ ] Failing test: `GetValue<int>` on `{"Value": 1}` returns `1`; `GetValue<float>` on
      `{"Value": 16.0}` returns `16f`; `GetValue<bool>` on `{"Value": true}` returns `true`;
      `GetValue<string>` on `{"Value": "White"}` returns `"White"`.
- [ ] Failing test: `GetValue<SourceType>` on `{"Value": 2}` returns the enum member for `2`
      (G9, G11).
- [ ] Failing test: `GetValue<T>` for a name not present returns `default(T)` and does not throw —
      matching `PropertySaveListExtensions.GetValue<T>` (`PropertySave.cs:120`).
- [ ] Failing test: decoding is driven by the requested `T`, not by the entry's `Type` string —
      `GetValue<int>` succeeds on an entry whose `Type` is absent, and on one whose `Type` reads
      `"Boolean"` while the value is numeric (G9).
- [ ] Implement `src/Glue/PropertySaveExtensions.cs` over `JsonElement`, covering
      `int`/`float`/`bool`/`string`/enum and their nullable forms.
- [ ] Failing test: nullable requests (`GetValue<int?>`) on an absent name return `null`, not `0`.

### 7.4 — Reference resolution and the read seam

- [ ] Failing test: `GlueProjectLoader` routes every read through its injectable `TextLoader`
      delegate and never touches `System.IO` (mirrors `TileMapLoadingTests.cs:15`).
- [ ] Failing test: loading the ChickenClicker fixture populates `Screens.Count == 3` and clears
      `ScreenReferences`, matching `LoadReferencedScreensAndEntities` (`GlueProjectSaveExtensions.cs:567`).
- [ ] Failing test: `"Screens\\MenuScreen"` resolves to `Screens/MenuScreen.glsj` with forward
      slashes on non-Windows (G7).
- [ ] Failing test: `\` normalization applies to path building only — `StartUpScreen` and
      `BaseScreen` identity comparisons still match on the original backslash form (G7).
- [ ] Failing test: a reference that matches a file only when case is ignored loads, and emits one
      `Warning`; a reference matching nothing still emits a `Warning` and does not silently pass (G8).
- [ ] Failing test: a `ScreenReferences` entry whose file is absent produces one `Warning`
      diagnostic and leaves the other screens loaded (G12).
- [ ] Failing test: an element whose internal `Name` disagrees with the reference that loaded it
      emits one `Warning`, and the file path stays authoritative (G13).
- [ ] Implement `GlueProjectLoader.Load`, `GlueLoadResult`, `GlueLoadDiagnostic`, `GlueLoadOptions`.
- [ ] Failing test: `GlueLoadOptions.Strict` throws on the first `Error` diagnostic.
- [ ] Failing test: loading the Beefball fixture populates four entities and, for `PlayerBall`,
      retains two `NamedObjects` and ten `CustomVariables` un-applied (proving Phase 1 parses
      without instantiating).
- [ ] Failing test: the `.glsj`/`.glej` omission rules are covered independently of `.gluj`'s — do
      not assume one generalizes to the other (G6).
- [ ] Failing test: a `.gluj` with `FileVersion` below 67 loads and emits one `Info` diagnostic,
      using the synthetic fixture rather than a real sample (D6).

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
- [ ] Log any further FRB1 bug found during implementation as an issue on the FRB1 repo (G2 and G5
      are already known; the epic explicitly welcomes more).
- [ ] Amend §5 in place with any gotcha implementation surfaces that this list missed, and note in
      §6 where a decision changed and why.
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
- [ ] Every gotcha in §5 is either covered by a test, fixed upstream in FRB1, or explicitly deferred
      to a named later phase — none silently dropped.
- [ ] The FRB1-side PR (§7.0) is merged, and `Fixtures/README.md` names the FRB1 commit it vendored.
