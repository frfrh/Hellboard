// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Tests.Loadout;

public sealed class LoadoutHistoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HellboardCompanionTests", Guid.NewGuid().ToString("N"));
    private readonly Clock _clock = new();
    private readonly LoadoutHistory _history;

    public LoadoutHistoryTests() => _history = new LoadoutHistory(new PlatformPaths(_root, _root), _clock);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Loadouts_and_problems_are_read_back_newest_first()
    {
        _history.Append(new LockedLoadout(DateTimeOffset.Now, ["eagle_500kg_bomb", null, null, null], [], LockSource.Ready, true));
        _history.Append(new RecognitionProblem(DateTimeOffset.Now, ProblemKind.UnrecognizedStratagem, "Slot 2: not recognized", @"C:\x.png", null));

        var items = _history.ReadRecent();

        items.Should().HaveCount(2);
        items[0].Problem!.Kind.Should().Be(ProblemKind.UnrecognizedStratagem);
        items[0].Problem!.ImagePath.Should().Be(@"C:\x.png");
        items[1].Loadout!.Stratagems[0].Should().Be("eagle_500kg_bomb");
        _history.ReadRecentLoadouts().Should().ContainSingle();
    }

    [Fact]
    public void Lines_of_older_versions_without_entry_type_are_loadouts()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_history.FilePath)!);
        File.WriteAllText(_history.FilePath, """{"timestamp":"2026-10-15T22:16:18+02:00","stratagems":["hellbomb",null,null,null],"missionStratagems":[],"source":"manual","isComplete":true}""" + "\n" + "not json\n");

        var items = _history.ReadRecent();

        items.Should().ContainSingle().Which.Loadout!.Source.Should().Be(LockSource.Manual);
    }

    [Fact]
    public void Recording_a_problem_raises_an_event()
    {
        RecognitionProblem? raised = null;
        _history.ProblemRecorded += (_, p) => raised = p;

        _history.Append(new RecognitionProblem(DateTimeOffset.Now, ProblemKind.RecognitionError, "boom", null, null));

        raised!.Message.Should().Be("boom");
    }

    [Fact]
    public void Hand_marks_round_trip_and_are_left_out_for_recognized_loadouts()
    {
        _history.Append(new LockedLoadout(DateTimeOffset.Now, ["hellbomb", null, null, null], [], LockSource.Ready, true));
        _history.Append(new LockedLoadout(DateTimeOffset.Now, ["eagle_airstrike", null, null, null], [], LockSource.ByHand, true, [true, false, false, false]));

        var lines = File.ReadAllLines(_history.FilePath);
        lines[0].Should().NotContain("byHand");
        lines[1].Should().Contain("\"source\":\"byHand\"").And.Contain("\"byHand\":[true,false,false,false]");

        var loadouts = _history.ReadRecentLoadouts();
        loadouts[0].Source.Should().Be(LockSource.ByHand);
        loadouts[0].IsSetByHand(0).Should().BeTrue();
        loadouts[1].ByHand.Should().BeNull();
    }

    [Fact]
    public void Recognized_content_of_a_hand_choice_round_trips()
    {
        _history.Append(new LockedLoadout(
            DateTimeOffset.Now, ["eagle_airstrike", null, null, null], [], LockSource.ByHand, true, [true, false, false, false], ["hellbomb", null, "?", null]));
        _history.Append(new LockedLoadout(DateTimeOffset.Now, ["hellbomb", null, null, null], [], LockSource.Ready, true));

        var lines = File.ReadAllLines(_history.FilePath);
        lines[0].Should().Contain("\"recognized\":[\"hellbomb\",null,\"?\",null]");
        lines[1].Should().NotContain("recognized");

        var loadouts = _history.ReadRecentLoadouts();
        loadouts[1].Recognized.Should().Equal("hellbomb", null, "?", null);
        loadouts[0].Recognized.Should().BeNull();
    }

    [Fact]
    public void Pending_hand_choice_is_shown_at_once_but_written_after_the_delay()
    {
        _history.AppendLater(HandChoice("eagle_airstrike"));
        _clock.Advance(TimeSpan.FromSeconds(10));
        _history.AppendLater(HandChoice("orbital_laser"));
        _clock.Advance(TimeSpan.FromSeconds(25));
        _history.FlushPending();

        File.Exists(_history.FilePath).Should().BeFalse();
        _history.ReadRecentLoadouts().Should().ContainSingle().Which.Stratagems[0].Should().Be("orbital_laser");

        _clock.Advance(TimeSpan.FromSeconds(5));
        _history.FlushPending();

        File.ReadAllLines(_history.FilePath).Should().ContainSingle().Which.Should().Contain("orbital_laser");
        _history.ReadRecentLoadouts().Should().ContainSingle();
    }

    [Fact]
    public void Pending_hand_choice_is_written_before_any_other_entry()
    {
        _history.AppendLater(HandChoice("eagle_airstrike"));
        _history.Append(new LockedLoadout(DateTimeOffset.Now, ["hellbomb", null, null, null], [], LockSource.Ready, true));

        _history.ReadRecentLoadouts().Select(l => l.Source).Should().Equal(LockSource.Ready, LockSource.ByHand);
    }

    [Fact]
    public void Forced_flush_writes_the_pending_hand_choice()
    {
        _history.AppendLater(HandChoice("eagle_airstrike"));
        _history.FlushPending(force: true);

        File.ReadAllLines(_history.FilePath).Should().ContainSingle();
    }

    [Fact]
    public void Discarded_hand_choice_is_never_written_and_the_same_record_is_not_written_twice()
    {
        var recognized = new LockedLoadout(DateTimeOffset.Now, ["hellbomb", null, null, null], [], LockSource.Ready, true);
        _history.Append(recognized);
        _history.AppendLater(HandChoice("eagle_airstrike"));

        _history.DiscardPending().Should().BeTrue();
        _history.Append(recognized);
        _history.FlushPending(force: true);

        File.ReadAllLines(_history.FilePath).Should().ContainSingle();
    }

    private static LockedLoadout HandChoice(string first) =>
        new(DateTimeOffset.Now, [first, null, null, null], [], LockSource.ByHand, true, [true, false, false, false]);

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
