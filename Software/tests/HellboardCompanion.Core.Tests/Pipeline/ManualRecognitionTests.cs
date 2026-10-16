// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Pipeline;
using HellboardCompanion.Core.Platform;
using HellboardCompanion.Core.Recognition;
using HellboardCompanion.Core.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;

namespace HellboardCompanion.Core.Tests.Pipeline;

public sealed class ManualRecognitionTests : IDisposable
{
    private const string PartionPrepare = "snapshot-20261015-221602-386-2560x1440.png";
    private const string ShipManagement = "snapshot-20261015-222021-693-2560x1440.png";

    private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "HellboardCompanionTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dataRoot))
        {
            Directory.Delete(_dataRoot, recursive: true);
        }
    }

    [Fact]
    public void Majority_vote_per_slot_ignores_single_outliers()
    {
        var a = new SlotValue(SlotState.Recognized, "eagle_500kg_bomb");
        var b = new SlotValue(SlotState.Recognized, "eagle_airstrike");
        LoadoutObservation Observation(SlotValue first, ReadyStatus status) => new(
            DateTimeOffset.Now, LoadoutScreenResult.NotVisible, status,
            new LoadoutSnapshot([first, SlotValue.Empty, SlotValue.Empty, SlotValue.Empty], [SlotValue.Empty, SlotValue.Empty, SlotValue.Empty, SlotValue.Empty]),
            TimeSpan.Zero);

        var (loadout, status) = ManualRecognitionResult.Majority(
        [
            Observation(a, ReadyStatus.Prepare), Observation(b, ReadyStatus.Unknown), Observation(a, ReadyStatus.Prepare),
        ]);

        loadout.PlayerSlots[0].Should().Be(a);
        status.Should().Be(ReadyStatus.Prepare);
    }

    [Fact]
    public async Task Hotkey_on_the_loadout_screen_locks_in_the_loadout()
    {
        using var pipeline = CreatePipeline(PartionPrepare, new RecognitionOptions { Mode = RecognitionMode.ManualOnly });
        var locked = new List<LockedLoadout>();
        pipeline.LoadoutLocked += (_, l) => locked.Add(l);

        var result = await pipeline.RecognizeManuallyAsync();

        result.Frames.Should().Be(LoadoutPipeline.BurstFrames);
        result.ScreenDetected.Should().BeTrue();
        result.Locked.Should().BeTrue();
        result.Problem.Should().BeNull();
        locked.Should().ContainSingle().Which.Source.Should().Be(LockSource.Manual);
        locked[0].Stratagems.Should().Equal("eagle_strafing_run", "anti_materiel_rifle", "directional_shield", "machine_gun_sentry");
        locked[0].MissionStratagems.Should().Equal("hellbomb");
        File.Exists(Path.Combine(_dataRoot, "local", "HellboardCompanion", "loadouts.jsonl")).Should().BeTrue();
    }

    [Fact]
    public async Task Test_mode_shows_the_result_but_locks_nothing_in()
    {
        using var pipeline = CreatePipeline(PartionPrepare, new RecognitionOptions { Mode = RecognitionMode.ManualOnly, ManualTransfer = false });

        var result = await pipeline.RecognizeManuallyAsync();

        result.TestMode.Should().BeTrue();
        result.Locked.Should().BeFalse();
        result.Loadout!.PlayerSlots[0].StratagemId.Should().Be("eagle_strafing_run");
        pipeline.Tracker.LastLocked.Should().BeNull();
    }

    [Fact]
    public async Task Other_screen_is_reported_with_a_warning_and_not_locked_in()
    {
        using var pipeline = CreatePipeline(ShipManagement, new RecognitionOptions { Mode = RecognitionMode.ManualOnly });

        var result = await pipeline.RecognizeManuallyAsync();

        result.ScreenDetected.Should().BeFalse();
        result.Locked.Should().BeFalse();
        result.Problem.Should().Contain("not detected");
        result.Screen.Should().NotBeNull();
    }

    [Fact]
    public async Task Snapshots_of_the_trigger_are_saved_when_enabled()
    {
        using var pipeline = CreatePipeline(PartionPrepare, new RecognitionOptions { Mode = RecognitionMode.ManualOnly, SaveSnapshotOnTrigger = true });

        var result = await pipeline.RecognizeManuallyAsync();

        result.SavedFiles.Should().HaveCount(LoadoutPipeline.BurstFrames).And.AllSatisfy(f => File.Exists(f).Should().BeTrue());
    }

    [Fact]
    public async Task Image_file_is_analyzed_without_a_running_game()
    {
        using var pipeline = CreatePipeline(frameFile: null, new RecognitionOptions { Mode = RecognitionMode.ManualOnly });
        var path = Path.Combine(LocalScenes.TestDataDirectory, "local", PartionPrepare);
        Assert.SkipUnless(File.Exists(path), "Local test image not available.");

        var observation = await pipeline.AnalyzeFileAsync(path);

        observation.ScreenVisible.Should().BeTrue();
        observation.Loadout!.PlayerSlots.Select(s => s.ToString()).Should().Equal("eagle_strafing_run", "anti_materiel_rifle", "directional_shield", "machine_gun_sentry");
        pipeline.Tracker.LastLocked.Should().BeNull("a file analysis never locks anything in");
    }

    [Fact]
    public async Task Missing_loadout_screen_is_recorded_as_problem_with_a_screenshot()
    {
        using var pipeline = CreatePipeline(ShipManagement, new RecognitionOptions { Mode = RecognitionMode.ManualOnly });

        await pipeline.RecognizeManuallyAsync();

        var problem = History().ReadRecent().Select(i => i.Problem).OfType<RecognitionProblem>().Should().ContainSingle().Subject;
        problem.Kind.Should().Be(ProblemKind.ScreenNotDetected);
        File.Exists(problem.ImagePath).Should().BeTrue();
    }

    [Fact]
    public async Task Unknown_stratagem_is_recorded_with_a_screenshot_of_its_slot()
    {
        using var pipeline = CreatePipeline(PartionPrepare, new RecognitionOptions { Mode = RecognitionMode.ManualOnly }, withoutIcon: "eagle_strafing_run");

        var result = await pipeline.RecognizeManuallyAsync();

        result.Locked.Should().BeFalse();
        var problem = History().ReadRecent().Select(i => i.Problem).OfType<RecognitionProblem>().Should().ContainSingle().Subject;
        problem.Kind.Should().Be(ProblemKind.UnrecognizedStratagem);
        problem.Message.Should().StartWith("Slot 1");
        var image = OpenCvSharp.Cv2.ImRead(problem.ImagePath!);
        image.Width.Should().BeInRange(100, 140, "the crop shows the slot (93 px at 1440p) with a margin");
    }

    [Fact]
    public async Task Stratagem_without_input_sequence_is_recorded_once()
    {
        var catalog = new StratagemCatalog(StratagemCatalog.LoadSeed().Entries.Select(e => e.Id == "machine_gun_sentry" ? e with { Sequence = null } : e));
        using var pipeline = CreatePipeline(PartionPrepare, new RecognitionOptions { Mode = RecognitionMode.ManualOnly }, catalog: catalog);

        await pipeline.RecognizeManuallyAsync();
        await Task.Delay(1100, TestContext.Current.CancellationToken);   // manual trigger debounce is in the service, not here
        await pipeline.RecognizeManuallyAsync();

        var problems = History().ReadRecent().Select(i => i.Problem).OfType<RecognitionProblem>().ToList();
        problems.Should().ContainSingle().Which.StratagemId.Should().Be("machine_gun_sentry");
        problems[0].Kind.Should().Be(ProblemKind.NoInputSequence);
    }

    private LoadoutHistory History() =>
        new(new PlatformPaths(Path.Combine(_dataRoot, "roaming"), Path.Combine(_dataRoot, "local")));

    private LoadoutPipeline CreatePipeline(string? frameFile, RecognitionOptions options, string? withoutIcon = null, StratagemCatalog? catalog = null)
    {
        Frame? frame = frameFile is null ? null : LocalScenes.Load(LocalScenes.Get(frameFile));
        var paths = new PlatformPaths(Path.Combine(_dataRoot, "roaming"), Path.Combine(_dataRoot, "local"));

        // Copy the locally cached icons into the data directory of this test (no network access).
        var iconStore = new StratagemIconStore(paths, NullLogger<StratagemIconStore>.Instance);
        foreach (var icon in LocalIcons.All.Where(i => i.Id != withoutIcon))
        {
            var target = Path.Combine(iconStore.Directory, icon.Group, Path.GetFileName(icon.SvgPath));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(icon.SvgPath, target, overwrite: true);
        }

        iconStore.LoadLocal();

        var width = frame?.Width ?? 1920;
        var height = frame?.Height ?? 1080;
        var grabber = new GameFrameGrabber(FakeGameLocator.Running(width, height), new FakeFrameSource(frame));
        var pipeline = new LoadoutPipeline(grabber, iconStore, new LoadoutHistory(paths), options, paths, NullLogger<LoadoutPipeline>.Instance, catalog ?? StratagemCatalog.LoadSeed());
        pipeline.Start();
        return pipeline;
    }
}
