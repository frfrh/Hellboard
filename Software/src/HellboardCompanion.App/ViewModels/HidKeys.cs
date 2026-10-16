// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.App.ViewModels;

/// <summary>Keys that can be chosen as the in-game stratagem key, by HID usage id (https://deskthority.net/wiki/Scancode).</summary>
public static class HidKeys
{
    public static IReadOnlyList<Choice<int>> All { get; } = Create();

    public static Choice<int> Find(int usageId) =>
        All.FirstOrDefault(k => k.Value == usageId) ?? new Choice<int>(usageId, $"Key code {usageId}");

    private static List<Choice<int>> Create()
    {
        var keys = new List<Choice<int>>
        {
            new(224, "Left Ctrl"),
            new(225, "Left Shift"),
            new(226, "Left Alt"),
            new(228, "Right Ctrl"),
            new(229, "Right Shift"),
            new(230, "Right Alt"),
            new(57, "Caps Lock"),
            new(43, "Tab"),
            new(44, "Space"),
        };
        for (var i = 0; i < 26; i++)
        {
            keys.Add(new Choice<int>(4 + i, ((char)('A' + i)).ToString()));
        }

        for (var i = 1; i <= 9; i++)
        {
            keys.Add(new Choice<int>(29 + i, i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        keys.Add(new Choice<int>(39, "0"));
        for (var i = 1; i <= 12; i++)
        {
            keys.Add(new Choice<int>(57 + i, $"F{i}"));
        }

        return keys;
    }
}
