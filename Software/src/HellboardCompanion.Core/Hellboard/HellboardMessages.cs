// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json.Serialization;

namespace HellboardCompanion.Core.Hellboard;

/// <summary>A stratagem as transferred to the board (hellboard-protocol.md §4.2). The name is never shortened.</summary>
public sealed record StratagemDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("sequence")] string Sequence);

/// <summary>Content of a <c>setLoadout</c> request.</summary>
/// <param name="Stratagems">Exactly 4 entries in slot order; <c>null</c> = empty / not transferable.</param>
/// <param name="MissionStratagems">List of the multi-function button (0–16 entries).</param>
public sealed record HellboardLoadout(
    [property: JsonPropertyName("stratagems")] IReadOnlyList<StratagemDefinition?> Stratagems,
    [property: JsonPropertyName("missionStratagems")] IReadOnlyList<StratagemDefinition> MissionStratagems)
{
    public bool Equals(HellboardLoadout? other) =>
        other is not null && Stratagems.SequenceEqual(other.Stratagems) && MissionStratagems.SequenceEqual(other.MissionStratagems);

    public override int GetHashCode() => HashCode.Combine(Stratagems.Count, MissionStratagems.Count, Stratagems.FirstOrDefault());
}

/// <summary>Answer to <c>ping</c>.</summary>
public sealed record HellboardInfo(
    [property: JsonPropertyName("board")] string? Board,
    [property: JsonPropertyName("fw")] string? Firmware,
    [property: JsonPropertyName("protocol")] int Protocol,
    [property: JsonPropertyName("micropython")] string? MicroPython);

/// <summary>Error answer of the board (hellboard-protocol.md §6).</summary>
public sealed class HellboardException(string code, string? message)
    : Exception($"Hellboard error '{code}': {message}")
{
    public string Code { get; } = code;
}
