// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Configuration;

/// <summary>
/// Loads and saves <see cref="CompanionSettings"/> (implementation plan §6.8). Missing values get their
/// defaults; an unreadable file is kept as <c>config.json.invalid</c> and replaced by defaults, so the
/// application always starts.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // local file, edited by hand: keep "+" readable
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Lock _lock = new();

    private SettingsStore(string filePath, CompanionSettings settings, string? loadProblem)
    {
        FilePath = filePath;
        Settings = settings;
        LoadProblem = loadProblem;
    }

    public string FilePath { get; }

    public CompanionSettings Settings { get; }

    /// <summary>Why the file could not be used (for the log), or <c>null</c>.</summary>
    public string? LoadProblem { get; }

    /// <summary>Raised after <see cref="Save"/>; services apply changes that need more than reading a value.</summary>
    public event EventHandler? Saved;

    public static SettingsStore Load(IPlatformPaths paths)
    {
        var filePath = Path.Combine(paths.ConfigDirectory, "config.json");
        if (!File.Exists(filePath))
        {
            var store = new SettingsStore(filePath, new CompanionSettings(), null);
            store.Write();
            return store;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<CompanionSettings>(File.ReadAllText(filePath), JsonOptions)
                ?? throw new JsonException("The file is empty.");
            return new SettingsStore(filePath, settings, null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            File.Copy(filePath, filePath + ".invalid", overwrite: true);
            var store = new SettingsStore(filePath, new CompanionSettings(), $"{filePath} could not be read ({ex.Message}); defaults are used, the file was kept as config.json.invalid.");
            store.Write();
            return store;
        }
    }

    /// <summary>Writes the current settings and notifies the services.</summary>
    public void Save()
    {
        Write();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Writes the current settings without notifying the services, for what only the window uses (e.g. its position).</summary>
    public void SaveQuietly() => Write();

    public string Serialize() => JsonSerializer.Serialize(Settings, JsonOptions);

    private void Write()
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, Serialize());
            File.Move(temporary, FilePath, overwrite: true);
        }
    }
}
