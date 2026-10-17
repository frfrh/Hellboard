// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using HellboardCompanion.Core.Catalog;

namespace HellboardCompanion.Core.Tests.Catalog;

public sealed class StratagemIconDownloaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("hellboard-icons").FullName;
    private readonly FakeGitHub _github = new();
    private readonly Clock _clock = new();
    private readonly StratagemIconDownloader _downloader;

    public StratagemIconDownloaderTests()
    {
        _downloader = new StratagemIconDownloader(new HttpClient(_github), _clock);
        _github.Files["Hangar/Eagle 500KG Bomb.svg"] = Svg("eagle");
        _github.Files["General Stratagems/Reinforce.svg"] = Svg("reinforce");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task First_update_downloads_all_icons()
    {
        var result = await Update();

        result.Added.Should().BeEquivalentTo("Hangar/Eagle 500KG Bomb.svg", "General Stratagems/Reinforce.svg");
        File.ReadAllText(Path.Combine(_dir, "Hangar", "Eagle 500KG Bomb.svg")).Should().Contain("eagle");
        StratagemIcon.LoadDirectory(_dir).Select(i => i.Id).Should().BeEquivalentTo("eagle_500kg_bomb", "reinforce");
    }

    [Fact]
    public async Task The_repository_is_checked_at_most_once_a_day_unless_forced()
    {
        await Update();
        _clock.Advance(TimeSpan.FromHours(5));

        (await Update()).Checked.Should().BeFalse();
        _github.TreeRequests.Should().Be(1);

        (await Update(force: true)).NotModified.Should().BeTrue("the ETag did not change");
        _github.TreeRequests.Should().Be(2);
        _github.Downloads.Should().Be(2);
    }

    [Fact]
    public async Task Changed_new_moved_and_removed_icons_are_handled()
    {
        await Update();
        _github.Files["Hangar/Eagle 500KG Bomb.svg"] = Svg("eagle v2");                       // changed
        _github.Files["Bridge/Orbital Laser.svg"] = Svg("laser");                             // new
        _github.Files["Mission/Reinforce.svg"] = _github.Files["General Stratagems/Reinforce.svg"];   // moved
        _github.Files.Remove("General Stratagems/Reinforce.svg");
        _clock.Advance(TimeSpan.FromDays(2));
        var downloadsBefore = _github.Downloads;

        var result = await Update();

        result.Updated.Should().Equal("Hangar/Eagle 500KG Bomb.svg");
        result.Added.Should().Equal("Bridge/Orbital Laser.svg");
        result.Moved.Should().Equal("General Stratagems/Reinforce.svg → Mission/Reinforce.svg");
        result.Retired.Should().BeEmpty();
        _github.Downloads.Should().Be(downloadsBefore + 2, "the moved icon is renamed locally, not downloaded");
        File.ReadAllText(Path.Combine(_dir, "Hangar", "Eagle 500KG Bomb.svg")).Should().Contain("eagle v2");
        File.Exists(Path.Combine(_dir, "Mission", "Reinforce.svg")).Should().BeTrue();

        _github.Files.Remove("Bridge/Orbital Laser.svg");
        (await Update(force: true)).Retired.Should().Equal("Bridge/Orbital Laser.svg");
        File.Exists(Path.Combine(_dir, "Bridge", "Orbital Laser.svg")).Should().BeTrue("removed icons are kept");
        (await Update(force: true)).Retired.Should().BeEmpty("reported only once");
    }

    [Fact]
    public async Task Icons_of_older_versions_are_not_downloaded_again()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Hangar"));
        File.WriteAllBytes(Path.Combine(_dir, "Hangar", "Eagle 500KG Bomb.svg"), _github.Files["Hangar/Eagle 500KG Bomb.svg"]);

        var result = await Update();

        result.Added.Should().Equal("General Stratagems/Reinforce.svg");
        _github.Downloads.Should().Be(1);
    }

    [Fact]
    public void Git_blob_sha_matches_git()
    {
        // git hash-object of a file containing "hello\n"
        StratagemIconDownloader.GitBlobSha(Encoding.ASCII.GetBytes("hello\n")).Should().Be("ce013625030ba8dba906f756967f9e9ca394464a");
    }

    private Task<IconUpdateResult> Update(bool force = false) => _downloader.UpdateAsync(_dir, force, TestContext.Current.CancellationToken);

    private static byte[] Svg(string text) => Encoding.UTF8.GetBytes($"<svg xmlns=\"http://www.w3.org/2000/svg\"><!-- {text} --></svg>");

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>Trees API with ETag / 304 and raw.githubusercontent.com downloads.</summary>
    private sealed class FakeGitHub : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

        public int TreeRequests { get; private set; }

        public int Downloads { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            if (uri.Host == "api.github.com")
            {
                TreeRequests++;
                var tree = new JsonArray([.. Files.Select(f => (JsonNode)new JsonObject
                {
                    ["path"] = f.Key, ["type"] = "blob", ["sha"] = StratagemIconDownloader.GitBlobSha(f.Value),
                })]);
                var etag = $"W/\"{StratagemIconDownloader.GitBlobSha(Encoding.UTF8.GetBytes(tree.ToJsonString()))}\"";
                if (request.Headers.TryGetValues("If-None-Match", out var values) && values.Contains(etag))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new JsonObject { ["tree"] = tree }.ToJsonString()) };
                response.Headers.TryAddWithoutValidation("ETag", etag);
                return Task.FromResult(response);
            }

            var path = Uri.UnescapeDataString(uri.AbsolutePath).Split('/', 5)[4];   // /owner/repo/branch/<path>
            if (!Files.TryGetValue(path, out var content))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            Downloads++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
        }
    }
}
