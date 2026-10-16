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
        await WaitFor(() => link.State.Status == HellboardStatus.Connected);

        link.Submit(LoadoutA);

        await WaitFor(() => link.Simulator!.SetLoadoutCount == 1);
        link.Simulator!.Stratagems![3]!["name"]!.GetValue<string>().Should().Be("Mortar Sentry");
        link.Simulator.MissionStratagems!.AsArray().Select(s => s!["name"]!.GetValue<string>()).Should().Equal("SOS Beacon");
        link.State.LastSent!.Stratagems[0]!.Name.Should().Be("Eagle 500KG Bomb");
    }

    [Fact]
    public async Task Same_loadout_is_not_sent_twice_but_a_changed_one_is()
    {
        using var link = Start(HellboardConnectionMode.Simulator);
        await WaitFor(() => link.State.Status == HellboardStatus.Connected);

        link.Submit(LoadoutA);
        await WaitFor(() => link.Simulator!.SetLoadoutCount == 1);
        link.Submit(LoadoutA with { Timestamp = DateTimeOffset.Now, Source = LockSource.Manual });
        link.Submit(LoadoutB);

        await WaitFor(() => link.Simulator!.SetLoadoutCount == 2);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        link.Simulator!.SetLoadoutCount.Should().Be(2);
    }

    [Fact]
    public async Task Restarted_board_gets_the_loadout_again()
    {
        using var link = Start(HellboardConnectionMode.Simulator);
        await WaitFor(() => link.State.Status == HellboardStatus.Connected);
        link.Submit(LoadoutA);
        await WaitFor(() => link.Simulator!.SetLoadoutCount == 1);

        link.Simulator!.Restart();

        await WaitFor(() => link.Simulator.SetLoadoutCount == 2);
    }

    [Fact]
    public async Task Flash_is_written_only_on_set_as_default()
    {
        using var link = Start(HellboardConnectionMode.Simulator);
        await WaitFor(() => link.State.Status == HellboardStatus.Connected);
        link.Submit(LoadoutA);
        link.Submit(LoadoutB);
        await WaitFor(() => link.Simulator!.SetLoadoutCount == 2);
        link.Simulator!.SaveCount.Should().Be(0, "transfers must not write the flash (F3)");

        await link.SetAsDefaultAsync();

        link.Simulator.SaveCount.Should().Be(1);
        link.Simulator.SavedDefault.Should().Contain("Eagle Strafing Run");
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
}
