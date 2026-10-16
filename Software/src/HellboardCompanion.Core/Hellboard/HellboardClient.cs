// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HellboardCompanion.Core.Hellboard;

/// <summary>
/// Request/response layer of the Hellboard protocol (hellboard-protocol.md §2–§4): one request at a
/// time, 500 ms timeout, up to 3 retries with the same id, non-protocol lines are ignored.
/// </summary>
public sealed class HellboardClient : IDisposable
{
    public const int ProtocolVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHellboardConnection _connection;
    private readonly TimeSpan _timeout;
    private readonly int _retries;
    private readonly SemaphoreSlim _sequential = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonObject>> _pending = new();
    private int _nextId;

    public HellboardClient(IHellboardConnection connection, TimeSpan? timeout = null, int retries = 3)
    {
        _connection = connection;
        _timeout = timeout ?? TimeSpan.FromMilliseconds(500);
        _retries = retries;
        _connection.LineReceived += OnLineReceived;
    }

    /// <summary>Unsolicited events of the board, e.g. <c>hello</c> after a restart.</summary>
    public event EventHandler<string>? EventReceived;

    public string Description => _connection.Description;

    public async Task<HellboardInfo> PingAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync("ping", null, cancellationToken).ConfigureAwait(false);
        return response.Deserialize<HellboardInfo>(JsonOptions)!;
    }

    public Task SetLoadoutAsync(HellboardLoadout loadout, CancellationToken cancellationToken = default) =>
        SendAsync("setLoadout", request =>
        {
            request["stratagems"] = JsonSerializer.SerializeToNode(loadout.Stratagems);
            request["missionStratagems"] = JsonSerializer.SerializeToNode(loadout.MissionStratagems);
        }, cancellationToken);

    public Task<JsonObject> GetStateAsync(CancellationToken cancellationToken = default) =>
        SendAsync("getState", null, cancellationToken);

    public Task<JsonObject> SetConfigAsync(JsonObject config, CancellationToken cancellationToken = default) =>
        SendAsync("setConfig", request => request["config"] = config.DeepClone(), cancellationToken);

    /// <summary>Writes the current loadout and configuration to the board's flash ("Set as default", F3).</summary>
    public Task SaveDefaultAsync(CancellationToken cancellationToken = default) =>
        SendAsync("saveDefault", null, cancellationToken);

    public Task EnterUpdateModeAsync(CancellationToken cancellationToken = default) =>
        SendAsync("enterUpdateMode", null, cancellationToken);

    public void Dispose()
    {
        _connection.LineReceived -= OnLineReceived;
        foreach (var pending in _pending.Values)
        {
            pending.TrySetCanceled();
        }

        _sequential.Dispose();
    }

    private async Task<JsonObject> SendAsync(string command, Action<JsonObject>? addParameters, CancellationToken cancellationToken)
    {
        await _sequential.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var id = Interlocked.Increment(ref _nextId);
            var request = new JsonObject { ["v"] = ProtocolVersion, ["id"] = id, ["cmd"] = command };
            addParameters?.Invoke(request);
            var line = request.ToJsonString();

            for (var attempt = 0; attempt <= _retries; attempt++)
            {
                var completion = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pending[id] = completion;
                _connection.WriteLine(line);
                try
                {
                    var response = await completion.Task.WaitAsync(_timeout, cancellationToken).ConfigureAwait(false);
                    if (response["ok"]?.GetValue<bool>() == true)
                    {
                        return response;
                    }

                    throw new HellboardException(
                        response["error"]?.GetValue<string>() ?? "unknown",
                        response["message"]?.GetValue<string>());
                }
                catch (TimeoutException)
                {
                    // retry with the same id – all commands are idempotent (protocol §3)
                }
                finally
                {
                    _pending.TryRemove(id, out _);
                }
            }

            throw new TimeoutException($"The Hellboard did not answer '{command}' ({_retries + 1} attempts).");
        }
        finally
        {
            _sequential.Release();
        }
    }

    private void OnLineReceived(object? sender, string line)
    {
        // The raw REPL / debug output may precede the JSON; only lines with a protocol object count.
        var start = line.IndexOf('{');
        if (start < 0)
        {
            return;
        }

        JsonObject? message;
        try
        {
            message = JsonNode.Parse(line[start..]) as JsonObject;
        }
        catch (JsonException)
        {
            return;
        }

        if (message is null || message["v"] is null)
        {
            return;
        }

        if (message["ack"] is JsonValue ack && ack.TryGetValue<int>(out var id))
        {
            if (_pending.TryGetValue(id, out var completion))
            {
                completion.TrySetResult(message);
            }
        }
        else if (message["event"]?.GetValue<string>() is { } eventName)
        {
            EventReceived?.Invoke(this, eventName);
        }
    }
}
