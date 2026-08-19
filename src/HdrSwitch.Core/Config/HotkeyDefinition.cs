namespace HdrSwitch.Core.Config;

/// <summary>A parsed global hotkey, ready for RegisterHotKey.</summary>
public sealed record HotkeyDefinition
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    /// <summary>Without this, holding the combination fires repeatedly.</summary>
    public const uint MOD_NOREPEAT = 0x4000;

    public required uint Modifiers { get; init; }

    public required uint VirtualKey { get; init; }

    public required string Text { get; init; }

    public uint ModifiersForRegistration => Modifiers | MOD_NOREPEAT;
}

/// <summary>Parses hotkey strings such as "Ctrl+Alt+H" or "Win+Shift+F9". Pure; unit tested.</summary>
public static class HotkeyParser
{
    public const string Default = "Ctrl+Alt+H";

    public static bool TryParse(string? input, out HotkeyDefinition? hotkey, out string? error)
    {
        hotkey = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "No hotkey specified.";
            return false;
        }

        uint modifiers = 0;
        uint virtualKey = 0;
        var keyToken = string.Empty;

        var parts = input.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control":
                    modifiers |= HotkeyDefinition.MOD_CONTROL;
                    continue;
                case "alt":
                    modifiers |= HotkeyDefinition.MOD_ALT;
                    continue;
                case "shift":
                    modifiers |= HotkeyDefinition.MOD_SHIFT;
                    continue;
                case "win" or "windows" or "meta":
                    modifiers |= HotkeyDefinition.MOD_WIN;
                    continue;
            }

            if (keyToken.Length > 0)
            {
                error = $"'{input}' names more than one key. Use modifiers plus a single key.";
                return false;
            }

            if (!TryParseKey(part, out virtualKey))
            {
                error = $"'{part}' is not a key HDR Switch recognises.";
                return false;
            }

            keyToken = part;
        }

        if (keyToken.Length == 0)
        {
            error = $"'{input}' has no main key -- add one, e.g. Ctrl+Alt+H.";
            return false;
        }

        if (modifiers == 0)
        {
            // A bare key would swallow that key system-wide. Refuse rather than break typing.
            error = "A global hotkey needs at least one modifier (Ctrl, Alt, Shift or Win).";
            return false;
        }

        hotkey = new HotkeyDefinition
        {
            Modifiers = modifiers,
            VirtualKey = virtualKey,
            Text = Format(modifiers, keyToken),
        };
        return true;
    }

    private static bool TryParseKey(string token, out uint virtualKey)
    {
        virtualKey = 0;

        if (token.Length == 1)
        {
            var c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z')
            {
                virtualKey = c;
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                virtualKey = c;
                return true;
            }

            return false;
        }

        // Function keys: F1 (0x70) through F24.
        if ((token[0] is 'F' or 'f') && int.TryParse(token.AsSpan(1), out var fn) && fn is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + fn - 1);
            return true;
        }

        virtualKey = token.ToLowerInvariant() switch
        {
            "space" => 0x20,
            "enter" or "return" => 0x0D,
            "tab" => 0x09,
            "escape" or "esc" => 0x1B,
            "home" => 0x24,
            "end" => 0x23,
            "pageup" => 0x21,
            "pagedown" => 0x22,
            "insert" => 0x2D,
            "delete" or "del" => 0x2E,
            "left" => 0x25,
            "up" => 0x26,
            "right" => 0x27,
            "down" => 0x28,
            "pause" => 0x13,
            "print" or "printscreen" => 0x2C,
            _ => 0,
        };

        return virtualKey != 0;
    }

    private static string Format(uint modifiers, string keyToken)
    {
        var parts = new List<string>(4);
        if ((modifiers & HotkeyDefinition.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & HotkeyDefinition.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & HotkeyDefinition.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & HotkeyDefinition.MOD_WIN) != 0) parts.Add("Win");

        parts.Add(keyToken.Length == 1 ? keyToken.ToUpperInvariant() : Capitalize(keyToken));
        return string.Join('+', parts);
    }

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}
