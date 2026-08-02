# Phase 2 — NamedObjects

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Implemented — see §9 |
| **Depends on** | Phase 1 (loader, model, type map) — landed |
| **Suggested branch** | `804-phase-2-namedobjects` |

---

## 1. The problem

Phase 1 reads a Glue project into data and boots an empty screen. **Nothing appears.** This phase
turns the parsed `NamedObjectSave` entries into real FRB2 objects that exist, are positioned, and
render — the first phase whose output a person can see.

Because Phase 2 also applies `InstructionSaves` (initial values), objects land in the right place at
the right size, not stacked at the origin. That is what makes this phase worth shipping on its own.

---

## 2. Scope

### In scope

1. Construct instances for `SourceType.FlatRedBallType`: `Sprite`, `AxisAlignedRectangle`, `Circle`,
   `Polygon`.
2. Apply `InstructionSaves` as initial property values, via reflection.
3. `AttachToContainer` parenting, including `RelativeX`/`RelativeY`/`RelativeZ` offsets.
4. `ContainedObjects` (nested members) and `IsList` / `PositionedObjectList<T>`.
5. Register constructed objects so they render.

### Out of scope

- `SourceType.File` — needs Phase 4 (assets). A `Sprite` with no texture is still constructed.
- `SourceType.Entity` — needs Phase 6 (inheritance) for full fidelity; nested entities are recorded
  as unbuildable for now.
- `CustomVariables` (Phase 3), states (Phase 7), collision relationships (Phase 9), tile types
  (Phase 10), camera (Phase 13).
- `ShapeCollection` and `Text` — **FRB2 has neither type.** See D12.

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | Objects exist | A loaded screen's shapes and sprites are real instances, not just data. | §6.1 |
| F2 | Objects are configured | They have the size, colour, and position the author gave them in Glue. | §6.2 |
| F3 | Objects are attached | A shape authored at an offset inside an entity sits at that offset and follows its parent. | §6.3 |
| F4 | Lists and nesting hold | An object inside a list or inside another object keeps that relationship. | §6.4 |
| F5 | Objects render | Loading a project and running it draws something. | §6.5 |

---

## 4. Proposed resolution

### Construction

A small factory keyed off the Phase 1 `GlueTypeMap`. Only the four mapped types construct; anything
else keeps reporting as unbuildable exactly as Phase 1 does. No new type-string logic — `GlueTypeName`
already parses, and this phase consumes it.

### Property assignment

`InstructionSaves` carry a `Member` name and a raw JSON value. Assignment is reflection over the
target instance's public properties, converting the `JsonElement` to the property's declared type.
Unknown members and unconvertible values become diagnostics, never exceptions — the Phase 1 posture.

### Attachment — the mapping is simpler than it looks

FRB1 gives an attached object both an absolute `X` and a `RelativeX`. **FRB2 has one property**:
`ISpatialAttachable.X` is *already* the offset from `Parent` when attached, and world space when not
(`src/IAttachable.cs`). `AbsoluteX`/`AbsoluteY`/`AbsoluteZ` are the computed world values, read-only.

So both Glue members collapse onto the same FRB2 property, selected by attachment state:

| Glue member | `AttachToContainer` | FRB2 target |
|---|---|---|
| `RelativeX`/`Y`/`Z` | true | `X`/`Y`/`Z` |
| `X`/`Y`/`Z` | false | `X`/`Y`/`Z` |
| `X`/`Y`/`Z` | true | ambiguous — see G21 |

Attachment must happen **before** the offsets are assigned, or the first frame renders in the wrong
place.

### Registration

`Entity.Add(IAttachable child, Layer?)` for objects owned by an entity — it parents and registers in
one call. `Screen.Add(IRenderable renderable, Layer?)` for screen-level objects.

---

## 5. Gotchas

Carried forward from Phase 1 where still live; new ones numbered from G21.

### G21 — Absolute position on an attached object is ambiguous

Glue can author an `X` instruction on an object that also has `AttachToContainer = true`. In FRB1
those are different properties, so the absolute assignment is simply overwritten by attachment. FRB2
has one property, so applying it would place the object at that value *as an offset* — a different,
silently wrong result.

