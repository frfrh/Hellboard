// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using AwesomeAssertions;
using HellboardCompanion.Core.Updates;

namespace HellboardCompanion.Core.Tests.Updates;

public sealed class CompanionUpdateCheckerTests
{
    private const string Releases = """
        [
          {"tag_name": "v0.9.0_companion", "html_url": "https://github.com/frfrh/Hellboard/releases/tag/v0.9.0_companion", "draft": true, "prerelease": false},
          {"tag_name": "v0.8.1_companion", "html_url": "https://github.com/frfrh/Hellboard/releases/tag/v0.8.1_companion", "draft": false, "prerelease": true},
          {"tag_name": "v2.0.0_firmware", "html_url": "https://github.com/frfrh/Hellboard/releases/tag/v2.0.0_firmware", "draft": false, "prerelease": false},
          {"tag_name": "v0.8.0_companion", "html_url": "https://github.com/frfrh/Hellboard/releases/tag/v0.8.0_companion", "draft": false, "prerelease": false},
          {"tag_name": "v0.7.0", "html_url": "https://github.com/frfrh/Hellboard/releases/tag/v0.7.0", "draft": false, "prerelease": false}
        ]
        """;

    [Fact]
    public void The_newest_published_companion_release_is_chosen()
    {
        var release = CompanionUpdateChecker.ParseLatest(Releases);

        release.Should().Be(new CompanionRelease(new Version(0, 8, 0), "v0.8.0_companion", "https://github.com/frfrh/Hellboard/releases/tag/v0.8.0_companion"));
        release!.VersionText.Should().Be("0.8.0");
    }

    [Fact]
    public void Tags_of_older_companion_releases_count_too()
    {
        var release = CompanionUpdateChecker.ParseLatest("""[{"tag_name": "v0.7.0", "html_url": "https://github.com/frfrh/Hellboard/releases/tag/v0.7.0", "draft": false, "prerelease": false}]""");

        release!.Version.Should().Be(new Version(0, 7, 0));
    }

    [Theory]
    [InlineData("0.7.0", null, true)]
    [InlineData("0.8.0", null, false)]   // same version
    [InlineData("0.9.0", null, false)]   // newer build than the release
    [InlineData("0.7.0", "0.8.0", false)]   // skipped
    [InlineData("0.7.0", "0.7.5", true)]   // an older skipped version does not hide a newer release
    public void A_release_is_offered_if_newer_and_not_skipped(string current, string? skipped, bool offered)
    {
        var release = new CompanionRelease(new Version(0, 8, 0), "v0.8.0_companion", "https://github.com/frfrh/Hellboard/releases/tag/v0.8.0_companion");

        CompanionUpdateChecker.IsOffered(release, Version.Parse(current), skipped).Should().Be(offered);
    }

    [Theory]
    [InlineData("0.8.0", true)]    // test build 0.8.0-test.1: the final 0.8.0 is offered
    [InlineData("0.9.0", false)]   // test build of a later version
    public void A_test_build_is_older_than_the_release_of_the_same_number(string current, bool offered)
    {
        var release = new CompanionRelease(new Version(0, 8, 0), "v0.8.0_companion", "https://github.com/frfrh/Hellboard/releases/tag/v0.8.0_companion");

        CompanionUpdateChecker.IsOffered(release, Version.Parse(current), null, currentIsPreRelease: true).Should().Be(offered);
    }

    [Theory]
    [InlineData("0.10.0", "0.10.0")]
    [InlineData("0.10.0-test.1", "0.10.0")]
    [InlineData("0.10.0+abc", "0.0.0")]   // ShortVersion never contains build metadata
    [InlineData("dev", "0.0.0")]
    public void The_build_version_ignores_a_test_suffix(string shortVersion, string expected)
    {
        CompanionUpdateChecker.BuildVersion(shortVersion).ToString(3).Should().Be(expected);
    }

    [Theory]
    [InlineData("0.6.0", "0.6.0")]
    [InlineData(" ", null)]
    [InlineData("0.6", null)]   // needs three parts
    [InlineData("old", null)]
    [InlineData(null, null)]
    public void The_test_switch_overrides_the_version_of_the_build(string? value, string? expected)
    {
        CompanionUpdateChecker.VersionOf(value).ToString(3).Should().Be(expected ?? AppInfo.ShortVersion);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""[{"tag_name": "v1.0.0_companion", "html_url": "https://example.com/v1", "draft": false, "prerelease": false}]""")]   // not a GitHub page
    public void No_release_without_a_matching_companion_release(string json)
    {
        CompanionUpdateChecker.ParseLatest(json).Should().BeNull();
    }

    [Theory]
    [InlineData("""{"message": "API rate limit exceeded"}""")]
    [InlineData("<html>")]
    public void An_unexpected_answer_is_reported(string json)
    {
        var parse = () => CompanionUpdateChecker.ParseLatest(json);

        parse.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public async Task The_releases_of_the_repository_are_queried_with_a_user_agent()
    {
        var github = new FakeGitHub(HttpStatusCode.OK, Releases);

        var release = await new CompanionUpdateChecker(new HttpClient(github)).GetLatestAsync(TestContext.Current.CancellationToken);

        release!.Tag.Should().Be("v0.8.0_companion");
        github.Request!.RequestUri!.ToString().Should().StartWith("https://api.github.com/repos/frfrh/Hellboard/releases");
        github.Request.Headers.UserAgent.ToString().Should().Contain("HellboardCompanion");
    }

    [Fact]
    public async Task A_refused_request_is_an_http_error()
    {
        var check = () => new CompanionUpdateChecker(new HttpClient(new FakeGitHub(HttpStatusCode.Forbidden, "{}")))
            .GetLatestAsync(TestContext.Current.CancellationToken);

        await check.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class FakeGitHub(HttpStatusCode status, string answer) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(answer) });
        }
    }
}
