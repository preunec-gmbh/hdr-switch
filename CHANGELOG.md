# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/preunec-gmbh/hdr-switch/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/preunec-gmbh/hdr-switch/releases/tag/v1.0.0
