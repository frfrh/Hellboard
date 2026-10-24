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
    public void Hand_choice_keeps_what_the_recognition_had_in_every_slot()
    {
        var first = HandChoice.Apply(Shown, null, [], 1, "flamethrower", Now);
        first.Recognized.Should().Equal(Shown);

        // The second choice starts from the first one: slot 2 still remembers the grenade launcher.
        var second = HandChoice.Apply(first.Stratagems, first.ByHand, [], 3, "orbital_laser", Now, first.Recognized);
        second.Recognized.Should().Equal(Shown);
        second.ByHand.Should().Equal(false, true, false, true);
    }

    [Fact]
    public void Unrecognized_slot_has_nothing_to_go_back_to()
    {
        string?[] shown = ["eagle_500kg_bomb", HandChoice.NotRecognized, null, null];

        var loadout = HandChoice.Apply(shown, null, [], 1, "flamethrower", Now);

        loadout.Recognized.Should().Equal("eagle_500kg_bomb", HandChoice.NotRecognized, null, null);
        HandChoice.TryGetRecognized(loadout.ByHand, loadout.Recognized, 1, out _).Should().BeFalse();
        HandChoice.Reset(loadout.Stratagems, loadout.ByHand, loadout.Recognized, [], 1, Now).Should().BeNull();
    }

    [Fact]
    public void Reset_sets_the_slot_back_and_keeps_the_other_hand_choices()
    {
        var first = HandChoice.Apply(Shown, null, ["hellbomb"], 1, "flamethrower", Now);
        var second = HandChoice.Apply(first.Stratagems, first.ByHand, first.MissionStratagems, 3, "orbital_laser", Now, first.Recognized);

        HandChoice.TryGetRecognized(second.ByHand, second.Recognized, 1, out var recognized).Should().BeTrue();
        recognized.Should().Be("grenade_launcher");
        var reset = HandChoice.Reset(second.Stratagems, second.ByHand, second.Recognized, second.MissionStratagems, 1, Now)!;

        reset.Stratagems.Should().Equal("eagle_500kg_bomb", "grenade_launcher", "supply_pack", "orbital_laser");
        reset.ByHand.Should().Equal(false, false, false, true);
        reset.MissionStratagems.Should().Equal("hellbomb");
        reset.Source.Should().Be(LockSource.ByHand);
    }

    [Fact]
    public void Resetting_the_last_hand_choice_leaves_no_hand_marks()
    {
        var choice = HandChoice.Apply(Shown, null, [], 1, "flamethrower", Now);

        var reset = HandChoice.Reset(choice.Stratagems, choice.ByHand, choice.Recognized, [], 1, Now)!;

        reset.Stratagems.Should().Equal(Shown);
        reset.ByHand.Should().BeNull();
        reset.Recognized.Should().BeNull();
    }

    [Fact]
    public void Reset_after_a_swap_swaps_back()
    {
        var swapped = HandChoice.Apply(Shown, null, [], 0, "supply_pack", Now);

        var reset = HandChoice.Reset(swapped.Stratagems, swapped.ByHand, swapped.Recognized, [], 0, Now)!;

        reset.Stratagems.Should().Equal(Shown);
        reset.ByHand.Should().BeNull();
    }

    [Fact]
    public void Reset_to_an_empty_slot_and_of_a_slot_not_set_by_hand()
    {
        string?[] shown = ["eagle_500kg_bomb", null, null, null];
        var choice = HandChoice.Apply(shown, null, [], 2, "flamethrower", Now);

        HandChoice.Reset(choice.Stratagems, choice.ByHand, choice.Recognized, [], 2, Now)!.Stratagems.Should().Equal(shown);
        HandChoice.Reset(choice.Stratagems, choice.ByHand, choice.Recognized, [], 0, Now).Should().BeNull();
    }

    [Fact]
    public void Hand_choice_of_version_0_10_without_recognized_content_cannot_be_reset()
    {
        // Written by 0.9/0.10: hand marks, but no recognized content.
        var loadout = HandChoice.Apply(Shown, [true, false, false, false], [], 3, "orbital_laser", Now);

        loadout.Recognized.Should().Equal(HandChoice.NotRecognized, "grenade_launcher", "supply_pack", "mortar_sentry");
        HandChoice.Reset(loadout.Stratagems, loadout.ByHand, loadout.Recognized, [], 0, Now).Should().BeNull();
        HandChoice.Reset(loadout.Stratagems, loadout.ByHand, loadout.Recognized, [], 3, Now).Should().NotBeNull();
    }

    [Fact]
    public void Choosing_the_recognized_stratagem_again_is_no_hand_choice()
    {
        var choice = HandChoice.Apply(Shown, null, [], 1, "flamethrower", Now);

        var back = HandChoice.Apply(choice.Stratagems, choice.ByHand, [], 1, "grenade_launcher", Now, choice.Recognized);

        back.ByHand.Should().BeNull();
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
