// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging.Abstractions;

namespace HellboardCompanion.Core.Tests.Hellboard;

public sealed class HellboardLinkTests
{
    private static readonly StratagemCatalog Catalog = StratagemCatalog.LoadSeed();
    private static readonly LockedLoadout LoadoutA = Locked("eagle_500kg_bomb", "grenade_launcher", "supply_pack", "mortar_sentry");
    private static readonly LockedLoadout LoadoutB = Locked("eagle_strafing_run", "anti_materiel_rifle", "directional_shield", "machine_gun_sentry");

    [Fact]
    public async Task Locked_loadout_is_sent_to_the_board()
    {
        using var link = Start(HellboardConnectionMode.Simulator);
        await WaitFor(() => link.State.Status == HellboardStatus.Connected && link.Simulator?.SetLoadoutCount == 1);

        link.Submit(LoadoutA);

        await WaitFor(() => link.Simulator!.SetLoadoutCount == 2);
        link.Simulator!.Stratagems![3]!["name"]!.GetValue<string>().Should().Be("Mortar Sentry");
        link.Simulator.MissionStratagems!.AsArray().Select(s => s!["name"]!.GetValue<string>()).Should().Equal("SoS Beacon");
        link.State.LastSent!.Stratagems[0]!.Name.Should().Be("Eagle 500kg Bomb");
    }

    [Fact]
    public async Task Same_loadout_is_not_sent_twice_but_a_changed_one_is()
    {
        using var link = Start(HellboardConnectionMode.Simulator);
        await WaitFor(() => link.State.Status == HellboardStatus.Connected && link.Simulator?.SetLoadoutCount == 1);

        link.Submit(LoadoutA);
        await WaitFor(() => link.Simulator!.SetLoadoutCount == 2);
        link.Submit(LoadoutA with { Timestamp = DateTimeOffset.Now, Source = LockSource.Manual });
        link.Submit(LoadoutB);

        await WaitFor(() => link.Simulator!.SetLoadoutCount == 3);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        link.Simulator!.SetLoadoutCount.Should().Be(3);
    }

    [Fact]
    public async Task Restarted_board_gets_the_loadout_again()
    {
        using var link = Start(HellboardConnectionMode.Simulator);
        await WaitFor(() => link.State.Status == HellboardStatus.Connected && link.Simulator?.SetLoadoutCount == 1);
        link.Submit(LoadoutA);
        await WaitFor(() => link.Simulator!.SetLoadoutCount == 2);

        link.Simulator!.Restart();

        await WaitFor(() => link.Simulator.SetLoadoutCount == 3);
    }

    [Fact]
    public async Task Flash_is_written_only_on_set_as_default()
    {
        using var link = Start(HellboardConnectionMode.Simulator);
        await WaitFor(() => link.State.Status == HellboardStatus.Connected && link.Simulator?.SetLoadoutCount == 1);
        link.Submit(LoadoutA);
        link.Submit(LoadoutB);
        await WaitFor(() => link.Simulator!.SetLoadoutCount == 3);
        link.Simulator!.SaveCount.Should().Be(0, "transfers must not write the flash (F3)");

        await link.SetAsDefaultAsync();

        link.Simulator.SaveCount.Should().Be(1);
        link.Simulator.SavedDefault.Should().Contain("Eagle Strafing Run");
    }

    [Fact]
    public async Task Fixed_multi_function_list_is_sent_before_any_loadout_was_locked()
    {
        var multiFunction = new MultiFunctionOptions { Mode = MultiFunctionMode.FixedListAndAutoDetect, FixedList = ["seaf_artillery", "eagle_rearm"] };
        using var link = new HellboardLink(new HellboardOptions { Mode = HellboardConnectionMode.Simulator }, multiFunction, Catalog, NullLogger<HellboardLink>.Instance);
        link.Start();

        await WaitFor(() => link.Simulator?.SetLoadoutCount == 1);
        link.Simulator!.MissionStratagems!.AsArray().Select(s => s!["name"]!.GetValue<string>()).Should().Equal("SEAF Artillery", "Eagle Rearm");
        link.Simulator.Stratagems!.AsArray().Should().AllSatisfy(s => s.Should().BeNull(), "the slots of the board are kept");

        multiFunction.FixedList = ["sos_beacon"];
        link.ApplySettings();

        await WaitFor(() => link.Simulator.SetLoadoutCount == 2);
        link.Simulator.MissionStratagems!.AsArray().Should().ContainSingle();
    }

