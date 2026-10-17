// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using HellboardCompanion.Core.Hellboard.Update;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging;

namespace HellboardCompanion.Core.Hellboard;

public enum HellboardConnectionMode
{
    /// <summary>Real board over USB serial.</summary>
    Serial,

    /// <summary>In-memory simulator.</summary>
    Simulator,

    /// <summary>Nothing is sent; the loadout is only built and logged.</summary>
    DryRun,
}

public sealed class HellboardOptions
{
    public HellboardConnectionMode Mode { get; set; } = HellboardConnectionMode.Serial;

    /// <summary>Fixed port (e.g. "COM8"); <c>null</c> = find the board by USB vendor id.</summary>
    public string? Port { get; set; }
}

public enum HellboardStatus
{
    /// <summary>Dry-run mode, nothing is sent.</summary>
    DryRun,

    /// <summary>No board with USB vendor id 2E8A found.</summary>
    NotFound,

    /// <summary>Board found, but it does not answer the protocol (firmware without protocol support).</summary>
    NoProtocol,

    /// <summary>Connected; the board answered <c>ping</c>.</summary>
    Connected,

    /// <summary>The firmware is being installed (implementation plan §6.10).</summary>
    Updating,
}

public sealed record HellboardState(
    HellboardStatus Status,
    string? Port,
    HellboardInfo? Info,
    HellboardLoadout? Pending,
    HellboardLoadout? LastSent,
    DateTimeOffset? LastSentAt,
    IReadOnlyList<string> NotTransferable,
    string? Message,
    bool? ConfigSaved = null);

/// <summary>
/// Keeps the connection to the Hellboard and transfers every locked loadout (implementation plan
/// §6.6). All board communication runs on one background loop. A loadout is sent only if it differs
/// from the one the board already has; after a reconnect or a restart of the board it is sent again.
/// A board that does not speak the protocol is released after the check, so other tools (e.g.
/// MicroPico) can use the port, and checked again later. The board configuration (F13) is taken over
/// from the board on the very first connect and sent to it after every connect and change; the
/// board's log entries (F12) are collected and written to the Companion log.
/// </summary>
public sealed class HellboardLink : IDisposable
{
    public const string UsbVendorId = "2E8A";

