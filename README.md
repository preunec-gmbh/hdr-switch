# HDR Switch

A Windows tray app for turning HDR on and off without digging through Settings — plus a thing
Windows itself does not do: **it notices when you start sharing your screen and offers to turn
HDR off first.**

Captured HDR reaches viewers washed out and desaturated, because the capture pipeline flattens
scRGB/FP16 to SDR badly. You normally only find out when someone on the call mentions it. HDR
Switch spots the capture starting and asks — then learns your answer so it stops asking.

Single self-contained `HdrSwitch.exe`. No installer, no .NET runtime needed, no admin rights,
no NuGet dependencies.

---

## What it does

| | |
|---|---|
| **Tray icon** | Left-click flips HDR on every capable display. Right-click for per-display control. The icon reflects the real state, even when you change HDR from Windows Settings or `Win+Alt+B`. |
| **Screen-share awareness** | When an app starts capturing while HDR is on, you get a prompt: *Turn HDR off* / *Keep HDR* / *Never ask for this app*. Answer the same way twice and it starts doing it for you — with an Undo that also unlearns. |
| **Global hotkey** | `Ctrl+Alt+H` by default, configurable. |
| **Command line** | `HdrSwitch.exe toggle` and friends, for shortcuts, Stream Deck, AutoHotkey, or scripts. |
| **Start with Windows** | Per-user Run key, no elevation. |
| **Game rules** | Turn HDR on while a chosen game runs, and back off when it exits. |
| **Auto HDR** | Toggles the DirectX Auto HDR setting from the tray menu. |

## Command line

```
HdrSwitch.exe                     run the tray app
HdrSwitch.exe on   [options]      turn HDR on
HdrSwitch.exe off  [options]      turn HDR off
HdrSwitch.exe toggle [options]    flip HDR
HdrSwitch.exe status [options]    current state
HdrSwitch.exe list [options]      displays and their HDR capability
HdrSwitch.exe selftest            diagnose the display API and struct layouts
HdrSwitch.exe brandcheck          render the brand assets and every UI surface to PNGs

  --display <n|name>   one display: 1-based index, or part of its name
  --out <file>         brandcheck only: where to write the previews
  --json               machine-readable output
  --quiet              no output, rely on the exit code
```

Exit codes: `0` success, `1` error, `2` no HDR-capable display.

```powershell
# bind this to a shortcut or a Stream Deck button
HdrSwitch.exe toggle --quiet

# only the external monitor
HdrSwitch.exe on --display Samsung

# for scripts
HdrSwitch.exe status --json
```

> **PowerShell note.** `HdrSwitch.exe` is a GUI-subsystem binary, so the tray app can start
> without flashing a console window. The side effect is that PowerShell does not wait for it when
> you call it inline — `& HdrSwitch.exe status` returns immediately with no output. Exit codes and
> redirected output work normally; just use `Start-Process -Wait`:
>
> ```powershell
> $p = Start-Process HdrSwitch.exe -ArgumentList 'toggle' -Wait -PassThru -NoNewWindow
> $p.ExitCode
> ```
>
> cmd.exe, bash, and Task Scheduler are unaffected.

## How screen-share detection works

Windows already tracks screen capture, in the same place it tracks camera and microphone access
for the privacy indicators:

```
HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\
    graphicsCaptureProgrammatic\[NonPackaged\]<app>
    graphicsCaptureWithoutBorder\[NonPackaged\]<app>
```

Each app subkey has `LastUsedTimeStart` and `LastUsedTimeStop` as `REG_QWORD` FILETIMEs. While a
capture is in progress, `Stop` is zero. HDR Switch subscribes with `RegNotifyChangeKeyValue`, so
it reacts in a few hundred milliseconds — no polling of graphics APIs, no injection, no driver,
no elevation.

Every modern capture app registers there: Discord, Chrome, Edge, OBS, Teams, Zoom, Slack.

**Rules are keyed on the executable file name, not its full path.** Discord reinstalls into a
version-stamped folder (`…\app-1.0.9254\Discord.exe`) on every update, so a path-based key would
silently forget what you taught it after each patch.

The **Advanced** tab has an opt-in process-name fallback for older tools that use legacy DXGI
Desktop Duplication and never register. It only knows whether an executable is *running*, not
whether it is *sharing*, so it is off by default and will produce false alarms if you enable it.

## Displays and the two Windows APIs

There are two generations of the CCD display API and HDR Switch prefers the newer one:

| | Read | Write | Available |
|---|---|---|---|
| **Modern** | `GET_ADVANCED_COLOR_INFO_2` (15) | `SET_HDR_STATE` (16) | Windows 11 24H2+ |
| **Legacy** | `GET_ADVANCED_COLOR_INFO` (9) | `SET_ADVANCED_COLOR_STATE` (10) | Windows 10 1803+ |

This matters more than it looks. The legacy API has a single "advanced colour" flag that
**conflates HDR with wide colour gamut**. On a machine with a WCG-but-not-HDR laptop panel and an
HDR monitor with WCG enabled, the legacy path reports:

```
[1] TL156VDXP0101   HDR off      <- wrong: the panel cannot do HDR at all
[2] LS27AG55x       HDR on       <- wrong: that is WCG being on, HDR is off
```

while the modern path correctly reports `HDR not supported` and `HDR off`. The legacy path is
therefore a genuine fallback for older Windows, not an equivalent. Set
`HDRSWITCH_FORCE_LEGACY=1` to exercise it deliberately.

