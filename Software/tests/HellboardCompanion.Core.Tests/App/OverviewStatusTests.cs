// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.App.ViewModels;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Platform;
using HellboardCompanion.Core.Tests.TestData;

namespace HellboardCompanion.Core.Tests.App;

public sealed class OverviewStatusTests
{
    private static readonly HellboardInfo Firmware200 = new("hellboard", "2.0.0", HellboardClient.ProtocolVersion, "1.24");

    public static TheoryData<string, StatusLine> AllLines()
    {
        var data = new TheoryData<string, StatusLine>();
        void Add(string name, StatusLine line) => data.Add(name, line);

        var running = FakeGameLocator.Running(2560, 1440).Current;
        Add("game running", OverviewStatus.Game(running));
        Add("game minimized", OverviewStatus.Game(running with { Location = running.Location! with { IsMinimized = true } }));
        Add("game starting", OverviewStatus.Game(GameSnapshot.Starting));
        Add("game not running", OverviewStatus.Game(GameSnapshot.NotRunning));

        Add("selecting", OverviewStatus.Recognition(TrackerState.Selecting, null, true));
        Add("locked", OverviewStatus.Recognition(TrackerState.Locked, LockSource.ScreenExit, true));
        Add("waiting for game", OverviewStatus.Recognition(TrackerState.Idle, null, false));
        Add("waiting for screen", OverviewStatus.Recognition(TrackerState.Idle, null, true));

        HellboardState Board(HellboardStatus status, HellboardInfo? info = null, string? message = null, string port = "COM8") =>
            new(status, port, info, null, null, null, [], message);
        Add("no board", OverviewStatus.Hellboard(null, "2.1.1"));
        Add("connected", OverviewStatus.Hellboard(Board(HellboardStatus.Connected, Firmware200), "2.0.0"));
        Add("outdated", OverviewStatus.Hellboard(Board(HellboardStatus.Connected, Firmware200), "2.1.1"));
        Add("simulator", OverviewStatus.Hellboard(Board(HellboardStatus.Connected, Firmware200, port: "Simulator"), "2.1.1"));
        Add("port in use", OverviewStatus.Hellboard(Board(HellboardStatus.NoProtocol, message: "COM8 is in use by another program"), "2.1.1"));
        Add("newer protocol", OverviewStatus.Hellboard(Board(HellboardStatus.NoProtocol, Firmware200 with { Protocol = HellboardClient.ProtocolVersion + 1 }), "2.1.1"));
        Add("incompatible", OverviewStatus.Hellboard(Board(HellboardStatus.NoProtocol, message: "no answer"), "2.1.1"));
        Add("updating", OverviewStatus.Hellboard(Board(HellboardStatus.Updating), "2.1.1"));
        Add("dry run", OverviewStatus.Hellboard(Board(HellboardStatus.DryRun), "2.1.1"));
        Add("not found", OverviewStatus.Hellboard(Board(HellboardStatus.NotFound), "2.1.1"));
        return data;
    }

    [Theory]
    [MemberData(nameof(AllLines))]
    public void Every_short_text_fits_on_one_line(string state, StatusLine line)
    {
        line.Text.Length.Should().BeLessThanOrEqualTo(StatusLine.MaxTextLength, state);
    }

    [Fact]
    public void Details_carry_what_used_to_make_the_text_long()
    {
        var running = OverviewStatus.Game(FakeGameLocator.Running(2560, 1440).Current);
        running.Should().Be(new StatusLine("Helldivers 2 running", "DISPLAY1 · 2560×1440", StatusLevel.Good));

        var outdated = OverviewStatus.Hellboard(new HellboardState(HellboardStatus.Connected, "COM8", Firmware200, null, null, null, [], null), "2.1.1");
        outdated.Should().Be(new StatusLine("Firmware outdated", "COM8 · 2.0.0 → 2.1.1, update on the Hellboard tab", StatusLevel.Attention));
        outdated.FullText.Should().Contain("Firmware outdated").And.Contain("2.1.1");

        OverviewStatus.Recognition(TrackerState.Locked, LockSource.Ready, true).Should().Be(new StatusLine("Loadout confirmed", "Ready", StatusLevel.Good));
    }

    [Theory]
    [InlineData(MultiFunctionMode.FixedListAndAutoDetect, "Fixed list: sos_beacon, eagle_rearm | Detected: hellbomb")]
    [InlineData(MultiFunctionMode.FixedList, "Fixed list: sos_beacon, eagle_rearm")]
    [InlineData(MultiFunctionMode.AutoDetect, "Detected: hellbomb")]
    public void The_multi_function_list_is_grouped_by_origin(MultiFunctionMode mode, string expected)
    {
        var options = new MultiFunctionOptions { Mode = mode, FixedList = ["sos_beacon", "eagle_rearm"] };
        string[] board = mode switch
        {
            MultiFunctionMode.FixedList => ["sos_beacon", "eagle_rearm"],
            MultiFunctionMode.AutoDetect => ["hellbomb"],
            _ => ["sos_beacon", "eagle_rearm", "hellbomb"],
        };

        Describe(MultiFunctionGroups.Split(board, id => id, options)).Should().Be(expected);
    }

    [Fact]
    public void A_detected_entry_that_is_also_in_the_fixed_list_counts_as_fixed()
    {
        // LoadoutTransferBuilder keeps the first occurrence: SOS Beacon from the fixed list.
        var options = new MultiFunctionOptions { Mode = MultiFunctionMode.FixedListAndAutoDetect, FixedList = ["sos_beacon"] };

        Describe(MultiFunctionGroups.Split(["sos_beacon", "hellbomb"], id => id, options)).Should().Be("Fixed list: sos_beacon | Detected: hellbomb");
    }

    [Fact]
    public void Empty_groups_are_not_shown()
    {
        var options = new MultiFunctionOptions { Mode = MultiFunctionMode.FixedListAndAutoDetect, FixedList = ["sos_beacon"] };

        MultiFunctionGroups.Split(Array.Empty<string>(), id => id, options).Should().BeEmpty();
        Describe(MultiFunctionGroups.Split(["sos_beacon"], id => id, options)).Should().Be("Fixed list: sos_beacon");
    }

    private static string Describe(IReadOnlyList<(string Title, IReadOnlyList<string> Entries)> groups) =>
        string.Join(" | ", groups.Select(g => $"{g.Title}: {string.Join(", ", g.Entries)}"));
}
