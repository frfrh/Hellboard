// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Serialization;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging;

namespace HellboardCompanion.Core.Catalog;

/// <summary>
/// The wiki layer of the stratagem catalog (wiki-source-plan §3.2): the last stratagem table read from
/// helldivers.wiki.gg, kept in <c>%LOCALAPPDATA%\HellboardCompanion\wiki\stratagems.json</c> so it also works
/// offline, checked at most once a day (unless forced), plus the code proposals the user dismissed.
/// </summary>
public sealed class WikiStratagemStore
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<WikiStratagemStore> _logger;
    private readonly TimeProvider _time;
    private readonly HttpMessageHandler? _handler;
    private readonly SemaphoreSlim _updating = new(1, 1);
    private readonly Lock _lock = new();
    private volatile IReadOnlyList<WikiStratagem> _stratagems = [];
    private Dictionary<string, string> _dismissed = new(StringComparer.Ordinal);

    /// <param name="handler">HTTP handler for tests; <c>null</c> = the real network.</param>
    public WikiStratagemStore(IPlatformPaths paths, ILogger<WikiStratagemStore> logger, TimeProvider? time = null, HttpMessageHandler? handler = null)
    {
        _logger = logger;
        _time = time ?? TimeProvider.System;
        _handler = handler;
        FilePath = Path.Combine(paths.DataDirectory, "wiki", "stratagems.json");
    }

    public string FilePath { get; }

    /// <summary>The stratagem table of the last successful check; empty before the first one.</summary>
    public IReadOnlyList<WikiStratagem> Stratagems => _stratagems;

    /// <summary>Time of the last successful check, <c>null</c> if there was none yet.</summary>
    public DateTimeOffset? CheckedAt { get; private set; }

    /// <summary>Error of the last check (offline, changed wiki …), <c>null</c> if it succeeded.</summary>
    public string? LastError { get; private set; }

    /// <summary>Raised when the stratagem table changed (the catalog is rebuilt).</summary>
    public event EventHandler? Changed;

    /// <summary>Raised after every check (also when nothing changed or it failed).</summary>
    public event EventHandler? UpdateFinished;

    /// <summary>Reads the local copy, without network access. A missing or broken file means "no wiki data yet".</summary>
    public void Load()
    {
        try
        {
            var cache = JsonSerializer.Deserialize<Cache>(File.ReadAllText(FilePath));
            if (cache is not null)
            {
                _stratagems = (cache.Stratagems ?? []).Select(r => new WikiStratagem(r.Page, r.Title, r.Type, r.Code, r.Image)).ToList();
                CheckedAt = cache.CheckedAt;
                lock (_lock)
                {
                    _dismissed = new Dictionary<string, string>(cache.Dismissed ?? [], StringComparer.Ordinal);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            if (File.Exists(FilePath))
            {
                _logger.LogWarning("Wiki stratagem cache {File} ignored: {Error}", FilePath, ex.Message);
            }
        }

        _logger.LogInformation("Wiki stratagems available: {Count} (checked {CheckedAt})", _stratagems.Count, CheckedAt?.ToString("u") ?? "never");
    }

    /// <summary>
    /// Reads the stratagem table of the wiki and keeps it if it changed; keeps the local copy when the wiki
    /// cannot be read. Without <paramref name="force"/> the wiki is asked at most once a day.
    /// </summary>
    public async Task UpdateAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await _updating.WaitAsync(cancellationToken);
        try
        {
            var now = _time.GetUtcNow();
            if (!force && _stratagems.Count > 0 && CheckedAt is { } last && now - last < CheckInterval)
            {
                return;
            }

            using var http = _handler is null ? new HttpClient() : new HttpClient(_handler, disposeHandler: false);
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"HellboardCompanion/{AppInfo.Version.Split('+')[0]} (+https://github.com/frfrh/Hellboard)");
            var stratagems = await new WikiClient(http).GetStratagemsAsync(cancellationToken);
            var changed = !stratagems.SequenceEqual(_stratagems);
            _stratagems = stratagems;
            CheckedAt = now;
            LastError = null;
            Save();
            _logger.LogInformation("Wiki stratagems checked: {Count} rows{Changed}", stratagems.Count, changed ? ", changed" : string.Empty);
            if (changed)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            LastError = ex.Message;
            _logger.LogWarning(ex, "The stratagem table of helldivers.wiki.gg could not be read, using the local copy");
        }
        finally
        {
            _updating.Release();
            UpdateFinished?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Whether the user dismissed exactly this proposal (a later, different wiki code shows again).</summary>
    public bool IsDismissed(WikiProposal proposal)
    {
        lock (_lock)
        {
            return _dismissed.TryGetValue(proposal.Id, out var code) && code == proposal.WikiCode;
        }
    }

    /// <summary>The proposals of <paramref name="catalog"/> the user has not dismissed.</summary>
    public IReadOnlyList<WikiProposal> OpenProposals(StratagemCatalog catalog) => catalog.WikiProposals.Where(p => !IsDismissed(p)).ToList();

    /// <summary>Remembers that the user keeps the current code although the wiki lists <see cref="WikiProposal.WikiCode"/>.</summary>
    public void Dismiss(WikiProposal proposal)
    {
        lock (_lock)
        {
            _dismissed[proposal.Id] = proposal.WikiCode;
        }

        Save();
    }

    private void Save()
    {
        Cache cache;
        lock (_lock)
        {
            cache = new Cache(CheckedAt, [.. _stratagems.Select(s => new Row(s.Page, s.Title, s.Type, s.Code, s.Image))], new Dictionary<string, string>(_dismissed));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(cache, JsonOptions));
        File.Move(FilePath + ".tmp", FilePath, overwrite: true);
    }

    private sealed record Cache(
        [property: JsonPropertyName("checkedAt")] DateTimeOffset? CheckedAt,
        [property: JsonPropertyName("stratagems")] List<Row> Stratagems,
        [property: JsonPropertyName("dismissed")] Dictionary<string, string> Dismissed);

    private sealed record Row(
        [property: JsonPropertyName("page")] string Page,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("image")] string Image);
}
