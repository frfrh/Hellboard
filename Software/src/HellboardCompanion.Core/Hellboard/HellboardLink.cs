// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
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
}

public sealed record HellboardState(
    HellboardStatus Status,
    string? Port,
    HellboardInfo? Info,
    HellboardLoadout? Pending,
    HellboardLoadout? LastSent,
    DateTimeOffset? LastSentAt,
    IReadOnlyList<string> NotTransferable,
    string? Message);

/// <summary>
/// Keeps the connection to the Hellboard and transfers every locked loadout (implementation plan
/// §6.6). All board communication runs on one background loop. A loadout is sent only if it differs
/// from the one the board already has; after a reconnect or a restart of the board it is sent again.
/// A board that does not speak the protocol is released after the check, so other tools (e.g.
/// MicroPico) can use the port, and checked again later.
/// </summary>
public sealed class HellboardLink : IDisposable
{
    public const string UsbVendorId = "2E8A";

    private static readonly TimeSpan SearchInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan NoProtocolRetryInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);

    private readonly HellboardOptions _options;
    private readonly MultiFunctionOptions _multiFunction;
    private readonly StratagemCatalog _catalog;
    private readonly ISerialPortDiscovery? _discovery;
    private readonly Func<string, IHellboardConnection> _openSerial;
    private readonly ILogger<HellboardLink> _logger;
    private readonly ConcurrentQueue<Func<Task>> _requests = new();
    private readonly SemaphoreSlim _wake = new(0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, DateTimeOffset> _noProtocolUntil = new(StringComparer.OrdinalIgnoreCase);

    private (HellboardConnectionMode Mode, string? Port) _appliedConnection;
    private IHellboardConnection? _connection;
    private HellboardClient? _client;
    private DateTimeOffset _lastHeartbeat;
    private LockedLoadout? _lastLocked;
    private bool _resendRequired;
    private Task? _loop;

    public HellboardLink(
        HellboardOptions options,
        MultiFunctionOptions multiFunction,
        StratagemCatalog catalog,
        ILogger<HellboardLink> logger,
        ISerialPortDiscovery? discovery = null,
        Func<string, IHellboardConnection>? openSerial = null)
    {
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
    /// Applies changed settings: reconnects when the connection mode or port changed, and rebuilds the
    /// transfer (e.g. a changed multi-function list); the board only receives it if it changed.
    /// </summary>
    public void ApplySettings() => Enqueue(() =>
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
        return SendPendingAsync();
    });

    /// <summary>"Set as default" (F3): writes the board's current loadout and configuration to its flash.</summary>
    public Task SetAsDefaultAsync()
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

                await SendPendingAsync().ConfigureAwait(false);
                await _client.SaveDefaultAsync().ConfigureAwait(false);
                _logger.LogInformation("Hellboard: current loadout saved as default");
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        return completion.Task;
    }

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
        _logger.LogInformation("Hellboard connected on {Port}: firmware {Firmware}, protocol {Protocol}", port, info.Firmware, info.Protocol);
        Update(State with { Status = HellboardStatus.Connected, Port = port, Info = info, Message = null });
        return true;
    }

    private void BuildPending()
    {
        if (_lastLocked is null)
        {
            return;
        }

        var transfer = LoadoutTransferBuilder.Build(_lastLocked, _catalog, _multiFunction);
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
        Update(State with { Status = HellboardStatus.NotFound, Info = null, Message = null });
        return Task.CompletedTask;
    });

    private void OnBoardEvent(object? sender, string eventName)
    {
        if (eventName == "hello")
        {
            // The board restarted: it runs its default again, so send the current loadout.
            Enqueue(() =>
            {
                _resendRequired = true;
                return SendPendingAsync();
            });
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
