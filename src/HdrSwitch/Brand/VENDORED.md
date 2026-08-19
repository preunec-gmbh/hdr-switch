# Vendored brand assets

Copied verbatim from `design-system-kit/` (the canonical source for the estate).
Re-sync by copying again — never edit these in place.

| File | Source in design-system-kit |
|---|---|
| `preunec-wordmark-mono.svg` | `brand/wordmark/preunec-wordmark-mono.svg` |
| `wordmark-metrics.json` | `brand/wordmark/metrics.json` |
| colour values in `../Ui/Brand.cs` | `package/tokens/preunec.css` (`:root` and `[data-theme="dark"]`) |

The `mono` wordmark variant is used because it is `fill="currentColor"` — the
variant intended for embedding where the host decides the colour. HDR Switch
paints it navy on light surfaces and white on dark ones, always one flat colour.

The wordmark is outlined Bezier paths, not font software. Sabon Bold is
commercially licensed and is deliberately absent from both repositories.

Synced: 2026-08-19 from design-system-kit @ 61f298c
