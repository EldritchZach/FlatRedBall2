---
name: engine-tdd
description: "Test-first discipline for FlatRedBall2 engine changes. Triggers whenever editing any file under src/ for a behavior change (bug fix or feature). Not for XML docs, renames, style-only edits, or sample code."
---

# Engine Changes Require a Failing Test First

Behavior changes in `src/` require a failing test in `tests/FlatRedBall2.Tests/` **before** the source edit. Write it, run it, watch it fail, then fix.

No "the cause is obvious, I'll skip the test" exception — that reasoning is how silent regressions ship. If you're about to edit `src/` without a failing test open, stop.

Exceptions: XML docs, style-only edits, pure renames, dead-code removal.

## New Shader / Blend-State Code — Self-Verify Rendered Output, Don't Just Trust the Math

A unit test on the pure math around a shader (offset clamping, parameter selection) proves nothing about the shader itself — the risk in new rendering code lives in the untested GPU/blend-state boundary, not the wiring calling into it. "No headless `GraphicsDevice`" is a reason unit tests can't cover it; it is not a reason to skip verification entirely.

Before saying a shader/blend-state change doesn't need manual testing, render it yourself: a throwaway capture harness (temporary `Draw`-hook that dumps `GraphicsDevice.GetBackBufferData` to PNG, reverted after) is enough to catch what math alone can't — e.g. a premultiplied-alpha texture (MonoGame's default) making an "additive offset" shader leak color into fully transparent pixels, invisible in any offset-clamping unit test but obvious in one rendered frame.

## API Design — Flag Before Implementing

Before adding any new `public` or `virtual` member to an engine base class (`Screen`, `Entity`, `FlatRedBallService`, etc.), stop and flag it as an API design decision. Ask before writing code. New public/virtual surface is a footgun risk — it implies intent to users, shows up in IntelliSense, and is hard to remove once shipped.
