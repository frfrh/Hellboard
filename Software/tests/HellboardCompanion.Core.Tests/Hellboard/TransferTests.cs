// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Loadout;

namespace HellboardCompanion.Core.Tests.Hellboard;

public sealed class TransferTests
{
    private static readonly StratagemCatalog Catalog = StratagemCatalog.LoadSeed();

    [Fact]
    public void Seed_catalog_contains_the_firmware_sequences()
    {
        Catalog.Entries.Should().HaveCountGreaterThanOrEqualTo(100);
        var bomb = Catalog.Find("eagle_500kg_bomb")!;
        bomb.Name.Should().Be("Eagle 500KG Bomb");
        bomb.Color.Should().Be("red");
        bomb.Sequence.Should().Be("URDDD");
        Catalog.Find("hellbomb")!.Sequence.Should().Be("DULDURDU");
        Catalog.Entries.Should().OnlyContain(e => e.Sequence == null || e.Sequence.All(c => "UDLR".Contains(c)));
    }

    [Fact]
    public void Full_names_are_transferred_without_shortening()
    {
        var transfer = LoadoutTransferBuilder.Build(Locked(["sta_x3_w_a_s_p_launcher", null, null, null], []), Catalog, new MultiFunctionOptions());

        transfer.Loadout.Stratagems[0]!.Name.Should().Be("StA-X3 W.A.S.P. Launcher");
        transfer.Loadout.Stratagems[0]!.Name.Length.Should().BeGreaterThan(16);
    }

    [Fact]
    public void Empty_and_unknown_slots_are_sent_as_null_and_reported()
    {
        var transfer = LoadoutTransferBuilder.Build(
            Locked(["eagle_500kg_bomb", null, "td_110_maelstrom", "not_in_catalog"], []), Catalog, new MultiFunctionOptions());

        transfer.Loadout.Stratagems.Select(s => s?.Name).Should().Equal("Eagle 500KG Bomb", null, null, null);
        transfer.NotTransferable.Should().Equal("td_110_maelstrom", "not_in_catalog");
    }

    [Theory]
    [InlineData(MultiFunctionMode.FixedList, new[] { "SOS Beacon", "Eagle Rearm" })]
    [InlineData(MultiFunctionMode.AutoDetect, new[] { "Hellbomb", "SOS Beacon" })]
    [InlineData(MultiFunctionMode.FixedListAndAutoDetect, new[] { "SOS Beacon", "Eagle Rearm", "Hellbomb" })]
    public void Multi_function_list_follows_the_selected_mode(MultiFunctionMode mode, string[] expected)
    {
        var options = new MultiFunctionOptions { Mode = mode, FixedList = ["sos_beacon", "eagle_rearm"] };

        var transfer = LoadoutTransferBuilder.Build(Locked([null, null, null, null], ["hellbomb", "sos_beacon"]), Catalog, options);

        transfer.Loadout.MissionStratagems.Select(s => s.Name).Should().Equal(expected);
    }

    private static LockedLoadout Locked(string?[] stratagems, string[] mission) =>
        new(DateTimeOffset.Now, stratagems, mission, LockSource.Ready, true);
}
