// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Tests;

public sealed class PlatformPathsTests
{
    [Fact]
    public void Directories_are_placed_below_the_given_roots()
    {
        var roaming = Path.Combine("root", "roaming");
        var local = Path.Combine("root", "local");

        var paths = new PlatformPaths(roaming, local);

        paths.ConfigDirectory.Should().Be(Path.Combine(roaming, "HellboardCompanion"));
        paths.DataDirectory.Should().Be(Path.Combine(local, "HellboardCompanion"));
        paths.LogDirectory.Should().Be(Path.Combine(local, "HellboardCompanion", "logs"));
    }

    [Theory]
    [InlineData("", "local")]
    [InlineData("roaming", " ")]
    public void Empty_roots_are_rejected(string roaming, string local)
    {
        var create = () => new PlatformPaths(roaming, local);

        create.Should().Throw<ArgumentException>();
    }
}
