// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json.Nodes;

namespace HellboardCompanion.Core.Hellboard;

public enum DirectionKeys
{
    Arrows,
    Wasd,
}

/// <summary>Names longer than a display line (decision F16).</summary>
public enum ScrollText
{
    /// <summary>Static, cut after 16 characters.</summary>
    Off,

    /// <summary>Only names longer than a line scroll, back and forth.</summary>
    Long,

    /// <summary>Only names longer than a line scroll, continuously like a ticker (gap, then the start again).</summary>
    Loop,
}

/// <summary>Feedback on the board for a received loadout (decision F17).</summary>
public enum TransferFeedback
{
    /// <summary>Hellpod animation on the display, flying in from the right (only the blue part of the display).</summary>
    HellpodHorizontal,

    /// <summary>Hellpod animation on the display, falling from the top.</summary>
    HellpodVertical,

    /// <summary>The LEDs flash twice in their colors (decision F4).</summary>
    Flash,
}

public enum BoardLogLevel
{
    Debug,
    Info,
    Warn,
    Error,
}

/// <summary>
/// Configuration of the Hellboard itself (decision F13), edited on the Hellboard tab and stored in the
/// Companion settings. Sent to the board with <c>setConfig</c> (RAM) after every connect and change;
/// "Save configuration on the Hellboard" makes it permanent (<c>saveConfig</c>).
/// </summary>
public sealed class BoardOptions
{
    /// <summary>
    /// <c>false</c> until the configuration of a connected board was taken over once, so an existing
    /// setup on the board is not overwritten by the defaults of a fresh Companion installation.
    /// </summary>
    public bool Adopted { get; set; }

    /// <summary>Stratagem id of button 1-1; <c>null</c> = button off.</summary>
    public string? FixedButton1 { get; set; } = "reinforce";

    /// <summary>Stratagem id of button 1-2; <c>null</c> = button off.</summary>
    public string? FixedButton2 { get; set; } = "resupply";

    /// <summary>HID usage id of the in-game stratagem key (e.g. 224 = left Ctrl, 6 = C).</summary>
    public int ModifierKey { get; set; } = 224;

    /// <summary><c>true</c>: the key is pressed once to open the menu; <c>false</c>: held while the code is entered.</summary>
    public bool ModifierToggle { get; set; }

    /// <summary>After pressing the stratagem key, until the stratagem menu is open (F15).</summary>
    public int MenuDelayMs { get; set; } = 50;

    /// <summary>How long an arrow key is held (F15).</summary>
    public int KeyPressMs { get; set; } = 50;

    /// <summary>Pause between two arrows (F15).</summary>
    public int KeyReleaseMs { get; set; } = 50;

    /// <summary>Display off after this time without a button press or transfer; 0 = off (F15).</summary>
    public int ScreenSaverSeconds { get; set; }

    public ScrollText ScrollText { get; set; } = ScrollText.Long;

    /// <summary>Needs firmware 2.2; older firmware ignores it and keeps flashing the LEDs.</summary>
    public TransferFeedback TransferFeedback { get; set; } = TransferFeedback.HellpodHorizontal;

    public DirectionKeys DirectionKeys { get; set; } = DirectionKeys.Arrows;

    public int Brightness { get; set; } = 80;

    public BoardLogLevel LogLevel { get; set; } = BoardLogLevel.Info;
}

/// <summary>Conversion between <see cref="BoardOptions"/> and the <c>config</c> object of the protocol (§4.4).</summary>
public static class BoardConfiguration
{
    public const int MinKeyDelayMs = 20;
    public const int MaxKeyDelayMs = 200;
    public const int MaxMenuDelayMs = 1000;
    public const int MaxScreenSaverSeconds = 3600;

