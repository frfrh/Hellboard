// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Hellboard.Update;

namespace HellboardCompanion.Core.Tests.Hellboard.Update;

public sealed class FirmwareVersionsTests
{
    [Theory]
    [InlineData("2.1.0", "2.1.1", -1)]
    [InlineData("2.1.1", "2.1.1", 0)]
    [InlineData("2.10.0", "2.9.9", 1)]
    [InlineData("2.0.0-dev", "2.0.0", -1)]
    [InlineData("dev", "2.0.0", -1)]
    [InlineData("3.0.0", "2.1.1", 1)]
    public void Versions_are_compared_numerically(string a, string b, int expected)
    {
        Math.Sign(FirmwareVersions.Compare(a, b)).Should().Be(expected);
    }

    [Fact]
    public void An_older_firmware_with_the_same_protocol_is_outdated()
    {
        FirmwareVersions.Classify(Connected("2.1.0"), "2.1.1").Should().Be(FirmwareStatus.Outdated);
        FirmwareVersions.Classify(Connected("2.1.1"), "2.1.1").Should().Be(FirmwareStatus.Current);
        FirmwareVersions.Classify(Connected("2.2.0"), "2.1.1").Should().Be(FirmwareStatus.Newer);
    }

    [Fact]
    public void A_board_without_protocol_is_incompatible_but_a_busy_port_is_not()
    {
        var noProtocol = new HellboardState(HellboardStatus.NoProtocol, "COM8", null, null, null, null, [], "The Hellboard firmware does not support the protocol yet (firmware update needed).");
        var busy = noProtocol with { Message = "COM8 is in use by another program (e.g. MicroPico)." };

        FirmwareVersions.Classify(noProtocol, "2.1.1").Should().Be(FirmwareStatus.Incompatible);
        FirmwareVersions.Classify(busy, "2.1.1").Should().Be(FirmwareStatus.Unknown);
    }

    [Fact]
    public void The_included_firmware_version_is_known()
    {
        FirmwareVersions.EmbeddedVersion.Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }

    private static HellboardState Connected(string firmware) =>
        new(HellboardStatus.Connected, "COM8", new HellboardInfo("hellboard", firmware, 1, "1.17.0"), null, null, null, [], null);
}
