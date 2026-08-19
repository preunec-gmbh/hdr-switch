# Security Policy

## Supported versions

| Version | Supported |
|---|---|
| 1.0.x | ✅ |
| < 1.0 | ❌ |

Only the latest release receives fixes.

## Reporting a vulnerability

**Do not open a public issue for a security problem.**

Use GitHub's [private vulnerability reporting](https://github.com/preunec-gmbh/hdr-switch/security/advisories/new),
or email **dev@preunec.com**.

Please include the `HdrSwitch.exe selftest` output, your Windows build, and the steps to
reproduce. You should get an acknowledgement within a few days.

## What this app touches

Worth knowing when judging whether something is a security issue:

| Surface | Access | Notes |
|---|---|---|
| Display configuration | Read + write | `DisplayConfigGetDeviceInfo` / `DisplayConfigSetDeviceInfo`. Per-user, no elevation. |
| `HKCU\…\CapabilityAccessManager\ConsentStore` | **Read only** | Only `LastUsedTimeStart` / `LastUsedTimeStop` on the two `graphicsCapture*` capabilities. |
| `HKCU\…\CurrentVersion\Run` | Read + write | Only when "Start with Windows" is enabled. |
| `HKCU\SOFTWARE\Microsoft\DirectX\UserGpuPreferences` | Read + write | Only the `AutoHDREnable` token; sibling tokens are preserved. |
| `HKCU\…\Themes\Personalize` | Read only | Light/dark theme. |
| Running process names | Read | Only when the opt-in process fallback or a game rule is enabled. |
| `%APPDATA%\HdrSwitch\settings.json` | Read + write | Preferences and learned per-app rules. |

Deliberate properties:

- **No network access whatsoever.** The app makes no outbound connections, has no telemetry, no
  update check, and no analytics. It ships with zero third-party NuGet dependencies.
- **Runs as `asInvoker`.** It never requests elevation, and nothing it does requires it.
- **It reads a privacy surface and keeps it minimal.** Screen-capture detection reads *that* an
  application is capturing and *which executable* it is — never what is on screen, never the
  capture content, and nothing beyond the two timestamps. Capture information is used only to
  render the local prompt and is not persisted beyond a per-app rule keyed on the executable name.

## Scope

In scope:

- Anything that lets HDR Switch write outside the registry keys and file listed above
- Privilege escalation, or a way to make the app run something it should not
- A crafted display or registry state that causes memory corruption through the interop layer
- Leaking capture information anywhere beyond the local UI

Out of scope:

- The absence of code signing on release binaries (known — see below)
- SmartScreen warnings on first run, which follow from the above
- Windows' own behaviour when HDR is toggled

## Code signing

Release binaries are **not** currently signed with an Authenticode certificate. Windows SmartScreen
will warn on first run. Verify downloads against the SHA-256 checksums published with each release,
and prefer building from source if that matters to you.