**How we tackle it.** When an object is attached and carries an absolute `X`/`Y`/`Z` instruction,
ignore the instruction and emit a warning naming the object. Matching FRB1's effective behaviour
(attachment wins) is the safe reading, and the warning surfaces authoring that never did what its
author expected.

### G22 — A `Sprite` with no texture is not a failure

`SourceType.File` sprites get their texture in Phase 4. Constructing a textureless `Sprite` now is
correct: it exists, is positioned, and will draw once Phase 4 supplies the texture. It must not be
reported as an error, or Phase 2's diagnostics drown in noise for every art-bearing project.

---

## 6. Tasks

Test-first throughout. Each group is roughly one commit.

### 6.1 — Construction

- [x] Failing test: a `NamedObjectSave` with `SourceClassType` `FlatRedBall.Math.Geometry.Circle`
      produces a real `Circle`.
- [x] Failing test: all four mapped types construct; an unmapped type produces no instance and one
      warning naming the object and its type.
- [x] Implement the object factory over `GlueTypeMap`.

### 6.2 — Property assignment

- [x] Failing test: an `InstructionSaves` entry `Radius = 16.0` lands as `Circle.Radius == 16f`.
- [x] Failing test: assignment covers float, int, bool, and string-named colour members.
- [x] Failing test: an unknown member name warns and does not throw.
- [x] Failing test: a value that cannot convert to the property type warns and leaves the default.
- [x] Implement reflection-based assignment, reusing any existing engine helper rather than writing a
      parallel one.

### 6.3 — Attachment

- [x] Failing test: an object with `AttachToContainer` gets `Parent` set to its owning entity.
- [x] Failing test: `RelativeX`/`RelativeY` land on FRB2's `X`/`Y`, and `AbsoluteX` reflects the
      parent's position plus the offset.
- [x] Failing test: attachment is applied before offsets, so no frame renders at the wrong position.
- [x] Failing test: an absolute `X` on an attached object is ignored with a warning (G21).

### 6.4 — Nesting and lists

- [x] Failing test: `ContainedObjects` are constructed and owned by their container.
- [x] Failing test: an `IsList` object produces a list whose members are the contained objects.
- [x] Failing test: a nested `SourceType.Entity` is recorded as deferred, not errored (Phase 6).

### 6.5 — Wire into the loaded screen

- [x] Failing test: a `GlueScreen` built from a `ScreenSave` has its objects constructed and
      registered.
- [x] Failing test: a `GlueEntity` built from an `EntitySave` likewise.
- [x] Failing test: the Beefball fixture builds its shapes with zero errors.
- [x] **Correction:** DoorsDemo's unmapped count stays at 13, and that is right. This phase added no
      new rows to `GlueTypeMap` — it made the already-mapped types actually construct. The count
      drops when Phases 9/10/13 claim tile collision, relationships, and the camera.
- [x] Vendor the Beefball fixture — shapes-only and tile-free, so it is the first project that can
      visibly work end to end.

### 6.6 — Wrap-up

- [ ] XML docs on new public types.
- [ ] Update this document's checkboxes and `plan/plan.md`.
- [ ] Record what Phase 3 must pick up.

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D12 | `ShapeCollection` and `Text` have no FRB2 equivalent | **Report as unbuildable and move on.** Adding two engine types to satisfy a loader is the tail wagging the dog; do it when a real project needs them, as its own scoped change. |
| D13 | Where does object construction live? | **A separate `GlueObjectBuilder`, not on `GlueScreen`.** Screens and entities both need it, and keeping it separate keeps it testable without a running engine. |
| D14 | Reflection vs. a hand-written property table | **Reflection**, matching the epic's data-driven ground rule. A hand-written table for four types would be faster but would have to grow for every type every later phase adds. |

---

## 8. Definition of done

- [ ] `dotnet build` clean, `dotnet test` green.
- [ ] Beefball's `PlayerBall` builds both its circles, at their authored radii, attached to the entity.
- [ ] DoorsDemo still loads with zero errors and a lower unmapped count than Phase 1's 13.
- [ ] Every gotcha in §5 is covered by a test or explicitly deferred.
