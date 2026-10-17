// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

namespace HellboardCompanion.Core.Tests.Catalog;

public sealed class StratagemReferenceStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("hellboard-references").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("problem-20261017-203720-128-slot4.png", ReferenceSlot.Player)]
    [InlineData("problem-20261016-181521-989-mission2.png", ReferenceSlot.Mission)]
    [InlineData("problem-20261016-182828-958-screen.png", null)]
    public void The_slot_kind_comes_from_the_problem_image_name(string file, ReferenceSlot? expected)
    {
        StratagemReferenceStore.SlotOfProblemImage(Path.Combine(_dir, file)).Should().Be(expected);
    }

    [Theory]
    [InlineData(116, 11, 94)]
    [InlineData(57, 5, 47)]
    public void The_slot_is_found_inside_a_saved_image(int width, int offset, int size)
    {
        StratagemReference.SlotIn(width).Should().Be((offset, size));
    }

    [Fact]
    public void Added_images_are_stored_per_stratagem_and_slot_kind()
    {
        var store = NewStore();
        var changes = 0;
        store.Changed += (_, _) => changes++;

        store.Add(Image(1), "expendable_anti_tank", ReferenceSlot.Player).Should().BeTrue();
        store.Add(Image(2), "hellbomb", ReferenceSlot.Mission).Should().BeTrue();

        store.References.Should().HaveCount(2);
        store.References.Should().Contain(r => r.Id == "expendable_anti_tank" && r.Slot == ReferenceSlot.Player);
        store.Count("hellbomb").Should().Be(1);
        changes.Should().Be(2);

        var reloaded = NewStore();
        reloaded.Load();
        reloaded.References.Select(r => (r.Id, r.Slot)).Should().BeEquivalentTo(store.References.Select(r => (r.Id, r.Slot)));
    }

    [Fact]
    public void The_same_image_is_not_stored_twice()
    {
        var store = NewStore();
        var image = Image(1);

        store.Add(image, "eat_411", ReferenceSlot.Player).Should().BeTrue();
        store.Add(image, "eat_411", ReferenceSlot.Player).Should().BeFalse();

        store.Count("eat_411").Should().Be(1);
    }

    [Fact]
    public void Only_the_newest_images_are_kept()
    {
        var store = NewStore();

        for (var i = 0; i < StratagemReferenceStore.MaxPerStratagem + 2; i++)
        {
            store.Add(Image(i), "eat_411", ReferenceSlot.Player).Should().BeTrue();
        }

        store.Count("eat_411").Should().Be(StratagemReferenceStore.MaxPerStratagem);
    }

    [Fact]
    public void Remove_deletes_all_images_of_a_stratagem()
    {
        var store = NewStore();
        store.Add(Image(1), "eat_411", ReferenceSlot.Player);
        store.Add(Image(2), "eat_411", ReferenceSlot.Mission);
        store.Add(Image(3), "bastion_mk_xvi", ReferenceSlot.Player);

        store.Remove("eat_411");

        store.References.Should().ContainSingle().Which.Id.Should().Be("bastion_mk_xvi");
    }

    [Fact]
    public void Only_stratagem_ids_are_accepted()
    {
        var store = NewStore();

        var act = () => store.Add(Image(1), "../outside", ReferenceSlot.Player);

        act.Should().Throw<ArgumentException>();
    }

    private StratagemReferenceStore NewStore() =>
        new(new PlatformPaths(_dir, _dir), NullLogger<StratagemReferenceStore>.Instance);

    /// <summary>A 116 px slot image with a distinct random pattern per <paramref name="seed"/>.</summary>
    private string Image(int seed)
    {
        using var image = new Mat(116, 116, MatType.CV_8UC3);
        new RNG((ulong)(seed + 1)).Fill(image, DistributionType.Uniform, Scalar.All(0), Scalar.All(255));
        var path = Path.Combine(_dir, $"problem-{seed}-slot1.png");
        Cv2.ImWrite(path, image);
        return path;
    }
}
