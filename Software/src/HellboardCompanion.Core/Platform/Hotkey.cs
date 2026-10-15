// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Platform;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

/// <summary>A global key combination, e.g. <c>Ctrl+Shift+F9</c>. <see cref="Key"/> is F1–F24, A–Z or 0–9.</summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, string Key)
{
    public static Hotkey Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var modifiers = HotkeyModifiers.None;
        string? key = null;
        foreach (var part in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL" or "STRG":
                    modifiers |= HotkeyModifiers.Control;
                    break;
                case "SHIFT":
                    modifiers |= HotkeyModifiers.Shift;
                    break;
                case "ALT":
                    modifiers |= HotkeyModifiers.Alt;
                    break;
                case "WIN" or "WINDOWS":
                    modifiers |= HotkeyModifiers.Windows;
                    break;
                default:
                    if (key is not null || !IsSupportedKey(part.ToUpperInvariant()))
                    {
                        throw new FormatException($"'{text}' is not a valid hotkey (expected e.g. 'Ctrl+Shift+F9').");
                    }

                    key = part.ToUpperInvariant();
                    break;
            }
        }

        return key is null
            ? throw new FormatException($"'{text}' contains no key.")
            : new Hotkey(modifiers, key);
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(Key);
        return string.Join('+', parts);
    }

    private static bool IsSupportedKey(string key) =>
        (key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]))
        || (key.Length is 2 or 3 && key[0] == 'F' && int.TryParse(key[1..], out var number) && number is >= 1 and <= 24);
}

/// <summary>Registers system-wide hotkeys (no keyboard hook, no input injection).</summary>
public interface IGlobalHotkeys : IDisposable
{
    /// <summary>Registers <paramref name="hotkey"/>; <paramref name="pressed"/> runs on a background thread.</summary>
    /// <returns>Disposing the result unregisters the hotkey.</returns>
    /// <exception cref="InvalidOperationException">The combination is already used by another application.</exception>
    IDisposable Register(Hotkey hotkey, Action pressed);
}
