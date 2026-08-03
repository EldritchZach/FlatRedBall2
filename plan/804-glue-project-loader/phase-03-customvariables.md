# Phase 3 — CustomVariables

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Not started |
| **Depends on** | Phase 2 (objects exist and are addressable by `Objects[name]`) |
| **Blocks** | Phase 7 (a state is a set of CustomVariable values), Phase 14 (the indexer) |
| **Suggested branch** | `804-phase-3-customvariables` |

---

## 1. The problem

Phase 2 builds a screen's objects from `NamedObjectSave` data. But the values an author tunes in
Glue's variable grid — a player's `MovementSpeed`, a shape's colour, a HUD's score — are
**`CustomVariables`**, a separate list that Phase 2 does not read at all.

Two thirds of a real project's authored values live here. Beefball's `PlayerBall` has ten
CustomVariables and only two NamedObjects.

The phase has two halves, and the second is the interesting one:

- **Exposed variables** name a property that already exists on the object (`X`, `Visible`, `Drag`).
- **Tunneled variables** forward to a member of a *contained* object: `CooldownCircleRadius` sets
  `Objects["CooldownCircle"].Radius`. 87 of 590 CustomVariables across FRB1 are tunneled.

---

## 2. Scope

### In scope

1. Apply `DefaultValue` to exposed variables.
2. Apply tunneled variables through `SourceObject` / `SourceObjectProperty`.
3. Honour `OverridingPropertyType` + `TypeConverter` coercion.
4. A property bag for variables that name no CLR member.
5. Correct ordering relative to Phase 2's `InstructionSaves`.

### Out of scope

- `CreatesEvent` — FRB2 has no variable-change event surface.
- `Scope`, `Category`, `Summary`, `PreferredDisplayerTypeName` — editor metadata (D30).
- `SetByDerived` / `DefinedByBase` merge behaviour → Phase 6.
- Variables whose declared type is a state category → Phase 7.
- CSV-typed variables (`"Ground in PlatformerValuesStatic.csv"`) → Phases 4 / 11 / 12.
- `HasAccompanyingVelocityProperty` (5 occurrences repo-wide).

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | Authored values apply | A Beefball paddle actually moves at its authored `MovementSpeed`. | §6.2 |
| F2 | Tunneling works | `CooldownCircleRadius` resizes the circle inside the entity. | §6.3 |
| F3 | Type coercion works | `ScoreHud.Score1 = 0` (an `int`) reaches a `string` `DisplayText`. | §6.4 |
| F4 | Unknown names survive | A variable with no CLR home is readable rather than lost. | §6.5 |
| F5 | Ordering matches Glue | A variable that overrides an instruction wins, as it does in FRB1. | §6.6 |

---

## 4. Proposed resolution

### Application order — copied from FRB1's generated code, not invented

Verified against checked-in generated output (`Samples/BeefballKni/Beefball/Entities/PlayerBall.Generated.cs`)
and `CodeWriter.cs:2729-2763`:

1. Construct all NamedObjects.
2. Attach children.
3. Per NamedObject: apply `CurrentState` (Phase 7), **then** its `InstructionSaves`.
4. Apply the element's `CustomVariables`, **in array order**.
5. `CustomInitialize`.

**Element CustomVariables therefore win over NamedObject instruction values.** `ScoreHud` proves it:
the NOS sets `Team1Score.DisplayText = "99"`, then the CV sets `Score1 = 0`, and the label reads
`0`. Getting this backwards would silently show placeholder text in every Beefball score.

Order *within* step 4 matters too — see G34.

### The assignment gate

FRB1 assigns a CustomVariable only when `DefaultValue != null`, `!IsShared`, and the target is not a
disabled object (`CustomVariableCodeGenerator.cs:840-843`). The first condition is the important
one and is the subject of G30.

### Tunneling

`SourceObject` names a NamedObject in the same element; `SourceObjectProperty` names a member on it.
Phase 2 already exposes exactly the lookup needed — `Objects[SourceObject]` — so tunneling is
`GlueValueConverter.TryConvert` against that object's property. No new machinery.

### Where non-exposed variables live

