// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Tests.Loadout;

public sealed class LoadoutHistoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HellboardCompanionTests", Guid.NewGuid().ToString("N"));
    private readonly LoadoutHistory _history;

    public LoadoutHistoryTests() => _history = new LoadoutHistory(new PlatformPaths(_root, _root));

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
}
