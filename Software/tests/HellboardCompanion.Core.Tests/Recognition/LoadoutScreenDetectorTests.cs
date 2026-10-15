// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Layout;
using HellboardCompanion.Core.Platform;
using HellboardCompanion.Core.Recognition;
using HellboardCompanion.Core.Tests.TestData;

namespace HellboardCompanion.Core.Tests.Recognition;

public sealed class LoadoutScreenDetectorTests(ITestOutputHelper output)
{
    private readonly LoadoutScreenDetector _detector = new(LoadoutLayout.Default);

    [Theory]
    [MemberData(nameof(LocalScenes.AllScenesAtAllHeights), MemberType = typeof(LocalScenes))]
    public void Detects_the_loadout_screen_only_where_it_is_shown(string file, int height)
    {
        var scene = LocalScenes.Get(file);
        var frame = LocalScenes.Load(scene, height);

        var result = _detector.Detect(frame);

        output.WriteLine($"{file} {frame.Width}×{frame.Height}: visible={result.IsVisible} button={result.ButtonScore:F2} grid={result.GridScore:F2} mission={result.MissionFrameScore:F2} offset={result.Offset}");
        result.IsVisible.Should().Be(scene.LoadoutScreen);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(12, -9)]
    [InlineData(-20, 15)]
    public void Shifted_card_is_found_and_the_offset_reported(int dx, int dy)
    {
        var scene = LocalScenes.Get("snapshot-20261015-221602-386-2560x1440.png");
        var frame = Shift(LocalScenes.Load(scene), dx, dy);

        var result = _detector.Detect(frame);

        result.IsVisible.Should().BeTrue();
        result.Offset.Should().Be(new PixelOffset(dx, dy));
    }

    [Fact]
    public void Black_frame_is_not_the_loadout_screen()
    {
        var bounds = new PixelRect(0, 0, 1920, 1080);

        _detector.Detect(new Frame(bounds, new byte[bounds.Area * 4], DateTimeOffset.Now)).IsVisible.Should().BeFalse();
    }

    /// <summary>Moves the image content by (dx, dy), filling the uncovered area with black.</summary>
    private static Frame Shift(Frame frame, int dx, int dy)
    {
        var pixels = new byte[frame.Bgra.Length];
        for (var y = 0; y < frame.Height; y++)
        {
            var sourceY = y - dy;
            if (sourceY < 0 || sourceY >= frame.Height)
            {
                continue;
            }

            var sourceX = Math.Max(0, -dx);
            var targetX = Math.Max(0, dx);
            var count = frame.Width - Math.Abs(dx);
            Buffer.BlockCopy(frame.Bgra, (sourceY * frame.Stride) + (sourceX * 4), pixels, (y * frame.Stride) + (targetX * 4), count * 4);
        }

        return new Frame(frame.ScreenBounds, pixels, frame.Timestamp);
    }
}
