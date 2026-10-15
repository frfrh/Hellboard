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
