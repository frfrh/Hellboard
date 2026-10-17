// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Hellboard;

namespace HellboardCompanion.Core.Tests.Hellboard;

public sealed class HellboardClientTests
{
    private static readonly StratagemDefinition Bomb = new("Eagle 500KG Bomb", "red", "URDDD");

    [Fact]
    public async Task Ping_reports_the_firmware()
    {
        using var client = new HellboardClient(new SimulatedHellboard());

        var info = await client.PingAsync(TestContext.Current.CancellationToken);

        info.Protocol.Should().Be(1);
        info.Firmware.Should().Be(SimulatedHellboard.FirmwareVersion);
    }

    [Fact]
    public async Task Loadout_is_transferred()
    {
        var board = new SimulatedHellboard();
        using var client = new HellboardClient(board);

        await client.SetLoadoutAsync(new HellboardLoadout([Bomb, null, null, null], [Bomb]), TestContext.Current.CancellationToken);

        board.Stratagems![0]!["name"]!.GetValue<string>().Should().Be("Eagle 500KG Bomb");
        board.SetLoadoutCount.Should().Be(1);
    }

    [Fact]
    public async Task Board_errors_become_exceptions()
    {
        using var client = new HellboardClient(new SimulatedHellboard());

        var send = () => client.SetLoadoutAsync(new HellboardLoadout([new StratagemDefinition("Broken", "red", "XYZ"), null, null, null], []), TestContext.Current.CancellationToken);

        (await send.Should().ThrowAsync<HellboardException>()).Which.Code.Should().Be("invalid_loadout");
    }

    [Fact]
    public async Task Lost_message_is_repeated_with_the_same_id()
    {
        var board = new LossyConnection(new SimulatedHellboard(), dropFirst: 1);
        using var client = new HellboardClient(board, TimeSpan.FromMilliseconds(100));

        await client.SetLoadoutAsync(new HellboardLoadout([Bomb, null, null, null], []), TestContext.Current.CancellationToken);

        board.Sent.Should().HaveCount(2);
        board.Sent[1].Should().Be(board.Sent[0]);
    }

    [Fact]
    public async Task Silent_board_times_out()
    {
        using var client = new HellboardClient(new SimulatedHellboard { Silent = true }, TimeSpan.FromMilliseconds(50), retries: 1);

        var ping = () => client.PingAsync(TestContext.Current.CancellationToken);

        await ping.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task A_new_connection_is_not_answered_with_results_of_an_earlier_one()
    {
        var board = new SimulatedHellboard();
        using (var first = new HellboardClient(board))
        {
            await first.SetConfigAsync(new System.Text.Json.Nodes.JsonObject { ["brightness"] = 10 }, TestContext.Current.CancellationToken);
        }

        using var second = new HellboardClient(board);
        await second.SetConfigAsync(new System.Text.Json.Nodes.JsonObject { ["brightness"] = 99 }, TestContext.Current.CancellationToken);

        board.Config["brightness"]!.GetValue<int>().Should().Be(99, "the request must be executed, not answered from the first connection");
    }

    [Fact]
    public async Task Non_protocol_output_is_ignored()
    {
        var board = new SimulatedHellboard();
        using var client = new HellboardClient(board);
        var events = new List<string>();
        client.EventReceived += (_, e) => events.Add(e["event"]!.GetValue<string>());

        board.Restart();   // sends the hello event
        await client.PingAsync(TestContext.Current.CancellationToken);

        events.Should().Equal("hello");
    }

    /// <summary>Drops the first outgoing lines, like a board that was busy.</summary>
    private sealed class LossyConnection(SimulatedHellboard inner, int dropFirst) : IHellboardConnection
    {
        private int _dropped;

        public List<string> Sent { get; } = [];

        public string Description => "Lossy";

        public event EventHandler<string>? LineReceived
        {
            add => inner.LineReceived += value;
            remove => inner.LineReceived -= value;
        }

        public event EventHandler? Closed
        {
            add => inner.Closed += value;
            remove => inner.Closed -= value;
        }

        public void WriteLine(string line)
        {
            Sent.Add(line);
            if (_dropped++ >= dropFirst)
            {
                inner.WriteLine(line);
            }
        }

        public void Dispose()
        {
        }
    }
}
