// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Layout;
using HellboardCompanion.Core.Platform;
using HellboardCompanion.Core.Recognition;
using HellboardCompanion.Core.Tests.TestData;

namespace HellboardCompanion.Core.Tests.Recognition;

public sealed class ReadyStatusDetectorTests(ITestOutputHelper output)
{
    private readonly ReadyStatusDetector _detector = new(LoadoutLayout.Default);

    [Theory]
    [InlineData(0.0, ReadyStatus.Prepare)]
    [InlineData(0.17, ReadyStatus.Prepare)]
    [InlineData(0.5, ReadyStatus.Unknown)]
    [InlineData(0.8, ReadyStatus.Ready)]
    public void Classifies_by_yellow_share(double yellowShare, ReadyStatus expected)
    {
        var frame = SyntheticButtonFrame(1920, 1080, yellowShare);

        _detector.Detect(frame).Status.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(LocalScenes.LoadoutScenesAtAllHeights), MemberType = typeof(LocalScenes))]
    public void Recognizes_the_status_in_real_captures(string file, int height)
    {
        var scene = LocalScenes.Get(file);
        var frame = LocalScenes.Load(scene, height);

        var result = _detector.Detect(frame);

        output.WriteLine($"{frame.Width}×{frame.Height}: {result.Status}, yellow share {result.YellowShare:P1}");
        result.Status.Should().Be(Enum.Parse<ReadyStatus>(scene.ReadyStatus!));
    }

    /// <summary>Dark frame whose ready button area has the given share of yellow columns.</summary>
    private static Frame SyntheticButtonFrame(int width, int height, double yellowShare)
    {
        var bounds = new PixelRect(0, 0, width, height);
        var pixels = new byte[bounds.Area * Frame.BytesPerPixel];
        var button = new LayoutScaler(LoadoutLayout.Default, bounds).ToScreen(LoadoutLayout.Default.ReadyButton);
        var yellowColumns = (int)Math.Round(button.Width * yellowShare);

        for (var y = button.Y; y < button.Bottom; y++)
        {
            for (var x = button.X; x < button.X + yellowColumns; x++)
            {
                var i = ((y * width) + x) * Frame.BytesPerPixel;
                pixels[i] = 0;          // B
                pixels[i + 1] = 230;    // G
                pixels[i + 2] = 255;    // R
            }
        }

        return new Frame(bounds, pixels, DateTimeOffset.Now);
    }
}
