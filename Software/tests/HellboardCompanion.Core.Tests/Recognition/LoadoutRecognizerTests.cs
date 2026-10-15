// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Layout;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Recognition;
using HellboardCompanion.Core.Tests.TestData;

namespace HellboardCompanion.Core.Tests.Recognition;

public sealed class LoadoutRecognizerTests(ITestOutputHelper output)
{
    private const string PartionPrepare = "snapshot-20261015-221602-386-2560x1440.png";
    private const string PartionReady = "snapshot-20261015-221618-113-2560x1440.png";
    private const string ShipManagement = "snapshot-20261015-222021-693-2560x1440.png";

    private static readonly Lazy<LoadoutRecognizer> Recognizer = new(() => new LoadoutRecognizer(LoadoutLayout.Default, LocalIcons.All));

    [Theory]
    [MemberData(nameof(LocalScenes.AllScenesAtAllHeights), MemberType = typeof(LocalScenes))]
    public void Recognizes_complete_scenes(string file, int height)
    {
        var scene = LocalScenes.Get(file);
        var frame = LocalScenes.Load(scene, height);
        using var recognizer = new LoadoutRecognizer(LoadoutLayout.Default, LocalIcons.All);

        var observation = recognizer.Recognize(frame);
        var repeated = recognizer.Recognize(frame);   // unchanged slots come from the cache

        output.WriteLine($"{frame.Width}×{frame.Height}: {observation.Duration.TotalMilliseconds:F1} ms, repeated {repeated.Duration.TotalMilliseconds:F1} ms, {observation.Status}, {observation.Loadout}");
        repeated.Loadout.Should().Be(observation.Loadout);
        observation.ScreenVisible.Should().Be(scene.LoadoutScreen);
        if (scene.LoadoutScreen)
        {
            observation.Status.ToString().Should().Be(scene.ReadyStatus);
            observation.Loadout!.PlayerSlots.Select(s => s.ToString()).Should().Equal(scene.PlayerSlots!);
            observation.Loadout.MissionSlots.Select(s => s.ToString()).Should().Equal(scene.OptionalMissionSlots!);
        }

        observation.Duration.Should().BeLessThan(TimeSpan.FromMilliseconds(300), "a new loadout must be recognized within a few frames");
        repeated.Duration.Should().BeLessThan(TimeSpan.FromMilliseconds(50), "an unchanged loadout screen must keep up with 10 frames per second");
    }

    [Fact]
    public void Replay_of_real_frames_locks_in_the_loadout_on_ready()
    {
        var tracker = new LoadoutTracker();
        var locked = new List<LockedLoadout>();
        tracker.Locked += (_, l) => locked.Add(l);

        Replay(tracker, (ShipManagement, 2), (PartionPrepare, 4), (PartionReady, 3), (ShipManagement, 3));

        locked.Should().ContainSingle();
        locked[0].Source.Should().Be(LockSource.Ready);
        locked[0].Stratagems.Should().Equal("eagle_strafing_run", "anti_materiel_rifle", "directional_shield", "machine_gun_sentry");
        locked[0].MissionStratagems.Should().Equal("hellbomb");
        locked[0].IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Replay_where_the_screen_closes_before_ready_is_seen_still_locks_in()
    {
        var tracker = new LoadoutTracker();
        var locked = new List<LockedLoadout>();
        tracker.Locked += (_, l) => locked.Add(l);

        Replay(tracker, (PartionPrepare, 4), (PartionReady, 1), (ShipManagement, 2));

        locked.Should().ContainSingle().Which.Source.Should().Be(LockSource.ScreenExit);
        locked[0].Stratagems[0].Should().Be("eagle_strafing_run");
    }

    private static void Replay(LoadoutTracker tracker, params (string File, int Frames)[] sequence)
    {
        var time = DateTimeOffset.Now;
        foreach (var (file, frames) in sequence)
        {
            var observation = Recognizer.Value.Recognize(LocalScenes.Load(LocalScenes.Get(file)));
            for (var i = 0; i < frames; i++)
            {
                time = time.AddMilliseconds(100);
                tracker.Process(observation with { Timestamp = time });
            }
        }
    }
}
