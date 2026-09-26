# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] — 2026-09-26

### Added

- **The tray shows who is sharing your screen.** While a share is on, the tray icon carries a
  red dot, the tooltip names the app, and the menu says what HDR Switch did — with *Restore HDR
  now* when it switched HDR off, or *Turn HDR off now…* when it did not.
- **Update prompts say what is new.** The "new version available" prompt lists the release's
  highlights. Release notes now come from this changelog, and the release workflow refuses to
  publish a version that has no section here.
- **Turkish and German interface.** HDR Switch follows the Windows display language, or a
  language picked in Settings. The command line stays English, because scripts read its output.

### Fixed

- The last line of a longer notification could be clipped; toasts now size their text the way
  it is actually drawn.

## [1.1.0] — 2026-09-26

### Added

- **Check for updates, and install in one click.** *Check for updates…* in the tray menu and in
  Settings. When a release is newer, *Update now* downloads it, verifies it against the
  published SHA-256, swaps `HdrSwitch.exe` in place (same path, so Start with Windows and
  shortcuts keep working) and restarts into the new version, which confirms the update. A
  failed download or a checksum mismatch changes nothing. It only checks when you click —
  there is no automatic or background check, so HDR Switch still makes no network connection
  on its own.
- **Only the shared screen loses HDR.** With more than one HDR display on, the prompt asks which
  screen is being shared (one button per screen, plus *All screens*), and the rule remembers the
  pick, so a learned rule switches off only that screen.

### Fixed

- **HDR switched off while the screen picker was still open.** Chromium browsers (Google Meet in
  Chrome or Edge) record a capture as soon as the picker opens, to draw its live thumbnails.
  HDR Switch took that as a share, then restored HDR when Share was pressed and switched it off
  again when the real capture started. Captures are now confirmed before anything happens:
  browsers by the picker → real-capture hand-off, other apps after 1.5 s. A cancelled picker
  or a tab share no longer prompts at all, and switching the shared source no longer flips HDR.

## [1.0.1] — 2026-08-19

### Added

- Product icon on the executable, taskbar and window. Built as a multi-resolution `.ico`
  (16-256) from the white mark on a brand-navy tile, so it stays legible against both a light
  and a dark taskbar. Small sizes use tighter padding, because at 16 px the tile margin costs
  more than it gives.
- Theme-aware logo and status badges in the README, plus a download section that explains the
  SmartScreen warning and how to verify the published SHA-256.

### Notes

- The tray icon deliberately still shows HDR state rather than the product mark: three letters
  do not survive being scaled to 16x16, and the tray's job is to say whether HDR is on.
- The binary is still unsigned, so Windows SmartScreen will warn on first run. See SECURITY.md.

## [1.0.0] — 2026-08-19

First release.

### Added

- **Per-display HDR control** from the tray: left-click flips every capable display, right-click
  for individual ones. The icon reflects the real state even when HDR is changed from Windows
  Settings or `Win+Alt+B`.
- **Screen-share awareness.** When an application starts capturing while HDR is on, a prompt
  offers to turn HDR off, because captured HDR reaches viewers washed out and desaturated.
  Detection reads the Windows `CapabilityAccessManager` consent store through
  `RegNotifyChangeKeyValue` — no polling, no injection, no elevation.
- **Suggest, then learn.** Two consistent answers for the same application promote it to an
  automatic rule, with an Undo that reverts the change *and* unlearns it. Rules are editable in
  Settings.
- **Global hotkey**, `Ctrl+Alt+H` by default, configurable.
- **Command line** — `on`, `off`, `toggle`, `status`, `list`, `selftest`, `brandcheck` — with
  `--display`, `--json` and `--quiet`, for shortcuts, Stream Deck, AutoHotkey and scripts.
  Exit codes: `0` success, `1` error, `2` no HDR-capable display.
- **Start with Windows** via the per-user Run key.
- **Game rules**: turn HDR on while a chosen game runs, and back off when it exits.
- **Auto HDR** toggle, rewriting only the `AutoHDREnable` token and preserving its siblings.
- **`selftest`** — reports the resolved display API, checks every interop struct's marshalled size
  against the Windows SDK header, and lists displays with raw capability flags.
- **`brandcheck`** — renders the wordmark, palette, tray icons, every Settings tab and both toast
  variants to PNG for review.

### Notes on behaviour

- **Success is judged by reading the display state back, not by the Win32 return code.**
  `SET_HDR_STATE` was observed returning `ERROR_ACCESS_DENIED` while applying the change
  correctly; trusting the return code reported failures for operations that had worked.
- **The modern display API is preferred and the legacy one is a genuine fallback, not an
  equivalent.** The legacy API conflates HDR with wide colour gamut and will report a
  WCG-only panel as HDR-capable. Set `HDRSWITCH_FORCE_LEGACY=1` to exercise the fallback.
- **Rules key on the executable file name, not its full path**, so they survive application
  updates that change the install directory.
- The process-name fallback for legacy capture tools is **off by default**: it cannot distinguish
  an application being open from one actually sharing.

[Unreleased]: https://github.com/preunec-gmbh/hdr-switch/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/preunec-gmbh/hdr-switch/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/preunec-gmbh/hdr-switch/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/preunec-gmbh/hdr-switch/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/preunec-gmbh/hdr-switch/releases/tag/v1.0.0