`selftest` prints which path resolved and checks every interop struct's marshalled size against
the sizes the SDK header implies — a layout mistake there would produce plausible-looking garbage
rather than an error.

### Why success is judged by reading the state back

`SET_HDR_STATE` **returns `ERROR_ACCESS_DENIED` (5) when enabling HDR on this hardware, and
applies the change anyway** — while returning `0` when disabling. An early build trusted the
return code and reported failures for operations that had plainly worked, exiting non-zero from a
command that did the right thing.

So the return code is treated as diagnostic information only (it is still reported in `--json`
and `selftest`), and success is decided by re-reading the display afterwards. The read is polled
rather than taken once, because changing HDR renegotiates the display link. The same mechanism
catches the opposite failure: a display on a bandwidth-limited link that accepts the call and
refuses the change.


## Branding

HDR Switch uses the house design system from
[`design-system-kit/`](../design-system-kit), which is the canonical source for the estate. The
assets are **vendored, not referenced** — the same pattern eir and ASSA use — and re-synced by
copying. Provenance is recorded in `src/HdrSwitch/Brand/VENDORED.md`.

| What | Vendored from |
|---|---|
| Colour roles in `Ui/Brand.cs` | `package/tokens/preunec.css` (`:root` and `[data-theme="dark"]`) |
| `Brand/preunec-wordmark-mono.svg` | `brand/wordmark/preunec-wordmark-mono.svg` |
| `Brand/wordmark-metrics.json` | `brand/wordmark/metrics.json` |

The kit ships CSS custom properties, which WinForms cannot consume, so the **semantic roles** are
mirrored in `Ui/Brand.cs` — `TextPrimary`, `AccentInteractive`, `SurfaceRaised` — rather than raw
hex values scattered through the UI. Both polarities are implemented and follow the Windows app
theme, per the kit's "do not assume a surface's polarity".

**The wordmark is never re-typeset.** Sabon Bold is commercially licensed and absent from both
repositories, so `Ui/Wordmark.cs` parses the outlined Bezier paths out of the supplied SVG and
renders them through a `GraphicsPath`. It enforces the guidelines mechanically: one flat colour
(navy on light, white on dark), proportional scaling only, 1× cap-height clear space, and it
**refuses to draw below the 80 px minimum** rather than emitting an illegible mark.

Two brand rules that shaped the UI:

- **Cyan is a fill, not a text colour** (3.33:1 on white). It is the tray-icon ring, the primary
  button fill and the active-tab indicator — never a label. Where a cyan-ish *text* colour was
  needed, `accent.ink` `#0B6FA8` is used instead.
- **Gradients are backgrounds.** The toast's accent stripe uses the sanctioned `brand`
  gradient on light surfaces and `brand-soft` on dark ones, because `brand` starts at navy and
  a navy stripe on a navy toast looks like a half-painted bug.

### Reviewing the brand after a re-sync

```bash
HdrSwitch.exe brandcheck --out preview.png
```

Writes the wordmark in both pairings, the minimum-size guard, the tray icons at 32 px and 16 px,
the palette and gradient — plus every Settings tab and both toast variants, rendered in-process.
Fonts resolve through a stack (`Inter → Segoe UI Variable Text → Segoe UI`), since the kit's web
faces are not installed on a stock Windows machine; `brandcheck` prints which ones actually
resolved instead of leaving it to guesswork.

## Building

Requires the .NET 9 SDK.

```bash
dotnet build   -c Release
dotnet test
dotnet publish src/HdrSwitch/HdrSwitch.csproj -c Release -r win-x64 \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true --self-contained true -o publish
```

Output is a single ~48 MB `publish/HdrSwitch.exe`. WinForms does not support `PublishTrimmed`, so
size comes from compression instead.

### Layout

```
src/HdrSwitch.Core/     all logic, no UI — this is what the tests cover
  Interop/              CCD + registry P/Invoke, transcribed from the SDK header
  Hdr/                  display enumeration, HDR read/write, Auto HDR
  Sharing/              consent-store reader and the change watcher
  Rules/                the suggest-then-learn engine, game watcher
  Config/               settings, hotkey parsing, startup registration
  Cli/                  argument parsing and output DTOs
src/HdrSwitch/          WinForms tray app, toast, settings window
  Brand/                vendored wordmark + metrics (see VENDORED.md)
  Ui/Brand.cs           design tokens mirrored from design-system-kit
  Ui/Wordmark.cs        renders the outlined wordmark SVG
tests/HdrSwitch.Tests/  96 tests over the pure logic
```

Settings live in `%APPDATA%\HdrSwitch\settings.json`. An unreadable file is renamed to
`.corrupt` and defaults are used, rather than refusing to start.

## Deliberate non-goals

- **SDR content brightness slider.** The value is readable (`GET_SDR_WHITE_LEVEL`, shown in
  `list`) but there is no documented setter — only the undocumented `SET_RESERVED1`. Not worth
  the risk.
- **Separate WCG control.** Reported, not toggled.
- **Knowing *what* is shared** (a window vs the whole display). The consent store does not
  expose it.

## Known limitations

- HDR cannot be forced on a display that reports `BlockedByPolicy` — usually a bandwidth-limited
  link. The app says so rather than pretending the toggle worked; it always reads the state back
  after writing, because the API can return success without the change sticking.
- Auto HDR changes do not affect already-running games; they need a restart.
- The process-name fallback cannot distinguish "open" from "sharing" (see above).