    public static JsonObject ToProtocol(BoardOptions options, StratagemCatalog catalog) => new()
    {
        ["fixed"] = new JsonObject
        {
            ["1_1"] = Definition(options.FixedButton1, catalog),
            ["1_2"] = Definition(options.FixedButton2, catalog),
        },
        ["modifierKey"] = Math.Clamp(options.ModifierKey, 4, 231),
        ["modifierToggle"] = options.ModifierToggle,
        ["menuDelayMs"] = Math.Clamp(options.MenuDelayMs, 0, MaxMenuDelayMs),
        ["keyPressMs"] = Math.Clamp(options.KeyPressMs, MinKeyDelayMs, MaxKeyDelayMs),
        ["keyReleaseMs"] = Math.Clamp(options.KeyReleaseMs, MinKeyDelayMs, MaxKeyDelayMs),
        ["keyDelayMs"] = Math.Clamp(options.KeyPressMs, MinKeyDelayMs, MaxKeyDelayMs),   // firmware 2.0 knows only this one
        ["screenSaverSeconds"] = Math.Clamp(options.ScreenSaverSeconds, 0, MaxScreenSaverSeconds),
        ["scrollText"] = options.ScrollText switch { ScrollText.Long => "long", ScrollText.Loop => "loop", _ => "off" },
        ["transferFeedback"] = options.TransferFeedback switch   // firmware before 2.2 ignores it
        {
            TransferFeedback.HellpodVertical => "hellpodVertical",
            TransferFeedback.Flash => "flash",
            _ => "hellpodHorizontal",
        },
        ["directionKeys"] = options.DirectionKeys == DirectionKeys.Wasd ? "wasd" : "arrows",
        ["brightness"] = Math.Clamp(options.Brightness, 0, 255),
        ["logLevel"] = options.LogLevel.ToString().ToLowerInvariant(),
    };

    /// <summary>Takes over the configuration a board reports (<c>getState</c>); unknown values keep the current setting.</summary>
    public static void Adopt(JsonObject config, BoardOptions options, StratagemCatalog catalog)
    {
        if (config["fixed"] is JsonObject fixedButtons)
        {
            options.FixedButton1 = FindId(fixedButtons["1_1"], catalog);
            options.FixedButton2 = FindId(fixedButtons["1_2"], catalog);
        }

        if (config["modifierKey"] is JsonValue key && key.TryGetValue<int>(out var modifier))
        {
            options.ModifierKey = modifier;
        }

        if (config["modifierToggle"] is JsonValue toggle && toggle.TryGetValue<bool>(out var isToggle))
        {
            options.ModifierToggle = isToggle;
        }

        if (config["keyDelayMs"] is JsonValue delay && delay.TryGetValue<int>(out var delayMs))
        {
            options.KeyPressMs = options.KeyReleaseMs = delayMs;   // firmware 2.0
        }

        if (Int(config, "menuDelayMs") is { } menuDelay)
        {
            options.MenuDelayMs = menuDelay;
        }

        if (Int(config, "keyPressMs") is { } press)
        {
            options.KeyPressMs = press;
        }

        if (Int(config, "keyReleaseMs") is { } release)
        {
            options.KeyReleaseMs = release;
        }

        if (Int(config, "screenSaverSeconds") is { } screenSaver)
        {
            options.ScreenSaverSeconds = screenSaver;
        }

        if (config["scrollText"]?.GetValue<string>() is { } scroll)
        {
            options.ScrollText = scroll switch { "long" => ScrollText.Long, "loop" => ScrollText.Loop, _ => ScrollText.Off };
        }

        // firmware before 2.2 does not report it: the setting stays as it is
        if (config["transferFeedback"]?.GetValue<string>() is { } feedback)
        {
            options.TransferFeedback = feedback switch
            {
                "hellpodVertical" => TransferFeedback.HellpodVertical,
                "flash" => TransferFeedback.Flash,
                _ => TransferFeedback.HellpodHorizontal,
            };
        }

        if (config["directionKeys"]?.GetValue<string>() is { } directions)
        {
            options.DirectionKeys = directions == "wasd" ? DirectionKeys.Wasd : DirectionKeys.Arrows;
        }

        if (config["brightness"] is JsonValue brightness && brightness.TryGetValue<int>(out var level))
        {
            options.Brightness = level;
        }

        if (config["logLevel"]?.GetValue<string>() is { } logLevel && Enum.TryParse<BoardLogLevel>(logLevel, ignoreCase: true, out var parsed))
        {
            options.LogLevel = parsed;
        }

        options.Adopted = true;
    }

    private static int? Int(JsonObject config, string key) =>
        config[key] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    private static JsonObject? Definition(string? id, StratagemCatalog catalog) =>
        id is not null && catalog.Find(id) is { IsTransferable: true } entry
            ? new JsonObject { ["id"] = entry.Id, ["name"] = entry.Name, ["color"] = entry.Color, ["sequence"] = entry.Sequence }
            : null;

    /// <summary>Catalog id of a stratagem definition from the board: by its id, otherwise by its name.</summary>
    private static string? FindId(JsonNode? definition, StratagemCatalog catalog)
    {
        if (definition is not JsonObject stratagem)
        {
            return null;
        }

        if (stratagem["id"]?.GetValue<string>() is { } id && catalog.Find(id) is not null)
        {
            return id;
        }

        var name = stratagem["name"]?.GetValue<string>();
        return catalog.Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))?.Id;
    }
}
