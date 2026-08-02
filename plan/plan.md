# FlatRedBall2 — Plan Index

This is the table of contents for multi-phase work in this repo. Each entry below points at an
**initiative folder** containing one markdown document per phase.

Use this folder for work that is too large for a single PR and needs to be sequenced across
several independently-shippable chunks. One-off changes do not need a plan document — just do
the work.

## How this folder is organized

```
plan/
  plan.md                                  <- this file (the index)
  <issue#>-<initiative-slug>/              <- one folder per initiative/epic
    phase-01-<slug>.md
    phase-02-<slug>.md
    ...
```

Rules:

- **`plan.md` is the index, never the content.** One row per phase, linking to the phase doc.
  Never inline phase detail here.
- **One document per phase.** A phase doc is self-contained: it restates the problem, the
  proposed resolution, and every step as a checkbox. A reader should not need the GitHub issue
  open to work the phase.
- **Phase docs are living.** Check boxes off as work lands. Add discovered work as new checkboxes
  rather than silently expanding an existing one.
- **Write the next phase doc when the previous phase is stable**, not up front. Writing all
  phases before any code exists produces documents that are wrong by the time they are read.
- **Status values:** `Not started` / `In progress` / `Blocked` / `Landed`. Update the row here
  when a phase's status changes.
- **When an initiative completes**, keep the folder — phase docs are the record of *why* a
  design turned out the way it did. Mark every row `Landed`.

Relationship to the other markdown folders:

| Folder | Holds |
|---|---|
| `plan/` | Multi-phase implementation plans (this folder) |
| `design/TODOS.md` | Small, actionable open items that don't warrant a plan |
| `design/*.md` | Design write-ups for a single subsystem |
| `.claude/designs/` | Game design documents for sample games |

## Initiatives

### [Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2](804-glue-project-loader/)

Tracking issue: [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804)

Flip the FRB1/Glue relationship: instead of Glue generating C# that FRB1 compiles, FRB2 loads
the JSON project files Glue produces and builds Screens/Entities from them directly, at runtime,
via reflection. No codegen step.

| Phase | Document | Status |
|---|---|---|
| 1 | [Foundations + Screens/Entities skeleton](804-glue-project-loader/phase-01-foundations.md) | In progress |
| 2 | NamedObjects | Not written |
| 3 | CustomVariables | Not written |
| 4 | Referenced files / assets | Not written |
| 5 | Gum integration | Not written |
| 6 | Inheritance | Not written |
| 7 | States & categories | Not written |
| 8 | Factories / spawning | Not written |
| 9 | Collision relationships | Not written |
| 10 | Tiled (TMX) | Not written |
| 11 | Top-down movement | Not written |
| 12 | Platformer movement | Not written |
| 13 | Camera / display setup | Not written |
| 14 | Name-based Screen navigation and Entity instantiation API | Not written |

The phase list above mirrors issue #804 and **is not exhaustive** — expect it to grow as
implementation surfaces schema corners the issue did not anticipate. Add new phases rather than
cramming unrelated work into an existing one.
