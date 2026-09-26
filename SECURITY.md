# Security Policy

## Supported versions

| Version | Supported |
|---|---|
| 1.1.x | ✅ |
| < 1.1 | ❌ |

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
| The folder `HdrSwitch.exe` runs from | Write | Only while installing an update: `HdrSwitch.exe.download`, then `HdrSwitch.exe.old`, which the new version deletes. |
| `api.github.com`, `github.com` (HTTPS) | Outbound | Only when checking for or installing an update — see below. |

Deliberate properties:

- **No network access unless you ask for an update.** No telemetry, no analytics, and zero
  third-party NuGet dependencies. The only outbound requests are the update check — an
  unauthenticated GET of this repository's latest release from the public GitHub API — and, if
  you click *Update now*, the download of that release's `HdrSwitch.exe` and `HdrSwitch.exe.sha256`.
  Nothing about you or your machine is sent beyond what any HTTPS request carries. The check runs
  only when you choose *Check for updates* — there is no automatic, scheduled or startup check.
- **Updates are verified before they replace anything.** Downloads are only accepted from this
  repository's `releases/download/` URLs, and the file must match the release's published
  SHA-256 or it is discarded. Note what that proves: the checksum comes from the same release as
  the binary, so it catches a corrupted or truncated download, not a compromised release. The
  binary is not code-signed yet (see below).
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
