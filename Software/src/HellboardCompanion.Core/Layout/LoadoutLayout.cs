// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;

namespace HellboardCompanion.Core.Layout;

/// <summary>Rectangle in reference coordinates (1920×1080 by default), see implementation plan §6.3.</summary>
public sealed record LayoutRect(double X, double Y, double Width, double Height);

/// <summary>
/// Positions of the local player's card elements on the loadout screen. The layout is data, so a
/// game update only requires re-measuring (implementation plan §6.3).
/// </summary>
public sealed record LoadoutLayout(
    int Version,
    int ReferenceWidth,
    int ReferenceHeight,
    LayoutRect ReadyButton,
    IReadOnlyList<LayoutRect> PlayerSlots,
    IReadOnlyList<LayoutRect> FixedMissionSlots,
    IReadOnlyList<LayoutRect> OptionalMissionSlots)
{
    private const string DefaultResourceName = "HellboardCompanion.Core.Layout.loadout-v1.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>The layout shipped with the application.</summary>
    public static LoadoutLayout Default { get; } = LoadDefault();

    public static LoadoutLayout Load(Stream json)
    {
        var layout = JsonSerializer.Deserialize<LoadoutLayout>(json, JsonOptions)
            ?? throw new InvalidDataException("The layout file is empty.");
        layout.Validate();
        return layout;
    }

    private static LoadoutLayout LoadDefault()
    {
        using var stream = typeof(LoadoutLayout).Assembly.GetManifestResourceStream(DefaultResourceName)
            ?? throw new InvalidOperationException($"Embedded layout '{DefaultResourceName}' not found.");
        return Load(stream);
    }

    private void Validate()
    {
        if (ReferenceWidth <= 0 || ReferenceHeight <= 0)
        {
            throw new InvalidDataException("The reference resolution must be positive.");
        }

        if (ReadyButton is null || PlayerSlots is not { Count: 4 } || FixedMissionSlots is null || OptionalMissionSlots is null)
        {
            throw new InvalidDataException("The layout must define the ready button, 4 player slots and the mission slots.");
        }
    }
}