    [Theory]
    [InlineData(MultiFunctionMode.FixedListAndAutoDetect, new[] { "SEAF Artillery", "Hellbomb", "Upload Data" })]
    [InlineData(MultiFunctionMode.AutoDetect, new[] { "Hellbomb", "Upload Data" })]
    [InlineData(MultiFunctionMode.FixedList, new[] { "SEAF Artillery" })]
    public async Task Detected_stratagems_of_the_previous_session_stay_in_the_multi_function_list(MultiFunctionMode mode, string[] expected)
    {
        var multiFunction = new MultiFunctionOptions { Mode = mode, FixedList = ["seaf_artillery"] };
        using var link = new HellboardLink(new HellboardOptions { Mode = HellboardConnectionMode.Simulator }, multiFunction, Catalog, NullLogger<HellboardLink>.Instance);
        link.RestorePreviousSession(LoadoutA with { MissionStratagems = ["hellbomb", "upload_data"] });
        link.Start();

        await WaitFor(() => link.Simulator?.SetLoadoutCount == 1);
        link.Simulator!.MissionStratagems!.AsArray().Select(s => s!["name"]!.GetValue<string>()).Should().Equal(expected);
        link.Simulator.Stratagems!.AsArray().Should().AllSatisfy(s => s.Should().BeNull(), "the slots of the board are kept");
    }

    [Fact]
    public async Task First_connect_takes_over_the_configuration_of_the_board()
    {
        var board = new BoardOptions { ModifierKey = 224, FixedButton2 = "sos_beacon" };
        var adopted = false;
        using var link = new HellboardLink(new HellboardOptions { Mode = HellboardConnectionMode.Simulator }, new MultiFunctionOptions(), Catalog, NullLogger<HellboardLink>.Instance, board: board);
        link.BoardConfigAdopted += (_, _) => adopted = true;
        link.Start();

        await WaitFor(() => adopted && link.State.ConfigSaved is not null);

        board.Adopted.Should().BeTrue();
        board.ModifierKey.Should().Be(6, "the simulated board uses C");
        board.FixedButton2.Should().Be("resupply");
        link.Simulator!.Config["modifierKey"]!.GetValue<int>().Should().Be(6);
    }

    [Fact]
    public async Task Configuration_changes_are_sent_and_saved_only_on_request()
    {
        var board = new BoardOptions { Adopted = true, ModifierKey = 224 };
        using var link = new HellboardLink(new HellboardOptions { Mode = HellboardConnectionMode.Simulator }, new MultiFunctionOptions(), Catalog, NullLogger<HellboardLink>.Instance, board: board);
        link.Start();
        await WaitFor(() => link.Simulator?.Config["modifierKey"]?.GetValue<int>() == 224);
        link.State.ConfigSaved.Should().BeFalse();

        board.FixedButton1 = "eagle_rearm";
        board.Brightness = 200;
        link.ApplySettings();
        await WaitFor(() => link.Simulator!.Config["brightness"]!.GetValue<int>() == 200);
        link.Simulator!.Config["fixed"]!["1_1"]!["name"]!.GetValue<string>().Should().Be("Eagle Rearm");
        link.Simulator.SaveConfigCount.Should().Be(0, "configuration changes stay in RAM (F3, F13)");

        await link.SaveBoardConfigAsync();

        link.Simulator.SaveConfigCount.Should().Be(1);
        link.State.ConfigSaved.Should().BeTrue();
    }

    [Fact]
    public async Task Board_log_entries_are_collected()
    {
        using var link = Start(HellboardConnectionMode.Simulator);
        await WaitFor(() => link.State.Status == HellboardStatus.Connected && link.Log.Count > 0);
        link.Log[0].Message.Should().Be("Simulator started");

        link.Simulator!.Log("warn", "Display not available");

        await WaitFor(() => link.Log.Any(e => e is { Level: "warn", Message: "Display not available" }));
    }

    [Fact]
    public async Task Board_without_protocol_is_reported_and_the_port_released()
    {
        var opened = new List<SimulatedHellboard>();
        using var link = new HellboardLink(
            new HellboardOptions { Mode = HellboardConnectionMode.Serial },
            new MultiFunctionOptions(),
            Catalog,
            NullLogger<HellboardLink>.Instance,
            new FakeDiscovery("COM8"),
            _ =>
            {
                var oldFirmware = new SimulatedHellboard { Silent = true };
                opened.Add(oldFirmware);
                return oldFirmware;
            });
        link.Start();

        await WaitFor(() => link.State.Status == HellboardStatus.NoProtocol, TimeSpan.FromSeconds(10));
        link.State.Port.Should().Be("COM8");
        link.State.Message.Should().Contain("firmware");
    }