`MovementSpeed`, `DashFrequency`, `Score1` have no member on FRB2's `Entity`. They need a bag on
`GlueScreen` / `GlueEntity`. Phase 14 turns that bag into an indexer; this phase only needs it to
exist and to be readable. **Resolution order is the design decision** — see D31 and Phase 14.

---

## 5. Gotchas

### G30 — Absent `DefaultValue` means "do not assign", not `default(T)` · **Blocker**

**423 of 590 CustomVariables in FRB1 have no `DefaultValue`.** FRB1 skips them entirely
(`CustomVariableCodeGenerator.cs:840`, with the comment *"if it's null the user doesn't want to
change what is set in the file or in the source object"*).

Beefball's `PlayerBall` declares `X`, `Y` and `Z` as CustomVariables with no `DefaultValue`. A reader
that treats absent as `0` **moves every entity in every project to the origin** — after Phase 2 put
it in the right place.

**How we tackle it.** `DefaultValue` is a non-nullable `JsonElement` in the mirror, so absent
deserializes to `JsonValueKind.Undefined` and an explicit null to `JsonValueKind.Null`. **Both mean
skip.** This is the same `Undefined`-vs-value trap that produced Phase 1's decode crash; the gate
belongs in one place.

### G31 — Sentinel strings are decoded by FRB1's property setters, which STJ does not run

Three members normalise a magic string in their setter:

| Member | Sentinel | Becomes | FRB1 line |
|---|---|---|---|
| `DefaultValue` | `"<NONE>"` | `null` | `CustomVariable.cs:96-99` |
| `SourceObject` | `"<NONE>"` | `""` | `:147-150` |
| `SourceObjectProperty` | `"<NONE>"` | `""` | `:162-165` |

Newtonsoft round-trips *through* those setters. **`System.Text.Json` binding to an auto-property does
not**, so FRB2 reads the literal string and treats `"<NONE>"` as a real value — producing a tunnel to
an object named `<NONE>`.

**How we tackle it.** Apply the mappings explicitly in the mirror. This is a recurring shape — see
also `NamedObjectSave.CurrentState` (Phase 7) and `SourceClassGenericType` — so put it in one helper
and use it everywhere a sentinel exists.

### G32 — `DefaultValue: ""` means "unset" for numeric and bool types

Glue suppresses assignment when the value is an empty string and the declared type is
`bool`/`int`/`float`/`long`/`byte`/`double`/`decimal` (`StateCodeGenerator.cs:154-167`). Real
occurrence: `VisibleBoolAsBool` in `TunneledVariableEntity.glej` has `"DefaultValue": ""` with
`Type: bool`.

**How we tackle it.** Same gate as G30, keyed on the declared `Type`. An empty string reaching
`bool.Parse` throws; reaching `float` conversion yields `0` and silently zeroes a real value.

### G33 — `OverridingPropertyType` and `TypeConverter` are behavioural, not cosmetic

The issue lists these as "decide which are meaningful vs editor-only metadata". **They are
meaningful**, and skipping them breaks a real sample.

Beefball's `ScoreHud.Score1`: `Type: string`, `OverridingPropertyType: int`,
`TypeConverter: <default>`, tunneling to `Team1Score.DisplayText`. FRB1 generates:

```csharp
public virtual int Score1 {
    get { return int.Parse(Team1Score.DisplayText); }
    set { Team1Score.DisplayText = value.ToString(); }
}
```

**The stored `DefaultValue` is in the overriding type** — `0` as a JSON number, for a variable whose
target property is a string. Converting straight to the target type gives `"0"` by luck here, but
`Comma Separating` (`"n0"` formatting) would give `"1,000"` where naive conversion gives `"1000"`.

Registered converters (`TypeConverterHelper.cs:32-76`): `<default>`, `Comma Separating`,
`Minutes:Seconds`, `Minutes:Seconds.Hundredths`. Only the first two appear on disk (13 occurrences).

**How we tackle it.** Implement `<default>` and `Comma Separating`; warn on the other two.

### G34 — Declaration order inside `CustomVariables` is load-bearing

In `StateEntity.glej`, `CurrentState` (which sets `X = 8`) is index 2 and `X` (default `0.0`) is
index 4 — so `X` ends at `0`. Reordering the list changes the result.

**How we tackle it.** Apply in array order and never sort. One exception exists and is genuinely
out of order: variables whose `SourceObject` is a `Layer` are applied early, during AddToManagers
(`CodeWriter.cs:1176-1183`). FRB2 has no `Layer` NamedObject support, so record the divergence and
move on.

### G35 — `Type` is bag-only, mandatory, and often not a CLR type name

`CustomVariable.Type` is `[JsonIgnore]` and read from `Properties` (`CustomVariable.cs:62-69`) —
Phase 1's G4. It is present on **590 of 590** variables; FRB1 *throws* on a null `Type`
(`ObjectFinder.cs:810-814`), so absence means malformed.

But the values are not CLR type names. Observed: `float`, `int`, `bool`, `string`, `Color`,
`Microsoft.Xna.Framework.Color`, `Texture2D`, `AnimationChainList`, `List<string>`,
`List<Vector2>`, `VariableState`, a state-category name (`TopOrBottom`), a CSV path
(`GlobalContent/GlobalCsv.csv`), a generated CSV class (`DoorsDemo.DataTypes.PlatformerValues`), and
a nullable Gum state (`…Runtime.Category1?`).

**How we tackle it.** `Type` drives *numeric widening and the empty-string gate* (G32) — nothing
else. The target property's CLR type still drives conversion, as `GlueValueConverter` already does.
Unrecognised `Type` values warn and skip rather than guessing.

### G36 — Structured values are strings inside JSON arrays

`List<Vector2>` on disk is `["-16, 16", "16, 16", …]` — comma-separated **strings**, parsed at
`CustomVariableExtensionMethods.cs:305-324`. `FloatRectangle?` is `"(x, y, w, h)"` (`:337-364`).

This is the same encoding as polygon `Points` (see the Phase 2 correction), so one decoder serves
both.

### G37 — The named-colour table is 26 entries and the real set is ~140

`GlueValueConverter.BuildNamedColors` (`src/Glue/GlueValueConverter.cs:149-178`) covers the obvious
names. `Aquamarine` appears on disk in `TunneledVariableEntity.glej`, and FRB1 does no validation at
all — it emits `Color.` + the raw string (`StateCodeGenerator.cs:857-859`).

**How we tackle it.** Extend the table to the full XNA set. The table exists because reflection over
`Color` is not trim-safe across the MonoGame and KNI backends (Phase 2's G26) — that reasoning still
holds, so the answer is more rows, not reflection.

### G38 — `CreatesProperty` reads a bag key with a different name

`CustomVariable.CreatesProperty` (singular) reads `Properties["CreatesProperties"]` (**plural**) —
`CustomVariable.cs:267`, `:271`. Two occurrences repo-wide. Cheap to get wrong, cheap to get right.

Related: `Category` is written **twice** — once as a JSON member and once as a bag entry, deliberately
(`CustomVariable.cs:285-295`). Verified identical in all 57 occurrences.

### G39 — FRB1 gates NamedObject instructions in a way FRB2 does not

`GenerateVariableAssignment` (`NamedObjectSaveCodeGenerator.cs:358-364`) only assigns an instruction
whose `Member` is a recognised exposed variable, a CustomVariable on the referenced element, a
NamedObject on it, or an ATI `VariableDefinition`. Anything else is **silently dropped**.

FRB2's `ApplyInstructions` reflects over any writable property and warns when it finds none — which
is *more* permissive. That is a defensible widening, but it is a divergence and should be a
deliberate one rather than an accident.

**How we tackle it.** Keep FRB2's behaviour; record it here so a future "why does FRB1 ignore this?"
has an answer.

---

## 6. Tasks

Test-first throughout.

### 6.1 — Model completion

- [ ] Add missing `CustomVariable` members: `IsShared`, `DefinedByBase`, `CreatesEvent`, `Summary`,
      and bag-backed `OverridingPropertyType`, `TypeConverter`, `CreatesProperty` (key
      `"CreatesProperties"` — G38), `HasAccompanyingVelocityProperty`.
- [ ] Failing test: `"<NONE>"` normalises for `DefaultValue`, `SourceObject`, `SourceObjectProperty`
      (G31).
- [ ] Failing test: an absent `DefaultValue` reads as `Undefined`, an explicit null as `Null`, and
      both classify as "do not assign" (G30).

### 6.2 — Exposed variables

- [ ] Failing test: `MovementSpeed = 300` from Beefball's `PlayerBall` lands in the bag.
- [ ] Failing test: `X` with no `DefaultValue` leaves the Phase 2 position untouched (G30) — assert
      against the real fixture, not synthetic JSON.
- [ ] Failing test: `DefaultValue: ""` on a `bool`-typed variable is skipped (G32).
- [ ] Failing test: a variable naming a real FRB2 property writes that property, not the bag.

### 6.3 — Tunneling

- [ ] Failing test: `CooldownCircleRadius = 16` sets `Objects["CooldownCircle"].Radius`.
- [ ] Failing test: `CircleInstanceColor = "White"` sets the circle's colour.
- [ ] Failing test: a tunnel naming a missing `SourceObject` warns and skips.
- [ ] Failing test: a tunnel whose `SourceObject` was unbuildable in Phase 2 warns, not throws.

### 6.4 — Type coercion

- [ ] Failing test: `ScoreHud.Score1` (int → string via `<default>`) reaches `DisplayText` as `"0"`.
- [ ] Failing test: `Comma Separating` formats `1000` as `"1,000"`.
- [ ] Failing test: `Minutes:Seconds` warns as unsupported.
- [ ] Failing test: `List<Vector2>` decodes from `["-16, 16", …]` (G36) — shared with polygon
      `Points`.
- [ ] Extend the named-colour table to the full XNA set; test `Aquamarine` (G37).

### 6.5 — The bag

- [ ] Failing test: a variable with no CLR member is readable afterwards.
- [ ] Failing test: reads are type-driven by the caller's `T`, matching `GetValue<T>`.
- [ ] Decide and document the resolution order (D31).

### 6.6 — Ordering

- [ ] Failing test: an element CustomVariable overrides a NamedObject instruction — Beefball's
      `ScoreHud` ends at `"0"`, not `"99"`.
- [ ] Failing test: declaration order is preserved; a later variable wins (G34).

### 6.7 — Wrap-up

- [ ] XML docs; update this document and `plan/plan.md`.
- [ ] Record what Phase 7 inherits: a state is "for each included variable, instruction-or-default",
      so it reuses everything above.

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D30 | Are `Scope` / `Category` meaningful in a data-driven model? | **No — editor-only.** `Scope` controls the *generated member's* C# accessibility; with no generated member there is nothing to scope. `Category` is grid grouping. Parse both, apply neither. (3 and 57 occurrences respectively, none in a sample game.) |
| D31 | One indexer for CustomVariables and NamedObjects, or two? | **One, with documented resolution order** — tunneled CV, then exposed CV, then NamedObject. Glue blocks a CustomVariable from shadowing a NamedObject but *not* the reverse, so collisions are creatable; throw on the ambiguous case rather than picking silently. Empirically **0 collisions in 273 FRB1 element files**. Final shape is Phase 14's call. |
| D32 | Honour `IsShared`? | **Skip with a diagnostic.** It makes the generated member `static`, which has no data-driven meaning, and it suppresses instance assignment. Five occurrences, none in a sample. |
| D33 | Implement `CreatesEvent`? | **No.** FRB2 has no variable-change event. 46 occurrences, but the event only matters to hand-written C# that does not exist here. |

---

## 8. Definition of done

- [ ] `dotnet build` clean; `dotnet test` green.
- [ ] A real `PublishTrimmed` emits no IL warnings from `src/Glue` (Phase 2 G26).
- [ ] Beefball's `PlayerBall` gets all ten CustomVariables, with the three tunneled ones reaching
      their circles.
- [ ] Beefball's `ScoreHud` labels read `"0"` — proving both tunneling and int→string coercion.
- [ ] No entity moves to the origin: the `X`/`Y`/`Z`-with-no-default case is covered by a test on
      the real fixture (G30).
- [ ] Every gotcha in §5 is covered by a test or explicitly deferred.
