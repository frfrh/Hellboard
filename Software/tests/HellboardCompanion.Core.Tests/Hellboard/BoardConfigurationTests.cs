// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json.Nodes;
using AwesomeAssertions;
using HellboardCompanion.Core.Hellboard;

namespace HellboardCompanion.Core.Tests.Hellboard;

public sealed class BoardConfigurationTests
{
    private static readonly StratagemCatalog Catalog = StratagemCatalog.LoadSeed();

    [Fact]
    public void Key_timings_screen_saver_and_scrolling_are_sent()
    {
        var options = new BoardOptions { MenuDelayMs = 400, KeyPressMs = 40, KeyReleaseMs = 20, ScreenSaverSeconds = 300, ScrollText = ScrollText.Off };

        var config = BoardConfiguration.ToProtocol(options, Catalog);

        config["menuDelayMs"]!.GetValue<int>().Should().Be(400);
        config["keyPressMs"]!.GetValue<int>().Should().Be(40);
        config["keyReleaseMs"]!.GetValue<int>().Should().Be(20);
        config["keyDelayMs"]!.GetValue<int>().Should().Be(40, "firmware 2.0 only knows one delay");
        config["screenSaverSeconds"]!.GetValue<int>().Should().Be(300);
        config["scrollText"]!.GetValue<string>().Should().Be("off");
    }

    [Fact]
    public void The_configuration_of_a_firmware_2_1_board_is_adopted()
    {
        var options = new BoardOptions();

        BoardConfiguration.Adopt(
            JsonNode.Parse("""{"menuDelayMs": 500, "keyPressMs": 40, "keyReleaseMs": 20, "screenSaverSeconds": 30, "scrollText": "off"}""")!.AsObject(),
            options,
            Catalog);

        options.MenuDelayMs.Should().Be(500);
        options.KeyPressMs.Should().Be(40);
        options.KeyReleaseMs.Should().Be(20);
        options.ScreenSaverSeconds.Should().Be(30);
        options.ScrollText.Should().Be(ScrollText.Off);
        options.Adopted.Should().BeTrue();
    }

    [Theory]
    [InlineData(ScrollText.Off, "off")]
    [InlineData(ScrollText.Long, "long")]
    [InlineData(ScrollText.Loop, "loop")]
    public void Every_scroll_mode_is_sent_and_adopted(ScrollText mode, string value)
    {
        BoardConfiguration.ToProtocol(new BoardOptions { ScrollText = mode }, Catalog)["scrollText"]!.GetValue<string>().Should().Be(value);
        var options = new BoardOptions();
        BoardConfiguration.Adopt(JsonNode.Parse($$"""{"scrollText": "{{value}}"}""")!.AsObject(), options, Catalog);
        options.ScrollText.Should().Be(mode);
    }

    [Fact]
    public void The_single_delay_of_a_firmware_2_0_board_sets_press_and_release()
    {
        var options = new BoardOptions();

        BoardConfiguration.Adopt(JsonNode.Parse("""{"keyDelayMs": 70}""")!.AsObject(), options, Catalog);

        options.KeyPressMs.Should().Be(70);
        options.KeyReleaseMs.Should().Be(70);
        options.MenuDelayMs.Should().Be(50, "unchanged default");
    }
}
