# Contributing

## The one rule that matters

**Anything that talks to Windows gets verified against Windows, not against the documentation.**

Three of the four real bugs found while building this were invisible on paper:

- `SET_HDR_STATE` returns `ERROR_ACCESS_DENIED` while succeeding.
- The legacy CCD API reports HDR as on when only wide-colour gamut is on.
- `SET_HDR_STATE` is device-info type **16**, not 17 — an off-by-one against the SDK header
  would have produced plausible garbage rather than an error.

So: if you change interop, run `HdrSwitch.exe selftest` and read the output. If you change the
UI, run `HdrSwitch.exe brandcheck` and *look at the PNGs*. A green build proves nothing about
either.

```bash
# the loop
dotnet build HdrSwitch.sln -c Release
dotnet test
./publish/HdrSwitch.exe selftest
```

---

## Project shape

```
src/HdrSwitch.Core/     all logic, no UI — this is what the tests cover
src/HdrSwitch/          WinForms tray app, toast, settings window
tests/HdrSwitch.Tests/  xunit, pure logic only
```

The split is deliberate. Interop and WinForms are not unit-testable, so everything that *can* be
tested lives in `Core` and the app project stays thin. If you find yourself wanting to test
something in `src/HdrSwitch/`, that is usually a sign the logic belongs in `Core`.

---

## Touching the display interop

`src/HdrSwitch.Core/Interop/DisplayConfigNative.cs` was transcribed from the installed Windows SDK
header:

```
C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\um\wingdi.h
```

Every constant carries the line number it came from. **Transcribe from the header, not from
memory or from a blog post** — the device-info type enum is dense and shifts as Windows adds
entries.

`HdrController.LayoutCheck()` asserts every struct's marshalled size against what the header
implies, and `selftest` prints the table. Add a row when you add a struct. A wrong layout does not
throw; it silently reads the wrong bytes.

### The two API generations

| | Read | Write | Available |
|---|---|---|---|
| Modern | `GET_ADVANCED_COLOR_INFO_2` (15) | `SET_HDR_STATE` (16) | Windows 11 24H2+ |
| Legacy | `GET_ADVANCED_COLOR_INFO` (9) | `SET_ADVANCED_COLOR_STATE` (10) | Windows 10 1803+ |

Read path and write path are tracked separately (`_apiPath` vs `_preferLegacyWrites`) because the
modern *reader* is strictly better and must not be downgraded just because a write fell back.

Exercise the fallback before claiming it works:

```bash
HDRSWITCH_FORCE_LEGACY=1 ./publish/HdrSwitch.exe selftest
```

### Never trust the return code

Success is decided by reading the display state back, never by the Win32 result. If you add an
operation, follow the same pattern: apply, poll until the state matches or the budget expires,
report based on what you observed.

---

## Touching screen-share detection

Detection reads the `CapabilityAccessManager` consent store. Two rules:

1. **Rules key on the executable file name, never the full path.** Discord reinstalls into a
   version-stamped directory on every update; a path key silently forgets the user's preference
   after each patch. There is a test for this — keep it passing.
2. **Never log or display capture *content*, and never widen what is read** beyond the
   `LastUsedTimeStart` / `LastUsedTimeStop` pair. This feature reads a privacy surface; it should
   stay boring.

`ConsentStoreReader` takes an `IRegistryProbe` so it can be tested without HKCU. Use the fake in
`ConsentStoreReaderTests` rather than writing to the real consent store.

---

## Touching the UI

Colours come from `Ui/Brand.cs`, which mirrors the semantic roles from
[`design-system-kit`](https://github.com/preunec-gmbh/design-system-kit). **Ask for a role, never
a hex value** — `Brand.TextPrimary`, not `#F5F6FA`. Both light and dark are live and follow the
Windows app theme.

Two brand rules the code depends on:

- **Cyan is a fill, not a text colour** (3.33:1 on white). It is the icon ring, the primary button
  fill, the active-tab indicator. If you need a cyan-ish label, use `Brand.AccentInk`.
- **The wordmark is never re-typeset.** `Ui/Wordmark.cs` renders the outlined SVG. Do not set
  "preunec" in a font, do not recolour it two-tone, do not draw it below 80 px — the code already
  refuses, keep it that way.

After any brand change, re-render and look:

```bash
./publish/HdrSwitch.exe brandcheck --out preview/brand.png
```

That writes the wordmark in both pairings, the minimum-size guard, the tray icons at 32 px and
16 px, every Settings tab, and both toast variants. It exists because a hand-written SVG path
parser and a WinForms layout are exactly the things no unit test catches — the settings layout
once pushed the rule-editing buttons off the bottom of the page, and only a screenshot showed it.

### Re-syncing brand assets

Copy them again from `design-system-kit`; never edit the vendored files in place, or the next sync
silently reverts your change. Update `src/HdrSwitch/Brand/VENDORED.md` with the new commit.

---

## Before you push

```bash
dotnet build HdrSwitch.sln -c Release   # zero warnings
dotnet test                              # all green
./publish/HdrSwitch.exe selftest         # layouts ok, API path sane
```

Then ask the question the test suite cannot: **have I watched this fail?** A check that has never
failed is not evidence. When adding a test for a bug, break the fix deliberately and confirm the
test goes red before you commit.

---

## Reporting a defect

Open an issue with the output of:

```bash
HdrSwitch.exe selftest
```

That one command carries the Windows build, which API path resolved, the struct layout table, all
displays with their raw capability flags, and anything currently capturing the screen — which is
most of what any HDR or detection bug turns on.
