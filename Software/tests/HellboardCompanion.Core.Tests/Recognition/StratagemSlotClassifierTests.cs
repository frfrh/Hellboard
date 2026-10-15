// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Layout;
using HellboardCompanion.Core.Platform;
using HellboardCompanion.Core.Recognition;
using HellboardCompanion.Core.Tests.TestData;

namespace HellboardCompanion.Core.Tests.Recognition;

public sealed class StratagemSlotClassifierTests(ITestOutputHelper output)
{
    private static readonly Lazy<StratagemSlotClassifier> PlayerClassifier =
        new(() => new StratagemSlotClassifier(LocalIcons.All, SlotStyle.PlayerSlot));

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