    /// <summary>
    /// The old firmware never reads the port: every ping stays in its 256-byte USB buffer, and once it is
    /// full the firmware updater cannot stop the firmware any more ("semaphore timeout").
    /// </summary>
    [Fact]
    public async Task An_old_firmware_is_asked_only_once_until_it_is_plugged_in_again()
    {
        var discovery = new SwitchableDiscovery { Ports = ["COM8"] };
        var written = new List<string>();
        using var link = new HellboardLink(
            new HellboardOptions { Mode = HellboardConnectionMode.Serial },
            new MultiFunctionOptions(),
            Catalog,
            NullLogger<HellboardLink>.Instance,
            discovery,
            _ => new RecordingSilentConnection(written));
        link.Start();

        await WaitFor(() => link.State.Status == HellboardStatus.NoProtocol, TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);   // more than two search intervals
        written.Should().ContainSingle("one ping, sent once, and no further checks while the board stays plugged in");

        discovery.Ports = [];
        await WaitFor(() => link.State.Status == HellboardStatus.NotFound);
        discovery.Ports = ["COM8"];
        await WaitFor(() => written.Count == 2, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Blocking_write_of_an_old_firmware_releases_the_port()
    {
        var opened = new List<BlockingConnection>();
        using var link = new HellboardLink(
            new HellboardOptions { Mode = HellboardConnectionMode.Serial },
            new MultiFunctionOptions(),
            Catalog,
            NullLogger<HellboardLink>.Instance,
            new FakeDiscovery("COM8"),
            _ =>
            {
                var connection = new BlockingConnection();
                opened.Add(connection);
                return connection;
            });
        link.Start();

        await WaitFor(() => link.State.Status == HellboardStatus.NoProtocol);
        opened.Should().ContainSingle().Which.Disposed.Should().BeTrue("the port must be released for other tools");
    }

    [Fact]
    public async Task Switching_from_dry_run_to_the_simulator_connects_and_sends()
    {
        var options = new HellboardOptions { Mode = HellboardConnectionMode.DryRun };
        using var link = new HellboardLink(options, new MultiFunctionOptions(), Catalog, NullLogger<HellboardLink>.Instance);
        link.Start();
        link.Submit(LoadoutA);
        await WaitFor(() => link.State.LastSent is not null);

        options.Mode = HellboardConnectionMode.Simulator;
        link.ApplySettings();

        await WaitFor(() => link.State.Status == HellboardStatus.Connected && link.Simulator?.SetLoadoutCount == 1);
    }

    [Fact]
    public async Task Changed_multi_function_list_is_sent_again()
    {
        var multiFunction = new MultiFunctionOptions();
        using var link = new HellboardLink(new HellboardOptions { Mode = HellboardConnectionMode.Simulator }, multiFunction, Catalog, NullLogger<HellboardLink>.Instance);
        link.Start();
        // Wait for the loadout sent on connect first: submitting before it made the count race (flaky test).
        await WaitFor(() => link.State.Status == HellboardStatus.Connected && link.Simulator?.SetLoadoutCount == 1);
        link.Submit(LoadoutA);
        await WaitFor(() => link.Simulator!.SetLoadoutCount == 2);

        multiFunction.FixedList = ["sos_beacon", "eagle_rearm"];
        link.ApplySettings();

        await WaitFor(() => link.Simulator!.SetLoadoutCount == 3);
        link.Simulator!.MissionStratagems!.AsArray().Should().HaveCount(2);
    }

    [Fact]
    public async Task Dry_run_builds_the_loadout_without_a_board()
    {
        using var link = Start(HellboardConnectionMode.DryRun);

        link.Submit(LoadoutA);

        await WaitFor(() => link.State.LastSent is not null);
        link.State.Status.Should().Be(HellboardStatus.DryRun);
        link.Simulator.Should().BeNull();
    }

    private static HellboardLink Start(HellboardConnectionMode mode)
    {
        var link = new HellboardLink(new HellboardOptions { Mode = mode }, new MultiFunctionOptions(), Catalog, NullLogger<HellboardLink>.Instance);
        link.Start();
        return link;
    }

    private static LockedLoadout Locked(params string[] stratagems) =>
        new(DateTimeOffset.Now, stratagems, [], LockSource.Ready, true);

    private static async Task WaitFor(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition not met in time.");
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Like the old firmware: it does not read the port, so writing times out.</summary>
    private sealed class BlockingConnection : IHellboardConnection
    {
        public bool Disposed { get; private set; }

        public string Description => "COM8";

        public event EventHandler<string>? LineReceived
        {
            add { }
            remove { }
        }

        public event EventHandler? Closed
        {
            add { }
            remove { }
        }

        public void WriteLine(string line) => throw new IOException("The semaphore timeout period has expired.");

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeDiscovery(params string[] ports) : ISerialPortDiscovery
    {
        public IReadOnlyList<string> FindPorts(string usbVendorId) => ports;
    }

    /// <summary>Board can be "unplugged" and "plugged in" again.</summary>
    private sealed class SwitchableDiscovery : ISerialPortDiscovery
    {
        public volatile string[] Ports = [];

        public IReadOnlyList<string> FindPorts(string usbVendorId) => Ports;
    }

    /// <summary>Like the old firmware: takes what is written (into its buffer) and never answers.</summary>
    private sealed class RecordingSilentConnection(List<string> written) : IHellboardConnection
    {
        public string Description => "COM8";

        public event EventHandler<string>? LineReceived
        {
            add { }
            remove { }
        }

        public event EventHandler? Closed
        {
            add { }
            remove { }
        }

        public void WriteLine(string line)
        {
            lock (written)
            {
                written.Add(line);
            }
        }

        public void Dispose()
        {
        }
    }
}
