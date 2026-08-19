namespace HdrSwitch.Core.Hdr;

/// <summary>Resolves a --display selector against the enumerated displays. Pure; unit tested.</summary>
public static class DisplaySelection
{
    /// <summary>
    /// A null or empty selector means every HDR-capable display. Otherwise the selector is a
    /// 1-based index or a case-insensitive fragment of the display name.
    /// </summary>
    public static bool TryResolve(
        IReadOnlyList<DisplayTarget> displays,
        string? selector,
        out IReadOnlyList<DisplayTarget> matched,
        out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(selector))
        {
            matched = displays.Where(d => d.CanToggle).ToList();
            return true;
        }

        var trimmed = selector.Trim();

        if (int.TryParse(trimmed, out var index))
        {
            if (index < 1 || index > displays.Count)
            {
                matched = [];
                error = displays.Count == 0
                    ? "No displays were found."
                    : $"Display {index} does not exist. Valid range is 1..{displays.Count}.";
                return false;
            }

            matched = [displays[index - 1]];
            return true;
        }

        var byName = displays
            .Where(d => d.Label.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (byName.Count == 0)
        {
            matched = [];
            var available = displays.Count == 0
                ? "none were detected"
                : string.Join(", ", displays.Select((d, i) => $"{i + 1}={d.Label}"));
            error = $"No display matches '{trimmed}' ({available}).";
            return false;
        }

        matched = byName;
        return true;
    }
}
