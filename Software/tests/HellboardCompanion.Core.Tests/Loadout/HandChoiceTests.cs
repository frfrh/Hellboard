// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Loadout;

namespace HellboardCompanion.Core.Tests.Loadout;

public sealed class HandChoiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 14, 3, 12, TimeSpan.FromHours(2));
    private static readonly string?[] Shown = ["eagle_500kg_bomb", "grenade_launcher", "supply_pack", "mortar_sentry"];

    [Fact]
    public void Sets_one_slot_and_keeps_the_rest()
    {
        var loadout = HandChoice.Apply(Shown, null, ["hellbomb"], 1, "flamethrower", Now);

        loadout.Stratagems.Should().Equal("eagle_500kg_bomb", "flamethrower", "supply_pack", "mortar_sentry");
        loadout.ByHand.Should().Equal(false, true, false, false);
        loadout.MissionStratagems.Should().Equal("hellbomb");
        loadout.Source.Should().Be(LockSource.ByHand);
        loadout.Timestamp.Should().Be(Now);
        loadout.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Earlier_hand_marks_are_kept()
    {
        var loadout = HandChoice.Apply(Shown, [true, false, false, false], [], 3, "orbital_laser", Now);

        loadout.ByHand.Should().Equal(true, false, false, true);
    }

    [Fact]
    public void Stratagem_from_another_slot_swaps_the_two_slots()
    {
        var loadout = HandChoice.Apply(Shown, null, [], 0, "supply_pack", Now);

        loadout.Stratagems.Should().Equal("supply_pack", "grenade_launcher", "eagle_500kg_bomb", "mortar_sentry");
        loadout.ByHand.Should().Equal(true, false, true, false);
    }

    [Fact]
    public void Empty_slot_can_be_set_and_a_slot_can_be_emptied()
    {
        string?[] shown = ["eagle_500kg_bomb", null, null, null];

        HandChoice.Apply(shown, null, [], 2, "flamethrower", Now).Stratagems.Should().Equal("eagle_500kg_bomb", null, "flamethrower", null);
        HandChoice.Apply(shown, null, [], 0, null, Now).Stratagems.Should().Equal(null, null, null, null);
    }

    [Fact]
    public void Remaining_unrecognized_slot_makes_the_loadout_incomplete()
    {
        string?[] shown = ["eagle_500kg_bomb", HandChoice.NotRecognized, HandChoice.NotRecognized, null];

        var partly = HandChoice.Apply(shown, null, [], 1, "flamethrower", Now);
        partly.Stratagems.Should().Equal("eagle_500kg_bomb", "flamethrower", null, null);
        partly.IsComplete.Should().BeFalse();

        var all = HandChoice.Apply(["eagle_500kg_bomb", HandChoice.NotRecognized, null, null], null, [], 1, "flamethrower", Now);
        all.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Recently_used_lists_each_player_stratagem_once_newest_first()
    {
        LockedLoadout[] history =
        [
            new(Now, ["orbital_laser", "flamethrower", null, null], ["hellbomb"], LockSource.Ready, true),
            new(Now, ["flamethrower", "supply_pack", "sos_beacon", null], [], LockSource.Ready, true),
        ];

        HandChoice.RecentlyUsed(history, new HashSet<string> { "sos_beacon" })
            .Should().Equal("orbital_laser", "flamethrower", "supply_pack");
        HandChoice.RecentlyUsed(history, new HashSet<string>(), maxCount: 2).Should().Equal("orbital_laser", "flamethrower");
    }
}
