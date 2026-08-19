## What this changes

<!-- One or two sentences. Link the issue if there is one. -->

Closes #

## Why

<!-- The problem behind the change, not a restatement of the diff. -->

## How it was verified

<!-- Delete what does not apply. The point is what you OBSERVED, not what should happen. -->

- [ ] `dotnet build HdrSwitch.sln -c Release` — no warnings
- [ ] `dotnet test` — all green
- [ ] `HdrSwitch.exe selftest` — struct layouts ok, API path as expected
- [ ] `HDRSWITCH_FORCE_LEGACY=1 HdrSwitch.exe selftest` — fallback still works *(interop changes)*
- [ ] `HdrSwitch.exe brandcheck` — rendered the PNGs and looked at them *(UI changes)*
- [ ] Tried it against a real screen share *(detection changes)*

<!-- Paste the relevant output. -->

```
```

## Did you watch it fail?

<!--
A check that has never failed is not evidence. If this PR adds or relies on a test,
break the fix on purpose, confirm the test goes red, then put it back. Say what you did.
-->

## Anything reviewers should know

<!-- Trade-offs, things deliberately left out, follow-up work. -->
