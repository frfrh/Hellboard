// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Layout;
using HellboardCompanion.Core.Platform;
using HellboardCompanion.Core.Recognition;
using HellboardCompanion.Core.Tests.TestData;

namespace HellboardCompanion.Core.Tests.Recognition;

public sealed class StratagemSlotClassifierTests(ITestOutputHelper output)
{
    // Without the mission stratagems, like LoadoutRecognizer in the app.
    private static readonly Lazy<StratagemSlotClassifier> PlayerClassifier = new(() =>
    {
        var missionOnly = StratagemCatalog.LoadSeed().MissionOnlyIds();
        return new StratagemSlotClassifier(LocalIcons.All.Where(icon => !missionOnly.Contains(icon.Id)), SlotStyle.PlayerSlot);
    });

    [Theory]
    [InlineData("Eagle 500KG Bomb", "eagle_500kg_bomb")]
    [InlineData("Anti-Materiel Rifle", "anti_materiel_rifle")]
    [InlineData("StA-X3 W.A.S.P. Launcher", "sta_x3_w_a_s_p_launcher")]
    [InlineData("Castellan’s Creed", "castellan_s_creed")]
    public void Ids_are_slugs_of_the_names(string name, string id)
    {
        StratagemIcon.ToId(name).Should().Be(id);
    }

    [Theory]
    [MemberData(nameof(LocalScenes.LoadoutScenesAtAllHeights), MemberType = typeof(LocalScenes))]
    public void Recognizes_the_player_stratagems(string file, int height)
    {
        var scene = LocalScenes.Get(file);
        var results = ClassifySlots(scene, height, LoadoutLayout.Default.PlayerSlots, PlayerClassifier.Value);

        results.Should().Equal(scene.PlayerSlots!);
    }

    [Theory]
    [MemberData(nameof(LocalScenes.LoadoutScenesAtAllHeights), MemberType = typeof(LocalScenes))]
    public void Recognizes_the_mission_stratagems(string file, int height)
    {
        var scene = LocalScenes.Get(file);
        var layout = LoadoutLayout.Default;

        var results = ClassifySlots(scene, height, [.. layout.FixedMissionSlots, .. layout.OptionalMissionSlots], MissionClassifier.Value);

        results.Should().Equal(["reinforce", "sos_beacon", "resupply", .. scene.OptionalMissionSlots!]);
    }

    /// <summary>
    /// Slot images as saved for the history (slot plus 1/8 margin, LoadoutPipeline.SaveCrop), in
    /// <c>testData/local/</c> (game content, never committed).
    /// </summary>
    [Theory]
    [InlineData("slot-guard_dog_rover.png", "guard_dog_rover")]   // 5 px higher than its SVG, emblem differs from K-9 only
    [InlineData("slot-orbital_precision_strike.png", "orbital_precision_strike")]   // like SEAF Artillery (mission only)
    [InlineData("slot-shield_generator_pack.png", "shield_generator_pack")]   // the shield backpacks differ in the emblem only
    [InlineData("slot-ballistic_shield_backpack.png", "ballistic_shield_backpack")]
    [InlineData("slot-anti_personnel_minefield.png", "anti_personnel_minefield")]   // like Anti-Tank Mines
    [InlineData("slot-warp_pack.png", "warp_pack")]   // 5 % larger than its SVG
    public void Recognizes_a_saved_player_slot(string file, string expected)
    {
        var path = Path.Combine(LocalScenes.TestDataDirectory, "local", file);
        Assert.SkipUnless(File.Exists(path), $"Local test image '{file}' is not available.");
        var frame = FrameFile.Load(path);
        var size = Enumerable.Range(1, frame.Width).First(s => s + (2 * (s / 8)) >= frame.Width);

        var result = PlayerClassifier.Value.Classify(frame, new PixelRect(size / 8, size / 8, size, size));

        output.WriteLine(result.ToString());
        result.State.Should().Be(SlotState.Recognized);
        result.StratagemId.Should().Be(expected);
    }

    /// <summary>Icons the templates do not cover yet must stay unknown instead of becoming a similar stratagem.</summary>
    [Theory]
    [InlineData("slot-expendable_anti_tank-badge.png", "expendable_anti_tank")]   // game-only badge, close to Recoilless Rifle
    [InlineData("slot-eat_411-badge.png", "eat_411")]
    public void Does_not_guess_a_wrong_stratagem(string file, string actual)
    {
        var path = Path.Combine(LocalScenes.TestDataDirectory, "local", file);
        Assert.SkipUnless(File.Exists(path), $"Local test image '{file}' is not available.");
        var frame = FrameFile.Load(path);
        var size = Enumerable.Range(1, frame.Width).First(s => s + (2 * (s / 8)) >= frame.Width);

        var result = PlayerClassifier.Value.Classify(frame, new PixelRect(size / 8, size / 8, size, size));

        output.WriteLine(result.ToString());
        if (result.State == SlotState.Recognized)
        {
            result.StratagemId.Should().Be(actual);
        }
    }

    private static readonly Lazy<StratagemSlotClassifier> MissionClassifier =
        new(() => new StratagemSlotClassifier(LocalIcons.All, SlotStyle.MissionSlot));

    private List<string> ClassifySlots(Scene scene, int height, IEnumerable<LayoutRect> slots, StratagemSlotClassifier classifier)
    {
        var frame = LocalScenes.Load(scene, height);
        var offset = new LoadoutScreenDetector(LoadoutLayout.Default).Detect(frame).Offset;
        var scaler = new LayoutScaler(LoadoutLayout.Default, new PixelRect(0, 0, frame.Width, frame.Height));

        var results = slots
            .Select(slot => scaler.ToScreen(slot))
            .Select(slot => classifier.Classify(frame, slot with { X = slot.X + offset.X, Y = slot.Y + offset.Y }))
            .ToList();

        output.WriteLine($"{frame.Width}×{frame.Height}: {string.Join(" | ", results)}");
        return results.Select(r => r.State switch
        {
            SlotState.Empty => "empty",
            SlotState.Recognized => r.StratagemId!,
            _ => "unknown",
        }).ToList();
    }
}
