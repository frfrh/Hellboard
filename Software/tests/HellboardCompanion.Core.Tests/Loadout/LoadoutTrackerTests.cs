// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Recognition;

namespace HellboardCompanion.Core.Tests.Loadout;

public sealed class LoadoutTrackerTests
{
    private static readonly LoadoutSnapshot LoadoutA = Snapshot("eagle_500kg_bomb", "grenade_launcher", "supply_pack", "mortar_sentry");
    private static readonly LoadoutSnapshot LoadoutB = Snapshot("eagle_strafing_run", "anti_materiel_rifle", "directional_shield", "machine_gun_sentry");

    private readonly LoadoutTracker _tracker = new();
    private readonly List<LockedLoadout> _locked = [];
    private DateTimeOffset _time = DateTimeOffset.UnixEpoch;

    public LoadoutTrackerTests() => _tracker.Locked += (_, l) => _locked.Add(l);

    [Fact]
    public void Prepare_then_ready_locks_in_the_stable_loadout()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 2);

        _locked.Should().ContainSingle();
        _locked[0].Source.Should().Be(LockSource.Ready);
        _locked[0].Stratagems.Should().Equal("eagle_500kg_bomb", "grenade_launcher", "supply_pack", "mortar_sentry");
        _tracker.State.Should().Be(TrackerState.Locked);
    }

    [Fact]
    public void Single_ready_frame_is_debounced()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 1);
        Feed(LoadoutA, ReadyStatus.Prepare, 3);

        _locked.Should().BeEmpty();
    }

    [Fact]
    public void Last_player_confirming_closes_the_screen_before_ready_is_seen()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        FeedNoScreen(2);

        _locked.Should().ContainSingle().Which.Source.Should().Be(LockSource.ScreenExit);
        _locked[0].Stratagems.Should().Equal(LockedLoadout.From(LoadoutA, _time, LockSource.ScreenExit).Stratagems);
        _tracker.State.Should().Be(TrackerState.Idle);
    }

    [Fact]
    public void Withdrawing_ready_allows_a_changed_loadout_to_be_locked_again()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 3);
        Feed(LoadoutA, ReadyStatus.Prepare, 3);
        Feed(LoadoutB, ReadyStatus.Prepare, 5);
        Feed(LoadoutB, ReadyStatus.Ready, 3);

        _locked.Select(l => l.Stratagems[0]).Should().Equal("eagle_500kg_bomb", "eagle_strafing_run");
        _locked[1].HasSameContent(_locked[0]).Should().BeFalse();
    }

    [Fact]
    public void Withdrawing_and_confirming_again_without_change_locks_the_same_content()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 3);
        Feed(LoadoutA, ReadyStatus.Prepare, 3);
        Feed(LoadoutA, ReadyStatus.Ready, 3);

        _locked.Should().HaveCount(2);
        _locked[1].HasSameContent(_locked[0]).Should().BeTrue();
    }

    [Fact]
    public void Leaving_the_screen_after_ready_does_not_lock_twice()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 3);
        FeedNoScreen(3);

        _locked.Should().ContainSingle();
        _tracker.State.Should().Be(TrackerState.Idle);
    }

    [Fact]
    public void Changing_slots_are_not_locked_before_they_are_stable()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutB, ReadyStatus.Prepare, 2);   // not yet stable
        FeedNoScreen(2);

        _locked.Should().ContainSingle().Which.Stratagems[0].Should().Be("eagle_500kg_bomb");
    }

    [Fact]
    public void Joining_while_already_ready_locks_as_soon_as_the_loadout_is_stable()
    {
        Feed(LoadoutB, ReadyStatus.Ready, 2);
        _locked.Should().BeEmpty();

        Feed(LoadoutB, ReadyStatus.Ready, 1);
        _locked.Should().ContainSingle().Which.Source.Should().Be(LockSource.Ready);
    }

    [Fact]
    public void Short_detection_gap_does_not_end_the_session()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        FeedNoScreen(1);
        Feed(LoadoutA, ReadyStatus.Prepare, 2);

        _locked.Should().BeEmpty();
        _tracker.State.Should().Be(TrackerState.Selecting);
    }

    [Fact]
    public void Unrecognized_slot_is_locked_as_incomplete()
    {
        var partial = new LoadoutSnapshot(
            [new(SlotState.Recognized, "eagle_500kg_bomb"), new(SlotState.Unknown, null), SlotValue.Empty, SlotValue.Empty],
            [SlotValue.Empty, SlotValue.Empty, SlotValue.Empty, SlotValue.Empty]);

        Feed(partial, ReadyStatus.Prepare, 3);
        Feed(partial, ReadyStatus.Ready, 2);

        _locked.Should().ContainSingle().Which.IsComplete.Should().BeFalse();
        _locked[0].Stratagems.Should().Equal("eagle_500kg_bomb", null, null, null);
    }

    [Fact]
    public void Mission_stratagems_are_reported_without_empty_slots()
    {
        var withHellbomb = LoadoutB with { MissionSlots = [new(SlotState.Recognized, "hellbomb"), SlotValue.Empty, SlotValue.Empty, SlotValue.Empty] };

        Feed(withHellbomb, ReadyStatus.Prepare, 3);
        Feed(withHellbomb, ReadyStatus.Ready, 2);

        _locked.Should().ContainSingle().Which.MissionStratagems.Should().Equal("hellbomb");
    }

    // ---- Hand choice (manual-selection plan §3.2) --------------------------------------------

    [Fact]
    public void Hand_choice_without_any_recognition_is_locked_in()
    {
        _tracker.LockByHand(ByHand(LoadoutA, 0, "eagle_airstrike"));

        _locked.Should().ContainSingle().Which.Source.Should().Be(LockSource.ByHand);
        _locked[0].Stratagems[0].Should().Be("eagle_airstrike");
        _locked[0].IsSetByHand(0).Should().BeTrue();
        _locked[0].IsSetByHand(1).Should().BeFalse();
        _tracker.LastLocked.Should().BeSameAs(_locked[0]);
        _tracker.LastRecognized.Should().BeNull();
    }

    [Fact]
    public void Correction_after_ready_is_not_undone_when_the_screen_closes()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 2);
        _tracker.LockByHand(ByHand(LoadoutA, 1, "flamethrower"));
        Feed(LoadoutA, ReadyStatus.Ready, 2);
        FeedNoScreen(2);

        _locked.Select(l => l.Source).Should().Equal(LockSource.Ready, LockSource.ByHand);
        _tracker.LastLocked!.Stratagems[1].Should().Be("flamethrower");
    }

    [Fact]
    public void Correction_on_the_loadout_screen_survives_ready_with_the_same_candidate()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        _tracker.LockByHand(ByHand(LoadoutA, 1, "flamethrower"));
        Feed(LoadoutA, ReadyStatus.Ready, 2);

        _locked.Should().ContainSingle().Which.Source.Should().Be(LockSource.ByHand);
        _tracker.State.Should().Be(TrackerState.Locked);
        _tracker.LastRecognized!.Source.Should().Be(LockSource.Ready);
        _tracker.LastLocked!.Stratagems[1].Should().Be("flamethrower");
    }

    [Fact]
    public void Same_misrecognition_in_the_next_mission_keeps_the_correction()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 2);
        FeedNoScreen(2);
        _tracker.LockByHand(ByHand(LoadoutA, 1, "flamethrower"));

        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 2);
        FeedNoScreen(2);

        _locked.Select(l => l.Source).Should().Equal(LockSource.Ready, LockSource.ByHand);
    }

    [Fact]
    public void Different_loadout_in_the_game_replaces_the_hand_choice()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 2);
        FeedNoScreen(2);
        _tracker.LockByHand(ByHand(LoadoutA, 1, "flamethrower"));

        Feed(LoadoutB, ReadyStatus.Prepare, 5);
        Feed(LoadoutB, ReadyStatus.Ready, 2);

        _locked.Select(l => l.Source).Should().Equal(LockSource.Ready, LockSource.ByHand, LockSource.Ready);
        _tracker.LastLocked!.HasSameContent(LockedLoadout.From(LoadoutB, _time, LockSource.Ready)).Should().BeTrue();

        // The old correction no longer applies: loadout A comes through again.
        FeedNoScreen(2);
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 2);
        _locked.Should().HaveCount(4);
    }

    [Fact]
    public void Withdrawing_ready_and_changing_the_loadout_replaces_the_hand_choice()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 2);
        _tracker.LockByHand(ByHand(LoadoutA, 1, "flamethrower"));
        Feed(LoadoutA, ReadyStatus.Prepare, 3);
        Feed(LoadoutB, ReadyStatus.Prepare, 5);
        Feed(LoadoutB, ReadyStatus.Ready, 2);

        _locked.Select(l => l.Source).Should().Equal(LockSource.Ready, LockSource.ByHand, LockSource.Ready);
    }

    [Fact]
    public void Screen_exit_while_selecting_with_the_corrected_candidate_is_skipped()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        _tracker.LockByHand(ByHand(LoadoutA, 1, "flamethrower"));
        FeedNoScreen(2);

        _locked.Should().ContainSingle().Which.Source.Should().Be(LockSource.ByHand);
        _tracker.State.Should().Be(TrackerState.Idle);
    }

    [Fact]
    public void Several_hand_choices_keep_the_first_corrected_content()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 2);
        var first = ByHand(LoadoutA, 1, "flamethrower");
        _tracker.LockByHand(first);
        _tracker.LockByHand(first with { Stratagems = ["orbital_laser", "flamethrower", "supply_pack", "mortar_sentry"], ByHand = [true, true, false, false] });
        FeedNoScreen(2);
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 2);

        _locked.Select(l => l.Source).Should().Equal(LockSource.Ready, LockSource.ByHand, LockSource.ByHand);
        _tracker.LastLocked!.Stratagems[0].Should().Be("orbital_laser");
    }

    [Fact]
    public void Manual_trigger_replaces_the_hand_choice_even_with_the_same_content()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 2);
        _tracker.LockByHand(ByHand(LoadoutA, 1, "flamethrower"));
        _tracker.LockManually(LoadoutA, _time);

        _tracker.LastLocked!.Source.Should().Be(LockSource.Manual);
    }

    [Fact]
    public void Live_screen_counts_as_corrected_while_it_shows_the_corrected_content()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        _tracker.IsCorrectedByHand(LoadoutA).Should().BeFalse();

        _tracker.LockByHand(ByHand(LoadoutA, 1, "flamethrower"));

        _tracker.IsCorrectedByHand(LoadoutA).Should().BeTrue();
        _tracker.IsCorrectedByHand(LoadoutB).Should().BeFalse();
    }

    [Fact]
    public void Back_to_recognized_locks_the_last_recognized_loadout_again()
    {
        Feed(LoadoutA, ReadyStatus.Prepare, 5);
        Feed(LoadoutA, ReadyStatus.Ready, 2);
        _tracker.LockByHand(ByHand(LoadoutA, 1, "flamethrower"));

        _tracker.RevertToRecognized().Should().BeTrue();

        _locked.Should().HaveCount(3);
        _locked[2].Should().BeSameAs(_locked[0]);
        _tracker.IsCorrectedByHand(LoadoutA).Should().BeFalse();
        _tracker.RevertToRecognized().Should().BeFalse();
    }

    [Fact]
    public void Back_to_recognized_without_any_recognition_does_nothing()
    {
        _tracker.LockByHand(ByHand(LoadoutA, 0, "eagle_airstrike"));

        _tracker.RevertToRecognized().Should().BeFalse();
        _locked.Should().ContainSingle();
    }

    private LockedLoadout ByHand(LoadoutSnapshot basis, int slot, string id)
    {
        var loadout = LockedLoadout.From(basis, _time, LockSource.ByHand);
        var stratagems = loadout.Stratagems.ToList();
        stratagems[slot] = id;
        var byHand = new bool[stratagems.Count];
        byHand[slot] = true;
        return loadout with { Stratagems = stratagems, ByHand = byHand };
    }

    private static LoadoutSnapshot Snapshot(params string[] player) => new(
        player.Select(id => new SlotValue(SlotState.Recognized, id)).ToList(),
        [SlotValue.Empty, SlotValue.Empty, SlotValue.Empty, SlotValue.Empty]);

    private void Feed(LoadoutSnapshot snapshot, ReadyStatus status, int frames)
    {
        var screen = new LoadoutScreenResult(true, default, 1, 1, 1);
        for (var i = 0; i < frames; i++)
        {
            _time = _time.AddMilliseconds(100);
            _tracker.Process(new LoadoutObservation(_time, screen, status, snapshot, TimeSpan.Zero));
        }
    }

    private void FeedNoScreen(int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            _time = _time.AddMilliseconds(100);
            _tracker.Process(new LoadoutObservation(_time, LoadoutScreenResult.NotVisible, ReadyStatus.Unknown, null, TimeSpan.Zero));
        }
    }
}