    private static readonly TimeSpan SearchInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan NoProtocolRetryInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);
    private const int MaxLogEntries = 500;

    private readonly HellboardOptions _options;
    private readonly MultiFunctionOptions _multiFunction;
    private readonly StratagemCatalog _catalog;
    private readonly ISerialPortDiscovery? _discovery;
    private readonly Func<string, IHellboardConnection> _openSerial;
    private readonly Func<string, IReplPort> _openRepl;
    private readonly ILogger<HellboardLink> _logger;
    private readonly ConcurrentQueue<Func<Task>> _requests = new();
    private readonly SemaphoreSlim _wake = new(0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, DateTimeOffset> _noProtocolUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly BoardOptions _board;
    private readonly List<HellboardLogEntry> _log = [];
    private readonly Lock _logLock = new();

    private (HellboardConnectionMode Mode, string? Port) _appliedConnection;
    private IHellboardConnection? _connection;
    private HellboardClient? _client;
    private DateTimeOffset _lastHeartbeat;
    private LockedLoadout? _lastLocked;
    private bool _resendRequired;
    private JsonObject? _lastConfig;
    private HellboardLoadout? _boardLoadout;
    private bool _configResendRequired;
    private Task? _loop;

    public HellboardLink(
        HellboardOptions options,
        MultiFunctionOptions multiFunction,
        StratagemCatalog catalog,
        ILogger<HellboardLink> logger,
        ISerialPortDiscovery? discovery = null,
        Func<string, IHellboardConnection>? openSerial = null,
        BoardOptions? board = null,
        Func<string, IReplPort>? openRepl = null)
    {
        _board = board ?? new BoardOptions();
        _openRepl = openRepl ?? (port => new SerialReplPort(port));
        _options = options;
        _multiFunction = multiFunction;
        _catalog = catalog;
        _logger = logger;
        _discovery = discovery;
        _openSerial = openSerial ?? (port => new SerialHellboardConnection(port));
        _appliedConnection = (options.Mode, options.Port);
        State = new HellboardState(
            options.Mode == HellboardConnectionMode.DryRun ? HellboardStatus.DryRun : HellboardStatus.NotFound,
            null, null, null, null, null, [], null);
    }

    public HellboardState State { get; private set; }

    /// <summary>Raised on the link thread whenever <see cref="State"/> changes.</summary>
    public event EventHandler<HellboardState>? StateChanged;

    /// <summary>A firmware log entry arrived (F12); raised on the link thread.</summary>
    public event EventHandler<HellboardLogEntry>? LogReceived;

    /// <summary>The configuration of a board was taken over into <see cref="BoardOptions"/> (first connect, F13); the settings should be saved.</summary>
    public event EventHandler? BoardConfigAdopted;

    /// <summary>The last firmware log entries, oldest first.</summary>
    public IReadOnlyList<HellboardLogEntry> Log
    {
        get
        {
            lock (_logLock)
            {
                return [.. _log];
            }
        }
    }

    /// <summary>The simulated board in simulator mode (for tests and diagnostics).</summary>
    public SimulatedHellboard? Simulator { get; private set; }

    public void Start() => _loop ??= Task.Run(() => RunAsync(_stop.Token));

    /// <summary>Transfers a locked loadout (or keeps it until a board is connected).</summary>
    public void Submit(LockedLoadout loadout) => Enqueue(() =>
    {
        _lastLocked = loadout;
        BuildPending();
        return SendPendingAsync();
    });

    /// <summary>Rebuilds the transfer from the last locked loadout, e.g. after the multi-function settings changed.</summary>
    public void Rebuild() => Enqueue(() =>
    {
        BuildPending();
        return SendPendingAsync();
    });

    /// <summary>
    /// Applies changed settings: reconnects when the connection mode or port changed, rebuilds the
    /// transfer (e.g. a changed multi-function list) and sends a changed board configuration; the
    /// board only receives what changed.
    /// </summary>
    public void ApplySettings() => Enqueue(async () =>
    {
        if ((_options.Mode, _options.Port) != _appliedConnection)
        {
            _appliedConnection = (_options.Mode, _options.Port);
            _logger.LogInformation("Hellboard connection changed to {Mode} {Port}", _options.Mode, _options.Port ?? "(auto)");
            Disconnect();
            _noProtocolUntil.Clear();
            Update(State with
            {
                Status = _options.Mode == HellboardConnectionMode.DryRun ? HellboardStatus.DryRun : HellboardStatus.NotFound,
                Port = null,
                Info = null,
                LastSent = null,
                LastSentAt = null,
                Message = null,
            });
        }

        BuildPending();
        await SendPendingAsync().ConfigureAwait(false);
        await SendConfigAsync().ConfigureAwait(false);
    });

    /// <summary>"Save configuration on the Hellboard" (F13): sends the configuration and writes it to the board's flash.</summary>
    public Task SaveBoardConfigAsync() => RunOnLink(async client =>
    {
        _configResendRequired = true;
        await SendConfigAsync().ConfigureAwait(false);
        var response = await client.SaveConfigAsync().ConfigureAwait(false);
        _logger.LogInformation("Hellboard: configuration saved on the board");
        Update(State with { ConfigSaved = response["configSaved"]?.GetValue<bool>() ?? true });
    });

    /// <summary>
    /// Installs <paramref name="bundle"/> on the board (implementation plan §6.10): a board with the protocol
    /// is asked to stop (<c>enterUpdateMode</c>, F8), a board with an older firmware is stopped with Ctrl-C.
    /// The files are written and verified through the raw REPL and only then swapped in; on any failure the
    /// old files stay or are restored. Afterwards the board restarts and the link connects again.
    /// </summary>
    public Task UpdateFirmwareAsync(FirmwareBundle bundle, IProgress<string>? progress = null)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(async () =>
        {
            try
            {
                if (_options.Mode != HellboardConnectionMode.Serial || State.Port is not { } port
                    || State.Status is not (HellboardStatus.Connected or HellboardStatus.NoProtocol))
                {
                    throw new InvalidOperationException("No Hellboard connected over USB.");
                }

                _logger.LogInformation("Hellboard firmware update to {Version} on {Port}", bundle.Version, port);
                if (_client is not null)
                {
                    progress?.Report("Stopping the Hellboard program");
                    try
                    {
                        await _client.EnterUpdateModeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is TimeoutException or IOException or HellboardException)
                    {
                        // falls back to Ctrl-C in the raw REPL
                    }
                }

                Disconnect();
                Update(State with { Status = HellboardStatus.Updating, Info = null, Message = null, ConfigSaved = null });
                await Task.Run(() => Install(port, bundle, progress)).ConfigureAwait(false);
                _logger.LogInformation("Hellboard firmware {Version} installed", bundle.Version);
                progress?.Report("Restarting the Hellboard");
                completion.SetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hellboard firmware update failed");
                completion.SetException(ex);
            }
            finally
            {
                _noProtocolUntil.Clear();
                _resendRequired = true;
                _configResendRequired = true;
                if (State.Status == HellboardStatus.Updating)
                {
                    Update(State with { Status = HellboardStatus.NotFound, Port = null });
                }
            }
        });
        return completion.Task;
    }

    /// <summary>"Set as default" (F3): writes the board's current loadout to its flash.</summary>
    public Task SetAsDefaultAsync() => RunOnLink(async client =>
    {
        await SendPendingAsync().ConfigureAwait(false);
        await client.SaveDefaultAsync().ConfigureAwait(false);
        _logger.LogInformation("Hellboard: current loadout saved as default");
    });

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        Disconnect();
        _stop.Dispose();
        _wake.Dispose();
    }

    /// <summary>Runs an action that needs a connected board on the link thread and reports its outcome.</summary>
    private Task RunOnLink(Func<HellboardClient, Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(async () =>
        {
            try
            {
                if (_client is null)
                {
                    throw new InvalidOperationException("No Hellboard connected.");
                }

                await action(_client).ConfigureAwait(false);
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        return completion.Task;
    }

    private void Enqueue(Func<Task> request)
    {
        _requests.Enqueue(request);
        _wake.Release();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                while (_requests.TryDequeue(out var request))
                {
                    await request().ConfigureAwait(false);
                }

                await MaintainConnectionAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hellboard communication failed");
                Disconnect();
                Update(State with { Status = HellboardStatus.NotFound, Info = null, Message = ex.Message });
            }

            try
            {
                await _wake.WaitAsync(SearchInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task MaintainConnectionAsync()
    {
        if (_options.Mode == HellboardConnectionMode.DryRun)
        {
            return;
        }

        if (_client is null)
        {
            await ConnectAsync().ConfigureAwait(false);
        }
        else if (DateTimeOffset.Now - _lastHeartbeat > HeartbeatInterval)
        {
            await _client.PingAsync().ConfigureAwait(false);   // detects an unplugged board
            _lastHeartbeat = DateTimeOffset.Now;
        }

        if (_client is not null && _resendRequired)
        {
            await SendPendingAsync().ConfigureAwait(false);
        }

        if (_client is not null && _configResendRequired)
        {
            await SendConfigAsync().ConfigureAwait(false);
        }
    }

    private async Task ConnectAsync()
    {
        if (_options.Mode == HellboardConnectionMode.Simulator)
        {
            Simulator ??= new SimulatedHellboard();
            await OpenAsync(Simulator, "Simulator").ConfigureAwait(false);
            return;
        }

        var ports = _options.Port is { } fixedPort ? [fixedPort] : _discovery?.FindPorts(UsbVendorId) ?? [];
        var candidates = ports.Where(p => !_noProtocolUntil.TryGetValue(p, out var until) || until < DateTimeOffset.Now).ToList();
        if (ports.Count == 0)
        {
            Update(State with { Status = HellboardStatus.NotFound, Port = null, Info = null, Message = null });
            return;
        }

        foreach (var port in candidates)
        {
            IHellboardConnection connection;
            try
            {
                connection = _openSerial(port);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Update(State with { Status = HellboardStatus.NoProtocol, Port = port, Message = $"{port} is in use by another program (e.g. MicroPico)." });
                continue;
            }

            bool connected;
            try
            {
                connected = await OpenAsync(connection, port).ConfigureAwait(false);
            }
            catch
            {
                connection.Dispose();   // never keep a port open that is not in use
                throw;
            }

            if (connected)
            {
                return;
            }

            // Old firmware without protocol: release the port and ask again later.
            connection.Dispose();
            _noProtocolUntil[port] = DateTimeOffset.Now + NoProtocolRetryInterval;
            _logger.LogInformation("Hellboard on {Port} does not answer the protocol (old firmware); port released, next check in {Seconds} s", port, NoProtocolRetryInterval.TotalSeconds);
            Update(State with { Status = HellboardStatus.NoProtocol, Port = port, Info = null, Message = "The Hellboard firmware does not support the protocol yet (firmware update needed)." });
        }
    }

    private void Install(string port, FirmwareBundle bundle, IProgress<string>? progress)
    {
        // The port was just closed; Windows may need a moment before it can be opened again.
        IReplPort? replPort = null;
        for (var attempt = 0; replPort is null; attempt++)
        {
            try
            {
                replPort = _openRepl(port);
            }
            catch (Exception ex) when (attempt < 10 && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(300);
            }
        }

        using (replPort)
        {
            var repl = new MicroPythonRawRepl(replPort);
            progress?.Report("Connecting to MicroPython");
            repl.Enter();
            try
            {
                new FirmwareInstaller(repl).Install(bundle, progress);
            }
            finally
            {
                repl.ExitAndSoftReset();
            }
        }
    }

    private async Task<bool> OpenAsync(IHellboardConnection connection, string port)
    {
        var client = new HellboardClient(connection);
        HellboardInfo info;
        try
        {
            info = await client.PingAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            // No answer, or the write blocks because the firmware does not read the port (old firmware).
            client.Dispose();
            return false;
        }

        if (info.Protocol > HellboardClient.ProtocolVersion)
        {
            client.Dispose();
            Update(State with { Status = HellboardStatus.NoProtocol, Port = port, Info = info, Message = $"The Hellboard uses protocol {info.Protocol}; please update Hellboard Companion." });
            return false;
        }

        _connection = connection;
        _client = client;
        _lastHeartbeat = DateTimeOffset.Now;
        connection.Closed += OnConnectionClosed;
        client.EventReceived += OnBoardEvent;
        _resendRequired = true;   // the board may have restarted with another loadout
        _configResendRequired = true;
        _logger.LogInformation("Hellboard connected on {Port}: firmware {Firmware}, protocol {Protocol}", port, info.Firmware, info.Protocol);
        Update(State with { Status = HellboardStatus.Connected, Port = port, Info = info, Message = null, ConfigSaved = null });
        await AfterConnectAsync(client).ConfigureAwait(false);
        return true;
    }

    /// <summary>Fetches the log of the board and, on the very first connect, takes over its configuration (F12, F13).</summary>
    private async Task AfterConnectAsync(HellboardClient client)
    {
        try
        {
            var entries = await client.GetLogAsync().ConfigureAwait(false);
            lock (_logLock)
            {
                _log.Clear();   // the board keeps its own history; avoid duplicates after a reconnect
            }

            foreach (var entry in entries)
            {
                AddLog(entry, writeToCompanionLog: false);
            }
        }
        catch (HellboardException ex)
        {
            _logger.LogDebug("Hellboard log not available: {Error}", ex.Message);
        }

        var state = await client.GetStateAsync().ConfigureAwait(false);
        ReadBoardLoadout(state);
        if (!_board.Adopted)
        {
            if (state["config"] is JsonObject config)
            {
                BoardConfiguration.Adopt(config, _board, _catalog);
                _logger.LogInformation("Hellboard: configuration of the board taken over (first connect)");
                BoardConfigAdopted?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Remembers the loadout the board reports, for applying the multi-function list before any loadout was locked.</summary>
    private void ReadBoardLoadout(JsonObject state)
    {
        try
        {
            var stratagems = state["stratagems"]?.Deserialize<List<StratagemDefinition?>>();
            var mission = state["missionStratagems"]?.Deserialize<List<StratagemDefinition>>();
            if (stratagems is { Count: 4 } && mission is not null)
            {
                _boardLoadout = new HellboardLoadout(stratagems, mission);
                BuildPending();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogDebug("Hellboard state not readable: {Error}", ex.Message);
        }
    }

    private async Task SendConfigAsync()
    {
        if (_client is null || _options.Mode == HellboardConnectionMode.DryRun)
        {
            return;
        }

        var config = BoardConfiguration.ToProtocol(_board, _catalog);
        if (!_configResendRequired && JsonNode.DeepEquals(config, _lastConfig))
        {
            return;
        }

        try
        {
            var response = await _client.SetConfigAsync(config).ConfigureAwait(false);
            _lastConfig = config;
            _configResendRequired = false;
            _logger.LogInformation("Hellboard: configuration sent");
            Update(State with { ConfigSaved = response["configSaved"]?.GetValue<bool>() });
        }
        catch (HellboardException ex)
        {
            _configResendRequired = false;   // the board rejects it; do not retry in a loop
            _logger.LogWarning("Hellboard rejected the configuration: {Error}", ex.Message);
            Update(State with { Message = $"Configuration rejected: {ex.Message}" });
        }
    }

    private void AddLog(HellboardLogEntry entry, bool writeToCompanionLog = true)
    {
        lock (_logLock)
        {
            _log.Add(entry);
            if (_log.Count > MaxLogEntries)
            {
                _log.RemoveAt(0);
            }
        }

        if (writeToCompanionLog)
        {
            var level = entry.Level switch
            {
                "error" => LogLevel.Error,
                "warn" => LogLevel.Warning,
                "debug" => LogLevel.Debug,
                _ => LogLevel.Information,
            };
            _logger.Log(level, "Hellboard log: {Message}", entry.Message);
        }

        LogReceived?.Invoke(this, entry);
    }

    private void BuildPending()
    {
        LoadoutTransfer transfer;
        if (_lastLocked is not null)
        {
            transfer = LoadoutTransferBuilder.Build(_lastLocked, _catalog, _multiFunction);
        }
        else if (_boardLoadout is not null)
        {
            transfer = LoadoutTransferBuilder.BuildMultiFunctionOnly(_boardLoadout, _catalog, _multiFunction);
        }
        else
        {
            return;
        }

        if (transfer.NotTransferable.Count > 0)
        {
            _logger.LogWarning("Not transferable (no input sequence in the catalog): {Ids}", string.Join(", ", transfer.NotTransferable));
        }

        Update(State with { Pending = transfer.Loadout, NotTransferable = transfer.NotTransferable });
    }

    private async Task SendPendingAsync()
    {
        var pending = State.Pending;
        if (pending is null)
        {
            return;
        }

        if (_options.Mode == HellboardConnectionMode.DryRun)
        {
            _logger.LogInformation("Hellboard dry run, would send: {Loadout}", Describe(pending));
            Update(State with { LastSent = pending, LastSentAt = DateTimeOffset.Now });
            return;
        }

        if (_client is null || (!_resendRequired && pending.Equals(State.LastSent)))
        {
            return;
        }

        await _client.SetLoadoutAsync(pending).ConfigureAwait(false);
        _resendRequired = false;
        _logger.LogInformation("Hellboard: loadout sent: {Loadout}", Describe(pending));
        Update(State with { LastSent = pending, LastSentAt = DateTimeOffset.Now });
    }

    private void OnConnectionClosed(object? sender, EventArgs e) => Enqueue(() =>
    {
        _logger.LogInformation("Hellboard disconnected");
        Disconnect();
        Update(State with { Status = HellboardStatus.NotFound, Info = null, Message = null, ConfigSaved = null });
        return Task.CompletedTask;
    });

    private void OnBoardEvent(object? sender, JsonObject message)
    {
        switch (message["event"]?.GetValue<string>())
        {
            case "hello":
                // The board restarted: it runs its saved default again, so send loadout and configuration.
                Enqueue(async () =>
                {
                    if (_lastLocked is null && _client is not null)
                    {
                        ReadBoardLoadout(await _client.GetStateAsync().ConfigureAwait(false));
                    }

                    _resendRequired = true;
                    _configResendRequired = true;
                    await SendPendingAsync().ConfigureAwait(false);
                    await SendConfigAsync().ConfigureAwait(false);
                });
                break;
            case "log":
                AddLog(HellboardLogEntry.FromJson(message, DateTimeOffset.Now));
                break;
        }
    }

    private void Disconnect()
    {
        if (_connection is not null)
        {
            _connection.Closed -= OnConnectionClosed;
        }

        _client?.Dispose();
        if (_connection is not SimulatedHellboard)
        {
            _connection?.Dispose();
        }

        _client = null;
        _connection = null;
    }

    private void Update(HellboardState state)
    {
        if (state.Equals(State))
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, state);
    }

    private static string Describe(HellboardLoadout loadout) =>
        $"{string.Join(", ", loadout.Stratagems.Select(s => s?.Name ?? "-"))} | multi-function: {string.Join(", ", loadout.MissionStratagems.Select(s => s.Name))}";
}
