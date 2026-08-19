# Support

## Start here

Run this and read the output — it answers most questions on its own:

```
HdrSwitch.exe selftest
```

It reports your Windows build, which display API resolved, the interop struct layout table, every
display with its raw capability flags, and anything currently capturing your screen.

## Common situations

**"HDR not supported" on a monitor I know does HDR.**
Check the flags in `selftest`. Bit 4 is `highDynamicRangeSupported`. A display can advertise wide
colour gamut without HDR — that is the difference between flags `0x45` and `0xD7`. Also check the
cable and the port: HDR needs the bandwidth, and a DisplayPort 1.2 link at high refresh may not
have it.

**"HDR blocked by system policy".**
Windows or the driver is refusing, usually a bandwidth-limited link. Lower the refresh rate or
resolution, or use a better cable, and re-run `selftest`.

**No prompt when I share my screen.**
The app only prompts when HDR is actually on — there is nothing to warn about otherwise. Then
check the rule for that app in **Settings → Screen sharing**; if it says "Never ask" or "Turn HDR
off automatically", that is a learned rule, and you can reset it there. Finally, confirm Windows
recorded the capture at all: `selftest` lists active captures.

**The hotkey does nothing.**
Another application has claimed it. The tray menu shows a warning when registration fails; pick a
different combination in **Settings → General**.

**`& HdrSwitch.exe status` prints nothing in PowerShell.**
Expected. It is a GUI-subsystem binary so the tray can start without a console flash, and
PowerShell does not wait for those. Use `Start-Process -Wait -PassThru` — see the README.

## Asking for help

Open a [question issue](https://github.com/preunec-gmbh/hdr-switch/issues/new/choose) and include
your `selftest` output. Without it, almost every answer is a guess.

For a bug, use the bug report template. For a security problem, do **not** open an issue — see
[SECURITY.md](SECURITY.md).

## Response expectations

This is a small tool maintained alongside other work. Issues are read, but there is no support SLA
and no guaranteed turnaround.
