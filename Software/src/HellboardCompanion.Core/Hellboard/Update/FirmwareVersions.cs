// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;

namespace HellboardCompanion.Core.Hellboard.Update;

/// <summary>How the firmware on the board relates to the one included in the Companion.</summary>
public enum FirmwareStatus
{
    /// <summary>No version known (no board, simulator, dry run).</summary>
    Unknown,

    /// <summary>Same version as included.</summary>
    Current,

    /// <summary>Older than the included one, but compatible: everything works, an update is available.</summary>
    Outdated,

    /// <summary>Newer than the included one (e.g. installed with a newer Companion).</summary>
    Newer,

    /// <summary>The board does not speak the protocol of this Companion: old firmware, or a newer protocol.</summary>
    Incompatible,
}

/// <summary>Compares firmware versions such as "2.1.0", "2.0.0-dev" or "dev".</summary>
public static class FirmwareVersions
{
    private static readonly Lazy<string?> Embedded = new(ReadEmbeddedVersion);

    /// <summary>Version of the firmware included in the Companion (<c>Firmware/manifest.json</c>), <c>null</c> if not embedded.</summary>
    public static string? EmbeddedVersion => Embedded.Value;

    public static FirmwareStatus Classify(HellboardState state, string? bundled)
    {
        if (state.Status == HellboardStatus.NoProtocol && !(state.Message?.Contains("in use", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return FirmwareStatus.Incompatible;   // old firmware without protocol, or a protocol newer than ours
        }

        if (state.Status != HellboardStatus.Connected || state.Port == "Simulator" || state.Info?.Firmware is not { } board || bundled is null)
        {
            return FirmwareStatus.Unknown;
        }

        return Compare(board, bundled) switch
        {
            < 0 => FirmwareStatus.Outdated,
            > 0 => FirmwareStatus.Newer,
            _ => FirmwareStatus.Current,
        };
    }

    /// <summary>Numeric comparison of major.minor.patch; a pre-release ("-dev") or "dev" is older than the release.</summary>
    public static int Compare(string a, string b)
    {
        var (numbersA, preA) = Parse(a);
        var (numbersB, preB) = Parse(b);
        for (var i = 0; i < 3; i++)
        {
            if (numbersA[i] != numbersB[i])
            {
                return numbersA[i].CompareTo(numbersB[i]);
            }
        }

        return preA == preB ? 0 : preA ? -1 : 1;
    }

    private static (int[] Numbers, bool PreRelease) Parse(string version)
    {
        var dash = version.IndexOf('-');
        var core = dash >= 0 ? version[..dash] : version;
        var parts = core.Split('.');
        var numbers = new int[3];
        var valid = parts.Length > 0;
        for (var i = 0; i < 3 && i < parts.Length; i++)
        {
            valid &= int.TryParse(parts[i], out numbers[i]);
        }

        // "dev" (no version file on the board) counts as the oldest possible version
        return valid ? (numbers, dash >= 0) : ([0, 0, 0], true);
    }

    private static string? ReadEmbeddedVersion()
    {
        using var stream = typeof(FirmwareVersions).Assembly.GetManifestResourceStream("Firmware/manifest.json");
        if (stream is null)
        {
            return null;
        }

        using var manifest = JsonDocument.Parse(stream);
        return manifest.RootElement.TryGetProperty("version", out var version) ? version.GetString() : null;
    }
}
