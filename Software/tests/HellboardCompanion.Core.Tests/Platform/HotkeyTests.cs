// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Tests.Platform;

public sealed class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Shift+F9", HotkeyModifiers.Control | HotkeyModifiers.Shift, "F9")]
    [InlineData("strg + shift + f9", HotkeyModifiers.Control | HotkeyModifiers.Shift, "F9")]
    [InlineData("Alt+F12", HotkeyModifiers.Alt, "F12")]
    [InlineData("Win+Ctrl+h", HotkeyModifiers.Windows | HotkeyModifiers.Control, "H")]
    [InlineData("F24", HotkeyModifiers.None, "F24")]
    public void Parses_valid_hotkeys(string text, HotkeyModifiers modifiers, string key)
    {
        Hotkey.Parse(text).Should().Be(new Hotkey(modifiers, key));
    }

    [Theory]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+Enter")]
    [InlineData("Ctrl+A+B")]
    public void Rejects_invalid_hotkeys(string text)
    {
        var parse = () => Hotkey.Parse(text);

        parse.Should().Throw<FormatException>();
    }

    [Fact]
    public void Formats_in_a_canonical_order()
    {
        Hotkey.Parse("shift+F9+ctrl").ToString().Should().Be("Ctrl+Shift+F9");
    }
}
